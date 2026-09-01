/// <summary>
/// Legacy support building battle-start effect payload.
/// buffType: "Generic_Energy" | "Generic_Shield" | "Generic_Draw"
/// </summary>
public struct BuffBuildingEffect
{
    public string buffType;
    public int value;
}

/// <summary>
/// 전투 씬 간 데이터를 전달하는 정적 클래스.
/// MonoBehaviour가 아니므로 씬 로드/언로드에 영향받지 않습니다.
/// </summary>
public static class BattleSceneData
{
    // 튜토리얼 모드
    public static bool IsTutorial;

    // Management → CardGame 전달 데이터
    public static string AttackerFactionName;
    public static string DefenderFactionName;
    public static string PlayerFactionName;
    public static string TargetCityName;
    public static System.Collections.Generic.List<string> PlayerDeckIds;
    public static bool PlayerControlsDefender;

    // 스캐빈저 슬롯 해금 전투 (플레이어 자기 도시의 잠긴 슬롯을 전투로 해금)
    public static bool ScavengerSlotUnlock;
    public static string ScavengerTargetCityName;
    public static int ScavengerTargetSlotIndex = -1;

    /// <summary>
    /// 방어자(적) AI 팩션이 실제 보유한 카드 인벤토리(수량만큼 펼친 ID 풀).
    /// InventoryDeckBuilder가 이 풀로 적 덱을 구성한다. 비어 있으면 임시 덱으로 폴백.
    /// </summary>
    public static System.Collections.Generic.List<string> EnemyInventoryCardIds;
    public static string EnemyFactionName;
    public static string EnemyCeoId;
    public static UnityEngine.Sprite EnemyCeoProfileSprite;
    public static int EnemyMaxHp;
    public static BattleModifierSnapshot AttackerModifiers = BattleModifierSnapshot.Empty();
    public static BattleModifierSnapshot DefenderModifiers = BattleModifierSnapshot.Empty();
    public static BigFivePersonality EnemyPersonality;

    // CardGame → Management 전달 데이터
    public static bool HasResult;
    public static bool AttackerWon;

    /// <summary>
    /// 전투 중 플레이어가 실제로 소모한 카드 ID 목록.
    /// 같은 카드를 여러 번 사용하면 중복 포함.
    /// </summary>
    public static System.Collections.Generic.List<string> ConsumedCardIds
        = new System.Collections.Generic.List<string>();

    /// <summary>
    /// 모든 데이터를 초기화합니다.
    /// </summary>
    public static void Clear()
    {
        IsTutorial = false;
        AttackerFactionName = null;
        DefenderFactionName = null;
        PlayerFactionName = null;
        TargetCityName = null;
        PlayerDeckIds = null;
        PlayerControlsDefender = false;
        ScavengerSlotUnlock = false;
        ScavengerTargetCityName = null;
        ScavengerTargetSlotIndex = -1;
        EnemyInventoryCardIds = null;
        EnemyFactionName = null;
        EnemyCeoId = null;
        EnemyCeoProfileSprite = null;
        EnemyMaxHp = 0;
        AttackerModifiers = BattleModifierSnapshot.Empty();
        DefenderModifiers = BattleModifierSnapshot.Empty();
        EnemyPersonality = null;
        HasResult = false;
        AttackerWon = false;
        ConsumedCardIds = new System.Collections.Generic.List<string>();
    }
}
