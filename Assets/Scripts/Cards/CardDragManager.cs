using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardDragManager : MonoBehaviour
{
    public static CardDragManager Instance { get; private set; }

    [SerializeField] private float useThreshold = 150f;

    private HandCardUI draggingCard;
    private Vector2 dragStartScreenPosition;

    public bool IsDragging => draggingCard != null;

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

    public void OnBeginDrag(HandCardUI card, PointerEventData eventData)
    {
        if (card == null) return;

        draggingCard = card;
        dragStartScreenPosition = eventData.position;

        card.SetDragging(true);
        card.SetHoverState(false);

        var canvasGroup = card.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
            canvasGroup.blocksRaycasts = false;

        var rt = card.RT;
        rt.SetAsLastSibling();
        rt.localRotation = Quaternion.identity;
        rt.localScale = card.OriginalScale * card.HoverScale;

        CardGameSFXManager.PlayCardGrab();
    }

    public void OnDrag(HandCardUI card, PointerEventData eventData)
    {
        if (draggingCard != card) return;

        var canvas = card.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        card.RT.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(HandCardUI card, PointerEventData eventData)
    {
        if (draggingCard != card) return;

        draggingCard = null;
        card.SetDragging(false);

        var canvasGroup = card.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
            canvasGroup.blocksRaycasts = true;

        float dragHeight = eventData != null
            ? eventData.position.y - dragStartScreenPosition.y
            : card.RT.anchoredPosition.y - card.OriginalPosition.y;

        if (dragHeight >= useThreshold)
        {
            if (BattleEngine.Instance != null && !BattleEngine.Instance.CanPlayCard(card.HandIndex))
            {
                card.RT.SetSiblingIndex(card.OriginalSiblingIndex);
                return;
            }

            CardGameSFXManager.PlayCardUse();
            card.InvokeUseCallback();
        }
        else
        {
            card.RT.SetSiblingIndex(card.OriginalSiblingIndex);
        }
    }
}
