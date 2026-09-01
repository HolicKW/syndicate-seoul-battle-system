using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 실드 + 상태이상(힘/약화/바이러스/부식/오버클럭/행운)을 EntityState에서 읽어 UI 갱신.
/// PlayerHP/EnemyHP와 동일한 바인딩 패턴. 플레이어/적 양쪽에 동일하게 사용.
///
/// 아이콘 이미지 구조:
///   [Status]Panel (GameObject - value > 0 일 때만 활성)
///   ├-- [Status]Icon  (Image - Resources/Images/CardGameIcon 에서 자동 로드)
///   └-- [Status]Text  (TMP_Text - 스택 수, 아이콘 우하단 오버레이)
///
///</summary>
public class BattleStatusUI : MonoBehaviour
{
    // 텍스트 우하단 오버레이 오프셋 (픽셀 단위, Inspector에서 조정 가능)
    [Header("스택 텍스트 오프셋 (우하단 기준)")]
    [SerializeField] private Vector2 stackTextOffset = new Vector2(-2f, 2f);

    [Header("실드 (Shield)")]
    [SerializeField] private GameObject shieldPanel;
    [SerializeField] private Image      shieldIcon;
    [SerializeField] private TMP_Text   shieldText;

    [Header("힘 (Strength)")]
    [SerializeField] private GameObject strengthPanel;
    [SerializeField] private Image      strengthIcon;
    [SerializeField] private TMP_Text   strengthText;

    [Header("약화 (Weakness)")]
    [SerializeField] private GameObject weaknessPanel;
    [SerializeField] private Image      weaknessIcon;
    [SerializeField] private TMP_Text   weaknessText;

    [Header("바이러스 (Virus)")]
    [SerializeField] private GameObject virusPanel;
    [SerializeField] private Image      virusIcon;
    [SerializeField] private TMP_Text   virusText;

    [Header("부식 (Corrosion)")]
    [SerializeField] private GameObject corrosionPanel;
    [SerializeField] private Image      corrosionIcon;
    [SerializeField] private TMP_Text   corrosionText;

    [Header("오버클럭 (Overclock)")]
    [SerializeField] private GameObject overclockPanel;
    [SerializeField] private Image      overclockIcon;
    [SerializeField] private TMP_Text   overclockText;

    [Header("행운 (Luck)")]
    [SerializeField] private GameObject luckPanel;
    [SerializeField] private Image      luckIcon;
    [SerializeField] private TMP_Text   luckText;

    [Header("네트워크 스택 (Network)")]
    [SerializeField] private GameObject networkPanel;
    [SerializeField] private Image      networkIcon;
    [SerializeField] private TMP_Text   networkText;

    [Header("표시 옵션")]
    [SerializeField] private bool showShield = true;

    private EntityState state;
    private int lastShieldValue;

    // -- 스프라이트 경로 상수 --------------------------------------

    private const string IconBasePath = "Images/CardGameIcon/";

    // -- 초기화 ----------------------------------------------------

    private void Awake()
    {
        LoadSpritesFromResources();
        ConfigureStackTextOverlays();
        ConfigureStatusTooltips();
    }

    private void OnEnable()
    {
        ConfigureStatusTooltips();
    }

    private void LoadSpritesFromResources()
    {
        ApplySprite(strengthIcon,  IconBasePath + "AttackUp_Image");
        ApplySprite(weaknessIcon,  IconBasePath + "AttackDown_Image");
        ApplySprite(virusIcon,     IconBasePath + "Virus_Image");
        ApplySprite(corrosionIcon, IconBasePath + "Corrosion_Image");
        ApplySprite(overclockIcon, IconBasePath + "OverClock_Image");
        ApplySprite(luckIcon,      IconBasePath + "Lucky_Image");
        ApplySprite(networkIcon,   IconBasePath + "Network_Icon");
        ApplySprite(shieldIcon,    IconBasePath + "Shield_Image");
    }

