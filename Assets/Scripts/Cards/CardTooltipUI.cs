using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 로그 내 카드 이름 호버 시 화면 정중앙에 표시되는 카드 프리뷰 오버레이.
/// 실제 손패 카드 프리팹(Resources/Prefabs/card)을 그대로 재사용해 카드를 보여주고,
/// 키워드 효과 설명은 우측 별도 패널로 분리해 표시한다.
/// BattleLogUI가 Init()으로 초기화하고, Show/Hide로 제어한다.
/// </summary>
public class CardTooltipUI : MonoBehaviour
{
    private GameObject    overlayGO;
    private RectTransform groupRT;     // 카드 + 키워드 패널을 묶는 중앙 정렬 컨테이너

    private HandCardUI    cardInstance;
    private RectTransform cardRT;

    private RectTransform keywordPanelRT;
    private TMP_Text      keywordText;

    private Canvas        rootCanvas;
    private TMP_FontAsset font;

    private const float CardDisplayH = 460f; // 카드 표시 높이(이 값에 맞춰 스케일 산출)
    private const float KeywordW     = 300f;
    private const float Gap          = 18f;
    private const float Pad          = 14f;

    private static readonly Color KwBg     = new Color(0.055f, 0.065f, 0.095f, 0.985f);
    private static readonly Color KwBorder = new Color(0.353f, 0.784f, 0.898f, 0.9f); // 스킬 시안

    /// <summary>캔버스(와 선택적 폰트)를 받아 오버레이와 패널을 동적으로 생성한다.</summary>
    public void Init(Canvas canvas, TMP_FontAsset font = null)
    {
        rootCanvas = canvas;
        this.font  = font;
        BuildOverlay();
        BuildGroup();
        BuildKeywordPanel();
        SetVisible(false);
    }

    public void Show(string cardId, Vector2 screenPos)
    {
        var card = CardDatabase.Instance.GetById(cardId);
        if (card == null) { Hide(); return; }

        if (!EnsureCardInstance()) { Hide(); return; }

        // 카드 프리팹을 표시 전용으로 바인딩(index -1 = 비전투 표시 모드)
        cardInstance.Setup(card, -1);
        cardInstance.SetGhostMode(false);
        cardInstance.SetClickCallback(null);
        cardInstance.gameObject.SetActive(true);

        // 카드 스케일을 표시 높이에 맞춰 산출
        float rawH = cardRT.rect.height;
        float scale = rawH > 1f ? Mathf.Clamp(CardDisplayH / rawH, 0.6f, 1.8f) : 1f;
        cardRT.localScale = new Vector3(scale, scale, 1f);

        float cardW = cardRT.rect.width  * scale;
        float cardH = cardRT.rect.height * scale;

        // 키워드 섹션(BuildSection 앞쪽 공백 제거)
        string keywordBody = KeywordTooltipBuilder.BuildSection(card).TrimStart('\n', '\r', ' ');
        bool hasKeyword = !string.IsNullOrEmpty(keywordBody);

        keywordPanelRT.gameObject.SetActive(hasKeyword);

        if (hasKeyword)
        {
            keywordText.text = keywordBody;
            keywordPanelRT.sizeDelta = new Vector2(KeywordW, cardH);

            float totalW = cardW + Gap + KeywordW;
            cardRT.anchoredPosition         = new Vector2(-totalW * 0.5f + cardW * 0.5f, 0f);
            keywordPanelRT.anchoredPosition = new Vector2( totalW * 0.5f - KeywordW * 0.5f, 0f);
        }
        else
        {
            cardRT.anchoredPosition = Vector2.zero;
        }

        SetVisible(true);
        groupRT.SetAsLastSibling();
    }

    public void Hide() => SetVisible(false);

    // -- 내부 --

    private void SetVisible(bool visible)
    {
        if (overlayGO != null) overlayGO.SetActive(visible);
        if (groupRT   != null) groupRT.gameObject.SetActive(visible);
    }

    private bool EnsureCardInstance()
    {
        if (cardInstance != null) return true;

        var prefab = Resources.Load<HandCardUI>("Prefabs/card");
        if (prefab == null)
        {
            Debug.LogWarning("[CardTooltipUI] Prefabs/card 프리팹을 찾을 수 없습니다.");
            return false;
        }

        cardInstance = Instantiate(prefab, groupRT);
        cardRT = cardInstance.GetComponent<RectTransform>();
        cardRT.anchorMin = cardRT.anchorMax = cardRT.pivot = new Vector2(0.5f, 0.5f);
        cardRT.anchoredPosition = Vector2.zero;

        // 툴팁 카드는 정적 표시이므로 입력을 가로채지 않게 한다(로그 호버 유지).
        var cg = cardInstance.GetComponent<CanvasGroup>();
        if (cg == null) cg = cardInstance.gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable   = false;

        return true;
    }

