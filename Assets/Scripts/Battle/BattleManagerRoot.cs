using UnityEngine;

public class BattleManagerRoot : MonoBehaviour
{
    public static BattleManagerRoot Instance { get; private set; }

    [Header("Hand")]
    [SerializeField] private HandUIManager handManager;
    [SerializeField] private HandHoverManager hoverManager;
    [SerializeField] private CardDragManager dragManager;

    [Header("Phase")]
    [SerializeField] private MulliganUI mulliganUI;
    [SerializeField] private CardMulliganHandler mulliganHandler;
    [SerializeField] private DismantleSelectUI dismantleManager;

    [Header("Feedback")]
    [SerializeField] private CardGameSFXManager sfxManager;
    [SerializeField] private CardKeywordTooltipManager tooltipManager;

    [Header("VFX")]
    [SerializeField] private CardUseDataScatterVfx cardUseVfx;
    [SerializeField] private CardDrawAssembleVfx cardDrawVfx;
    [SerializeField] private CardDismantleVfx cardDismantleVfx;
    [SerializeField] private TurnBannerVfx turnBannerVfx;
    [SerializeField] private GambleResultVfx gambleResultVfx;

    public HandUIManager HandManager => handManager;
    public HandHoverManager HoverManager => hoverManager;
    public CardDragManager DragManager => dragManager;
    public MulliganUI MulliganUI => mulliganUI;
    public CardMulliganHandler MulliganHandler => mulliganHandler;
    public DismantleSelectUI DismantleManager => dismantleManager;
    public CardGameSFXManager SfxManager => sfxManager;
    public CardKeywordTooltipManager TooltipManager => tooltipManager;
    public CardUseDataScatterVfx CardUseVfx => cardUseVfx;
    public CardDrawAssembleVfx CardDrawVfx => cardDrawVfx;
    public CardDismantleVfx CardDismantleVfx => cardDismantleVfx;
    public TurnBannerVfx TurnBannerVfx => turnBannerVfx;
    public GambleResultVfx GambleResultVfx => gambleResultVfx;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        ResolveChildren();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void ResolveChildren()
    {
        if (handManager == null) handManager = GetComponentInChildren<HandUIManager>();
        if (hoverManager == null) hoverManager = GetComponentInChildren<HandHoverManager>();
        if (dragManager == null) dragManager = GetComponentInChildren<CardDragManager>();
        if (mulliganUI == null) mulliganUI = GetComponentInChildren<MulliganUI>();
        if (mulliganHandler == null) mulliganHandler = GetComponentInChildren<CardMulliganHandler>();
        if (dismantleManager == null) dismantleManager = GetComponentInChildren<DismantleSelectUI>();
        if (sfxManager == null) sfxManager = GetComponentInChildren<CardGameSFXManager>();
        if (tooltipManager == null) tooltipManager = GetComponentInChildren<CardKeywordTooltipManager>();
        if (cardUseVfx == null) cardUseVfx = GetComponentInChildren<CardUseDataScatterVfx>();
        if (cardDrawVfx == null) cardDrawVfx = GetComponentInChildren<CardDrawAssembleVfx>();
        if (cardDismantleVfx == null) cardDismantleVfx = GetComponentInChildren<CardDismantleVfx>();
        if (turnBannerVfx == null) turnBannerVfx = GetComponentInChildren<TurnBannerVfx>();
        if (gambleResultVfx == null) gambleResultVfx = GetComponentInChildren<GambleResultVfx>();
    }
}
