using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 멀리건 페이즈 전용 UI 관리.
/// 배경 패널, 확인 버튼, 카드 배치를 담당한다.
/// </summary>
public class MulliganUI : MonoBehaviour
{
    [Header("Mulligan Instruction")]
    [SerializeField] private GameObject mulliganInstructionGO;

    [Header("Confirm Button")]
    [SerializeField] private Button mulliganConfirmBtn;
    [SerializeField] private Vector2 confirmBtnSize = new Vector2(200f, 50f);
    [SerializeField] private Vector2 confirmBtnOffset = new Vector2(0f, -50f);
    [SerializeField] private Color confirmBtnColor = new Color(0.2f, 0.6f, 0.9f, 1f);
    [SerializeField] private float confirmBtnFontSize = 22f;
    [SerializeField] private TMP_FontAsset confirmBtnFont;

    [Header("Canvas 참조")]
    [SerializeField] private Canvas canvasRef;

    [Header("Background")]
    [SerializeField] private Sprite mulliganBGSprite;
    [SerializeField] private Color mulliganBGColor = Color.white;
    [SerializeField] private Vector2 mulliganBGPositionOffset = Vector2.zero;
    [SerializeField] private Vector2 mulliganBGSizeMultiplier = Vector2.one;
    [SerializeField] private Color focusOverlayColor = new Color(0f, 0f, 0f, 0.9f);
    [SerializeField] private bool focusOverlayBlocksRaycasts = true;

    private GameObject mulliganFocusOverlay;
    private GameObject mulliganBGPanel;
    private GameObject mulliganCardArea;
    private Transform canvasTransform;
    private Vector2 mulliganBGBaseSizeDelta;
    private Vector2 mulliganBGBaseAnchoredPosition;
    private const string MulliganBGSpritePath = "Images/CardGameUi/MuliganPanel";

    /// <summary>
    /// 멀리건 확인 버튼 클릭 시 외부에서 구독하는 이벤트.
    /// </summary>
    public event Action OnConfirmClicked;

    private void OnDestroy()
    {
        if (mulliganConfirmBtn != null)
            mulliganConfirmBtn.onClick.RemoveListener(OnConfirmButtonClicked);
    }

    /// <summary>
    /// 확인 버튼 폰트를 외부에서 설정 (EndTurnButton 폰트 재활용 등).
    /// </summary>
    public void SetFallbackFont(TMP_FontAsset font)
    {
        if (confirmBtnFont == null)
            confirmBtnFont = font;
    }

    /// <summary>
    /// 멀리건 UI를 표시하고 카드를 Canvas 중앙에 직선 배치한다.
    /// </summary>
    public void Show(List<HandCardUI> cards, float cardScale, Transform handArea)
    {
        EnsureCanvasTransform();
        CreateFocusOverlay(handArea);
        ShowInstructionText();
        CreateBGPanel(handArea);

        if (mulliganFocusOverlay != null)
            mulliganFocusOverlay.SetActive(true);

        if (mulliganBGPanel != null)
            mulliganBGPanel.SetActive(true);

        // 카드를 Canvas 직속 자식으로 이동 → 클릭 인식 보장
        if (canvasTransform != null)
        {
            float spacing = 250f;
            float startX = -(cards.Count - 1) * spacing / 2f;

            for (int i = 0; i < cards.Count; i++)
            {
                var cardGO = cards[i].gameObject;
                cardGO.transform.SetParent(canvasTransform, false);

                var rt = cards[i].RT;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(startX + i * spacing, -50f);
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one * cardScale;
            }
        }

        // 배경 패널 크기를 카드에 맞춤
        FitBGToCards(cards, cardScale);
        FitCardAreaToCards(cards, cardScale);
        BringInstructionToFront();

        ConfigureConfirmButton(mulliganConfirmBtn);
        PositionConfirmButton();
        if (mulliganConfirmBtn != null)
        {
            mulliganConfirmBtn.gameObject.SetActive(true);
            mulliganConfirmBtn.transform.SetAsLastSibling();
        }
    }

