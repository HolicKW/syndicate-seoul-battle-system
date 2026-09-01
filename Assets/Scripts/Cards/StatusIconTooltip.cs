using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class StatusIconTooltip : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
{
    private Canvas canvas;
    private RectTransform rectTransform;
    private TMP_Text valueText;
    private string title;
    private string body;
    private bool isHovering;

    public void Configure(Canvas ownerCanvas, string statusTitle, string description, TMP_Text stackText)
    {
        canvas = ownerCanvas;
        rectTransform = transform as RectTransform;
        title = statusTitle;
        body = description;
        valueText = stackText;
    }

    private void Awake()
    {
        rectTransform = transform as RectTransform;
    }

    private void Update()
    {
        if (canvas == null || rectTransform == null || string.IsNullOrEmpty(title) || string.IsNullOrEmpty(body))
            return;

        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        bool containsPointer = RectTransformUtility.RectangleContainsScreenPoint(rectTransform, Input.mousePosition, eventCamera);
        if (containsPointer)
        {
            isHovering = true;
            Show(Input.mousePosition);
        }
        else if (isHovering)
        {
            isHovering = false;
            KeywordTooltipPopup.Hide();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        Show(eventData.position);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (isHovering)
            Show(eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        KeywordTooltipPopup.Hide();
    }

    private void OnDisable()
    {
        if (isHovering)
        {
            isHovering = false;
            KeywordTooltipPopup.Hide();
        }
    }

    private void Show(Vector2 screenPosition)
    {
        if (canvas == null || string.IsNullOrEmpty(title) || string.IsNullOrEmpty(body))
            return;

        string stackLine = valueText != null && !string.IsNullOrWhiteSpace(valueText.text)
            ? $"\n현재 수치: {valueText.text}"
            : string.Empty;

        KeywordTooltipPopup.Show(canvas, $"<b>{title}</b>\n{body}{stackLine}", screenPosition);
    }
}
