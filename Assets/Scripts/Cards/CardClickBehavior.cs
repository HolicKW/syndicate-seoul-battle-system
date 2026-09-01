using System;
using UnityEngine;

/// <summary>
/// HandCardUI의 입력 동작을 정의하는 전략 인터페이스.
/// 모드(전투/멀리건/해체선택/인벤토리)별로 클릭과 드래그/호버 허용 여부가 달라진다.
/// </summary>
public interface ICardClickBehavior
{
    /// <summary>카드 클릭 시 실행할 동작.</summary>
    void OnClick(HandCardUI card);

    /// <summary>드래그(카드 사용) 허용 여부.</summary>
    bool IsDraggable { get; }

    /// <summary>호버 애니메이션 허용 여부.</summary>
    bool AllowsHover { get; }
}

// -----------------------------------------------------------------------------

/// <summary>
/// 일반 전투 모드. 드래그로 카드를 사용하며 클릭은 무시한다.
/// </summary>
public class BattleClickBehavior : ICardClickBehavior
{
    public static readonly BattleClickBehavior Instance = new BattleClickBehavior();

    public void OnClick(HandCardUI card) { } // 드래그로만 사용
    public bool IsDraggable => true;
    public bool AllowsHover => true;
}

// -----------------------------------------------------------------------------

/// <summary>
/// 멀리건 모드. 클릭으로 교체 선택 토글. 드래그/호버 불가.
/// </summary>
public class MulliganClickBehavior : ICardClickBehavior
{
    public void OnClick(HandCardUI card) => card.ToggleMulliganSelected();
    public bool IsDraggable => false;
    public bool AllowsHover => false;
}

// -----------------------------------------------------------------------------

/// <summary>
/// 해체 선택 모드. 클릭 시 콜백 호출. 드래그 불가, 호버는 허용.
/// </summary>
public class DismantleSelectClickBehavior : ICardClickBehavior
{
    private readonly Action<HandCardUI> callback;

    public DismantleSelectClickBehavior(Action<HandCardUI> callback)
    {
        this.callback = callback;
    }

    public void OnClick(HandCardUI card) => callback?.Invoke(card);
    public bool IsDraggable => false;
    public bool AllowsHover => true;
}

// -----------------------------------------------------------------------------

/// <summary>
/// 인벤토리/덱 빌더 모드 (handIndex == -1). 클릭으로 덱에 카드 추가.
/// 드래그/호버 불가 (레이아웃 그룹이 위치를 제어).
/// </summary>
public class InventoryClickBehavior : ICardClickBehavior
{
    public static readonly InventoryClickBehavior Instance = new InventoryClickBehavior();

    public void OnClick(HandCardUI card) => card.InvokeUseCallback();
    public bool IsDraggable => false;
    public bool AllowsHover => false;
}
