using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 전투 진입점 및 페이즈 전환 컨트롤러.
/// BattleEngine/TurnManager에 게임 로직을 위임하고,
/// UI 관리는 HandUIManager/MulliganUI에 위임한다.
/// 덱 빌드 → StarterDeckFactory / UI 동기화 → BattleInitializer.UIBinder.cs
/// </summary>
public partial class BattleInitializer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DeckManager deckManager;
    [SerializeField] private Transform handArea;
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private EnemyHP enemyHP;
    [SerializeField] private PlayerHP playerHP;
    [SerializeField] private PlayerDeck playerDeck;
    [SerializeField] private CEODatabaseSO ceoDatabase;
    [SerializeField] private float cardScale = 1.5f;

    [Header("전투 설정")]
    [SerializeField] private int playerMaxHp = PlayerBattleBaseStats.BaseMaxHp;
    [SerializeField] private int playerBaseEnergy = PlayerBattleBaseStats.BaseEnergy;
    [SerializeField] private int enemyMaxHp = 50;
    [SerializeField] private int enemyBaseEnergy = 3;

    [Header("플레이어 상태 UI")]
    [SerializeField] private BattleStatusUI playerStatusUI;
    [SerializeField] private AssistStatusBarUI assistStatusBarUI;
    [SerializeField] private EnergyUI playerEnergyUI;
    [FormerlySerializedAs("player" + "Power" + "ListUI")]
    [SerializeField] private CoreListUI playerCoreListUI;

    [Header("적 상태 UI")]
    [SerializeField] private BattleStatusUI enemyStatusUI;
    [SerializeField] private EnergyUI enemyEnergyUI;
    [FormerlySerializedAs("enemy" + "Power" + "ListUI")]
    [SerializeField] private CoreListUI enemyCoreListUI;

    [Header("전투 로그 / 훈련 모드 (허수아비)")]
    [SerializeField] private bool trainingMode;
    [SerializeField] private BattleLogUI battleLogUI;

    [Header("BGM")]
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.5f;

    // 페이즈 상태
    private bool isMulliganPhase = true;
    private AudioSource bgmSource;

    [Header("Battle Components (Inspector 연결 필수)")]
    [SerializeField] private BattleManagerRoot battleManagerRoot;
    [SerializeField] private BattleEngine engine;
    [SerializeField] private TurnManager turnManager;
    [FormerlySerializedAs("powerManager")]
    [SerializeField] private CoreManager coreManager;
    [SerializeField] private EnemyAI enemyAI;
    [SerializeField] private ScavengerEnemyScript scavengerEnemyScript;
    [SerializeField] private EnemyAIProfileSO enemyAIProfile;
    [SerializeField] private HandUIManager handUIManager;
    [SerializeField] private MulliganUI mulliganUI;
    [SerializeField] private DismantleSelectUI dismantleSelectUI;
    [SerializeField] private OpenAccessTypeSelectUI openAccessTypeSelectUI;
    [SerializeField] private CardUseDataScatterVfx cardUseVfx;
    [SerializeField] private CardDrawAssembleVfx cardDrawVfx;
    [SerializeField] private CardDismantleVfx cardDismantleVfx;
    [SerializeField] private TurnBannerVfx turnBannerVfx;
    [SerializeField] private GambleResultVfx gambleResultVfx;
    [SerializeField] private CardGameSFXManager sfxManager;
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private UnityEngine.UI.Button endTurnButton;
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private UnityEngine.UI.Button pauseMenuDismissButton;
    [SerializeField] private UnityEngine.UI.Button resumeButton;
    [SerializeField] private UnityEngine.UI.Button surrenderButton;

    private bool isResolvingCardUse;
    private bool isTurnStartDrawAnimating;
    private bool isInitialHandAnimating;
    private bool playPostMulliganHandVfxOnNextTurnStart;

    void Start()
    {
        if (cardPrefab == null)
            cardPrefab = Resources.Load<GameObject>("Prefabs/card");

        if (handArea == null)
            handArea = transform;

        InitComponents();

        if (BattleSceneData.IsTutorial && TutorialManager.Instance != null)
            TutorialManager.Instance.BeginBattleTutorial();
        else if (BattleSceneData.IsTutorial)
        {
            Debug.LogWarning("[BattleInitializer] 튜토리얼 전투 플래그가 있지만 TutorialManager가 없어 일반 전투로 시작합니다.");
            BattleSceneData.IsTutorial = false;
        }

        StartBattle();
    }

    void OnEnable()
    {
        SoundManager.GetOrCreate().SettingsChanged += ApplyBgmVolume;
    }

    void OnDestroy()
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.SettingsChanged -= ApplyBgmVolume;

        if (engine != null)
        {
            engine.OnStateChanged -= SyncHpUI;
            engine.OnGambleResult -= OnGambleResult;
        }
        if (turnManager != null)
            turnManager.OnTurnStart -= OnTurnStarted;
        if (endTurnButton != null)
            endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
        if (pauseMenuDismissButton != null)
            pauseMenuDismissButton.onClick.RemoveListener(ClosePauseMenu);
        if (resumeButton != null)
            resumeButton.onClick.RemoveListener(OnResumeClicked);
        if (surrenderButton != null)
            surrenderButton.onClick.RemoveListener(OnSurrenderClicked);
    }

    // ===================================================
    //  컴포넌트 초기화
    // ===================================================

    private void InitComponents()
    {
        // BattleManagerRoot에서 매니저 참조 획득 (Inspector 미연결 시 폴백)
        if (battleManagerRoot == null)
            battleManagerRoot = FindFirstObjectByType<BattleManagerRoot>();

        if (battleManagerRoot != null)
        {
            if (handUIManager == null) handUIManager = battleManagerRoot.HandManager;
            if (mulliganUI == null) mulliganUI = battleManagerRoot.MulliganUI;
            if (dismantleSelectUI == null) dismantleSelectUI = battleManagerRoot.DismantleManager;
            if (sfxManager == null) sfxManager = battleManagerRoot.SfxManager;
            if (cardUseVfx == null) cardUseVfx = battleManagerRoot.CardUseVfx;
            if (cardDrawVfx == null) cardDrawVfx = battleManagerRoot.CardDrawVfx;
            if (cardDismantleVfx == null) cardDismantleVfx = battleManagerRoot.CardDismantleVfx;
            if (turnBannerVfx == null) turnBannerVfx = battleManagerRoot.TurnBannerVfx;
            if (gambleResultVfx == null) gambleResultVfx = battleManagerRoot.GambleResultVfx;
        }

        // SerializeField 미연결 시 GetComponent 폴백
        if (mulliganUI       == null) mulliganUI       = GetComponent<MulliganUI>();
        if (handUIManager    == null) handUIManager    = GetComponent<HandUIManager>();
        if (engine           == null) engine           = GetComponent<BattleEngine>();
        if (turnManager      == null) turnManager      = GetComponent<TurnManager>();
        if (coreManager == null) coreManager = GetComponent<CoreManager>();
        if (enemyAI          == null) enemyAI          = GetComponent<EnemyAI>();
        if (scavengerEnemyScript == null) scavengerEnemyScript = GetComponent<ScavengerEnemyScript>();
        // 스캐빈저 슬롯 해금 전투에서만 스캐빈저 적(덱/HP/AI)을 사용한다. 일반 전쟁에선 비활성화.
        // ScavengerEnemyScript는 BattleInitializer와 같은 GameObject에 있으므로
        // gameObject.SetActive가 아니라 컴포넌트 enabled로만 토글한다(배틀 루트 비활성화 방지).
        if (scavengerEnemyScript != null)
            scavengerEnemyScript.enabled = BattleSceneData.ScavengerSlotUnlock;
        if (dismantleSelectUI == null) dismantleSelectUI = GetComponent<DismantleSelectUI>();
        if (openAccessTypeSelectUI == null) openAccessTypeSelectUI = GetComponent<OpenAccessTypeSelectUI>();
        if (cardUseVfx      == null) cardUseVfx      = GetComponent<CardUseDataScatterVfx>();
        if (cardDrawVfx     == null) cardDrawVfx     = GetComponent<CardDrawAssembleVfx>();
        if (cardDismantleVfx == null) cardDismantleVfx = GetComponent<CardDismantleVfx>();
        if (turnBannerVfx   == null) turnBannerVfx   = GetComponent<TurnBannerVfx>();
        if (gambleResultVfx == null) gambleResultVfx = GetComponent<GambleResultVfx>();
        if (sfxManager      == null) sfxManager      = GetComponent<CardGameSFXManager>();
        if (rootCanvas       == null) rootCanvas       = GetComponentInParent<Canvas>();
        if (rootCanvas       == null) rootCanvas       = FindFirstObjectByType<Canvas>();
        if (assistStatusBarUI == null) assistStatusBarUI = FindAssistStatusBarUI();

        if (cardUseVfx == null)
            cardUseVfx = gameObject.AddComponent<CardUseDataScatterVfx>();
        if (cardDrawVfx == null)
            cardDrawVfx = gameObject.AddComponent<CardDrawAssembleVfx>();
        if (cardDismantleVfx == null)
            cardDismantleVfx = gameObject.AddComponent<CardDismantleVfx>();
        if (turnBannerVfx == null)
            turnBannerVfx = gameObject.AddComponent<TurnBannerVfx>();
        if (gambleResultVfx == null)
            gambleResultVfx = gameObject.AddComponent<GambleResultVfx>();
        if (sfxManager == null)
            sfxManager = gameObject.AddComponent<CardGameSFXManager>();
        if (openAccessTypeSelectUI == null)
            openAccessTypeSelectUI = gameObject.AddComponent<OpenAccessTypeSelectUI>();

        if (mulliganUI == null) { Debug.LogError("[BattleInitializer] MulliganUI 컴포넌트가 없습니다. Inspector에서 연결하거나 씬에 추가하세요."); return; }
        if (engine     == null) { Debug.LogError("[BattleInitializer] BattleEngine 컴포넌트가 없습니다."); return; }

        mulliganUI.OnConfirmClicked += OnMulliganConfirm;

        // EndTurnButton 폰트를 MulliganUI에 전달
        if (endTurnButton != null)
        {
            var textObj = endTurnButton.transform.Find("EndTurnBtn_Text");
            var tmpText = textObj != null
                ? textObj.GetComponent<TMPro.TMP_Text>()
                : endTurnButton.GetComponentInChildren<TMPro.TMP_Text>();
            if (tmpText != null && tmpText.font != null)
                mulliganUI.SetFallbackFont(tmpText.font);

            endTurnButton.onClick.AddListener(OnEndTurnClicked);
        }

        EnsurePauseMenuPanel();
        if (pauseMenuDismissButton != null)
        {
            pauseMenuDismissButton.onClick.RemoveListener(ClosePauseMenu);
            pauseMenuDismissButton.onClick.AddListener(ClosePauseMenu);
        }

        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveListener(OnResumeClicked);
            resumeButton.onClick.AddListener(OnResumeClicked);
        }

        if (surrenderButton != null)
        {
            surrenderButton.onClick.RemoveListener(OnSurrenderClicked);
            surrenderButton.onClick.AddListener(OnSurrenderClicked);
        }

        if (handUIManager == null) { Debug.LogError("[BattleInitializer] HandUIManager 컴포넌트가 없습니다."); return; }
        handUIManager.Init(handArea, cardPrefab, cardScale, mulliganUI);
        UpdateTurnInputState();
    }

    // ===================================================
    //  전투 시작
    // ===================================================

    void StartBattle()
    {
        var deckCards = DeckProviderResolver.Resolve(playerDeck).Build();

        if (deckCards.Count == 0)
        {
            Debug.LogError("[BattleInitializer] 덱에 카드가 없습니다.");
            return;
        }

        ClearBattleLog();

        // BattleEngine 초기화
        ApplyEnemyCeoProfile();
        BattleModifierSnapshot attackerModifiers = BattleSceneData.AttackerModifiers ?? BattleModifierSnapshot.Empty();
        BattleModifierSnapshot defenderModifiers = BattleSceneData.DefenderModifiers ?? BattleModifierSnapshot.Empty();
        assistStatusBarUI?.Bind(attackerModifiers);
        int actualEnemyHp = trainingMode ? 99999 : Mathf.Max(1, ResolveEnemyMaxHp() + defenderModifiers.maxHpBonus);
        int actualPlayerHp = Mathf.Max(1, playerMaxHp + attackerModifiers.maxHpBonus);
        int actualPlayerEnergy = Mathf.Max(0, playerBaseEnergy + attackerModifiers.maxEnergyBonus);
        int actualEnemyEnergy = Mathf.Max(0, enemyBaseEnergy + defenderModifiers.maxEnergyBonus);
        BattleDeckInfectionApplier.InsertRansomwareIntoDeck(deckCards, defenderModifiers.supportEffects);
        engine.InitBattle(deckCards, actualPlayerHp, actualPlayerEnergy,
                          actualEnemyHp, actualEnemyEnergy);

        BigFivePersonality resolvedPersonality = BattleSceneData.EnemyPersonality
            ?? (enemyAIProfile != null ? enemyAIProfile.defaultPersonality : null)
            ?? new BigFivePersonality();

        if (enemyAI != null)
        {
            enemyAI.InitializePersonality(ClonePersonality(resolvedPersonality));
            enemyAI.InitializeWeights(enemyAIProfile);
            if (scavengerEnemyScript != null && scavengerEnemyScript.isActiveAndEnabled)
                scavengerEnemyScript.ConfigureEnemyAI(enemyAI);
        }

        // 전투 진입 시점에 Management 씬에서 수집한 일회성 전투 보정 적용
        BuffBuildingApplier.Apply(engine.Player, engine.Enemy, attackerModifiers.legacyBuffEffects);
        ApplyBattleModifiers(engine.Player, attackerModifiers);
        ApplyBattleModifiers(engine.Enemy, defenderModifiers);

        // 훈련 모드 설정
        if (trainingMode)
        {
            engine.IsTrainingMode = true;
            engine.Enemy.baseEnergy = 0;
            engine.Enemy.shield = 999;
            BattleLogger.Log(BattleLogType.Info, "=== 훈련 모드 시작 (허수아비) ===");
        }
        else
        {
            engine.IsTrainingMode = false;
            BattleLogger.Log(BattleLogType.Info, "=== 전투 시작 ===");
        }

        // CoreManager 초기화
        CoreManager.Instance?.Init(engine);

        // 적 덱 세팅 (방어자 인벤토리로 구성, 실패 시 임시 덱 폴백)
        var enemyDeck = BuildEnemyDeck(resolvedPersonality);
        BattleDeckInfectionApplier.InsertRansomwareIntoDeck(enemyDeck, attackerModifiers.supportEffects);
        BattleCardCostInterferenceApplier.ApplyEnemyCardCostUp(enemyDeck, attackerModifiers.supportEffects);
        if (enemyDeck.Count > 0)
            engine.SetEnemyDeck(enemyDeck);

        engine.InitialDraw(5);

        BindBattleStateUI();
    }

    /// <summary>
    /// 적 덱을 구성한다. WarManager가 방어자 인벤토리로 채운 EnemyDeckIds가
    /// 최소 장수(Deck.MinDeckSize) 이상이면 그것으로 덱을 만들고,
    /// 아니면 균형형 임시 덱(TempDeckFactory)으로 폴백한다.
    /// </summary>
    private List<CardData> BuildEnemyDeck(BigFivePersonality enemyPersonality)
    {
        if (scavengerEnemyScript != null && scavengerEnemyScript.isActiveAndEnabled)
        {
            var scavengerDeck = scavengerEnemyScript.BuildDeck();
            if (scavengerDeck.Count > 0)
            {
                Debug.Log($"[BattleInitializer] 스캐빈저 전용 덱 사용: {scavengerDeck.Count}장");
                return scavengerDeck;
            }
        }

        AIStrategy strategy = EnemyAI.ResolveStrategyFromBigFive(enemyPersonality);

        var ids = BattleSceneData.EnemyInventoryCardIds;
        if (ids != null && ids.Count > 0)
        {
            var deck = InventoryDeckBuilder.Build(ids, strategy);
            if (deck.Count > 0)
            {
                Debug.Log($"[BattleInitializer] 방어자 인벤토리 덱 사용: {deck.Count}장");
                return deck;
            }
        }

        // 폴백도 전략 반영 (인벤토리가 없거나 구성 실패 시)
        Debug.Log("[BattleInitializer] 방어자 인벤토리 덱 없음/구성 실패. 임시 덱(전략 반영)으로 폴백합니다.");
        return TempDeckFactory.CreateEnemyDeck(strategy);
    }

    private int ResolveEnemyMaxHp()
    {
        if (scavengerEnemyScript != null && scavengerEnemyScript.isActiveAndEnabled)
            return scavengerEnemyScript.EnemyMaxHp;

        return BattleSceneData.EnemyMaxHp > 0 ? BattleSceneData.EnemyMaxHp : enemyMaxHp;
    }

    private void ApplyEnemyCeoProfile()
    {
        if (enemyHP == null)
            return;

        CEOData ceo = null;
        if (!string.IsNullOrWhiteSpace(BattleSceneData.EnemyCeoId))
        {
            if (ceoDatabase == null)
                ceoDatabase = ScriptableObject.CreateInstance<CEODatabaseSO>();

            ceo = ceoDatabase.GetCEOByID(BattleSceneData.EnemyCeoId);
            if (ceo != null)
                enemyHP.SetDisplayName(ceo.name);
        }

        if (BattleSceneData.EnemyCeoProfileSprite != null)
        {
            if (!enemyHP.SetProfileSprite(BattleSceneData.EnemyCeoProfileSprite))
                Debug.LogWarning($"[BattleInitializer] EnemyCharacter SpriteRenderer not found. CEO ID={BattleSceneData.EnemyCeoId}");

            return;
        }

        if (string.IsNullOrWhiteSpace(BattleSceneData.EnemyCeoId))
            return;

        if (ceo == null || ceo.profileSprite == null)
        {
            Debug.LogWarning($"[BattleInitializer] Enemy CEO profile sprite not found. CEO ID={BattleSceneData.EnemyCeoId}");
            return;
        }

        if (!enemyHP.SetProfileSprite(ceo.profileSprite))
            Debug.LogWarning($"[BattleInitializer] EnemyCharacter SpriteRenderer not found. CEO ID={ceo.id}");
    }

    private AssistStatusBarUI FindAssistStatusBarUI()
    {
        AssistStatusBarUI found = FindFirstObjectByType<AssistStatusBarUI>(FindObjectsInactive.Include);
        if (found != null)
            return found;

        GameObject statusBar = GameObject.Find("AssistStatusBar");
        if (statusBar == null)
            return null;

        return statusBar.AddComponent<AssistStatusBarUI>();
    }

    private void ApplyBattleModifiers(EntityState entity, BattleModifierSnapshot modifiers)
    {
        if (entity == null || modifiers == null)
            return;

        if (modifiers.startShieldBonus > 0)
        {
            entity.shield += modifiers.startShieldBonus;
            Debug.Log($"[BattleModifier] StartShieldBonus applied. Bonus={modifiers.startShieldBonus}");
        }

        if (modifiers.startDrawBonus > 0)
        {
            entity.extraDraw += modifiers.startDrawBonus;
            Debug.Log($"[BattleModifier] StartDrawBonus applied. Bonus={modifiers.startDrawBonus}");
        }
    }

    // ===================================================
    //  튜토리얼 전투
    // ===================================================

    private void StartTutorialBattle()
    {
        var tm = TutorialManager.Instance;
        tm.BeginBattleTutorial();

        var bc = tm.BattleController;
        if (bc == null)
        {
            Debug.LogWarning("[BattleInitializer] 튜토리얼 전투 컨트롤러를 만들 수 없어 일반 전투로 시작합니다.");
            BattleSceneData.IsTutorial = false;
            StartBattle();
            return;
        }

        var deckCards = CardDatabase.Instance.GetByIds(bc.GetPlayerDeckIds());
        if (deckCards.Count == 0)
        {
            Debug.LogError("[BattleInitializer] 튜토리얼 덱에 카드가 없습니다.");
            return;
        }

        ClearBattleLog();
        assistStatusBarUI?.Bind(BattleModifierSnapshot.Empty());
        if (string.IsNullOrWhiteSpace(BattleSceneData.EnemyCeoId))
        {
            BattleSceneData.EnemyCeoId = bc.GetEnemyCeoId();
            BattleSceneData.EnemyCeoProfileSprite = ResolveEnemyCeoProfileSprite(BattleSceneData.EnemyCeoId);
        }
        else if (BattleSceneData.EnemyCeoProfileSprite == null)
        {
            BattleSceneData.EnemyCeoProfileSprite = ResolveEnemyCeoProfileSprite(BattleSceneData.EnemyCeoId);
        }

        ApplyEnemyCeoProfile();

        engine.InitBattle(deckCards, playerMaxHp, playerBaseEnergy,
                          bc.GetEnemyHp(), bc.GetEnemyEnergy());
        engine.IsTrainingMode = false;

        BattleLogger.Log(BattleLogType.Info, "=== 튜토리얼 전투 시작 ===");

        CoreManager.Instance?.Init(engine);

        var enemyDeck = CardDatabase.Instance.GetByIds(bc.GetEnemyDeckIds());
        if (enemyDeck.Count > 0)
            engine.SetEnemyDeck(enemyDeck);

        engine.InitialDraw(5);

        BindBattleStateUI();
    }

    private Sprite ResolveEnemyCeoProfileSprite(string ceoId)
    {
        if (string.IsNullOrWhiteSpace(ceoId))
            return null;

        if (ceoDatabase == null)
            ceoDatabase = ScriptableObject.CreateInstance<CEODatabaseSO>();

        CEOData ceo = ceoDatabase.GetCEOByID(ceoId);
        return ceo != null ? ceo.profileSprite : null;
    }

    private void ClearBattleLog()
    {
        BattleLogger.Enabled = true;
        if (battleLogUI != null)
            battleLogUI.ClearLog();
        else
            BattleLogger.Clear();
    }

    private void BindBattleStateUI()
    {
        engine.OnStateChanged -= SyncHpUI;
        engine.OnStateChanged += SyncHpUI;
        engine.OnGambleResult -= OnGambleResult;
        engine.OnGambleResult += OnGambleResult;
        turnManager.OnTurnStart -= OnTurnStarted;
        turnManager.OnTurnStart += OnTurnStarted;

        if (enemyHP  != null) enemyHP.BindState(engine.Enemy);
        if (playerHP != null) playerHP.BindState(engine.Player);
        if (playerStatusUI    != null) playerStatusUI.BindState(engine.Player);
        if (enemyStatusUI     != null) enemyStatusUI.BindState(engine.Enemy);
        if (playerEnergyUI    != null) playerEnergyUI.BindState(engine.Player);
        if (enemyEnergyUI     != null) enemyEnergyUI.BindState(engine.Enemy);
        if (playerCoreListUI != null) playerCoreListUI.BindState(engine.Player);
        if (enemyCoreListUI  != null) enemyCoreListUI.BindState(engine.Enemy);

        SyncDeckUI();
        RefreshHandUI();
    }

    // ===================================================
    //  페이즈 전환
    // ===================================================

    private void RefreshHandUI()
    {
        if (engine == null || engine.Player == null) return;

        handUIManager.RefreshHandUI(
            engine.Player.hand,
            isMulliganPhase,
            OnCardUsed,
            engine.Player);
    }

    public void OnEndTurnClicked()
    {
        if (isMulliganPhase) return;
        if (engine == null || engine.IsBattleEnded) return;
        if (isInitialHandAnimating) return;
        if (isTurnStartDrawAnimating) return;
        if (turnManager != null && turnManager.IsTurnTransitionInProgress) return;

        Debug.Log("[BattleInitializer] 턴 종료 버튼 클릭!");

        if (BattleSceneData.IsTutorial && TutorialManager.Instance != null)
            TutorialManager.Instance.NotifyCondition(TutorialCondition.EndTurn);

        if (endTurnButton != null)
            endTurnButton.interactable = false;

        turnManager.EndPlayerTurn();
    }

    public void OnSurrenderClicked()
    {
        if (engine == null || engine.IsBattleEnded) return;
        if (isInitialHandAnimating) return;
        if (isResolvingCardUse) return;
        if (isTurnStartDrawAnimating) return;
        if (turnManager == null) return;
        if (turnManager.IsTurnTransitionInProgress) return;

        ClosePauseMenu();
        CardGameSFXManager.PlayBasicClick();
        engine.Surrender();
        UpdateTurnInputState();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
            return;

        TogglePauseMenuByEscape();
    }

    public void OnResumeClicked()
    {
        CardGameSFXManager.PlayBasicClick();
        ClosePauseMenu();
    }

    private void TogglePauseMenuByEscape()
    {
        if (engine == null || engine.IsBattleEnded)
            return;

        if (pauseMenuPanel == null)
            EnsurePauseMenuPanel();
        if (pauseMenuPanel == null)
            return;

        if (pauseMenuPanel.activeSelf)
            ClosePauseMenu();
        else
            OpenPauseMenu();
    }

    private void OpenPauseMenu()
    {
        if (pauseMenuPanel == null)
            return;

        pauseMenuPanel.SetActive(true);
        pauseMenuPanel.transform.SetAsLastSibling();
        UpdateTurnInputState();
    }

    private void ClosePauseMenu()
    {
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);

        UpdateTurnInputState();
    }

    private bool IsPauseMenuOpen()
    {
        return pauseMenuPanel != null && pauseMenuPanel.activeSelf;
    }

    private void EnsurePauseMenuPanel()
    {
        if (pauseMenuPanel == null && rootCanvas != null)
        {
            Transform existing = rootCanvas.transform.Find("BattlePausePanel");
            if (existing != null)
                pauseMenuPanel = existing.gameObject;
        }

        if (pauseMenuPanel == null)
        {
            Debug.LogWarning("[BattleInitializer] BattlePausePanel is not assigned.");
            return;
        }

        Transform panel = pauseMenuPanel.transform;

        if (pauseMenuDismissButton == null)
        {
            Transform dismiss = panel.Find("DismissArea");
            if (dismiss != null)
                dismiss.TryGetComponent(out pauseMenuDismissButton);
        }

        if (resumeButton == null)
        {
            Transform resume = panel.Find("Dialog/ResumeButton");
            if (resume != null)
                resume.TryGetComponent(out resumeButton);
        }

        if (surrenderButton == null)
        {
            Transform surrender = panel.Find("Dialog/SurrenderButton");
            if (surrender != null)
                surrender.TryGetComponent(out surrenderButton);
        }

        pauseMenuPanel.SetActive(false);
    }

    private void OnMulliganConfirm()
    {
        if (!isMulliganPhase) return;
        if (isInitialHandAnimating) return;
        if (engine == null) return;

        var replaceIndices = mulliganUI.CollectSelectedIndices(
            new List<HandCardUI>(handUIManager.Cards));

        if (replaceIndices.Count > 0)
        {
            engine.TryMulligan(replaceIndices);
            Debug.Log($"[BattleInitializer] 멀리건: {replaceIndices.Count}장 교체 완료.");
        }
        else
        {
            Debug.Log("[BattleInitializer] 교체할 카드 없이 멀리건 완료.");
        }

        isMulliganPhase = false;
        isInitialHandAnimating = true;
        playPostMulliganHandVfxOnNextTurnStart = true;

        if (BattleSceneData.IsTutorial && TutorialManager.Instance != null)
            TutorialManager.Instance.NotifyCondition(TutorialCondition.MulliganConfirm);

        turnManager.BeginFirstTurn();
        PlayBGM();
    }

    // ===================================================
    //  턴 배너
    // ===================================================

    /// <summary>도박 판정 결과를 화면 중앙 글리치 패널 연출로 표시한다.</summary>
    private void OnGambleResult(GambleResultInfo info)
    {
        if (gambleResultVfx == null || rootCanvas == null) return;
        gambleResultVfx.Play(info, rootCanvas);
    }

    private void OnTurnStarted(EntityState entity)
    {
        if (engine == null) return;
        int drawnThisStart = turnManager != null ? turnManager.LastStartTurnDrawCount : 0;

        if (turnBannerVfx != null && rootCanvas != null && entity == engine.Player)
        {
            turnBannerVfx.QueuePlay(rootCanvas, true);
        }
        else if (turnBannerVfx != null && rootCanvas != null && entity == engine.Enemy)
        {
            turnBannerVfx.QueuePlay(rootCanvas, false);
        }

        SyncDeckUI();
        SyncHpUI();
        RefreshHandUI();

        if (entity == engine.Player && playPostMulliganHandVfxOnNextTurnStart)
        {
            playPostMulliganHandVfxOnNextTurnStart = false;
            UpdateTurnInputState();
            StartCoroutine(PlayPostMulliganHandVfx());
            return;
        }

        if (entity == engine.Player && drawnThisStart > 0)
        {
            isTurnStartDrawAnimating = true;
            UpdateTurnInputState();
            StartCoroutine(PlayTurnStartDrawVfx(drawnThisStart));
            return;
        }

        isTurnStartDrawAnimating = false;
        UpdateTurnInputState();
    }

    // ===================================================
    //  카드 사용 흐름
    // ===================================================

    private void OnCardUsed(HandCardUI cardUI)
    {
        if (engine == null || engine.IsBattleEnded || isResolvingCardUse) return;
        if (isInitialHandAnimating) return;
        if (isTurnStartDrawAnimating) return;
        if (turnManager != null && (!turnManager.IsPlayerTurn || turnManager.IsTurnTransitionInProgress)) return;

        int handIndex = cardUI.HandIndex;

        if (!engine.CanPlayCard(handIndex))
        {
            Debug.Log("[BattleInitializer] 카드 사용 불가 (에너지 부족 또는 턴 강제 종료)");
            RefreshHandUI();
            return;
        }

        var card = engine.Player.hand[handIndex];

        if (BattleSceneData.IsTutorial && TutorialManager.Instance != null
            && TutorialManager.Instance.BattleController != null
            && !TutorialManager.Instance.BattleController.IsCardAllowedForCurrentStep(card))
        {
            Debug.Log("[BattleInitializer] 튜토리얼: 현재 단계에서 허용되지 않는 카드입니다.");
            RefreshHandUI();
            return;
        }

        int chosenCount = CountChosenDismantles(card);
        int cloneCount = CountChosenCardCopies(card);
        bool needsOpenAccessType = NeedsOpenAccessTypeSelect(card);
        isResolvingCardUse = true;

        if (needsOpenAccessType)
            StartCoroutine(PlayCardWithOpenAccessTypeSelect(cardUI, card, handIndex));
        else if (chosenCount > 0)
            StartCoroutine(PlayCardWithDismantleSelect(handIndex, chosenCount));
        else if (cloneCount > 0)
            StartCoroutine(PlayCardWithCardCopySelect(handIndex, cloneCount));
        else
            StartCoroutine(PlayCardUseVfxAndExecute(cardUI, card, handIndex));
    }

    private int CountChosenDismantles(CardData card)
    {
        if (card.effects == null) return 0;
        int total = 0;
        foreach (var eff in card.effects)
            if (eff.mode == "chosen" && IsdismantleType(eff.type))
                total += Mathf.Max(1, Mathf.RoundToInt(eff.value > 0 ? eff.value : 1));
        return total;
    }

    private static bool IsdismantleType(string type) =>
        type != null && (type == "dismantle" || type.StartsWith("dismantle"));

    private static bool NeedsOpenAccessTypeSelect(CardData card)
    {
        if (card?.effects == null) return false;
        foreach (var eff in card.effects)
            if (eff != null && eff.type == "openAccessDraw" && string.IsNullOrEmpty(eff.filter))
                return true;
        return false;
    }

    private IEnumerator PlayCardWithOpenAccessTypeSelect(HandCardUI cardUI, CardData card, int handIndex)
    {
        if (openAccessTypeSelectUI == null)
            openAccessTypeSelectUI = gameObject.AddComponent<OpenAccessTypeSelectUI>();

        yield return StartCoroutine(openAccessTypeSelectUI.ShowAndWait(rootCanvas));
        engine.SetPendingOpenAccessFilter(openAccessTypeSelectUI.GetResult());

        int updatedHandIndex = engine.Player.hand.IndexOf(card);
        if (updatedHandIndex < 0) updatedHandIndex = handIndex;
        yield return StartCoroutine(PlayCardUseVfxAndExecute(
            cardUI != null ? cardUI : FindHandCardUIForCard(card),
            card,
            updatedHandIndex));
    }

    private int CountChosenCardCopies(CardData card)
    {
        if (card.effects == null) return 0;
        int total = 0;
        foreach (var eff in card.effects)
            if (eff.mode == "chosen" && eff.type == "cloneHandCard")
                total += Mathf.Max(1, Mathf.RoundToInt(eff.value > 0 ? eff.value : 1));
        return total;
    }

    private IEnumerator PlayCardWithDismantleSelect(int handIndex, int chosenCount)
    {
        var card = engine.Player.hand[handIndex];

        int drawFirst = 0;
        int minSelect = 0;
        if (card.effects != null)
        {
            foreach (var eff in card.effects)
            {
                if (eff.mode == "chosen" && IsdismantleType(eff.type))
                {
                    if (eff.drawBeforeSelect > 0)
                        drawFirst = Mathf.Max(drawFirst, eff.drawBeforeSelect);
                    if (eff.minValue > 0)
                        minSelect = Mathf.Max(minSelect, eff.minValue);
                }
            }
        }

        if (drawFirst > 0)
        {
            engine.DrawCards(engine.Player, drawFirst);
            RefreshHandUI();
            yield return null;
        }

        var allCardUIs = new System.Collections.Generic.List<HandCardUI>(handUIManager.Cards);
        var availableCardUIs = new System.Collections.Generic.List<HandCardUI>();
        for (int i = 0; i < allCardUIs.Count; i++)
        {
            if (handUIManager.Cards[i].CardData != card)
                availableCardUIs.Add(allCardUIs[i]);
        }

        if (availableCardUIs.Count == 0)
        {
            yield return StartCoroutine(PlayCardUseVfxAndExecute(FindHandCardUIForCard(card), card, handIndex));
            yield break;
        }

        int actualCount = Mathf.Min(chosenCount, availableCardUIs.Count);
        int actualMin   = Mathf.Min(minSelect > 0 ? minSelect : actualCount, actualCount);

        yield return StartCoroutine(dismantleSelectUI.ShowAndWait(
            availableCardUIs, actualCount, rootCanvas, actualMin));

        var selected = dismantleSelectUI.GetResult();
        engine.PendingDismantleTargets.Clear();
        engine.PendingDismantleTargets.AddRange(selected);

        engine.Player.skipNextDrawCount += drawFirst;

        int updatedHandIndex = engine.Player.hand.IndexOf(card);
        if (updatedHandIndex < 0) updatedHandIndex = handIndex;
        yield return StartCoroutine(PlayCardUseVfxAndExecute(FindHandCardUIForCard(card), card, updatedHandIndex));
    }

    private IEnumerator PlayCardWithCardCopySelect(int handIndex, int chosenCount)
    {
        var card = engine.Player.hand[handIndex];
        string filter = GetChosenCardCopyFilter(card);

        var allCardUIs = new System.Collections.Generic.List<HandCardUI>(handUIManager.Cards);
        var availableCardUIs = new System.Collections.Generic.List<HandCardUI>();
        for (int i = 0; i < allCardUIs.Count; i++)
        {
            var candidate = handUIManager.Cards[i].CardData;
            if (candidate == card) continue;
            if (!MatchesCardCopyFilter(candidate, filter)) continue;
            availableCardUIs.Add(allCardUIs[i]);
        }

        if (availableCardUIs.Count == 0)
        {
            yield return StartCoroutine(PlayCardUseVfxAndExecute(FindHandCardUIForCard(card), card, handIndex));
            yield break;
        }

        int actualCount = Mathf.Min(chosenCount, availableCardUIs.Count);
        yield return StartCoroutine(dismantleSelectUI.ShowAndWait(
            availableCardUIs, actualCount, rootCanvas, actualCount, "복사할"));

        var selected = dismantleSelectUI.GetResult();
        engine.PendingDismantleTargets.Clear();
        engine.PendingDismantleTargets.AddRange(selected);

        int updatedHandIndex = engine.Player.hand.IndexOf(card);
        if (updatedHandIndex < 0) updatedHandIndex = handIndex;
        yield return StartCoroutine(PlayCardUseVfxAndExecute(FindHandCardUIForCard(card), card, updatedHandIndex));
    }

    private static string GetChosenCardCopyFilter(CardData card)
    {
        if (card.effects == null) return null;
        foreach (var eff in card.effects)
            if (eff.mode == "chosen" && eff.type == "cloneHandCard")
                return eff.filter;
        return null;
    }

    private static bool MatchesCardCopyFilter(CardData card, string filter)
    {
        if (card == null) return false;
        if (string.IsNullOrEmpty(filter)) return true;
        if (filter == "network") return card.HasKeyword("network");
        if (filter == "extract") return card.HasKeyword("extract");
        if (filter == "attack") return card.type == CardType.Attack;
        if (filter == "skill") return card.type == CardType.Skill;
        if (filter == "core") return card.type == CardType.Core;
        if (filter == "coreOrSkill") return card.type == CardType.Core || card.type == CardType.Skill;
        return false;
    }

    private IEnumerator PlayCardUseVfxAndExecute(HandCardUI cardUI, CardData card, int fallbackHandIndex)
    {
        bool committed = false;
        CardPlayResolution resolution = CardPlayResolution.Empty;

        void Commit()
        {
            if (committed || engine == null || engine.IsBattleEnded)
                return;

            committed = true;
            int updatedHandIndex = engine.Player != null ? engine.Player.hand.IndexOf(card) : -1;
            PlayEstimatedDamageHit(card, engine.Player, engine.Enemy);
            resolution = ExecutePlayCard(updatedHandIndex >= 0 ? updatedHandIndex : fallbackHandIndex);
        }

        HandCardUI sourceCardUI = cardUI != null ? cardUI : FindHandCardUIForCard(card);

        if (cardUseVfx != null && sourceCardUI != null)
        {
            yield return StartCoroutine(cardUseVfx.Play(sourceCardUI, card, rootCanvas, Commit));
        }
        else
        {
            Commit();
        }

        if (!committed)
            Commit();

        HidePlayedCardIfRemoved(sourceCardUI, card);

        if (resolution.DismantleEvents.Count > 0 && cardDismantleVfx != null)
        {
            yield return StartCoroutine(cardDismantleVfx.PlayExisting(
                resolution.DismantleEvents,
                handUIManager,
                rootCanvas,
                engine.Player));
        }

        RefreshHandUIAfterCardResolution(resolution.CardsAddedByCardEffect);

        if (resolution.CardsAddedByCardEffect.Count > 0)
            yield return StartCoroutine(PlayCardEffectDrawVfx(resolution.CardsAddedByCardEffect));

        isResolvingCardUse = false;

        if (engine != null && engine.Player != null && engine.Player.forceEndTurn)
        {
            engine.Player.forceEndTurn = false;
            OnEndTurnClicked();
        }
        else
        {
            UpdateTurnInputState();
        }
    }

    public IEnumerator PlayEnemyCardUseVfxAndExecute(int handIndex, CardData card, EntityState enemy, EntityState player)
    {
        if (engine == null || engine.IsBattleEnded || card == null)
            yield break;

        bool committed = false;
        List<DismantleVfxEvent> dismantleEvents = null;

        void Commit()
        {
            if (committed || engine == null || engine.IsBattleEnded)
                return;

            committed = true;
            int updatedHandIndex = enemy != null ? enemy.hand.IndexOf(card) : -1;
            PlayEstimatedDamageHit(card, enemy, player);
            engine.PlayCard(updatedHandIndex >= 0 ? updatedHandIndex : handIndex, enemy, player);
            dismantleEvents = engine.DismantleVfxQueue.ConsumePending();
            SyncDeckUI();
            SyncHpUI();
            UpdateTurnInputState();
        }

        HandCardUI sourceCardUI = CreateEnemyCardVfxSource(card, handIndex, enemy);

        if (cardUseVfx != null && sourceCardUI != null)
        {
            CardGameSFXManager.PlayCardUse();
            yield return StartCoroutine(cardUseVfx.Play(
                sourceCardUI,
                card,
                rootCanvas,
                Commit,
                CardUseDataScatterVfx.CloneVisibility.ArtworkOnly));
        }
        else
        {
            Commit();
        }

        if (!committed)
            Commit();

        if (sourceCardUI != null)
            Destroy(sourceCardUI.gameObject);

        if (dismantleEvents != null && dismantleEvents.Count > 0 && cardDismantleVfx != null)
        {
            yield return StartCoroutine(cardDismantleVfx.PlayExisting(
                dismantleEvents,
                handUIManager,
                rootCanvas,
                engine.Player));
        }

        RefreshHandUI();
    }

    private HandCardUI CreateEnemyCardVfxSource(CardData card, int handIndex, EntityState enemy)
    {
        if (cardPrefab == null || rootCanvas == null)
            return null;

        GameObject cardGO = Instantiate(cardPrefab, rootCanvas.transform);
        var cardUI = cardGO.GetComponent<HandCardUI>();
        if (cardUI == null)
            cardUI = cardGO.AddComponent<HandCardUI>();

        cardUI.Setup(card, handIndex, enemy);
        cardUI.SetupMulligan(false);
        cardUI.SetClickCallback(null);
        cardUI.enabled = false;

        var group = cardGO.GetComponent<CanvasGroup>();
        if (group == null)
            group = cardGO.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        var rect = cardGO.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = GetEnemyCardVfxAnchoredPosition();
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one * Mathf.Max(0.1f, cardScale * 0.9f);
        }

        cardGO.transform.SetAsLastSibling();
        cardGO.SetActive(true);
        return cardUI;
    }

    private Vector2 GetEnemyCardVfxAnchoredPosition()
    {
        RectTransform canvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;
        if (canvasRect == null)
            return Vector2.zero;

        return new Vector2(0f, canvasRect.rect.height * 0.32f);
    }

    private HandCardUI FindHandCardUIForCard(CardData card)
    {
        if (handUIManager == null || card == null)
            return null;

        foreach (var cardUI in handUIManager.Cards)
        {
            if (cardUI == null)
                continue;

            if (object.ReferenceEquals(cardUI.CardData, card) || cardUI.CardData?.id == card.id)
                return cardUI;
        }

        return null;
    }

    private static int EstimateCardDamage(CardData card, EntityState caster, EntityState target)
    {
        return SumEstimatedDamage(card?.effects, caster, target);
    }

    private static int SumEstimatedDamage(IEnumerable<CardEffect> effects, EntityState caster, EntityState target)
    {
        if (effects == null) return 0;

        int total = 0;
        foreach (var eff in effects)
        {
            if (eff == null)
                continue;

            if (eff.type == "damage")
                total += Mathf.RoundToInt(eff.value * eff.EffectiveHits);
            else if (IsScaledDamageEffect(eff.type))
                total += Mathf.RoundToInt(ResolveEstimatedScaledDamage(eff, caster, target) * eff.EffectiveHits);
            else if (eff.type == "damageByDebuff")
                total += EstimateDamageByDebuff(eff, target);
            else if (eff.type == "damageMaxPercent")
                total += EstimateDamageMaxPercent(eff, target);

            total += SumEstimatedDamage(eff.thenEffects, caster, target);
            total += SumEstimatedDamage(eff.elseEffects, caster, target);
            total += SumEstimatedDamage(eff.effects, caster, target);
        }

        return total;
    }

    private static bool IsScaledDamageEffect(string effectType)
    {
        return effectType == "scaledDamage"
            || effectType == "damageByLuckStack"
            || effectType == "damagePlusByLuckyThisBattle"
            || effectType == "damagePlusByUnluckyThisBattle";
    }

    private static float ResolveEstimatedScaledDamage(CardEffect effect, EntityState caster, EntityState target)
    {
        if (effect == null)
            return 0f;

        string source = effect.scaling != null && !string.IsNullOrEmpty(effect.scaling.source)
            ? effect.scaling.source
            : GetScaledDamageFallbackSource(effect.type);
        float multiplier = effect.scaling != null ? effect.scaling.multiplier : 1f;
        float sourceValue = ResolveEstimatedScalingSource(source, caster, target);
        float value = effect.value + sourceValue * multiplier;

        if (effect.scaling != null && effect.scaling.max > 0)
            value = Mathf.Min(value, effect.scaling.max);
        if (effect.scaling != null && effect.scaling.min > 0)
            value = Mathf.Max(value, effect.scaling.min);

        return Mathf.Max(0f, value);
    }

    private static string GetScaledDamageFallbackSource(string effectType)
    {
        return effectType switch
        {
            "damageByLuckStack" => "luck",
            "damagePlusByLuckyThisBattle" => "luckyThisBattle",
            "damagePlusByUnluckyThisBattle" => "unluckyThisBattle",
            _ => null,
        };
    }

    private static float ResolveEstimatedScalingSource(string source, EntityState caster, EntityState target)
    {
        if (string.IsNullOrEmpty(source) || caster == null)
            return 0f;

        target ??= caster;

        return source switch
        {
            "networkStacks" => caster.networkStacks,
            "networkStacksWithSelf" => caster.networkStacks + (caster.doubleNetworkStacks ? 2 : 1),
            "overclockStacks" => caster.overclockStacks,
            "selfDamageThisTurn" => caster.Turn.selfDamageThisTurn,
            "totalDamageThisTurn" => caster.Turn.totalDamageThisTurn,
            "dismantledThisTurn" => caster.Turn.dismantledThisTurn,
            "dismantledThisBattle" => caster.dismantledThisBattle,
            "targetVirus" => target.virus,
            "targetHandCount" => target.hand != null ? target.hand.Count : 0,
            "corrosion" => target.corrosion,
            "luck" => caster.luck,
            "luckyThisTurn" => caster.Turn.luckyThisTurn,
            "unluckyThisTurn" => caster.Turn.unluckyThisTurn,
            "hp" => caster.hp,
            "maxHp" => caster.maxHp,
            "enemyMaxHp" => target.maxHp,
            "overflowStacks" => caster.overflowNext,
            "rebuildCount" => caster.totalRebuildsThisBattle,
            "hpDifference" => Mathf.Abs(caster.hp - target.hp),
            "baseEnergy" => caster.baseEnergy,
            "luckyThisBattle" => caster.luckyThisBattle,
            "unluckyThisBattle" => caster.unluckyThisBattle,
            "gambleSuccessThisTurn" => caster.Turn.gambleSuccessThisTurn,
            "targetLostHp" => target.maxHp - target.hp,
            "cardAccumCount" => 0,
            _ => 0f,
        };
    }

    private static int EstimateDamageByDebuff(CardEffect effect, EntityState target)
    {
        if (effect == null || target == null)
            return 0;

        string stat = effect.stat ?? "virus";
        float multiplier = effect.value > 0 ? effect.value : 1f;
        int stacks = stat == "corrosion" ? target.corrosion : target.virus;
        return Mathf.Max(0, Mathf.RoundToInt(stacks * multiplier));
    }

    private static int EstimateDamageMaxPercent(CardEffect effect, EntityState target)
    {
        if (effect == null || target == null)
            return 0;

        int damage = Mathf.RoundToInt(target.maxHp * (effect.value / 100f));
        if (effect.scaling != null && effect.scaling.max > 0)
            damage = Mathf.Min(damage, effect.scaling.max);
        if (effect.scaling != null && effect.scaling.min > 0)
            damage = Mathf.Max(damage, effect.scaling.min);

        return Mathf.Max(0, damage);
    }

    private static void PlayEstimatedDamageHit(CardData card, EntityState caster, EntityState target)
    {
        int estimatedDamage = EstimateCardDamage(card, caster, target);
        if (estimatedDamage > 0)
            CardGameSFXManager.PlayDamageHit(estimatedDamage);
    }

    private CardPlayResolution ExecutePlayCard(int handIndex)
    {
        CardData playedCard = (handIndex >= 0 && handIndex < engine.Player.hand.Count)
            ? engine.Player.hand[handIndex] : null;
        var handBefore = new List<CardData>(engine.Player.hand);

        engine.PlayCard(handIndex);
        var cardsAddedByCardEffect = CollectNewHandCards(handBefore, engine.Player.hand);
        var rebuiltCardsReturnedToHand = engine.ConsumeRebuiltCardsReturnedToHand();
        AddMissingCardReferences(cardsAddedByCardEffect, rebuiltCardsReturnedToHand);
        var dismantleEvents = engine.DismantleVfxQueue.ConsumePending();

        if (BattleSceneData.IsTutorial && TutorialManager.Instance != null && playedCard != null)
        {
            switch (playedCard.type)
            {
                case CardType.Attack:
                    TutorialManager.Instance.NotifyCondition(TutorialCondition.PlayAttackCard);
                    break;
                case CardType.Skill:
                    TutorialManager.Instance.NotifyCondition(TutorialCondition.PlaySkillCard);
                    break;
                case CardType.Core:
                    TutorialManager.Instance.NotifyCondition(TutorialCondition.PlayCoreCard);
                    break;
            }
        }

        SyncDeckUI();
        SyncHpUI();
        UpdateTurnInputState();

        return new CardPlayResolution(cardsAddedByCardEffect, dismantleEvents);
    }

    private void RefreshHandUIAfterCardResolution(IReadOnlyList<CardData> cardsAddedByCardEffect)
    {
        if (cardsAddedByCardEffect != null && cardsAddedByCardEffect.Count > 0 && handUIManager != null)
        {
            handUIManager.RefreshHandUIKeepingExisting(
                engine.Player.hand,
                cardsAddedByCardEffect,
                OnCardUsed,
                engine.Player);
        }
        else
        {
            RefreshHandUI();
        }
    }

    private void HidePlayedCardIfRemoved(HandCardUI sourceCardUI, CardData playedCard)
    {
        if (sourceCardUI == null || playedCard == null || engine?.Player == null)
            return;

        if (engine.Player.hand.Contains(playedCard))
            return;

        sourceCardUI.gameObject.SetActive(false);
    }

    private sealed class CardPlayResolution
    {
        public static readonly CardPlayResolution Empty = new CardPlayResolution(
            new List<CardData>(),
            new List<DismantleVfxEvent>());

        public CardPlayResolution(
            List<CardData> cardsAddedByCardEffect,
            List<DismantleVfxEvent> dismantleEvents)
        {
            CardsAddedByCardEffect = cardsAddedByCardEffect ?? new List<CardData>();
            DismantleEvents = dismantleEvents ?? new List<DismantleVfxEvent>();
        }

        public List<CardData> CardsAddedByCardEffect { get; }
        public List<DismantleVfxEvent> DismantleEvents { get; }
    }

    private IEnumerator PlayCardEffectDrawVfx(IReadOnlyList<CardData> drawnCards)
    {
        if (cardDrawVfx == null || handUIManager == null || drawnCards == null || drawnCards.Count <= 0)
            yield break;

        const float stagger = 0.05f;
        int animatedCount = 0;
        for (int i = 0; i < handUIManager.Cards.Count; i++)
        {
            var cardUI = handUIManager.Cards[i];
            if (cardUI == null || !ContainsCardReference(drawnCards, cardUI.CardData))
                continue;

            StartCoroutine(cardDrawVfx.Play(cardUI, animatedCount * stagger));
            animatedCount++;
        }

        if (animatedCount > 0)
            yield return new WaitForSeconds(cardDrawVfx.EstimatedDuration + (animatedCount - 1) * stagger);
    }

    private static List<CardData> CollectNewHandCards(IReadOnlyList<CardData> handBefore, IReadOnlyList<CardData> handAfter)
    {
        var added = new List<CardData>();
        if (handAfter == null)
            return added;

        foreach (var card in handAfter)
        {
            if (card != null && !ContainsCardReference(handBefore, card))
                added.Add(card);
        }

        return added;
    }

    private static void AddMissingCardReferences(List<CardData> target, IReadOnlyList<CardData> source)
    {
        if (target == null || source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            CardData card = source[i];
            if (card != null && !ContainsCardReference(target, card))
                target.Add(card);
        }
    }

    private static bool ContainsCardReference(IReadOnlyList<CardData> cards, CardData target)
    {
        if (cards == null || target == null)
            return false;

        for (int i = 0; i < cards.Count; i++)
        {
            if (ReferenceEquals(cards[i], target))
                return true;
        }

        return false;
    }

    private IEnumerator PlayTurnStartDrawVfx(int drawCount)
    {
        if (cardDrawVfx == null || handUIManager == null || drawCount <= 0)
        {
            isTurnStartDrawAnimating = false;
            UpdateTurnInputState();
            yield break;
        }

        int totalCards = handUIManager.Cards.Count;
        int animatedCount = Mathf.Min(drawCount, totalCards);
        if (animatedCount <= 0)
        {
            isTurnStartDrawAnimating = false;
            UpdateTurnInputState();
            yield break;
        }

        int startIndex = totalCards - animatedCount;
        yield return StartCoroutine(PlayHandEntryVfx(startIndex, animatedCount));

        isTurnStartDrawAnimating = false;
        UpdateTurnInputState();
    }

    private IEnumerator PlayPostMulliganHandVfx()
    {
        if (cardDrawVfx == null || handUIManager == null || handUIManager.Cards.Count <= 0)
        {
            isInitialHandAnimating = false;
            UpdateTurnInputState();
            yield break;
        }

        yield return StartCoroutine(PlayHandEntryVfx(0, handUIManager.Cards.Count));
        isInitialHandAnimating = false;
        UpdateTurnInputState();
    }

    private IEnumerator PlayHandEntryVfx(int startIndex, int count)
    {
        if (cardDrawVfx == null || handUIManager == null || count <= 0)
            yield break;

        int totalCards = handUIManager.Cards.Count;
        if (totalCards <= 0)
            yield break;

        int clampedStartIndex = Mathf.Clamp(startIndex, 0, totalCards - 1);
        int endIndexExclusive = Mathf.Min(totalCards, clampedStartIndex + count);
        int animatedCount = endIndexExclusive - clampedStartIndex;
        if (animatedCount <= 0)
            yield break;

        const float stagger = 0.05f;

        for (int i = clampedStartIndex; i < endIndexExclusive; i++)
        {
            var cardUI = handUIManager.Cards[i];
            if (cardUI != null)
                StartCoroutine(cardDrawVfx.Play(cardUI, (i - clampedStartIndex) * stagger));
        }

        yield return new WaitForSeconds(cardDrawVfx.EstimatedDuration + (animatedCount - 1) * stagger);
    }

    private void UpdateTurnInputState()
    {
        bool canInteract = !isMulliganPhase
            && engine != null
            && !engine.IsBattleEnded
            && !isInitialHandAnimating
            && !isResolvingCardUse
            && !isTurnStartDrawAnimating
            && turnManager != null
            && turnManager.IsPlayerTurn
            && !turnManager.IsTurnTransitionInProgress
            && !IsPauseMenuOpen();

        bool canSurrender = engine != null
            && !engine.IsBattleEnded
            && !isInitialHandAnimating
            && !isResolvingCardUse
            && !isTurnStartDrawAnimating
            && turnManager != null
            && !turnManager.IsTurnTransitionInProgress
            && IsPauseMenuOpen();

        if (endTurnButton != null)
            endTurnButton.interactable = canInteract;

        if (surrenderButton != null)
            surrenderButton.interactable = canSurrender;
    }

    // ===================================================
    //  BGM
    // ===================================================

    private void PlayBGM()
    {
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.clip = Resources.Load<AudioClip>("Audio/BGM/Battle/사이버 저항 벡터");
            bgmSource.loop = true;
        }

        SoundManager.GetOrCreate().ApplyBgmSource(bgmSource);
        ApplyBgmVolume();

        if (bgmSource.clip != null && !bgmSource.isPlaying)
            bgmSource.Play();
    }

    private void ApplyBgmVolume()
    {
        if (bgmSource == null)
            return;

        SoundManager manager = SoundManager.GetOrCreate();
        bgmSource.volume = manager != null
            ? manager.GetBgmSourceVolume(bgmVolume)
            : bgmVolume;
    }

    private static BigFivePersonality ClonePersonality(BigFivePersonality source)
    {
        if (source == null)
            return new BigFivePersonality();

        return new BigFivePersonality
        {
            openness = source.openness,
            conscientiousness = source.conscientiousness,
            extraversion = source.extraversion,
            agreeableness = source.agreeableness,
            neuroticism = source.neuroticism,
        };
    }
}
