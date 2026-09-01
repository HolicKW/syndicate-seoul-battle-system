using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "CardTypeVisualPalette", menuName = "NewWorld/Card Type Visual Palette")]
public class CardTypeVisualPalette : ScriptableObject
{
    [FormerlySerializedAs("attackGlow")]
    [SerializeField] private Color attackFrameColor = new Color(0.898f, 0.314f, 0.314f, 1f); // #E55050
    [FormerlySerializedAs("skillGlow")]
    [SerializeField] private Color skillFrameColor  = new Color(0.353f, 0.784f, 0.898f, 1f); // #5AC8E5
    [FormerlySerializedAs("powerGlow")]
    [FormerlySerializedAs("powerFrameColor")]
    [SerializeField] private Color coreFrameColor  = new Color(0.725f, 0.408f, 0.898f, 1f); // #B968E5
    [SerializeField] private Color ipFrameColor = new Color(1f, 0.72f, 0.18f, 1f); // #FFB82E

    public static Color GetDefaultFrameColor(CardData card)
    {
        if (card != null && IsIpCard(card))
            return new Color(1f, 0.72f, 0.18f, 1f);

        return card != null ? GetDefaultFrameColor(card.type) : Color.white;
    }

    public static Color GetDefaultFrameColor(CardType type)
    {
        switch (type)
        {
            case CardType.Attack: return new Color(0.898f, 0.314f, 0.314f, 1f);
            case CardType.Skill:  return new Color(0.353f, 0.784f, 0.898f, 1f);
            case CardType.Core:  return new Color(0.725f, 0.408f, 0.898f, 1f);
            default:              return Color.white;
        }
    }

    public Color GetFrameColor(CardData card)
    {
        if (card != null && IsIpCard(card))
            return ipFrameColor;

        return card != null ? GetFrameColor(card.type) : Color.white;
    }

    public Color GetFrameColor(CardType type)
    {
        switch (type)
        {
            case CardType.Attack: return attackFrameColor;
            case CardType.Skill:  return skillFrameColor;
            case CardType.Core:  return coreFrameColor;
            default:              return Color.white;
        }
    }

    private static bool IsIpCard(CardData card)
    {
        return card != null && string.Equals(card.pack, "IP", System.StringComparison.OrdinalIgnoreCase);
    }
}
