using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 활성 코어 카드 목록을 원형 카드 아트 아이콘 그리드로 표시하는 UI.
///
/// 씬 셋업:
///   CoreList (이 컴포넌트 + 배경 Image)
///   └-- CoreListContent (itemContainer)  ← 런타임에 GridLayout + 스크롤 뷰포트로 구성됨
///
/// 아이콘이 패널보다 많아지면 줄바꿈 후 세로 스크롤로 정리된다.
/// 카드 아트는 런타임 생성된 원형 마스크로 동그랗게 따오고, 외곽에 티어 컬러 링을 두른다.
/// 아이콘 호버 시 CardTooltipUI로 카드 프리뷰를 표시한다.
/// </summary>
public class CoreListUI : MonoBehaviour
{
    [Header("레이아웃")]
    [Tooltip("아이콘을 붙일 부모 Transform")]
    [SerializeField] private Transform itemContainer;

    [Header("아이콘 설정")]
    [SerializeField] private float iconSize = 44f;
    [SerializeField] private int   iconColumns = 5;
    [SerializeField] private float iconSpacing = 4f;
    [SerializeField] private float iconBorder = 3f;   // 티어 컬러 링 두께
    [SerializeField] private float titleHeight = 24f; // 상단 타이틀이 차지하는 높이(스크롤 영역에서 제외)

