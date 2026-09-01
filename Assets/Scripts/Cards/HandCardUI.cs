using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

[ExecuteAlways]
public class HandCardUI : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerDownHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    [SerializeField] private TMP_Text cardNameText;
    [SerializeField] private TMP_Text cardCostText;
    [SerializeField] private TMP_Text cardDescriptionText;
    [SerializeField] private TMP_Text ownedCountText;
    [SerializeField] private Image cardImage;
    [SerializeField] private Image cardFrameImage;

    [Header("Type Visuals")]
    [SerializeField] private CardTypeVisualPalette typePalette;

    private static CardTypeVisualPalette cachedPaletteFallback;

    [Header("Condition Met Visual")]
    [SerializeField] private bool showConditionMetVisual = true;
    [SerializeField] private bool previewConditionMetAura;
    [SerializeField] private Color conditionMetAuraColor = new Color(0.34f, 0.95f, 1f, 0.55f);
    [SerializeField] private float conditionMetAuraPadding = 24f;
    [SerializeField] private float conditionMetPulseSpeed = 2.4f;
    [SerializeField] private float conditionMetPulseMinAlpha = 0.35f;
    [SerializeField] private float conditionMetPulseMaxAlpha = 0.75f;
    [SerializeField] private float conditionMetScalePulse = 0.035f;

    [Header("Hover Settings")]
    [SerializeField] private float hoverRaise = 60f;
    [SerializeField] private float hoverScale = 1.1f;

    private ICardClickBehavior behavior = BattleClickBehavior.Instance;

    private static Sprite fallbackCardSprite;
    private CardData cardData;
    private int handIndex;

    private Vector2 originalPosition;
    private Quaternion originalRotation = Quaternion.identity;
    private Vector3 originalScale = Vector3.one;
    private int originalSiblingIndex;
    private bool isHovered;
    private bool isDragging;

    // 해체 선택 모드
    private bool isDismantleSelected;

    public CardData CardData => cardData;
    public int HandIndex => handIndex;
    public bool IsDragging => isDragging;
    public bool IsHovered => isHovered;
    public Vector2 OriginalPosition => originalPosition;
    public Quaternion OriginalRotation => originalRotation;
    public Vector3 OriginalScale => originalScale;
    public int OriginalSiblingIndex => originalSiblingIndex;
    public float HoverRaise => hoverRaise;
    public float HoverScale => hoverScale;
    public Image CardImage => cardImage;
    public Image CardFrameImage => cardFrameImage;
    public TMP_Text CardDescriptionText => cardDescriptionText;
    public bool IsMulliganSelected => CardMulliganHandler.Instance != null && CardMulliganHandler.Instance.IsSelected(this);
    public bool IsDismantleSelected => isDismantleSelected;

    private RectTransform rt;
    private CanvasGroup canvasGroup;
    private UnityEngine.UI.Outline conditionMetOutline;
    private bool conditionMetVisualActive;
    private EntityState conditionPreviewCaster;
    private BattleEngine conditionPreviewEngine;

    public RectTransform RT
    {
        get
        {
            if (rt == null) rt = GetComponent<RectTransform>();
            return rt;
        }
    }

    void Awake()
    {
        rt = GetComponent<RectTransform>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null && Application.isPlaying)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        originalScale = transform.localScale;
        originalRotation = transform.localRotation;
    }

    // ===================================================
    //  데이터 바인딩
    // ===================================================

    public void Setup(CardData data, int index, EntityState caster = null) => Setup(data, index, 0, caster);

    public void Setup(CardData data, int index, int ownedCount, EntityState caster = null)
    {
        cardData = data;
        handIndex = index;
        conditionPreviewCaster = caster;
        conditionPreviewEngine = BattleEngine.Instance;

        behavior = (index == -1) ? InventoryClickBehavior.Instance : BattleClickBehavior.Instance;

        if (cardNameText != null)
        {
            cardNameText.text = data.cardName;
            cardNameText.color = CardTierColors.GetNameColor(data.tier);
        }

        if (cardCostText != null)
        {
            var engine = BattleEngine.Instance;
            int displayCost = (engine != null && caster != null)
                ? engine.GetEffectiveCost(data, caster)
                : data.cost;
            cardCostText.text = displayCost.ToString();
        }

        if (cardDescriptionText != null)
        {
            string description = caster != null
                ? data.GetFormattedDescription(caster)
                : data.GetFormattedDescription();
            cardDescriptionText.text = KeywordTooltipBuilder.BuildLinkedDescription(data, description);
        }

        if (handIndex == -1 && ownedCount > 0)
        {
            EnsureOwnedCountText();
            if (ownedCountText != null)
            {
                ownedCountText.text = $"x {ownedCount}";
                ownedCountText.gameObject.SetActive(true);
            }
        }
        else if (ownedCountText != null)
        {
            ownedCountText.gameObject.SetActive(false);
        }

        SetupArtwork(data);
        ApplyFrameColor(data);
        ApplyConditionMetVisual(data, caster);
    }

    private void ApplyFrameColor(CardData data)
    {
        if (cardFrameImage == null) return;

        var palette = typePalette;
        if (palette == null)
        {
            if (cachedPaletteFallback == null)
                cachedPaletteFallback = Resources.Load<CardTypeVisualPalette>("Databases/CardTypeVisualPalette");
            palette = cachedPaletteFallback;
        }

        cardFrameImage.color = palette != null
            ? palette.GetFrameColor(data)
            : CardTypeVisualPalette.GetDefaultFrameColor(data);
    }

    private void ApplyConditionMetVisual(CardData data, EntityState caster)
    {
        SetConditionMetVisualActive(ShouldShowConditionMetVisual(data, caster));
    }

    private bool ShouldShowConditionMetVisual(CardData data, EntityState caster)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && !gameObject.scene.IsValid())
            return false;

        if (!Application.isPlaying && previewConditionMetAura)
            return showConditionMetVisual;
