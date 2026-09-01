using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 코어 카드 시스템. 28종 코어 효과의 즉시 효과 및 트리거 효과를 관리한다.
/// BattleEngine.CheckCoreTriggers에서 위임받아 실행한다.
/// </summary>
public partial class CoreManager : MonoBehaviour
{
    public static CoreManager Instance { get; private set; }

    private BattleEngine engine;
    private EffectInterpreter interpreter;

    private Dictionary<string, System.Action<CoreContext>> customCoreHandlers;
    private Dictionary<string, System.Action<CoreContext>> immediateCoreHandlers;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// BattleEngine 초기화 후 호출. 참조를 연결하고 핸들러를 등록한다.
    /// </summary>
    public void Init(BattleEngine battleEngine)
    {
        if (Instance == null)
            Instance = this;

        engine      = battleEngine;
        interpreter = engine.Interpreter;

        customCoreHandlers    = new Dictionary<string, System.Action<CoreContext>>();
        immediateCoreHandlers = new Dictionary<string, System.Action<CoreContext>>();

        RegisterAllCoreHandlers();
    }

    private void RegisterAllCoreHandlers()
    {
        RegisterImmediateHandlers();
        RegisterTurnHandlers();
        RegisterCardPlayedHandlers();
    }

    // ===================================================
    //  즉시 효과 (코어 카드 사용 직후 1회)
    // ===================================================

    /// <summary>
    /// 코어 카드가 activeCores에 추가될 때 즉시 효과를 적용한다.
    /// </summary>
    public void ApplyImmediateCore(CardData coreCard, EntityState entity)
    {
        if (coreCard.coreEffect == null) return;
        if (coreCard.coreEffect.trigger != "immediate" &&
            !string.IsNullOrEmpty(coreCard.coreEffect.trigger)) return;

        string pt = coreCard.coreEffect.coreType;
        Debug.Log($"[Core] '{coreCard.cardName}' 즉시 효과 발동");

        if (!string.IsNullOrEmpty(pt) &&
            immediateCoreHandlers.TryGetValue(pt, out var handler))
        {
            handler(new CoreContext { Core = coreCard, Entity = entity, Engine = engine });
            return;
        }

        if (coreCard.coreEffect.effects != null)
            ExecuteEffects(coreCard, entity);
    }

    // ===================================================
    //  트리거 핸들링
    // ===================================================

    /// <summary>
    /// 특정 트리거에 해당하는 모든 활성 코어 효과를 처리한다.
    /// </summary>
    public void HandleTrigger(string trigger, EntityState entity, CardData playedCard = null)
    {
        if (entity.activeCores == null || entity.activeCores.Count == 0) return;

        var cores = new List<CardData>(entity.activeCores);
        foreach (var core in cores)
        {
            if (core.coreEffect == null) continue;
            if (core.coreEffect.trigger != trigger) continue;

            Debug.Log($"[Core] '{core.cardName}' 발동 (트리거: {trigger})");

            string pt  = core.coreEffect.coreType;
            var    ctx = new CoreContext
            {
                Core      = core,
                Entity     = entity,
                PlayedCard = playedCard,
                Engine     = engine,
            };

            if (!string.IsNullOrEmpty(pt))
            {
                if (entity.opponent == null)
                {
                    Debug.LogWarning($"[CoreManager] entity.opponent이 null입니다. coreType: {pt}");
                    continue;
                }

                if (customCoreHandlers.TryGetValue(pt, out var handler))
                    handler(ctx);
                else if (core.coreEffect.effects != null)
                    ExecuteEffects(core, entity);
            }
            else if (core.coreEffect.effects != null)
            {
                ExecuteEffects(core, entity);
            }
        }
    }

    // ===================================================
    //  도박 결과 이벤트
    // ===================================================

    public void OnGambleResult(EntityState entity, bool success, CardData card)
    {
        if (entity.activeCores == null) return;

        foreach (var core in entity.activeCores)
        {
            string coreType = core.coreEffect?.coreType;

            if (coreType == "drawOnLuckyChance")
            {
                if (!success) continue;

                float drawChance = core.coreEffect.value > 0
                    ? core.coreEffect.value
                    : 0.33f;
                if (drawChance > 1f) drawChance /= 100f;
                drawChance = Mathf.Clamp01(drawChance);
                if (UnityEngine.Random.value < drawChance)
                {
                    Debug.Log($"[Core] '{core.cardName}' 발동 (트리거: gambleResult, 성공: true)");
                    engine.DrawCards(entity, 1);
                }
                continue;
            }

            if (coreType == "rouletteMaster")
            {
                Debug.Log($"[Core] '{core.cardName}' 발동 (트리거: gambleResult, 성공: {success})");

                if (success)
                {
                    int successGain = core.coreEffect.value > 0
                        ? Mathf.Max(1, Mathf.RoundToInt(core.coreEffect.value))
                        : 2;
                    entity.luck += successGain;
                }
                else
                {
                    entity.luck += Mathf.Max(1, card?.cost ?? 1);
                }
            }
        }
    }

    // ===================================================
    //  바이러스 소모 이벤트
    // ===================================================

    public void OnVirusConsumed(EntityState entity, int consumed)
    {
        if (entity.activeCores == null) return;

        var cores = new List<CardData>(entity.activeCores);
        foreach (var core in cores)
        {
            string pt = core.coreEffect?.coreType;
            if (pt == null) continue;

            bool isConsumedTrigger = core.coreEffect.trigger == "virusConsumed";
            bool isAbsoluteCarrier = pt == "absoluteCarrier";
            if (!isConsumedTrigger && !isAbsoluteCarrier) continue;

            Debug.Log($"[Core] '{core.cardName}' 발동 (트리거: virusConsumed, 소모량: {consumed})");

            float val    = core.coreEffect.value;
            float ratio  = val > 0 ? val : 0.5f;
            int   amount = Mathf.Max(1, Mathf.RoundToInt(consumed * ratio));

            switch (pt)
            {
                case "bioWeaponVault":
                    entity.strength += amount;
                    break;
                case "immuneSystemTakeover":
                    entity.shield += amount;
                    break;
                case "absoluteCarrier":
                case "absoluteCarrierEnergy":
                    entity.energy++;
                    break;
                default:
                    if (core.coreEffect.effects != null)
                        ExecuteEffects(core, entity);
                    break;
            }
        }
    }

    // ===================================================
    //  바이러스 부여 이벤트
    // ===================================================

    public void OnVirusApplied(EntityState caster, EntityState target, int amount)
    {
        if (caster.activeCores == null) return;

        var cores = new List<CardData>(caster.activeCores);
        foreach (var core in cores)
        {
            if (core.coreEffect?.trigger != "virusApplied") continue;

            Debug.Log($"[Core] '{core.cardName}' 발동 (트리거: virusApplied, 부여량: {amount})");

            string pt = core.coreEffect.coreType;
            switch (pt)
            {
                case "viralLab":
                    target.virus += 1;
                    break;
                default:
                    if (core.coreEffect.effects != null)
                        ExecuteEffects(core, caster);
                    break;
            }
        }
    }

    // ===================================================
    //  헬퍼
    // ===================================================

    private void ExecuteEffects(CardData core, EntityState entity)
    {
        if (entity.opponent == null) return;

        var ctx = new EffectContext
        {
            Caster = entity,
            Target = entity.opponent,
            Card   = core,
            Engine = engine,
        };
        interpreter.ExecuteAll(core.coreEffect.effects, ctx);
    }
}
