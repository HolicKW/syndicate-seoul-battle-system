using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardMulliganHandler : MonoBehaviour
{
    public static CardMulliganHandler Instance { get; private set; }

    private readonly Dictionary<HandCardUI, MulliganState> states = new Dictionary<HandCardUI, MulliganState>();

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

    public void SetupMulligan(HandCardUI card, bool isMulligan)
    {
        if (card == null) return;

        if (!isMulligan)
        {
            RemoveState(card);
            return;
        }

        if (!states.ContainsKey(card))
            states[card] = new MulliganState();

        var state = states[card];
        state.isSelected = false;
        EnsureOverlay(card, state);
        state.overlay.SetActive(false);
    }

    public void ToggleSelected(HandCardUI card)
    {
        if (card == null) return;

        if (!states.TryGetValue(card, out var state))
            return;

        state.isSelected = !state.isSelected;
        if (state.overlay != null)
            state.overlay.SetActive(state.isSelected);

        CardGameSFXManager.PlayMulliganCardClick();
        Debug.Log($"[CardMulliganHandler] '{card.CardData?.cardName}' 교체 선택: {state.isSelected}");
    }

    public bool IsSelected(HandCardUI card)
    {
        return states.TryGetValue(card, out var state) && state.isSelected;
    }

    public void ClearAll()
    {
        foreach (var kvp in states)
        {
            if (kvp.Value.overlay != null)
                Destroy(kvp.Value.overlay);
        }
        states.Clear();
    }

    private void RemoveState(HandCardUI card)
    {
        if (!states.TryGetValue(card, out var state))
            return;

        if (state.overlay != null)
            Destroy(state.overlay);
        states.Remove(card);
    }

    private void EnsureOverlay(HandCardUI card, MulliganState state)
    {
        if (state.overlay != null) return;

        var overlay = new GameObject("MulliganOverlay",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(card.transform, false);

        var overlayRT = overlay.GetComponent<RectTransform>();
        overlayRT.anchorMin = Vector2.zero;
        overlayRT.anchorMax = Vector2.one;
        overlayRT.offsetMin = Vector2.zero;
        overlayRT.offsetMax = Vector2.zero;

        var overlayImg = overlay.GetComponent<Image>();
        overlayImg.color = new Color(0.8f, 0.1f, 0.1f, 0.55f);
        overlayImg.raycastTarget = false;
        overlay.transform.SetAsLastSibling();

        var textGO = new GameObject("OverlayText",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(overlay.transform, false);

        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0f, 0.35f);
        textRT.anchorMax = new Vector2(1f, 0.65f);
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        var tmp = textGO.GetComponent<TextMeshProUGUI>();
        tmp.text = "교체";
        tmp.fontSize = 28;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        state.overlay = overlay;
    }

    private class MulliganState
    {
        public bool isSelected;
        public GameObject overlay;
    }
}
