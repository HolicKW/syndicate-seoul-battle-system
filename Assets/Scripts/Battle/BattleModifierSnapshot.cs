using System;
using System.Collections.Generic;

[Serializable]
public class BattleModifierSnapshot
{
    public int maxHpBonus;
    public int maxEnergyBonus;
    public int startShieldBonus;
    public int startDrawBonus;
    public List<BattleSupportEffect> supportEffects = new List<BattleSupportEffect>();
    public List<BuffBuildingEffect> legacyBuffEffects = new List<BuffBuildingEffect>();

    public static BattleModifierSnapshot Empty()
    {
        return new BattleModifierSnapshot();
    }
}
