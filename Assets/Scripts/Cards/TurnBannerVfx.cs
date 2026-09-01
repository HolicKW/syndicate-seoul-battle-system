using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays turn-start banner frames using a persistent hierarchy object when available.
/// The assigned banner RectTransform is left user-editable in the scene hierarchy.
/// </summary>
public class TurnBannerVfx : MonoBehaviour
{
    [SerializeField] private string resourceFolder = "Images/CardBackground/banner";
    [SerializeField] private string enemyResourceFolder = "Images/CardBackground/banner2";
    [SerializeField] private float framesPerSecond = 24f;
    [SerializeField] private float holdDuration = 0.5f;
    [SerializeField] private float fadeOutDuration = 0.3f;

    [Header("Hierarchy Banner")]
    [SerializeField] private RectTransform bannerRoot;
    [SerializeField] private Image bannerImage;
    [SerializeField] private CanvasGroup bannerCanvasGroup;
    [SerializeField] private bool createRuntimeFallback = true;

    private Sprite[] playerFrames;
    private Sprite[] enemyFrames;
    private readonly Queue<BannerRequest> pendingRequests = new Queue<BannerRequest>();
    private Coroutine queueRoutine;

    public bool IsPlaying { get; private set; }

    private struct BannerRequest
    {
        public Canvas Canvas;
        public bool IsPlayerTurn;
    }

    private void Reset()
    {
        TryAutoBind();
        HidePersistentBanner();
    }