    private static void ApplySprite(Image icon, string resourcePath)
    {
        if (icon == null) return;
        var sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null)
        {
            icon.sprite = sprite;
            icon.color  = Color.white;
        }
        else
        {
            icon.color = Color.clear;
            Debug.LogWarning($"[BattleStatusUI] 스프라이트를 찾을 수 없음: {resourcePath}");
        }
    }

    /// <summary>
    /// 각 스택 텍스트를 아이콘 우하단에 오버레이 되도록 RectTransform을 설정한다.
    /// </summary>
    private void ConfigureStackTextOverlays()
    {
        SetTextToCenter(shieldText);
        SetTextToBottomRight(strengthText);
        SetTextToBottomRight(weaknessText);
        SetTextToBottomRight(virusText);
        SetTextToBottomRight(corrosionText);
        SetTextToBottomRight(overclockText);
        SetTextToBottomRight(luckText);
        SetTextToBottomRight(networkText);
    }

    private void SetTextToCenter(TMP_Text text)
    {
        if (text == null) return;
        text.raycastTarget = false;

        var rt = text.rectTransform;
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
    }

    private void SetTextToBottomRight(TMP_Text text)
    {
        if (text == null) return;
        text.raycastTarget = false;

        var rt = text.rectTransform;
        rt.anchorMin        = new Vector2(1f, 0f);
        rt.anchorMax        = new Vector2(1f, 0f);
        rt.pivot            = new Vector2(1f, 0f);
        rt.anchoredPosition = stackTextOffset;
    }

    private void ConfigureStatusTooltips()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        ConfigureStatusTooltip(shieldPanel, shieldIcon, shieldText, canvas, "실드", "피해를 먼저 흡수하는 보호 수치입니다. 피해를 받을 때 HP보다 먼저 감소합니다.");
        ConfigureStatusTooltip(strengthPanel, strengthIcon, strengthText, canvas, "힘", "내가 주는 공격 피해가 이 수치만큼 증가합니다.");
        ConfigureStatusTooltip(weaknessPanel, weaknessIcon, weaknessText, canvas, "약화", "대상이 주는 공격 피해가 이 수치만큼 감소하고, 턴 종료 시 1씩 감소합니다.");
        ConfigureStatusTooltip(virusPanel, virusIcon, virusText, canvas, "바이러스", "대상에게 쌓이는 감염 스택입니다. 일부 카드는 바이러스를 소모하거나 바이러스 수치에 비례해 강해집니다.");
        ConfigureStatusTooltip(corrosionPanel, corrosionIcon, corrosionText, canvas, "부식", "대상의 턴 시작 시 실드를 깎고, 발동 후 1 감소합니다.");
        ConfigureStatusTooltip(overclockPanel, overclockIcon, overclockText, canvas, "오버클럭", "일부 카드의 조건과 효과를 강화하는 전투 스택입니다. 카드 사용으로 증가할 수 있습니다.");
        ConfigureStatusTooltip(luckPanel, luckIcon, luckText, canvas, "행운", "확률형 효과에 관여하는 전투 수치입니다.");
        ConfigureStatusTooltip(networkPanel, networkIcon, networkText, canvas, "네트워크", "네트워크 카드 효과와 연동되는 전투 스택입니다. 많을수록 관련 카드가 강해집니다.");
    }

    private static void ConfigureStatusTooltip(GameObject panel, Image icon, TMP_Text valueText, Canvas canvas, string title, string description)
    {
        if (panel == null && icon == null) return;

        GameObject target = panel != null ? panel : icon != null ? icon.gameObject : null;
        ConfigureTooltipTarget(target, valueText, canvas, title, description);

        if (panel != null)
        {
            var panelGraphic = panel.GetComponent<Graphic>();
            if (panelGraphic != null)
                panelGraphic.raycastTarget = true;
        }

        if (icon != null)
            icon.raycastTarget = panel == null;
    }

    private static void ConfigureTooltipTarget(GameObject target, TMP_Text valueText, Canvas canvas, string title, string description)
    {
        if (target == null) return;

        var tooltip = target.GetComponent<StatusIconTooltip>();
        if (tooltip == null)
            tooltip = target.AddComponent<StatusIconTooltip>();

        tooltip.Configure(canvas, title, description, valueText);
    }

    // -- 외부 접근용 -----------------------------------------------

    public enum StatusType { Shield, Strength, Weakness, Virus, Corrosion, Overclock, Luck, Network }

    public Image GetIcon(StatusType type) => type switch
    {
        StatusType.Shield    => shieldIcon,
        StatusType.Strength  => strengthIcon,
        StatusType.Weakness  => weaknessIcon,
        StatusType.Virus     => virusIcon,
        StatusType.Corrosion => corrosionIcon,
        StatusType.Overclock => overclockIcon,
        StatusType.Luck      => luckIcon,
        StatusType.Network   => networkIcon,
        _                    => null
    };

    // -- 바인딩 ----------------------------------------------------

    public void BindState(EntityState entityState)
    {
        ConfigureStatusTooltips();
        state = entityState;
        lastShieldValue = entityState != null ? entityState.shield : 0;
        SyncFromState();
    }

    public void SyncFromState()
    {
        if (state == null) return;
        ConfigureStatusTooltips();

        bool playShieldGainSfx = Application.isPlaying
            && isActiveAndEnabled
            && showShield
            && state.shield > lastShieldValue;

        if (showShield) SetPanelValue(shieldPanel, shieldText, state.shield);
        SetPanelValue(strengthPanel,  strengthText,  state.strength);
        SetPanelValue(weaknessPanel,  weaknessText,  state.weakness);
        SetPanelValue(virusPanel,     virusText,     state.virus);
        SetPanelValue(corrosionPanel, corrosionText, state.corrosion);
        SetPanelValue(overclockPanel, overclockText, state.overclockStacks);
        SetPanelValue(luckPanel,      luckText,      state.luck);
        SetPanelValue(networkPanel,   networkText,   state.networkStacks);

        if (playShieldGainSfx)
            CardGameSFXManager.PlayShieldGain();

        lastShieldValue = state.shield;
    }

    private void SetPanelValue(GameObject panel, TMP_Text text, int value)
    {
        if (panel != null)
            panel.SetActive(value > 0);
        if (text != null)
            text.text = value.ToString();
    }
}
