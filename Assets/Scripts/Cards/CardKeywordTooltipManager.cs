using UnityEngine;
using TMPro;

public class CardKeywordTooltipManager : MonoBehaviour
{
    public static CardKeywordTooltipManager Instance { get; private set; }

    public static CardKeywordTooltipManager GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        var go = new GameObject("CardKeywordTooltipManager");
        return go.AddComponent<CardKeywordTooltipManager>();
    }

    private HandCardUI trackedCard;
    private string currentKeywordLinkId;
    private Canvas parentCanvas;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetTrackedCard(HandCardUI card)
    {
        if (trackedCard == card) return;

        Hide();
        trackedCard = card;

        if (card != null)
            parentCanvas = card.GetComponentInParent<Canvas>();
    }

    public void ClearTrackedCard(HandCardUI card)
    {
        if (trackedCard != card) return;
        SetTrackedCard(null);
    }

    private void Update()
    {
        if (trackedCard == null || trackedCard.CardData == null || trackedCard.IsDragging)
        {
            Hide();
            return;
        }

        var descriptionText = trackedCard.CardDescriptionText;
        if (descriptionText == null)
        {
            Hide();
            return;
        }

        Vector2 screenPosition = Input.mousePosition;
        Camera cam = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? parentCanvas.worldCamera
            : null;

        int linkIndex = TMP_TextUtilities.FindIntersectingLink(descriptionText, screenPosition, cam);
        if (linkIndex == -1)
        {
            Hide();
            return;
        }

        TMP_LinkInfo linkInfo = descriptionText.textInfo.linkInfo[linkIndex];
        string keyword = linkInfo.GetLinkID();
        string line = KeywordTooltipBuilder.BuildLine(trackedCard.CardData, keyword);
        if (string.IsNullOrEmpty(line))
        {
            Hide();
            return;
        }

        currentKeywordLinkId = keyword;
        KeywordTooltipPopup.Show(parentCanvas, line, screenPosition);
    }

    public void Hide()
    {
        if (string.IsNullOrEmpty(currentKeywordLinkId))
            return;

        currentKeywordLinkId = null;
        KeywordTooltipPopup.Hide();
    }
}
