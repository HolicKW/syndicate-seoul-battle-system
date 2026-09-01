using UnityEngine;

/// <summary>
/// 적 AI 전략 타입.
/// </summary>
public enum AIStrategy { Aggressive, Defensive, Balanced, Random }

/// <summary>
/// 적 턴에 카드를 자율 선택하여 BattleEngine.PlayCard()를 호출한다.
/// 플레이어와 완전히 동일한 카드 시스템을 사용.
/// </summary>
public class EnemyAI : MonoBehaviour
{
    public static EnemyAI Instance { get; private set; }

    [SerializeField] private AIStrategy strategy = AIStrategy.Balanced;
    [SerializeField] private BigFivePersonality personality = new BigFivePersonality();

    private EnemyAIProfileSO.StrategyWeights aggressiveWeights = CreateDefaultAggressiveWeights();
    private EnemyAIProfileSO.StrategyWeights defensiveWeights = CreateDefaultDefensiveWeights();
    private EnemyAIProfileSO.StrategyWeights balancedWeights = CreateDefaultBalancedWeights();
    private EnemyAIProfileSO.StrategyWeights randomWeights = CreateDefaultRandomWeights();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        strategy = ResolveStrategyFromBigFive(personality);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void InitializePersonality(BigFivePersonality p)
    {
        personality = ClonePersonality(p) ?? new BigFivePersonality();
        strategy = ResolveStrategyFromBigFive(personality);
    }

    public void InitializeWeights(EnemyAIProfileSO profile)
    {
        aggressiveWeights = CloneWeights(profile != null ? profile.aggressive : null, CreateDefaultAggressiveWeights());
        defensiveWeights = CloneWeights(profile != null ? profile.defensive : null, CreateDefaultDefensiveWeights());
        balancedWeights = CloneWeights(profile != null ? profile.balanced : null, CreateDefaultBalancedWeights());
        randomWeights = CloneWeights(profile != null ? profile.random : null, CreateDefaultRandomWeights());
    }

    // ===================================================
    //  턴 실행
    // ===================================================

    /// <summary>
    /// 적 턴 전체를 실행한다. TurnManager.EndPlayerTurn()에서 호출.
    /// 에너지가 부족하거나 사용 가능한 카드가 없을 때까지 카드를 선택·사용한다.
    /// </summary>
    public int PlayTurn(EntityState enemy, EntityState player)
    {
        var engine = BattleEngine.Instance;
        if (engine == null) return 0;

        int playedCount = 0;

        int safetyLimit = 30; // 무한 루프 방지
        while (safetyLimit-- > 0)
        {
            if (enemy.forceEndTurn) break;
            if (engine.IsBattleEnded) break;

            int bestIndex = SelectBestCard(enemy, player);
            if (bestIndex < 0) break; // 사용 가능한 카드 없음

            engine.PlayCard(bestIndex, enemy, player);
            playedCount++;
        }

        return playedCount;
    }

    public int SelectBestPlayableCard(EntityState enemy, EntityState player)
    {
        return SelectBestCard(enemy, player);
    }

    // ===================================================
    //  카드 선택
    // ===================================================

    /// <summary>
    /// 손패에서 가장 높은 점수의 카드 인덱스를 반환한다.
    /// 사용 가능한 카드가 없으면 -1 반환.
    /// </summary>
    private int SelectBestCard(EntityState enemy, EntityState player)
    {
        var engine = BattleEngine.Instance;
        int bestIdx = -1;
        float bestScore = float.MinValue;

        for (int i = 0; i < enemy.hand.Count; i++)
        {
            if (!engine.CanPlayCard(i, enemy)) continue;

            float score = EvaluateCard(enemy.hand[i], enemy, player);
            if (score > bestScore)
            {
                bestScore = score;
                bestIdx = i;
            }
        }

        return bestIdx;
    }

    // ===================================================
    //  카드 평가
    // ===================================================

    /// <summary>
    /// 카드 점수를 계산한다.
    /// 티어 기본점수 + 프로필 가중치 + 상태 보정 + 시너지 보정 + 무작위성.
    /// </summary>
    private float EvaluateCard(CardData card, EntityState enemy, EntityState player)
    {
        float score = card.tier * 10f;

        CardEvaluator.CardEvaluation eval = CardEvaluator.Evaluate(card, enemy, player);
        EnemyAIProfileSO.StrategyWeights weights = GetWeightsForStrategy(strategy);

        score += eval.damage * weights.wDamage;
        score += eval.shield * weights.wShield;
        score += eval.draw * weights.wDraw;
        score += eval.heal * weights.wHeal;
        score += eval.statusBuff * weights.wStatusBuff;
        score += eval.statusDebuff * weights.wStatusDebuff;
        score += eval.energy * weights.wEnergy;
        score += eval.discard * weights.wDiscard;
        score += eval.coreLongTerm * weights.wCoreLongTerm;
        score -= eval.unknownValue * 0.5f;

        float myHpRatio = enemy.maxHp > 0 ? (float)enemy.hp / enemy.maxHp : 1f;
        float targetHpRatio = player.maxHp > 0 ? (float)player.hp / player.maxHp : 1f;

        if (myHpRatio < 0.3f)
        {
            score += eval.shield * 0.6f;
            score += eval.heal * 0.8f;
            if (card.type == CardType.Skill) score += 10f;
        }

        if (targetHpRatio < 0.3f)
        {
            score += eval.damage * 0.35f;
            if (card.type == CardType.Attack) score += 10f;
        }

        int effectiveCost = card.cost;
        if (BattleEngine.Instance != null)
            effectiveCost = BattleEngine.Instance.GetEffectiveCost(card, enemy);

        if (enemy.energy <= 1 && effectiveCost <= 1)
            score += 5f;

        if (card.HasKeyword("overclock") && enemy.overclockStacks > 0)
            score += enemy.overclockStacks * 3f;
        if (card.HasKeyword("network") && enemy.networkStacks > 0)
            score += enemy.networkStacks * 3f;

        int remainingAfter = enemy.energy - effectiveCost;
        if (remainingAfter > 2)
            score -= remainingAfter;

        float noiseMax = strategy == AIStrategy.Random ? 10f : 3f;
        score += Random.Range(0f, noiseMax);

        return score;
    }