    private void OnValidate()
    {
        TryAutoBind();
        if (!Application.isPlaying)
            HidePersistentBanner();
    }

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        TryAutoBind();
        playerFrames = LoadFrames(resourceFolder, "Images/CardBackground/banner");
        enemyFrames = LoadFrames(enemyResourceFolder, "Images/CardBackground/banner2");
        HidePersistentBanner();
    }

    private void OnDisable()
    {
        pendingRequests.Clear();
        queueRoutine = null;
        IsPlaying = false;
        HidePersistentBanner();
    }

    private Sprite[] LoadFrames(string configuredPath, string fallbackPath)
    {
        string resourcePath = string.IsNullOrWhiteSpace(configuredPath)
            || configuredPath == "Images/CardBackground"
            || configuredPath == "Images/banner"
            || configuredPath == "Images/banner2"
            ? fallbackPath
            : configuredPath;

        var loadedFrames = Resources.LoadAll<Sprite>(resourcePath);
        if (loadedFrames != null && loadedFrames.Length > 0)
        {
            Array.Sort(loadedFrames, CompareFrames);
            return loadedFrames;
        }

        var singleFrame = Resources.Load<Sprite>(resourcePath);
        if (singleFrame != null)
            return new[] { singleFrame };

        Debug.LogWarning($"[TurnBannerVfx] Resources/{resourcePath} sprites were not found.");
        return Array.Empty<Sprite>();
    }

    public IEnumerator Play(Canvas canvas, bool isPlayerTurn)
    {
        Sprite[] frames = isPlayerTurn ? playerFrames : enemyFrames;
        if (frames == null || frames.Length == 0)
            yield break;

        bool temporaryBanner = false;
        ResolveBanner(canvas, ref temporaryBanner);
        if (bannerRoot == null || bannerImage == null || bannerCanvasGroup == null)
            yield break;

        CardGameSFXManager.PlayBanner();

        bannerRoot.gameObject.SetActive(true);
        bannerRoot.SetAsLastSibling();

        bannerImage.enabled = true;
        bannerImage.raycastTarget = false;
        bannerImage.preserveAspect = true;
        bannerImage.sprite = frames[0];

        bannerCanvasGroup.blocksRaycasts = false;
        bannerCanvasGroup.interactable = false;
        bannerCanvasGroup.alpha = 1f;

        float frameDuration = 1f / Mathf.Max(1f, framesPerSecond);
        for (int i = 0; i < frames.Length; i++)
        {
            bannerImage.sprite = frames[i];
            if (i < frames.Length - 1)
                yield return new WaitForSeconds(frameDuration);
        }

        yield return new WaitForSeconds(holdDuration);

        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            bannerCanvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeOutDuration);
            yield return null;
        }

        if (temporaryBanner && bannerRoot != null)
        {
            var temporary = bannerRoot.gameObject;
            bannerRoot = null;
            bannerImage = null;
            bannerCanvasGroup = null;
            Destroy(temporary);
            yield break;
        }

        HidePersistentBanner();
    }

    public void QueuePlay(Canvas canvas, bool isPlayerTurn)
    {
        pendingRequests.Clear();

        if (queueRoutine != null)
        {
            StopCoroutine(queueRoutine);
            queueRoutine = null;
        }

        HidePersistentBanner();
        IsPlaying = true;

        pendingRequests.Enqueue(new BannerRequest
        {
            Canvas = canvas,
            IsPlayerTurn = isPlayerTurn
        });

        if (isActiveAndEnabled)
        {
            queueRoutine = StartCoroutine(ProcessQueue());
        }
        else
        {
            IsPlaying = false;
        }
    }

    public float GetPlaybackDuration(bool isPlayerTurn)
    {
        Sprite[] frames = isPlayerTurn ? playerFrames : enemyFrames;
        int frameCount = frames != null ? frames.Length : 0;

        float frameDuration = 1f / Mathf.Max(1f, framesPerSecond);
        float animationDuration = frameCount > 1
            ? (frameCount - 1) * frameDuration
            : 0f;

        return animationDuration + holdDuration + fadeOutDuration;
    }

    public IEnumerator Play(Canvas canvas)
    {
        return Play(canvas, true);
    }

    private IEnumerator ProcessQueue()
    {
        while (pendingRequests.Count > 0)
        {
            BannerRequest request = pendingRequests.Dequeue();
            yield return Play(request.Canvas, request.IsPlayerTurn);
        }

        IsPlaying = false;
        queueRoutine = null;
    }

    private void ResolveBanner(Canvas canvas, ref bool temporaryBanner)
    {
        TryAutoBind();
        if (bannerRoot != null && bannerImage != null && bannerCanvasGroup != null)
            return;

        if (!createRuntimeFallback || canvas == null)
            return;

        temporaryBanner = true;

        var go = new GameObject("TurnBanner_Runtime", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        go.transform.SetParent(canvas.transform, false);

        bannerRoot = go.GetComponent<RectTransform>();
        bannerRoot.anchorMin = new Vector2(0.5f, 0.5f);
        bannerRoot.anchorMax = new Vector2(0.5f, 0.5f);
        bannerRoot.pivot = new Vector2(0.5f, 0.5f);
        bannerRoot.anchoredPosition = Vector2.zero;
        bannerRoot.sizeDelta = new Vector2(1400f, 788f);

        bannerImage = go.GetComponent<Image>();
        bannerCanvasGroup = go.GetComponent<CanvasGroup>();
    }

    private void TryAutoBind()
    {
        if (bannerRoot == null)
        {
            var child = transform.Find("TurnBanner");
            if (child is RectTransform rectTransform)
                bannerRoot = rectTransform;
        }

        if (bannerRoot == null)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                var canvasChild = canvas.transform.Find("TurnBanner");
                if (canvasChild is RectTransform canvasRectTransform)
                    bannerRoot = canvasRectTransform;
            }
        }

        if (bannerRoot == null)
            return;

        if (bannerImage == null)
            bannerImage = bannerRoot.GetComponent<Image>();
        if (bannerCanvasGroup == null)
            bannerCanvasGroup = bannerRoot.GetComponent<CanvasGroup>();
    }

    private void HidePersistentBanner()
    {
        if (bannerImage != null)
        {
            bannerImage.sprite = null;
            bannerImage.enabled = false;
            bannerImage.raycastTarget = false;
        }

        if (bannerCanvasGroup != null)
        {
            bannerCanvasGroup.alpha = 0f;
            bannerCanvasGroup.blocksRaycasts = false;
            bannerCanvasGroup.interactable = false;
        }
    }

    private static int CompareFrames(Sprite a, Sprite b)
    {
        int aIndex = ExtractFrameIndex(a);
        int bIndex = ExtractFrameIndex(b);
        if (aIndex != bIndex)
            return aIndex.CompareTo(bIndex);

        string aName = a != null ? a.name : string.Empty;
        string bName = b != null ? b.name : string.Empty;
        return string.CompareOrdinal(aName, bName);
    }

    private static int ExtractFrameIndex(Sprite sprite)
    {
        if (sprite == null || string.IsNullOrEmpty(sprite.name))
            return int.MaxValue;

        int separatorIndex = sprite.name.LastIndexOf('_');
        if (separatorIndex >= 0 && separatorIndex + 1 < sprite.name.Length)
        {
            if (int.TryParse(sprite.name.Substring(separatorIndex + 1), out int parsedIndex))
                return parsedIndex;
        }

        return int.MaxValue;
    }
}