#endif
        return IsConditionMetNow(data, caster);
    }

    private bool IsConditionMetNow(CardData data, EntityState caster)
    {
        return showConditionMetVisual &&
            data != null &&
            caster != null &&
            handIndex >= 0 &&
            CardConditionPreviewUtility.HasSatisfiedCondition(data, caster, conditionPreviewEngine ?? BattleEngine.Instance);
    }

    private void SetConditionMetVisualActive(bool active)
    {
        conditionMetVisualActive = active;

        if (active)
            EnsureConditionMetOutline();

        if (conditionMetOutline != null)
        {
            conditionMetOutline.enabled = active && gameObject.activeInHierarchy;
            if (active)
                ApplyConditionMetOutline(1f);
        }
    }

    private void EnsureConditionMetOutline()
    {
        if (conditionMetOutline != null || cardFrameImage == null)
            return;

        // 카드 프레임 이미지에 Outline 컴포넌트를 붙여 프레임 실루엣을 따라 외곽선을 그린다.
        conditionMetOutline = cardFrameImage.GetComponent<UnityEngine.UI.Outline>();
        if (conditionMetOutline == null)
        {
            conditionMetOutline = cardFrameImage.gameObject.AddComponent<UnityEngine.UI.Outline>();
            // 에디터 프리뷰에서 프레임 프리팹에 영구 직렬화되지 않도록 한다.
            conditionMetOutline.hideFlags = HideFlags.DontSave;
        }

        conditionMetOutline.useGraphicAlpha = true;
        conditionMetOutline.enabled = false;
    }

    // pulse01: 0(최소 강조) ~ 1(최대 강조)
    private void ApplyConditionMetOutline(float pulse01)
    {
        if (conditionMetOutline == null)
            return;

        Color color = conditionMetAuraColor;
        color.a = Mathf.Lerp(conditionMetPulseMinAlpha, conditionMetPulseMaxAlpha, pulse01);
        conditionMetOutline.effectColor = color;

        float thickness = conditionMetAuraPadding * (1f + conditionMetScalePulse * pulse01);
        conditionMetOutline.effectDistance = new Vector2(thickness, thickness);
    }

    // ===================================================
    //  시각 모드
    // ===================================================

    public void SetGhostMode(bool isGhost)
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null) canvasGroup.alpha = isGhost ? 0.35f : 1.0f;

        if (cardImage != null)
            cardImage.color = isGhost ? new Color(0.5f, 0.6f, 1.0f, 1.0f) : Color.white;

        if (cardNameText != null) cardNameText.alpha = isGhost ? 0.6f : 1.0f;
        if (cardCostText != null) cardCostText.alpha = isGhost ? 0.6f : 1.0f;
        if (cardDescriptionText != null) cardDescriptionText.alpha = isGhost ? 0.6f : 1.0f;
    }

    public void SetDismantleSelectMode(bool enabled, Action<HandCardUI> callback = null)
    {
        isDismantleSelected = false;
        behavior = enabled
            ? (ICardClickBehavior)new DismantleSelectClickBehavior(callback)
            : BattleClickBehavior.Instance;
        UpdateDismantleVisual();
    }

    public void SetDismantleSelected(bool selected)
    {
        isDismantleSelected = selected;
        UpdateDismantleVisual();
    }

    private void UpdateDismantleVisual()
    {
        if (cardImage == null) return;
        bool inDismantleMode = behavior is DismantleSelectClickBehavior;
        cardImage.color = (inDismantleMode && isDismantleSelected)
            ? new Color(1f, 0.4f, 0.4f)
            : Color.white;
    }

    // ===================================================
    //  상태 관리
    // ===================================================

    public void SaveOriginalTransform()
    {
        if (rt == null) rt = GetComponent<RectTransform>();
        originalPosition = rt.anchoredPosition;
        originalRotation = rt.localRotation;
        originalScale = rt.localScale;
        originalSiblingIndex = rt.GetSiblingIndex();
    }

    public void SetHoverState(bool hovered)
    {
        isHovered = hovered;
    }

    public void SetDragging(bool dragging)
    {
        isDragging = dragging;
    }

    public void SetBehavior(ICardClickBehavior newBehavior)
    {
        behavior = newBehavior ?? BattleClickBehavior.Instance;
    }

    // ===================================================
    //  호버 (HandHoverManager에서 제어)
    // ===================================================

    public void SetHovered(bool hovered)
    {
        if (isDragging || !behavior.AllowsHover) return;

        if (hovered && !isHovered)
        {
            isHovered = true;
            rt.SetAsLastSibling();

            CardKeywordTooltipManager.GetOrCreate().SetTrackedCard(this);
        }
        else if (!hovered && isHovered)
        {
            isHovered = false;
            rt.SetSiblingIndex(originalSiblingIndex);

            if (CardKeywordTooltipManager.Instance != null)
                CardKeywordTooltipManager.Instance.ClearTrackedCard(this);
        }
    }

    // ===================================================
    //  멀리건 (CardMulliganHandler에 위임)
    // ===================================================

    public void SetupMulligan(bool isMulligan)
    {
        behavior = isMulligan ? (ICardClickBehavior)new MulliganClickBehavior() : BattleClickBehavior.Instance;

        if (cardImage != null)
            cardImage.color = Color.white;

        if (CardMulliganHandler.Instance != null)
            CardMulliganHandler.Instance.SetupMulligan(this, isMulligan);
    }

    public void ToggleMulliganSelected()
    {
        if (CardMulliganHandler.Instance != null)
            CardMulliganHandler.Instance.ToggleSelected(this);
    }

    // ===================================================
    //  입력 이벤트 -> 매니저 위임
    // ===================================================

    public void OnPointerDown(PointerEventData eventData)
    {
        if (rt != null && ShouldBringToFrontOnPointer())
            rt.SetAsLastSibling();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (rt != null && ShouldBringToFrontOnPointer())
            rt.SetAsLastSibling();

        behavior.OnClick(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        CardKeywordTooltipManager.GetOrCreate().SetTrackedCard(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (CardKeywordTooltipManager.Instance != null)
            CardKeywordTooltipManager.Instance.ClearTrackedCard(this);
    }

    private bool ShouldBringToFrontOnPointer()
    {
        return behavior != null && (behavior.IsDraggable || behavior.AllowsHover);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!behavior.IsDraggable) return;

        if (CardKeywordTooltipManager.Instance != null)
            CardKeywordTooltipManager.Instance.Hide();

        if (CardDragManager.Instance != null)
            CardDragManager.Instance.OnBeginDrag(this, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!behavior.IsDraggable) return;

        if (CardDragManager.Instance != null)
            CardDragManager.Instance.OnDrag(this, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!behavior.IsDraggable) return;

        if (CardDragManager.Instance != null)
            CardDragManager.Instance.OnEndDrag(this, eventData);
    }

    // ===================================================
    //  콜백
    // ===================================================

    private Action<HandCardUI> onUseCallback;

    public void SetClickCallback(Action<HandCardUI> callback)
    {
        onUseCallback = callback;
    }

    public void InvokeUseCallback() => onUseCallback?.Invoke(this);

    private void Update()
    {
        if (rt == null) rt = GetComponent<RectTransform>();

        bool active = ShouldShowConditionMetVisual(cardData, conditionPreviewCaster);
        if (active != conditionMetVisualActive)
            SetConditionMetVisualActive(active);

        if (!conditionMetVisualActive || conditionMetOutline == null)
            return;

        float pulse = (Mathf.Sin(GetConditionMetPreviewTime() * conditionMetPulseSpeed) + 1f) * 0.5f;
        ApplyConditionMetOutline(pulse);
    }

    private static float GetConditionMetPreviewTime()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            return (float)UnityEditor.EditorApplication.timeSinceStartup;
#endif
        return Time.unscaledTime;
    }

    // ===================================================
    //  아트워크
    // ===================================================

    private void SetupArtwork(CardData data)
    {
        if (string.IsNullOrEmpty(data.artworkPath))
            return;

        Sprite artwork = Resources.Load<Sprite>(data.artworkPath);
        if (artwork == null)
            return;

        if (cardImage == null)
        {
            var artGO = new GameObject("CardArtwork", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            artGO.transform.SetParent(transform, false);

            var artRT = artGO.GetComponent<RectTransform>();
            artRT.anchorMin = new Vector2(0.05f, 0.25f);
            artRT.anchorMax = new Vector2(0.95f, 0.85f);
            artRT.offsetMin = Vector2.zero;
            artRT.offsetMax = Vector2.zero;
            artGO.transform.SetSiblingIndex(0);

            cardImage = artGO.GetComponent<Image>();
            cardImage.raycastTarget = false;
        }

        cardImage.enabled = true;
        cardImage.sprite = artwork;
        cardImage.preserveAspect = false;
    }

    private void EnsureOwnedCountText()
    {
        if (ownedCountText != null) return;

        var go = new GameObject("OwnedCountText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(transform, false);

        var rtText = go.GetComponent<RectTransform>();
        rtText.anchorMin = new Vector2(0.5f, 0f);
        rtText.anchorMax = new Vector2(0.5f, 0f);
        rtText.anchoredPosition = new Vector2(0, 20);
        rtText.sizeDelta = new Vector2(100, 30);

        ownedCountText = go.GetComponent<TextMeshProUGUI>();
        ownedCountText.fontSize = 20;
        ownedCountText.alignment = TextAlignmentOptions.Center;
        ownedCountText.color = new Color(1f, 0.92f, 0.016f);
        ownedCountText.fontStyle = FontStyles.Bold;
        ownedCountText.raycastTarget = false;

        var outline = go.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
    }

    private void OnDisable()
    {
        if (CardKeywordTooltipManager.Instance != null)
            CardKeywordTooltipManager.Instance.Hide();

        if (conditionMetOutline != null)
            conditionMetOutline.enabled = false;
    }
}
