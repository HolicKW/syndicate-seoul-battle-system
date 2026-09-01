using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class AssistStatusBarUI : MonoBehaviour
{
    private const string SurvivalIconPath = "UIs/health_assist";
    private const string OffenseIconPath = "UIs/attack_assist";
    private const string ResourceIconPath = "UIs/ability_assist";

    [Header("Layout")]
    [SerializeField, Min(1f)] private float iconSize = 30f;
    [SerializeField, Min(0f)] private float iconPadding = 3f;
    [SerializeField, Min(1f)] private float fallbackIconFontSize = 24f;
    [SerializeField] private Vector2Int rootPadding = new Vector2Int(5, 5);
    [SerializeField, Min(0f)] private float slotSpacing = 8f;

    [SerializeField] private Color activeSurvivalColor = new Color(0.96f, 0.24f, 0.30f, 1f);
    [SerializeField] private Color activeOffenseColor = new Color(1f, 0.55f, 0.16f, 1f);
    [SerializeField] private Color activeResourceColor = new Color(1f, 0.86f, 0.24f, 1f);
    [SerializeField] private Color inactiveIconColor = new Color(0.42f, 0.45f, 0.50f, 0.72f);
    [SerializeField] private Color activeSlotColor = new Color(0.12f, 0.14f, 0.20f, 0.92f);
    [SerializeField] private Color inactiveSlotColor = new Color(0.06f, 0.07f, 0.10f, 0.62f);

    private Canvas canvas;
    private AssistIconSlot survivalSlot;
    private AssistIconSlot offenseSlot;
    private AssistIconSlot resourceSlot;

    private void Awake()
    {
        EnsureBuilt();
    }

    private void OnValidate()
    {
        iconSize = Mathf.Max(1f, iconSize);
        iconPadding = Mathf.Clamp(iconPadding, 0f, iconSize * 0.5f);
        fallbackIconFontSize = Mathf.Max(1f, fallbackIconFontSize);
        rootPadding = new Vector2Int(Mathf.Max(0, rootPadding.x), Mathf.Max(0, rootPadding.y));
        slotSpacing = Mathf.Max(0f, slotSpacing);

        if (survivalSlot != null || offenseSlot != null || resourceSlot != null)
            ApplyLayoutSettings();
    }

    public void Bind(BattleModifierSnapshot modifiers)
    {
        EnsureBuilt();

        IReadOnlyListSafe supportEffects = new IReadOnlyListSafe(modifiers);
        ConfigureSlot(survivalSlot, AssistBuffSummaryBuilder.BuildGroup(AssistBuffGroup.Survival, supportEffects.Value));
        ConfigureSlot(offenseSlot, AssistBuffSummaryBuilder.BuildGroup(AssistBuffGroup.Offense, supportEffects.Value));
        ConfigureSlot(resourceSlot, AssistBuffSummaryBuilder.BuildGroup(AssistBuffGroup.Resource, supportEffects.Value));
    }

    private void EnsureBuilt()
    {
        if (survivalSlot != null && offenseSlot != null && resourceSlot != null)
            return;

        canvas = GetComponentInParent<Canvas>();
        ConfigureRoot();
        ClearChildren();

        survivalSlot = CreateSlot("SurvivalAssistIcon", SurvivalIconPath, "HP", activeSurvivalColor);
        offenseSlot = CreateSlot("OffenseAssistIcon", OffenseIconPath, "ATK", activeOffenseColor);
        resourceSlot = CreateSlot("ResourceAssistIcon", ResourceIconPath, "ENE", activeResourceColor);
    }

    private void ConfigureRoot()
    {
        var layout = GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
            layout = gameObject.AddComponent<HorizontalLayoutGroup>();

        layout.padding = new RectOffset(rootPadding.x, rootPadding.x, rootPadding.y, rootPadding.y);
        layout.spacing = slotSpacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var image = GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.1f, 0.1f, 0.15f, 0.7f);
            image.raycastTarget = false;
        }
    }

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    private AssistIconSlot CreateSlot(string objectName, string spriteResourcePath, string fallbackIconText, Color activeColor)
    {
        var slotGO = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        slotGO.transform.SetParent(transform, false);

        var rect = slotGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(iconSize, iconSize);

        var layoutElement = slotGO.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = iconSize;
        layoutElement.preferredHeight = iconSize;
        layoutElement.minWidth = iconSize;
        layoutElement.minHeight = iconSize;
        layoutElement.flexibleWidth = 0f;
        layoutElement.flexibleHeight = 0f;

        var background = slotGO.GetComponent<Image>();
        background.color = inactiveSlotColor;
        background.raycastTarget = true;

        var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGO.transform.SetParent(slotGO.transform, false);

        var iconRect = iconGO.GetComponent<RectTransform>();
        iconRect.localScale = new Vector3(2f, 2f, 2f);
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(iconPadding, iconPadding);
        iconRect.offsetMax = new Vector2(-iconPadding, -iconPadding);

        var iconImage = iconGO.GetComponent<Image>();
        iconImage.sprite = Resources.Load<Sprite>(spriteResourcePath);
        iconImage.preserveAspect = true;
        iconImage.color = inactiveIconColor;
        iconImage.raycastTarget = false;

        TMP_Text fallbackIcon = null;
        if (iconImage.sprite == null)
        {
            iconImage.enabled = false;
            fallbackIcon = CreateFallbackIcon(slotGO.transform, fallbackIconText);
        }

        return new AssistIconSlot(slotGO, background, iconImage, fallbackIcon, activeColor);
    }

    private TMP_Text CreateFallbackIcon(Transform parent, string iconText)
    {
        var textGO = new GameObject("FallbackIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(parent, false);

        var textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var icon = textGO.GetComponent<TMP_Text>();
        icon.text = iconText;
        icon.fontSize = fallbackIconFontSize;
        icon.fontStyle = FontStyles.Bold;
        icon.alignment = TextAlignmentOptions.Center;
        icon.color = inactiveIconColor;
        icon.raycastTarget = false;

        return icon;
    }

    private void ApplyLayoutSettings()
    {
        ConfigureRoot();
        ApplySlotLayout(survivalSlot);
        ApplySlotLayout(offenseSlot);
        ApplySlotLayout(resourceSlot);
    }

    private void ApplySlotLayout(AssistIconSlot slot)
    {
        if (slot == null)
            return;

        var rect = slot.Root.GetComponent<RectTransform>();
        if (rect != null)
            rect.sizeDelta = new Vector2(iconSize, iconSize);

        var layoutElement = slot.Root.GetComponent<LayoutElement>();
        if (layoutElement != null)
        {
            layoutElement.preferredWidth = iconSize;
            layoutElement.preferredHeight = iconSize;
            layoutElement.minWidth = iconSize;
            layoutElement.minHeight = iconSize;
        }

        if (slot.IconImage != null)
        {
            var iconRect = slot.IconImage.GetComponent<RectTransform>();
            if (iconRect != null)
            {
                iconRect.localScale = new Vector3(2f, 2f, 2f);
                iconRect.offsetMin = new Vector2(iconPadding, iconPadding);
                iconRect.offsetMax = new Vector2(-iconPadding, -iconPadding);
            }
        }

        if (slot.FallbackIcon != null)
            slot.FallbackIcon.fontSize = fallbackIconFontSize;
    }

    private void ConfigureSlot(AssistIconSlot slot, AssistBuffGroupSummary summary)
    {
        if (slot == null || summary == null)
            return;

        bool isActive = summary.HasActiveEffects;
        slot.Background.color = isActive ? activeSlotColor : inactiveSlotColor;
        slot.SetIconColor(isActive, inactiveIconColor);

        var tooltip = slot.Root.GetComponent<StatusIconTooltip>();
        if (tooltip == null)
            tooltip = slot.Root.AddComponent<StatusIconTooltip>();

        tooltip.Configure(canvas, summary.Title, summary.BuildTooltip(), null);
    }

    private sealed class AssistIconSlot
    {
        public GameObject Root { get; }
        public Image Background { get; }
        public Image IconImage { get; }
        public TMP_Text FallbackIcon { get; }
        public Color ActiveColor { get; }

        public AssistIconSlot(
            GameObject root,
            Image background,
            Image iconImage,
            TMP_Text fallbackIcon,
            Color activeColor)
        {
            Root = root;
            Background = background;
            IconImage = iconImage;
            FallbackIcon = fallbackIcon;
            ActiveColor = activeColor;
        }

        public void SetIconColor(bool isActive, Color inactiveColor)
        {
            if (IconImage != null && IconImage.enabled)
                IconImage.color = isActive ? Color.white : inactiveColor;

            if (FallbackIcon != null)
                FallbackIcon.color = isActive ? ActiveColor : inactiveColor;
        }
    }

    private readonly struct IReadOnlyListSafe
    {
        public System.Collections.Generic.IReadOnlyList<BattleSupportEffect> Value { get; }

        public IReadOnlyListSafe(BattleModifierSnapshot modifiers)
        {
            Value = modifiers != null && modifiers.supportEffects != null
                ? modifiers.supportEffects
                : System.Array.Empty<BattleSupportEffect>();
        }
    }
}
