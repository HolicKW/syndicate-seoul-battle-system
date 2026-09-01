/// <summary>
/// BattleInitializer - UI 동기화 메서드 모음.
/// SerializeField 참조는 BattleInitializer.cs에서 유지되므로 Inspector 영향 없음.
/// </summary>
public partial class BattleInitializer
{
    /// <summary>DeckManager 카운터 UI를 BattleEngine 상태로 동기화.</summary>
    private void SyncDeckUI()
    {
        if (deckManager != null && engine != null && engine.Player != null)
            deckManager.SyncFromEngine(engine.Player);
    }

    /// <summary>HP/Status/Energy/Core UI를 EntityState로 동기화.</summary>
    private void SyncHpUI()
    {
        if (enemyHP  != null) enemyHP.SyncFromState();
        if (playerHP != null) playerHP.SyncFromState();

        if (playerStatusUI != null) playerStatusUI.SyncFromState();
        if (enemyStatusUI  != null) enemyStatusUI.SyncFromState();
        if (playerEnergyUI != null) playerEnergyUI.SyncFromState();
        if (enemyEnergyUI  != null) enemyEnergyUI.SyncFromState();
        if (playerCoreListUI != null) playerCoreListUI.SyncFromState();
        if (enemyCoreListUI  != null) enemyCoreListUI.SyncFromState();
    }
}
