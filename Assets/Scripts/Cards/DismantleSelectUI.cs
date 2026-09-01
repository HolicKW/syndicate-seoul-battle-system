using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// chosen 모드 해체 시 플레이어가 손패에서 직접 카드를 클릭해 선택하는 모달 UI.
/// 상단 오버레이에 선택 슬롯을 표시하고, 손패(하단)는 그대로 노출해 클릭 가능하게 한다.
/// </summary>
public class DismantleSelectUI : MonoBehaviour
{
    // -- 런타임 상태 --
    private GameObject overlay;
    private readonly List<HandCardUI> availableCardUIs = new List<HandCardUI>();
    private readonly List<HandCardUI> selectedCardUIs = new List<HandCardUI>();
    private readonly List<CardData> cachedResult = new List<CardData>();
    private int requiredCount;
    private int minCount;
    private Button confirmButton;
    private TMP_Text instructionText;
    private Transform selectedSlotArea;
    private string actionLabel = "해체할";
    private bool isDone;

    /// <summary>
    /// 선택 UI를 표시하고 플레이어가 requiredCount장을 선택할 때까지 대기한다.
    /// availableCardUIs: 선택 가능한 손패 HandCardUI 목록 (사용할 카드 제외).
    /// </summary>
    public IEnumerator ShowAndWait(
        List<HandCardUI> availableCardUIs,
        int selectCount,
        Canvas canvas,
        int minimumCount = 0,
        string actionLabel = "해체할")
    {
        isDone = false;
        selectedCardUIs.Clear();
        this.availableCardUIs.Clear();
        this.availableCardUIs.AddRange(availableCardUIs);
        requiredCount = selectCount;
        minCount = minimumCount > 0 ? minimumCount : selectCount;
        this.actionLabel = string.IsNullOrEmpty(actionLabel) ? "해체할" : actionLabel;

        // 손패 카드들을 해체 선택 모드로 전환
        foreach (var cardUI in this.availableCardUIs)
            cardUI.SetDismantleSelectMode(true, OnHandCardClicked);

        BuildUI(selectCount, canvas);

        yield return new WaitUntil(() => isDone);

        // DestroyUI 전에 결과 캐싱 (DestroyUI가 selectedCardUIs를 비우므로)
        cachedResult.Clear();
        foreach (var cardUI in selectedCardUIs)
            cachedResult.Add(cardUI.CardData);

        // 선택 모드 해제
        foreach (var cardUI in this.availableCardUIs)
            cardUI.SetDismantleSelectMode(false);

        DestroyUI();
    }

    /// <summary>플레이어가 선택한 카드 목록을 반환한다. ShowAndWait 완료 후 호출.</summary>
    public List<CardData> GetResult()
    {
        return new List<CardData>(cachedResult);
    }

    // -- UI 구성 --

    private void BuildUI(int selectCount, Canvas canvas)
    {
        // 상단 오버레이 (손패 영역 하단 35%는 제외해 손패가 그대로 보이고 클릭 가능)
        var overlayGO = new GameObject("DismantleOverlay",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlayGO.transform.SetParent(canvas.transform, false);
        overlay = overlayGO;

        var overlayRT = overlayGO.GetComponent<RectTransform>();
        overlayRT.anchorMin = new Vector2(0f, 0.35f);
        overlayRT.anchorMax = Vector2.one;
        overlayRT.offsetMin = Vector2.zero;
        overlayRT.offsetMax = Vector2.zero;
        overlayRT.SetAsLastSibling();

        overlayGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.82f);

        // 안내 텍스트 (오버레이 상단)
        var instrGO = CreateText(overlayGO.transform, "InstructionText",
            anchorMin: new Vector2(0.05f, 0.80f), anchorMax: new Vector2(0.95f, 0.98f));
        instructionText = instrGO.GetComponent<TMP_Text>();
        instructionText.fontSize = 22;
        instructionText.alignment = TextAlignmentOptions.Center;
        instructionText.color = Color.white;
        UpdateInstruction();

        // 선택된 카드 슬롯 영역 (오버레이 중앙)
        var slotAreaGO = new GameObject("SelectedSlotArea", typeof(RectTransform));
        slotAreaGO.transform.SetParent(overlayGO.transform, false);
        var slotRT = slotAreaGO.GetComponent<RectTransform>();
        slotRT.anchorMin = new Vector2(0.05f, 0.28f);
        slotRT.anchorMax = new Vector2(0.95f, 0.78f);
        slotRT.offsetMin = Vector2.zero;
        slotRT.offsetMax = Vector2.zero;

        var hlg = slotAreaGO.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 16;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        selectedSlotArea = slotAreaGO.transform;

        // selectCount만큼 빈 슬롯 생성
        for (int i = 0; i < selectCount; i++)
            CreateSlot(i);

        // 확인 버튼
        var btnGO = new GameObject("ConfirmBtn",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(overlayGO.transform, false);
        var btnRT = btnGO.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0.38f, 0.05f);
        btnRT.anchorMax = new Vector2(0.62f, 0.20f);
        btnRT.offsetMin = Vector2.zero;
        btnRT.offsetMax = Vector2.zero;
        btnGO.GetComponent<Image>().color = new Color(0.15f, 0.55f, 0.15f);

        var btnTxtGO = CreateText(btnGO.transform, "BtnText", Vector2.zero, Vector2.one);
        var btnTxt = btnTxtGO.GetComponent<TMP_Text>();
        btnTxt.text = "확인";
        btnTxt.fontSize = 22;
        btnTxt.alignment = TextAlignmentOptions.Center;
        btnTxt.color = Color.white;

        confirmButton = btnGO.GetComponent<Button>();
        confirmButton.interactable = false;
        confirmButton.onClick.AddListener(OnConfirm);
    }