    public static AIStrategy ResolveStrategyFromBigFive(BigFivePersonality p)
    {
        BigFivePersonality source = p ?? new BigFivePersonality();

        float openness = source.Openness01;
        float conscientiousness = source.Conscientiousness01;
        float extraversion = source.Extraversion01;
        float agreeableness = source.Agreeableness01;
        float neuroticism = source.Neuroticism01;

        float aggressiveScore = extraversion * 0.6f + (1f - agreeableness) * 0.4f;
        float defensiveScore = conscientiousness * 0.7f + (1f - extraversion) * 0.3f;
        float randomScore = openness * 0.5f + neuroticism * 0.5f;
        float balancedScore = 1f - (
            Mathf.Abs(openness - 0.5f) +
            Mathf.Abs(conscientiousness - 0.5f) +
            Mathf.Abs(extraversion - 0.5f) +
            Mathf.Abs(agreeableness - 0.5f) +
            Mathf.Abs(neuroticism - 0.5f)) / 5f;

        AIStrategy resolved = AIStrategy.Balanced;
        float bestScore = balancedScore;

        if (aggressiveScore > bestScore)
        {
            bestScore = aggressiveScore;
            resolved = AIStrategy.Aggressive;
        }

        if (defensiveScore > bestScore)
        {
            bestScore = defensiveScore;
            resolved = AIStrategy.Defensive;
        }

        if (randomScore > bestScore)
            resolved = AIStrategy.Random;

        return resolved;
    }

    private EnemyAIProfileSO.StrategyWeights GetWeightsForStrategy(AIStrategy currentStrategy)
    {
        switch (currentStrategy)
        {
            case AIStrategy.Aggressive:
                return aggressiveWeights;
            case AIStrategy.Defensive:
                return defensiveWeights;
            case AIStrategy.Random:
                return randomWeights;
            default:
                return balancedWeights;
        }
    }

    private static BigFivePersonality ClonePersonality(BigFivePersonality source)
    {
        if (source == null)
            return null;

        return new BigFivePersonality
        {
            openness = source.openness,
            conscientiousness = source.conscientiousness,
            extraversion = source.extraversion,
            agreeableness = source.agreeableness,
            neuroticism = source.neuroticism,
        };
    }

    private static EnemyAIProfileSO.StrategyWeights CloneWeights(
        EnemyAIProfileSO.StrategyWeights source,
        EnemyAIProfileSO.StrategyWeights fallback)
    {
        EnemyAIProfileSO.StrategyWeights template = source ?? fallback;
        return new EnemyAIProfileSO.StrategyWeights
        {
            wDamage = template.wDamage,
            wShield = template.wShield,
            wDraw = template.wDraw,
            wHeal = template.wHeal,
            wStatusBuff = template.wStatusBuff,
            wStatusDebuff = template.wStatusDebuff,
            wEnergy = template.wEnergy,
            wDiscard = template.wDiscard,
            wCoreLongTerm = template.wCoreLongTerm,
        };
    }

    private static EnemyAIProfileSO.StrategyWeights CreateDefaultAggressiveWeights()
    {
        return new EnemyAIProfileSO.StrategyWeights
        {
            wDamage = 1.9f,
            wShield = 0.5f,
            wDraw = 0.8f,
            wHeal = 0.6f,
            wStatusBuff = 0.9f,
            wStatusDebuff = 1.2f,
            wEnergy = 0.9f,
            wDiscard = 1.0f,
            wCoreLongTerm = 0.8f,
        };
    }

    private static EnemyAIProfileSO.StrategyWeights CreateDefaultDefensiveWeights()
    {
        return new EnemyAIProfileSO.StrategyWeights
        {
            wDamage = 0.9f,
            wShield = 1.8f,
            wDraw = 1.0f,
            wHeal = 1.6f,
            wStatusBuff = 0.9f,
            wStatusDebuff = 0.8f,
            wEnergy = 1.0f,
            wDiscard = 1.1f,
            wCoreLongTerm = 0.8f,
        };
    }

    private static EnemyAIProfileSO.StrategyWeights CreateDefaultBalancedWeights()
    {
        return new EnemyAIProfileSO.StrategyWeights
        {
            wDamage = 1.25f,
            wShield = 1.2f,
            wDraw = 1.1f,
            wHeal = 1.0f,
            wStatusBuff = 1.0f,
            wStatusDebuff = 1.0f,
            wEnergy = 1.0f,
            wDiscard = 1.0f,
            wCoreLongTerm = 1.0f,
        };
    }

    private static EnemyAIProfileSO.StrategyWeights CreateDefaultRandomWeights()
    {
        return new EnemyAIProfileSO.StrategyWeights
        {
            wDamage = 1.0f,
            wShield = 0.9f,
            wDraw = 1.0f,
            wHeal = 0.9f,
            wStatusBuff = 1.0f,
            wStatusDebuff = 1.0f,
            wEnergy = 1.0f,
            wDiscard = 0.9f,
            wCoreLongTerm = 1.1f,
        };
    }
}
