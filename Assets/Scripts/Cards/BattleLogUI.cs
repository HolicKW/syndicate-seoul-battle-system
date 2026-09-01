using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// 전투 로그를 스크롤 가능한 패널에 실시간 표시.
/// 전투 로그 UI.
///
/// 씬 구조:
///   BattleLogPanel (이 컴포넌트)
///   └-- Viewport
///       └-- LogContent (contentContainer에 연결)
///
/// Inspector에서 contentContainer를 연결한다.
/// 카드 이름 마커( §card:id§name§ )를 TMP 링크 태그로 변환하고,
/// 마우스 호버 시 CardTooltipUI를 표시한다.
/// </summary>
public class BattleLogUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("References")]
    [SerializeField] private Transform contentContainer;
    [SerializeField] private ScrollRect scrollRect;

    [Header("토글 버튼")]
    [SerializeField] private Button toggleButton;
    [SerializeField] private TMP_Text toggleButtonText;
    [SerializeField] private TMP_Text titleText;

    [Header("설정")]
    [SerializeField] private int maxEntries = 200;
    [SerializeField] private int fontSize = 13;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private TMP_FontAsset tooltipFont; // 호버 카드 툴팁 전용 폰트(미지정 시 font 사용)
    [SerializeField] private bool startCollapsed = true;
    [SerializeField] private float screenEdgePadding = 8f;

    // 카드 링크 마커 정규식: §card:id§name§  (\u00A7 = §)
    // @"" 리터럴을 피하고 일반 문자열로 유니코드 이스케이프 처리
    private static readonly Regex CardRefRegex =
        new Regex("\u00A7card:([^\u00A7]+)\u00A7([^\u00A7]+)\u00A7", RegexOptions.Compiled);
    private const string PositionPrefPrefix = "BattleLogUI.Position.";

    private readonly List<GameObject> rows = new();
    private bool isPanelVisible = true;
    private string positionPrefKey;

    private RectTransform rectTransform;
    private RectTransform parentRectTransform;
    private Vector2 expandedSizeDelta;
    private Vector2 dragStartPointerLocal;
    private Vector2 dragStartAnchoredPosition;
    private CardTooltipUI tooltip;
    private Camera uiCamera;
    private const float BottomScrollThreshold = 0.01f;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        parentRectTransform = rectTransform != null ? rectTransform.parent as RectTransform : null;
        if (rectTransform != null)
            expandedSizeDelta = rectTransform.sizeDelta;
        positionPrefKey = PositionPrefPrefix + SceneManager.GetActiveScene().name;
        LoadSavedPosition();
        isPanelVisible = !startCollapsed;

        if (titleText == null)
        {
            var title = transform.Find("LogTitle");
            if (title != null)
                titleText = title.GetComponent<TMP_Text>();
        }

        if (toggleButton == null && toggleButtonText != null)
            toggleButton = toggleButtonText.GetComponentInParent<Button>();

        if (toggleButton != null)
        {
            toggleButton.onClick.RemoveListener(TogglePanel);
            toggleButton.onClick.AddListener(TogglePanel);
        }
        ConfigureScrollRect();

        // 루트 Canvas 찾기
        Canvas canvas = null;
        var c = GetComponentInParent<Canvas>();
        if (c != null) canvas = c.rootCanvas;
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();

        uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;

        // CardTooltipUI 생성
        tooltip = gameObject.AddComponent<CardTooltipUI>();
        if (canvas != null)
            tooltip.Init(canvas, tooltipFont != null ? tooltipFont : font);

        // 사이즈 조정(접힘/펼침)은 Start 코루틴에서 레이아웃이 확정된 뒤 처리한다.
        // Awake 시점에는 캔버스 스케일러/레이아웃이 아직 적용되지 않아
        // rectTransform.rect.height가 0이거나 부정확하고, 그 값으로 접힘 높이와
        // 상단 고정 위치를 계산하면 배경 패널이 엉뚱한 크기/위치로 가버린다.
        // 여기서는 사이즈에 의존하지 않는 시각 요소만 먼저 반영한다.
        if (scrollRect != null)
            scrollRect.gameObject.SetActive(isPanelVisible);
        if (toggleButtonText != null)
            toggleButtonText.text = isPanelVisible ? "▶" : "◀";
        if (titleText != null)
            titleText.text = "전투 로그";

        // 로그 전체 텍스트(타이틀/토글)에 로그 폰트를 일괄 적용한다.
        if (font != null)
        {
            if (titleText != null)        titleText.font = font;
            if (toggleButtonText != null) toggleButtonText.font = font;
        }
    }

    private IEnumerator Start()
    {
        // 첫 레이아웃/캔버스 스케일러 적용을 기다린 뒤 접힘 크기를 계산한다.
        yield return null;
        Canvas.ForceUpdateCanvases();
        ApplyPanelVisibility();
    }

    void OnEnable()
    {
        BattleLogger.OnLogAdded += OnLogEntry;
        RebuildAll();
    }

    void OnDisable()
    {
        SavePosition();
        BattleLogger.OnLogAdded -= OnLogEntry;
        tooltip?.Hide();
    }

    /// <summary>
    /// 로그 패널 표시/숨김 토글.
    /// 패널 외부의 토글 버튼 onClick에 연결한다.
    /// </summary>
    public void TogglePanel()
    {
        CardGameSFXManager.PlayBasicClick();
        isPanelVisible = !isPanelVisible;
        ApplyPanelVisibility();
    }

    private void ApplyPanelVisibility()
    {
        if (scrollRect != null)
            scrollRect.gameObject.SetActive(isPanelVisible);

        if (rectTransform != null)
        {
            Vector2 targetSizeDelta = isPanelVisible
                ? expandedSizeDelta
                : new Vector2(expandedSizeDelta.x, GetCollapsedSizeDeltaY());
            SetSizeDeltaKeepingTop(targetSizeDelta);
        }

        if (toggleButtonText != null)
            toggleButtonText.text = isPanelVisible ? "▶" : "◀";

        if (titleText != null)
            titleText.text = "전투 로그";

        ClampToParentBounds();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        BeginPanelDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        DragPanel(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        EndPanelDrag();
    }

    public void BeginPanelDrag(PointerEventData eventData)
    {
        if (rectTransform == null || parentRectTransform == null)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out dragStartPointerLocal);
        dragStartAnchoredPosition = rectTransform.anchoredPosition;
    }

    public void DragPanel(PointerEventData eventData)
    {
        if (rectTransform == null || parentRectTransform == null)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 pointerLocal);

        rectTransform.anchoredPosition = dragStartAnchoredPosition + pointerLocal - dragStartPointerLocal;
    }

    public void EndPanelDrag()
    {
        ClampToParentBounds();
        SavePosition();
    }

    private void ConfigureScrollRect()
    {
        if (scrollRect == null)
            return;

        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = Mathf.Max(scrollRect.scrollSensitivity, 20f);
    }

    private float GetCollapsedSizeDeltaY()
    {
        if (rectTransform == null)
            return expandedSizeDelta.y;

        float expandedHeight = rectTransform.rect.height;
        if (expandedHeight <= 0f)
            return expandedSizeDelta.y;

        return expandedSizeDelta.y - expandedHeight + 30f;
    }

    private void SetSizeDeltaKeepingTop(Vector2 targetSizeDelta)
    {
        if (rectTransform == null)
            return;

        float topY = rectTransform.anchoredPosition.y
            + rectTransform.rect.height * (1f - rectTransform.pivot.y);

        rectTransform.sizeDelta = targetSizeDelta;

        Vector2 position = rectTransform.anchoredPosition;
        position.y = topY - rectTransform.rect.height * (1f - rectTransform.pivot.y);
        rectTransform.anchoredPosition = position;
    }

    private void ClampToParentBounds()
    {
        if (rectTransform == null || parentRectTransform == null)
            return;

        Vector3[] worldCorners = new Vector3[4];
        rectTransform.GetWorldCorners(worldCorners);

        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        for (int i = 0; i < worldCorners.Length; i++)
        {
            Vector3 local = parentRectTransform.InverseTransformPoint(worldCorners[i]);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }

        Rect parentRect = parentRectTransform.rect;
        Vector2 delta = Vector2.zero;

        float leftLimit = parentRect.xMin + screenEdgePadding;
        float rightLimit = parentRect.xMax - screenEdgePadding;
        float bottomLimit = parentRect.yMin + screenEdgePadding;
        float topLimit = parentRect.yMax - screenEdgePadding;

        if (max.y > topLimit)
            delta.y = topLimit - max.y;
        else if (min.y < bottomLimit)
            delta.y = bottomLimit - min.y;

        if (max.x > rightLimit)
            delta.x = rightLimit - max.x;
        else if (min.x < leftLimit)
            delta.x = leftLimit - min.x;

        if (delta != Vector2.zero)
            rectTransform.anchoredPosition += delta;
    }

    private void LoadSavedPosition()
    {
        if (rectTransform == null || string.IsNullOrEmpty(positionPrefKey))
            return;

        string xKey = positionPrefKey + ".x";
        string topYKey = positionPrefKey + ".topY";
        string legacyYKey = positionPrefKey + ".y";
        if (!PlayerPrefs.HasKey(xKey))
            return;

        Vector2 position = rectTransform.anchoredPosition;
        position.x = PlayerPrefs.GetFloat(xKey);

        if (PlayerPrefs.HasKey(topYKey))
        {
            float topY = PlayerPrefs.GetFloat(topYKey);
            position.y = topY - rectTransform.rect.height * (1f - rectTransform.pivot.y);
        }
        else if (PlayerPrefs.HasKey(legacyYKey))
        {
            position.y = PlayerPrefs.GetFloat(legacyYKey);
        }

        rectTransform.anchoredPosition = position;
        ClampToParentBounds();
    }

    private void SavePosition()
    {
        if (rectTransform == null || string.IsNullOrEmpty(positionPrefKey))
            return;

        ClampToParentBounds();

        Vector2 position = rectTransform.anchoredPosition;
        float topY = position.y + rectTransform.rect.height * (1f - rectTransform.pivot.y);

        PlayerPrefs.SetFloat(positionPrefKey + ".x", position.x);
        PlayerPrefs.SetFloat(positionPrefKey + ".topY", topY);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 로그 초기화 (전투 시작 시 호출).
    /// </summary>
    public void ClearLog()
    {
        BattleLogger.Clear();
        foreach (var row in rows)
            Destroy(row);
        rows.Clear();
        ScrollToBottom();
    }

    private void OnLogEntry(BattleLogEntry entry)
    {
        bool shouldScrollToBottom = ShouldAutoScrollToBottom();
        AddRow(entry);

        // 오래된 항목 제거
        while (rows.Count > maxEntries)
        {
            Destroy(rows[0]);
            rows.RemoveAt(0);
        }

        if (shouldScrollToBottom)
            ScrollToBottom();
    }

    private void RebuildAll()
    {
        foreach (var row in rows)
            Destroy(row);
        rows.Clear();

        foreach (var entry in BattleLogger.Entries)
            AddRow(entry);

        ScrollToBottom();
    }

    private void AddRow(BattleLogEntry entry)
    {
        if (contentContainer == null) return;

        var obj = new GameObject("LogRow", typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(contentContainer, false);

        var text = obj.GetComponent<TMP_Text>();
        text.text = FormatEntry(entry);
        text.fontSize = fontSize;
        text.color = GetColor(entry);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TMPro.TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        if (font != null) text.font = font;

        var rt = obj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 0);
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);

        // ContentSizeFitter로 자동 높이
        var fitter = obj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // 카드 링크가 포함된 항목에만 호버 핸들러 추가
        if (HasCardRef(entry.message) && tooltip != null)
        {
            var handler = obj.AddComponent<LogRowLinkHandler>();
            handler.Init(tooltip, uiCamera);
        }

        rows.Add(obj);
    }

    private static string FormatEntry(BattleLogEntry entry)
    {
        string prefix = entry.type switch
        {
            BattleLogType.Info        => "<b>[정보]</b> ",
            BattleLogType.Effect      => "<b>[효과]</b> ",
            BattleLogType.Damage      => "<b>[데미지]</b> ",
            BattleLogType.StateChange => "<b>[상태]</b> ",
            BattleLogType.Warning     => "<b>[경고]</b> ",
            BattleLogType.Gamble      => "<b>[도박]</b> ",
            _ => "",
        };

        // 카드 마커 → TMP 링크 태그 변환 (밑줄 + hover 가능)
        string body = CardRefRegex.Replace(entry.message,
            "<link=\"$1\"><u>$2</u></link>");

        return prefix + body;
    }

    private static bool HasCardRef(string message)
    {
        return message != null && message.Contains('\u00A7');
    }

    private static Color GetColor(BattleLogEntry entry)
    {
        if (entry != null)
        {
            switch (entry.actor)
            {
                case BattleLogActor.Player:
                    return new Color(0.55f, 0.88f, 1f);
                case BattleLogActor.Enemy:
                    return new Color(1f, 0.55f, 0.48f);
            }
        }

        return GetTypeColor(entry != null ? entry.type : BattleLogType.Info);
    }

    private static Color GetTypeColor(BattleLogType type)
    {
        return type switch
        {
            BattleLogType.Info        => new Color(0.85f, 0.85f, 0.85f),   // 밝은 회색
            BattleLogType.Effect      => new Color(0.5f, 0.85f, 1f),       // 하늘색
            BattleLogType.Damage      => new Color(1f, 0.45f, 0.35f),      // 빨간색
            BattleLogType.StateChange => new Color(0.45f, 1f, 0.55f),      // 초록색
            BattleLogType.Warning     => new Color(1f, 0.85f, 0.3f),       // 노란색
            BattleLogType.Gamble      => new Color(1f, 0.75f, 0f),         // 금색
            _ => Color.white,
        };
    }

    private bool ShouldAutoScrollToBottom()
    {
        return scrollRect == null
            || rows.Count == 0
            || !CanScroll()
            || scrollRect.verticalNormalizedPosition <= BottomScrollThreshold
            || !scrollRect.gameObject.activeInHierarchy;
    }

    private bool CanScroll()
    {
        if (scrollRect == null || scrollRect.content == null || scrollRect.viewport == null)
            return false;

        return scrollRect.content.rect.height > scrollRect.viewport.rect.height + 1f;
    }

    private void ScrollToBottom()
    {
        if (scrollRect == null)
            return;

        Canvas.ForceUpdateCanvases();

        if (scrollRect.content != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);

        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }
}
