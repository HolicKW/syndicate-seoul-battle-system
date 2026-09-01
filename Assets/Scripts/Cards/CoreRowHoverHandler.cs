using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CoreRowHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private string cardId;
    private CardTooltipUI tooltip;

    public void Init(string cardId, CardTooltipUI tooltipUI)
    {
        this.cardId = cardId;
        tooltip = tooltipUI;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!string.IsNullOrEmpty(cardId) && tooltip != null)
            tooltip.Show(cardId, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        tooltip?.Hide();
    }

    private void OnDisable()
    {
        tooltip?.Hide();
    }
}
