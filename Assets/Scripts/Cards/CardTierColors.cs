using UnityEngine;

public static class CardTierColors
{
    public static Color GetNameColor(int tier)
    {
        return tier switch
        {
            1 => new Color32(232, 238, 247, 255),
            2 => new Color32(98, 200, 255, 255),
            3 => new Color32(198, 140, 255, 255),
            4 => new Color32(255, 216, 107, 255),
            5 => new Color32(229, 80, 80, 255),
            _ => Color.white,
        };
    }
}