    /// <summary>
    /// 멀리건 UI를 숨긴다.
    /// </summary>
    public void Hide()
    {
        if (mulliganInstructionGO != null)
            mulliganInstructionGO.SetActive(false);

        if (mulliganFocusOverlay != null)
            mulliganFocusOverlay.SetActive(false);

        if (mulliganBGPanel != null)
            mulliganBGPanel.SetActive(false);

        if (mulliganCardArea != null)
            mulliganCardArea.SetActive(false);

        if (mulliganConfirmBtn != null)
            mulliganConfirmBtn.gameObject.SetActive(false);
    }

    /// <summary>
    /// 선택된 카드 인덱스를 수집한다.
    /// </summary>
    public List<int> CollectSelectedIndices(List<HandCardUI> cards)
    {
        var indices = new List<int>();
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].IsMulliganSelected)
                indices.Add(cards[i].HandIndex);
        }
        return indices;
    }

    // -- Private --

    private void EnsureCanvasTransform()
    {
        if (canvasTransform != null)
        {
            var cachedCanvas = canvasTransform.GetComponent<Canvas>();
            if (IsBattleSceneCanvas(cachedCanvas))
                return;

            canvasTransform = null;
        }

        if (IsBattleSceneCanvas(canvasRef))
        {
            canvasTransform = canvasRef.transform;
            return;
        }

        // 폴백: 부모 계층 → 씬 전체에서 Canvas 탐색
        var parentCanvas = GetComponentInParent<Canvas>();
        if (IsBattleSceneCanvas(parentCanvas))
        {
            canvasTransform = parentCanvas.transform;
            return;
        }

        var namedCanvas = GameObject.Find("Canvas")?.GetComponent<Canvas>();
        if (IsBattleSceneCanvas(namedCanvas))
        {
            canvasTransform = namedCanvas.transform;
            return;
        }

        var sceneCanvas = FindBattleSceneCanvas();
        if (sceneCanvas != null)
            canvasTransform = sceneCanvas.transform;
        else
            Debug.LogWarning("[MulliganUI] Canvas를 찾을 수 없습니다. Inspector에서 canvasRef를 연결하세요.");
    }

    private Canvas FindBattleSceneCanvas()
    {
        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);

        foreach (var canvas in canvases)
        {
            if (IsBattleSceneCanvas(canvas) && canvas.name == "Canvas")
                return canvas;
        }

        foreach (var canvas in canvases)
        {
            if (IsBattleSceneCanvas(canvas))
                return canvas;
        }

        return null;
    }

    private bool IsBattleSceneCanvas(Canvas canvas)
    {
        return canvas != null
            && canvas.gameObject.scene == gameObject.scene
            && canvas.GetComponentInParent<TutorialDialogueUI>() == null
            && canvas.GetComponentInParent<TutorialManager>() == null;
    }

    private void ShowInstructionText()
    {
        if (mulliganInstructionGO == null) return;

        // 프리팹이면 1회 인스턴스 생성
        if (mulliganInstructionGO.scene.name == null || mulliganInstructionGO.scene.rootCount == 0)
        {
            EnsureCanvasTransform();
            if (canvasTransform != null)
            {
                mulliganInstructionGO = Instantiate(mulliganInstructionGO, canvasTransform);
                Debug.Log("[MulliganUI] 멀리건 안내 텍스트 프리팹 인스턴스 생성 완료.");
            }
        }

        mulliganInstructionGO.SetActive(true);
    }

    private void BringInstructionToFront()
    {
        if (mulliganInstructionGO != null && mulliganInstructionGO.activeSelf)
            mulliganInstructionGO.transform.SetAsLastSibling();
    }

    private void CreateFocusOverlay(Transform handArea)
    {
        if (mulliganFocusOverlay != null)
        {
            ApplyFocusOverlaySettings();
            return;
        }

        EnsureCanvasTransform();
        if (canvasTransform == null) return;

        mulliganFocusOverlay = new GameObject("MulliganFocusOverlay",
            typeof(RectTransform), typeof(Image));
        mulliganFocusOverlay.transform.SetParent(canvasTransform, false);

        var overlayRT = mulliganFocusOverlay.GetComponent<RectTransform>();
        overlayRT.anchorMin = Vector2.zero;
        overlayRT.anchorMax = Vector2.one;
        overlayRT.offsetMin = Vector2.zero;
        overlayRT.offsetMax = Vector2.zero;
        overlayRT.pivot = new Vector2(0.5f, 0.5f);

        ApplyFocusOverlaySettings();

        mulliganFocusOverlay.transform.SetSiblingIndex(handArea.GetSiblingIndex());
    }

    private void CreateBGPanel(Transform handArea)
    {
        if (mulliganBGPanel != null)
        {
            ApplyBGImageSettings();
            return;
        }

        EnsureCanvasTransform();
        if (canvasTransform == null) return;

        mulliganBGPanel = new GameObject("MulliganBGPanel",
            typeof(RectTransform), typeof(Image));
        mulliganBGPanel.transform.SetParent(canvasTransform, false);

        ApplyBGImageSettings();

        mulliganBGPanel.transform.SetSiblingIndex(handArea.GetSiblingIndex());
    }

    private void ApplyFocusOverlaySettings()
    {
        if (mulliganFocusOverlay == null) return;

        var overlayImg = mulliganFocusOverlay.GetComponent<Image>();
        overlayImg.color = focusOverlayColor;
        overlayImg.raycastTarget = focusOverlayBlocksRaycasts;
    }

    private void ApplyBGImageSettings()
    {
        if (mulliganBGPanel == null) return;

        var bgImg = mulliganBGPanel.GetComponent<Image>();
        bgImg.sprite = GetMulliganBGSprite();
        bgImg.color = bgImg.sprite != null ? mulliganBGColor : new Color(0f, 0f, 0f, 0.6f);
        bgImg.raycastTarget = false;
    }

    private Sprite GetMulliganBGSprite()
    {
        if (mulliganBGSprite == null)
            mulliganBGSprite = Resources.Load<Sprite>(MulliganBGSpritePath);

        return mulliganBGSprite;
    }

    private void EnsureCardArea()
    {
        if (mulliganCardArea != null)
            return;

        EnsureCanvasTransform();
        if (canvasTransform == null) return;

        mulliganCardArea = new GameObject("MulliganCardArea", typeof(RectTransform));
        mulliganCardArea.transform.SetParent(canvasTransform, false);
    }

    private void FitCardAreaToCards(List<HandCardUI> cards, float cardScale)
    {
        if (cards == null || cards.Count == 0) return;

        EnsureCardArea();
        if (mulliganCardArea == null) return;

        var areaRT = mulliganCardArea.GetComponent<RectTransform>();
        areaRT.anchorMin = new Vector2(0.5f, 0.5f);
        areaRT.anchorMax = new Vector2(0.5f, 0.5f);
        areaRT.pivot = new Vector2(0.5f, 0.5f);

        float firstX = cards[0].RT.anchoredPosition.x;
        float lastX = cards[cards.Count - 1].RT.anchoredPosition.x;
        float cardY = cards[0].RT.anchoredPosition.y;
        float cardW = cards[0].RT.sizeDelta.x * cardScale;
        float cardH = cards[0].RT.sizeDelta.y * cardScale;
        float padding = 24f;

        float totalWidth = (lastX - firstX) + cardW + padding * 2f;
        float totalHeight = cardH + padding * 2f;
        float centerX = (firstX + lastX) * 0.5f;

        areaRT.anchoredPosition = new Vector2(centerX, cardY);
        areaRT.sizeDelta = new Vector2(totalWidth, totalHeight);

        mulliganCardArea.SetActive(true);
        mulliganCardArea.transform.SetAsLastSibling();
    }

    private void FitBGToCards(List<HandCardUI> cards, float cardScale)
    {
        if (mulliganBGPanel == null || cards.Count == 0) return;

        var bgRT = mulliganBGPanel.GetComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0.5f, 0.5f);
        bgRT.anchorMax = new Vector2(0.5f, 0.5f);

        float firstX = cards[0].RT.anchoredPosition.x;
        float lastX = cards[cards.Count - 1].RT.anchoredPosition.x;
        float cardY = cards[0].RT.anchoredPosition.y;
        float cardW = cards[0].RT.sizeDelta.x * cardScale;
        float cardH = cards[0].RT.sizeDelta.y * cardScale;

        float padding = 40f;
        float btnSpace = Mathf.Abs(confirmBtnOffset.y) + confirmBtnSize.y;
        float totalWidth = (lastX - firstX) + cardW + padding * 2;
        float cardTop = cardY + cardH / 2f + padding;
        float cardBottom = cardY - cardH / 2f - padding - btnSpace;
        float totalHeight = cardTop - cardBottom;
        float centerX = (firstX + lastX) / 2f;
        float centerY = (cardTop + cardBottom) / 2f;

        mulliganBGBaseSizeDelta = new Vector2(totalWidth, totalHeight);
        mulliganBGBaseAnchoredPosition = new Vector2(centerX, centerY) + mulliganBGPositionOffset;

        bgRT.sizeDelta = new Vector2(
            mulliganBGBaseSizeDelta.x * Mathf.Max(0.01f, mulliganBGSizeMultiplier.x),
            mulliganBGBaseSizeDelta.y * Mathf.Max(0.01f, mulliganBGSizeMultiplier.y));
        bgRT.anchoredPosition = mulliganBGBaseAnchoredPosition;

        mulliganBGPanel.transform.SetAsLastSibling();
        foreach (var cardUI in cards)
            cardUI.transform.SetAsLastSibling();
    }

    private void PositionConfirmButton()
    {
        if (mulliganConfirmBtn == null || mulliganBGPanel == null) return;

        var bgRT = mulliganBGPanel.GetComponent<RectTransform>();
        float btnSpace = Mathf.Abs(confirmBtnOffset.y) + confirmBtnSize.y;
        Vector2 baseSize = mulliganBGBaseSizeDelta == Vector2.zero ? bgRT.sizeDelta : mulliganBGBaseSizeDelta;
        Vector2 basePosition = mulliganBGBaseAnchoredPosition == Vector2.zero ? bgRT.anchoredPosition : mulliganBGBaseAnchoredPosition;
        float cardBottomY = basePosition.y - baseSize.y / 2f + btnSpace;

        var btnRT = mulliganConfirmBtn.GetComponent<RectTransform>();
        btnRT.anchoredPosition = new Vector2(basePosition.x, cardBottomY + confirmBtnOffset.y);
    }

    private void ConfigureConfirmButton(Button button)
    {
        if (button == null) return;

        var btnImg = button.GetComponent<Image>();
        if (btnImg != null)
            btnImg.color = confirmBtnColor;

        var btnRT = button.GetComponent<RectTransform>();
        if (btnRT != null)
            btnRT.sizeDelta = confirmBtnSize;

        var tmp = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            tmp.text = "교체 확인";
            tmp.fontSize = confirmBtnFontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;
            tmp.raycastTarget = false;

            if (confirmBtnFont != null)
                tmp.font = confirmBtnFont;
        }

        button.onClick.RemoveListener(OnConfirmButtonClicked);
        button.onClick.AddListener(OnConfirmButtonClicked);
    }

    private void OnConfirmButtonClicked()
    {
        CardGameSFXManager.PlayMulliganConfirm();
        OnConfirmClicked?.Invoke();
    }
}
