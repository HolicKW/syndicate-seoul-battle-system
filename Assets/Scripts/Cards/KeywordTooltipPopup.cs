using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class KeywordTooltipPopup : MonoBehaviour
{
    private const float PanelWidth = 300f;
    private const float Padding = 12f;

    private static KeywordTooltipPopup instance;

    private Canvas rootCanvas;
    private RectTransform panelRT;
    private TMP_Text bodyText;

    public static void Show(Canvas canvas, string text, Vector2 screenPosition)
    {
        if (canvas == null || string.IsNullOrEmpty(text))
        {
            Hide();
            return;
        }

        Ensure(canvas);
        instance.SetText(text);
        instance.SetPosition(screenPosition);
        instance.panelRT.gameObject.SetActive(true);
        instance.panelRT.SetAsLastSibling();
    }

    public static void Hide()
    {
        if (instance != null && instance.panelRT != null)
            instance.panelRT.gameObject.SetActive(false);
    }

    private static void Ensure(Canvas canvas)
    {
        if (instance != null && instance.rootCanvas == canvas)
            return;

        if (instance != null)
            Destroy(instance.gameObject);

        var go = new GameObject("KeywordTooltipPopup");
        go.transform.SetParent(canvas.transform, false);
        instance = go.AddComponent<KeywordTooltipPopup>();
        instance.rootCanvas = canvas;
        instance.Build();
    }

    private void Build()
    {
        panelRT = gameObject.AddComponent<RectTransform>();
        gameObject.AddComponent<CanvasRenderer>();

        var image = gameObject.AddComponent<Image>();
        image.color = new Color(0.06f, 0.07f, 0.10f, 0.96f);
        image.raycastTarget = false;

        var group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        var shadow = gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(2f, -2f);

        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.pivot = new Vector2(0f, 1f);
        panelRT.sizeDelta = new Vector2(PanelWidth, 96f);

        var textGO = new GameObject("KeywordTooltipText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(transform, false);

        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(Padding, Padding);
        textRT.offsetMax = new Vector2(-Padding, -Padding);

        bodyText = textGO.GetComponent<TMP_Text>();
        bodyText.fontSize = 13f;
        bodyText.color = new Color(0.92f, 0.92f, 0.92f);
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        bodyText.overflowMode = TextOverflowModes.Overflow;
        bodyText.raycastTarget = false;

        gameObject.SetActive(false);
    }

    private void SetText(string text)
    {
        bodyText.text = text;
        bodyText.ForceMeshUpdate();
        float height = Mathf.Clamp(bodyText.preferredHeight + Padding * 2f, 54f, 170f);
        panelRT.sizeDelta = new Vector2(PanelWidth, height);
    }

    private void SetPosition(Vector2 screenPosition)
    {
        var canvasRT = rootCanvas.transform as RectTransform;
        Camera cam = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        if (canvasRT == null ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screenPosition, cam, out var localPoint))
            return;

        Vector2 offset = new Vector2(18f, 18f);
        Vector2 position = localPoint + offset;
        Rect rect = canvasRT.rect;
        Vector2 size = panelRT.sizeDelta;

        position.x = Mathf.Clamp(position.x, rect.xMin + 8f, rect.xMax - size.x - 8f);
        position.y = Mathf.Clamp(position.y, rect.yMin + size.y + 8f, rect.yMax - 8f);
        panelRT.anchoredPosition = position;
    }
}