    [Header("제목 텍스트 (선택)")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private string titleFormat = "코어 ({0})";  // {0} = 개수

    private EntityState state;
    private readonly List<GameObject> rows = new();
    private CardTooltipUI cachedTooltip;
    private bool layoutInitialized;

    private static Sprite circleSprite;

    // -- 바인딩 ------------------------------------------------------

    /// <summary>EntityState를 바인딩한다. BattleEngine 초기화 후 호출.</summary>
    public void BindState(EntityState entityState)
    {
        state = entityState;
        SyncFromState();
    }

    /// <summary>activeCores를 읽어 UI 아이콘을 재생성한다.</summary>
    public void SyncFromState()
    {
        if (state == null) return;
        RebuildRows(state.activeCores);
    }

    // -- 내부 ---------------------------------------------------------

    private void RebuildRows(List<CardData> cores)
    {
        EnsureLayout();

        foreach (var row in rows)
            Destroy(row);
        rows.Clear();

        if (titleText != null)
            titleText.text = string.Format(titleFormat, cores?.Count ?? 0);

        bool hasCores = cores != null && cores.Count > 0;
        gameObject.SetActive(hasCores);
        if (!hasCores) return;

        foreach (var core in cores)
        {
            var icon = CreateIcon(core);
            if (icon != null)
                rows.Add(icon);
        }
    }

    private CardTooltipUI GetTooltip()
    {
        if (cachedTooltip == null)
            cachedTooltip = FindFirstObjectByType<CardTooltipUI>();
        return cachedTooltip;
    }

    // -- 그리드 + 스크롤 셋업(최초 1회) -------------------------------

    private void EnsureLayout()
    {
        if (layoutInitialized || itemContainer == null) return;
        layoutInitialized = true;

        var contentRT = itemContainer as RectTransform ?? itemContainer.GetComponent<RectTransform>();
        var panelRT   = GetComponent<RectTransform>();
        if (contentRT == null || panelRT == null) return;

        // 1) 타이틀 아래 영역을 차지하는 뷰포트(RectMask2D) 생성, content를 그 아래로 이동
        var viewportGO = new GameObject("CoreScrollViewport", typeof(RectTransform), typeof(RectMask2D));
        var viewportRT = viewportGO.GetComponent<RectTransform>();
        viewportRT.SetParent(panelRT, false);
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = new Vector2(0f, 0f);
        viewportRT.offsetMax = new Vector2(0f, -titleHeight);
        viewportRT.SetAsLastSibling();

        contentRT.SetParent(viewportRT, false);
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot     = new Vector2(0.5f, 1f);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta = new Vector2(0f, contentRT.sizeDelta.y);

        // 2) VerticalLayoutGroup → GridLayoutGroup 교체
        // LayoutGroup은 한 오브젝트에 하나만 허용되므로, 지연 Destroy가 아닌
        // DestroyImmediate로 먼저 제거해야 GridLayoutGroup 추가가 성공한다.
        var grid = itemContainer.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            var vlg = itemContainer.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) DestroyImmediate(vlg);
            grid = itemContainer.gameObject.AddComponent<GridLayoutGroup>();
        }
        grid.padding         = new RectOffset(4, 4, 2, 2);
        grid.cellSize        = new Vector2(iconSize, iconSize);
        grid.spacing         = new Vector2(iconSpacing, iconSpacing);
        grid.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis       = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment  = TextAnchor.UpperLeft;
        grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, iconColumns);

        var csf = itemContainer.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = itemContainer.gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // 3) ScrollRect (세로 전용)
        var scroll = GetComponent<ScrollRect>();
        if (scroll == null) scroll = gameObject.AddComponent<ScrollRect>();
        scroll.content          = contentRT;
        scroll.viewport         = viewportRT;
        scroll.horizontal       = false;
        scroll.vertical         = true;
        scroll.movementType     = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 18f;
    }

    // -- 원형 아이콘 생성 --------------------------------------------

    private GameObject CreateIcon(CardData core)
    {
        if (itemContainer == null)
        {
            Debug.LogWarning("[CoreListUI] itemContainer가 연결되지 않았습니다.");
            return null;
        }

        var circle = GetCircleSprite();

        // 루트: 티어 컬러 원(외곽 링) + 호버 감지
        var root = new GameObject("CoreIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.transform.SetParent(itemContainer, false);

        var rootImg = root.GetComponent<Image>();
        rootImg.sprite        = circle;
        rootImg.color         = CardTierColors.GetNameColor(core.tier);
        rootImg.raycastTarget = true;
        rootImg.alphaHitTestMinimumThreshold = 0.5f; // 원 영역만 호버 감지

        // 마스크: 안쪽 원(링 두께만큼 inset) — 자식 아트를 동그랗게 클리핑
        var maskGO = new GameObject("Mask", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
        maskGO.transform.SetParent(root.transform, false);
        var maskRT = maskGO.GetComponent<RectTransform>();
        maskRT.anchorMin = Vector2.zero;
        maskRT.anchorMax = Vector2.one;
        maskRT.offsetMin = new Vector2(iconBorder, iconBorder);
        maskRT.offsetMax = new Vector2(-iconBorder, -iconBorder);

        var maskImg = maskGO.GetComponent<Image>();
        maskImg.sprite        = circle;
        maskImg.raycastTarget = false;
        maskGO.GetComponent<Mask>().showMaskGraphic = false;

        // 아트워크
        var artGO = new GameObject("Art", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        artGO.transform.SetParent(maskGO.transform, false);
        var artRT = artGO.GetComponent<RectTransform>();
        artRT.anchorMin = Vector2.zero;
        artRT.anchorMax = Vector2.one;
        artRT.offsetMin = Vector2.zero;
        artRT.offsetMax = Vector2.zero;

        var artImg = artGO.GetComponent<Image>();
        artImg.raycastTarget = false;
        artImg.preserveAspect = false;

        Sprite art = string.IsNullOrEmpty(core.artworkPath)
            ? null
            : Resources.Load<Sprite>(core.artworkPath);
        if (art != null)
            artImg.sprite = art;
        else
            artImg.color = new Color(0.18f, 0.18f, 0.22f, 1f); // 아트 없으면 단색

        var hover = root.AddComponent<CoreRowHoverHandler>();
        hover.Init(core.id, GetTooltip());

        return root;
    }

    /// <summary>런타임 생성·캐시되는 안티앨리어싱 원형(흰색 알파) 스프라이트.</summary>
    private static Sprite GetCircleSprite()
    {
        if (circleSprite != null) return circleSprite;

        const int S = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };

        float c = (S - 1) * 0.5f;
        float r = c - 1f;
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float dx = x - c, dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(r - d + 0.5f); // 가장자리 1px AA
                px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();

        circleSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return circleSprite;
    }
}
