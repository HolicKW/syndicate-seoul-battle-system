using UnityEngine;
using TMPro;

/// <summary>
/// 에너지 (energy / baseEnergy)를 EntityState에서 읽어 UI 갱신.
/// PlayerHP/EnemyHP와 동일한 바인딩 패턴.
/// </summary>
public class EnergyUI : MonoBehaviour
{
    [SerializeField] private TMP_Text energyText;

    private EntityState state;

    /// <summary>
    /// EntityState를 바인딩한다. BattleEngine 초기화 후 호출한다.
    /// </summary>
    public void BindState(EntityState entityState)
    {
        state = entityState;
        SyncFromState();
    }

    /// <summary>
    /// state.energy / state.baseEnergy를 읽어 UI를 갱신한다.
    /// </summary>
    public void SyncFromState()
    {
        if (state == null || energyText == null) return;
        energyText.text = $"{state.energy}/{state.baseEnergy}";
    }
}
