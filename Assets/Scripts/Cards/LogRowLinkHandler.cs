using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 배틀 로그 각 행(row)에 붙여 TMP 링크 태그 위 마우스 호버를 감지한다.
/// BattleLogUI.AddRow()에서 동적으로 추가된다.
/// </summary>
public class LogRowLinkHandler : MonoBehaviour,
    IPointerMoveHandler, IPointerExitHandler
{
    private TMP_Text tmpText;
    private CardTooltipUI tooltip;
    private Camera uiCamera;
    private string hoveredLinkId;

    public void Init(CardTooltipUI tooltipUI, Camera cam)
    {
        tooltip   = tooltipUI;
        uiCamera  = cam;
        tmpText   = GetComponent<TMP_Text>();
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (tmpText == null || tooltip == null) return;

        int linkIdx = TMP_TextUtilities.FindIntersectingLink(
            tmpText, eventData.position, uiCamera);

        if (linkIdx >= 0)
        {
            string linkId = tmpText.textInfo.linkInfo[linkIdx].GetLinkID();
            if (linkId != hoveredLinkId)
            {
                hoveredLinkId = linkId;
                tooltip.Show(linkId, eventData.position);
            }
        }
        else
        {
            if (hoveredLinkId != null)
            {
                hoveredLinkId = null;
                tooltip.Hide();
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hoveredLinkId = null;
        tooltip?.Hide();
    }

    private void OnDestroy()
    {
        tooltip?.Hide();
    }
}