    private void CreateSlot(int index)
    {
        var slotGO = new GameObject($"Slot_{index}",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        slotGO.transform.SetParent(selectedSlotArea, false);

        var rt = slotGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(110f, 150f);

        slotGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);

        var txtGO = CreateText(slotGO.transform, "SlotLabel",
            new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f));
        var txt = txtGO.GetComponent<TMP_Text>();
        txt.text = "?";
        txt.fontSize = 28;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = new Color(1f, 1f, 1f, 0.25f);
    }

    // -- 이벤트 --

    private void OnHandCardClicked(HandCardUI cardUI)
    {
        if (selectedCardUIs.Contains(cardUI))
        {
            selectedCardUIs.Remove(cardUI);
            cardUI.SetDismantleSelected(false);
        }
        else if (selectedCardUIs.Count < requiredCount)
        {
            selectedCardUIs.Add(cardUI);
            cardUI.SetDismantleSelected(true);
        }

        RefreshSlots();
        UpdateInstruction();

        if (confirmButton != null)
            confirmButton.interactable = (selectedCardUIs.Count >= minCount);
    }

    private void RefreshSlots()
    {
        if (selectedSlotArea == null) return;

        for (int i = 0; i < selectedSlotArea.childCount; i++)
        {
            var slot = selectedSlotArea.GetChild(i);
            var img = slot.GetComponent<Image>();
            var label = slot.GetComponentInChildren<TMP_Text>();

            if (i < selectedCardUIs.Count)
            {
                // 선택된 카드 표시
                img.color = new Color(1f, 0.28f, 0.28f, 0.45f);
                if (label != null)
                {
                    label.text = selectedCardUIs[i].CardData.cardName;
                    label.fontSize = 14;
                    label.color = Color.white;
                }
            }
            else
            {
                // 빈 슬롯
                img.color = new Color(1f, 1f, 1f, 0.08f);
                if (label != null)
                {
                    label.text = "?";
                    label.fontSize = 28;
                    label.color = new Color(1f, 1f, 1f, 0.25f);
                }
            }
        }
    }

    private void OnConfirm()
    {
        CardGameSFXManager.PlayBasicClick();
        isDone = true;
    }

    private void UpdateInstruction()
    {
        if (instructionText == null) return;
        string countDisplay = minCount == requiredCount
            ? $"{requiredCount}장"
            : $"{minCount}~{requiredCount}장";
        instructionText.text =
            $"{actionLabel} 카드를 {countDisplay} 선택하세요.  ({selectedCardUIs.Count}/{requiredCount})\n" +
            "아래 손패에서 카드를 클릭하세요.";
    }

    private void DestroyUI()
    {
        if (overlay != null) Destroy(overlay);
        selectedCardUIs.Clear();
        availableCardUIs.Clear();
    }

    // -- 헬퍼 --

    private static GameObject CreateText(
        Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
    }
}