    private void BuildOverlay()
    {
        overlayGO = NewImage("CardTooltipOverlay", rootCanvas.transform, out var img);
        var rt = overlayGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        img.color         = new Color(0f, 0f, 0f, 0.62f);
        img.raycastTarget = false; // 로그 hover 계속 작동하도록
    }

    private void BuildGroup()
    {
        var go = new GameObject("CardTooltipGroup", typeof(RectTransform));
        go.transform.SetParent(rootCanvas.transform, false);
        groupRT = go.GetComponent<RectTransform>();
        groupRT.anchorMin = groupRT.anchorMax = groupRT.pivot = new Vector2(0.5f, 0.5f);
        groupRT.anchoredPosition = Vector2.zero;
        groupRT.sizeDelta = Vector2.zero;
    }

    private void BuildKeywordPanel()
    {
        // 외곽 테두리
        var borderGO = NewImage("KeywordPanel", groupRT, out var border);
        keywordPanelRT = borderGO.GetComponent<RectTransform>();
        keywordPanelRT.anchorMin = keywordPanelRT.anchorMax = keywordPanelRT.pivot = new Vector2(0.5f, 0.5f);
        keywordPanelRT.sizeDelta = new Vector2(KeywordW, CardDisplayH);
        border.color = KwBorder;

        var glow = borderGO.AddComponent<Outline>();
        glow.effectColor    = new Color(KwBorder.r, KwBorder.g, KwBorder.b, 0.5f);
        glow.effectDistance = new Vector2(5f, -5f);

        // 내부 배경
        var bgGO = NewImage("KW_Bg", borderGO.transform, out var bg);
        StretchInset(bgGO.GetComponent<RectTransform>(), 2.5f);
        bg.color = KwBg;

        // 상단 타이틀
        var title = MakeText(bgGO.transform, "KW_Title",
            new Vector2(Pad, -Pad), new Vector2(KeywordW - Pad * 2f, 24f),
            15f, FontStyles.Bold, new Color(0.65f, 0.85f, 1f));
        title.text = "키워드 효과";

        // 구분선
        var divGO = NewImage("KW_Divider", bgGO.transform, out var div);
        var divRT = divGO.GetComponent<RectTransform>();
        divRT.anchorMin = new Vector2(0f, 1f);
        divRT.anchorMax = new Vector2(1f, 1f);
        divRT.pivot     = new Vector2(0.5f, 1f);
        divRT.offsetMin = new Vector2(Pad, -Pad - 28f);
        divRT.offsetMax = new Vector2(-Pad, -Pad - 27f);
        div.color = new Color(0.4f, 0.6f, 1f, 0.4f);

        // 키워드 본문(상단 영역을 채우고 아래로 흐름)
        keywordText = MakeText(bgGO.transform, "KW_Body",
            new Vector2(Pad, -Pad - 36f), new Vector2(KeywordW - Pad * 2f, CardDisplayH - 60f),
            13f, FontStyles.Normal, new Color(0.9f, 0.9f, 0.92f));
        keywordText.textWrappingMode = TMPro.TextWrappingModes.Normal;
        keywordText.overflowMode     = TextOverflowModes.Overflow;
        keywordText.lineSpacing      = 8f;
        var kbRT = keywordText.rectTransform;
        kbRT.anchorMin = new Vector2(0f, 0f);
        kbRT.anchorMax = new Vector2(1f, 1f);
        kbRT.offsetMin = new Vector2(Pad, Pad);
        kbRT.offsetMax = new Vector2(-Pad, -Pad - 36f);
    }

    // ---- 빌드 헬퍼 ----

    private static GameObject NewImage(string name, Transform parent, out Image image)
    {
        var go = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return go;
    }

    private static void StretchInset(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    private TMP_Text MakeText(Transform parent, string name,
        Vector2 anchoredPos, Vector2 size,
        float fontSize, FontStyles style, Color color)
    {
        var go = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = size;

        var txt = go.GetComponent<TMP_Text>();
        txt.fontSize         = fontSize;
        txt.fontStyle        = style;
        txt.color            = color;
        txt.alignment        = TextAlignmentOptions.TopLeft;
        txt.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        txt.overflowMode     = TextOverflowModes.Overflow;
        txt.raycastTarget    = false;
        if (font != null) txt.font = font;
        return txt;
    }
}
