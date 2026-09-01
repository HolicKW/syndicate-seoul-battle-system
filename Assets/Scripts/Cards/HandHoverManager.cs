using System.Collections.Generic;
using UnityEngine;

public class HandHoverManager : MonoBehaviour
{
    [Header("Hover Animation")]
    [SerializeField] private float hoverSpeed = 12f;
    [SerializeField] private float screenPadding = 16f;

    [Header("References")]
    [SerializeField] private RectTransform handAreaRT;

    private readonly List<HandCardUI> cards = new List<HandCardUI>();
    private HandCardUI currentHovered;
    private Canvas parentCanvas;
    private RectTransform canvasRT;

    void Awake()
    {
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
            parentCanvas = FindFirstObjectByType<Canvas>();
        if (parentCanvas != null)
            canvasRT = parentCanvas.GetComponent<RectTransform>();
    }

    public void SetHandArea(RectTransform handArea)
    {
        handAreaRT = handArea;
    }

    public void SetCards(List<HandCardUI> handCards)
    {
        cards.Clear();
        cards.AddRange(handCards);
        currentHovered = null;
    }

    void Update()
    {
        UpdateHoverDetection();
        AnimateCards();
    }

    private void UpdateHoverDetection()
    {
        if (cards.Count == 0 || canvasRT == null) return;

        bool anyDragging = false;
        foreach (var card in cards)
        {
            if (IsHoverEligible(card) && card.IsDragging)
            {
                anyDragging = true;
                break;
            }
        }

        if (anyDragging)
        {
            if (currentHovered != null)
            {
                currentHovered.SetHovered(false);
                currentHovered = null;
            }
            return;
        }

        Camera cam = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;

        if (handAreaRT == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            handAreaRT, Input.mousePosition, cam, out Vector2 handLocal);

        Rect handRect = handAreaRT.rect;
        float yMargin = 80f;
        bool mouseInHandArea = handLocal.x >= handRect.xMin && handLocal.x <= handRect.xMax &&
                               handLocal.y >= handRect.yMin - yMargin && handLocal.y <= handRect.yMax + yMargin;

        HandCardUI closest = null;

        if (mouseInHandArea)
        {
            float minDist = float.MaxValue;

            foreach (var card in cards)
            {
                if (!IsHoverEligible(card) || card.IsDragging)
                    continue;

                float cardX = card.OriginalPosition.x;
                float dist = Mathf.Abs(handLocal.x - cardX);

                if (dist < minDist)
                {
                    minDist = dist;
                    closest = card;
                }
            }
        }
        else if (IsHoverEligible(currentHovered))
        {
            var cardRT = currentHovered.RT;
            if (cardRT != null && RectTransformUtility.RectangleContainsScreenPoint(cardRT, Input.mousePosition, cam))
                closest = currentHovered;
        }

        if (closest != currentHovered)
        {
            if (currentHovered != null)
                currentHovered.SetHovered(false);

            currentHovered = closest;

            if (currentHovered != null)
                currentHovered.SetHovered(true);
        }
    }

    private void AnimateCards()
    {
        float speed = hoverSpeed * Time.deltaTime;

        foreach (var card in cards)
        {
            if (card == null || card.IsDragging) continue;

            if (!IsHoverEligible(card))
            {
                if (card.IsHovered)
                    card.SetHovered(false);
                continue;
            }

            var rt = card.RT;
            if (rt == null) continue;

            if (card.IsHovered)
            {
                Vector3 targetScale = card.OriginalScale * card.HoverScale;
                var targetPos = new Vector2(card.OriginalPosition.x, card.OriginalPosition.y + card.HoverRaise);
                targetPos = KeepCardInsideCanvas(rt, targetPos, targetScale);
                rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, targetPos, speed);
                rt.localRotation = Quaternion.Lerp(rt.localRotation, Quaternion.identity, speed);
                rt.localScale = Vector3.Lerp(rt.localScale, targetScale, speed);
            }
            else
            {
                rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, card.OriginalPosition, speed);
                rt.localRotation = Quaternion.Lerp(rt.localRotation, card.OriginalRotation, speed);
                rt.localScale = Vector3.Lerp(rt.localScale, card.OriginalScale, speed);
            }
        }
    }

    private static bool IsHoverEligible(HandCardUI card)
    {
        if (card == null || !card.gameObject.activeSelf || !card.enabled)
            return false;

        var group = card.GetComponent<CanvasGroup>();
        return group == null || (group.blocksRaycasts && group.interactable);
    }

    private Vector2 KeepCardInsideCanvas(RectTransform cardRT, Vector2 targetPos, Vector3 targetScale)
    {
        if (cardRT == null || canvasRT == null || cardRT.parent == null)
            return targetPos;

        Bounds bounds = GetProjectedCanvasBounds(cardRT, targetPos, targetScale);
        Rect canvasRect = canvasRT.rect;
        float minY = canvasRect.yMin + screenPadding;
        float maxY = canvasRect.yMax - screenPadding;

        float yOffset = 0f;
        if (bounds.min.y < minY)
            yOffset += minY - bounds.min.y;

        if (bounds.max.y + yOffset > maxY)
            yOffset -= bounds.max.y + yOffset - maxY;

        if (Mathf.Approximately(yOffset, 0f))
            return targetPos;

        return targetPos + CanvasLocalDeltaToParentDelta(cardRT.parent, new Vector2(0f, yOffset));
    }

    private Bounds GetProjectedCanvasBounds(RectTransform cardRT, Vector2 targetPos, Vector3 targetScale)
    {
        Vector3[] localCorners = new Vector3[4];
        cardRT.GetLocalCorners(localCorners);

        Bounds bounds = new Bounds();
        bool initialized = false;
        Quaternion targetRotation = Quaternion.identity;
        Transform parent = cardRT.parent;

        for (int i = 0; i < localCorners.Length; i++)
        {
            Vector3 scaledCorner = Vector3.Scale(localCorners[i], targetScale);
            Vector3 parentLocal = new Vector3(targetPos.x, targetPos.y, 0f) + targetRotation * scaledCorner;
            Vector3 world = parent.TransformPoint(parentLocal);
            Vector3 canvasLocal = canvasRT.InverseTransformPoint(world);

            if (!initialized)
            {
                bounds = new Bounds(canvasLocal, Vector3.zero);
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(canvasLocal);
            }
        }

        return bounds;
    }

    private Vector2 CanvasLocalDeltaToParentDelta(Transform parent, Vector2 canvasDelta)
    {
        Vector3 worldStart = canvasRT.TransformPoint(Vector3.zero);
        Vector3 worldEnd = canvasRT.TransformPoint(new Vector3(canvasDelta.x, canvasDelta.y, 0f));
        Vector3 parentStart = parent.InverseTransformPoint(worldStart);
        Vector3 parentEnd = parent.InverseTransformPoint(worldEnd);
        Vector3 parentDelta = parentEnd - parentStart;
        return new Vector2(parentDelta.x, parentDelta.y);
    }
}
