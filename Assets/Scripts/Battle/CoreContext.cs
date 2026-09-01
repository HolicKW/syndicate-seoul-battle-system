/// <summary>
/// 코어 핸들러 실행 컨텍스트.
/// CoreManager의 customCoreHandlers / immediateCoreHandlers에 전달된다.
/// </summary>
public class CoreContext
{
    public CardData     Core;
    public EntityState  Entity;
    public CardData     PlayedCard;
    public BattleEngine Engine;

    /// <summary>coreEffect.value 단축 접근자</summary>
    public float Val => Core.coreEffect.value;

    /// <summary>entity.opponent 단축 접근자</summary>
    public EntityState Opponent => Entity.opponent;
}
