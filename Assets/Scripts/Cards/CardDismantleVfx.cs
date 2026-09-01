using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardDismantleVfx : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float duration = 0.85f;
    [SerializeField] private float stagger = 0.06f;

    [Header("Motion")]
    [SerializeField] private float liftDistance = 340f;
    [SerializeField] private float scaleBoost = 0.1f;
    [SerializeField, Range(0.1f, 0.8f)] private float risePortion = 0.4f;

    [Header("Dissolve")]
    [SerializeField, Range(0f, 0.2f)] private float edgeWidth = 0.055f;
    [SerializeField] private Color edgeColor = new Color(0.35f, 0.92f, 1f, 1f);
    [SerializeField] private float noiseScale = 22f;
    [SerializeField] private float shardCount = 18f;
    [SerializeField, Range(0f, 1f)] private float verticalSplitStrength = 0.78f;
    [SerializeField, Range(0f, 1f)] private float upwardBias = 0.62f;

    private const string DissolveShaderName = "UI/Card Dismantle Vertical Split";
    private static Shader dissolveShader;

    private sealed class Target
    {
        public HandCardUI CardUI;
        public RectTransform Rect;
        public CanvasGroup Group;
        public Vector2 BasePosition;
        public Vector3 BaseScale;
        public Quaternion BaseRotation;
        public float Delay;
        public readonly List<GraphicState> Graphics = new List<GraphicState>();
        public readonly List<DissolveTarget> Dissolves = new List<DissolveTarget>();
    }

    private sealed class GraphicState
    {
        public Graphic Graphic;
        public Color OriginalColor;
    }

    private sealed class DissolveTarget
    {
        public Image Image;
        public Material OriginalMaterial;
        public Material RuntimeMaterial;
    }

    public IEnumerator PlayExisting(
        IReadOnlyList<DismantleVfxEvent> events,
        HandUIManager handUIManager,
        Canvas rootCanvas,
        EntityState visibleEntity)
    {
        if (events == null || events.Count == 0 || handUIManager == null || rootCanvas == null)
            yield break;

        var targets = new List<Target>();
        for (int i = 0; i < events.Count; i++)
        {
            DismantleVfxEvent evt = events[i];
            if (evt.Entity != visibleEntity || evt.Source != DismantleVfxSource.Hand)
                continue;

            HandCardUI cardUI = FindCardUI(handUIManager, evt.Card);
            if (cardUI == null)
                continue;

            Target target = PrepareTarget(cardUI, rootCanvas, targets.Count * stagger);
            if (target != null)
                targets.Add(target);
        }

        if (targets.Count == 0)
            yield break;

        ArrangeRemainingHandCards(handUIManager);

        float totalDuration = duration + stagger * (targets.Count - 1);
        float elapsed = 0f;
        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;
            for (int i = 0; i < targets.Count; i++)
                UpdateTarget(targets[i], elapsed);

            yield return null;
        }

        for (int i = 0; i < targets.Count; i++)
            CompleteTarget(targets[i]);
    }

    private Target PrepareTarget(HandCardUI cardUI, Canvas rootCanvas, float delay)
    {
        if (cardUI == null || cardUI.RT == null)
            return null;

        cardUI.SetHovered(false);
        cardUI.SetClickCallback(null);
        cardUI.enabled = false;

        var group = cardUI.GetComponent<CanvasGroup>();
        if (group == null)
            group = cardUI.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        group.alpha = 1f;

        RectTransform rect = cardUI.RT;
        rect.SetParent(rootCanvas.transform, true);
        rect.SetAsLastSibling();

        var target = new Target
        {
            CardUI = cardUI,
            Rect = rect,
            Group = group,
            BasePosition = rect.anchoredPosition,
            BaseScale = rect.localScale,
            BaseRotation = rect.localRotation,
            Delay = delay,
        };

        CaptureGraphics(target);
        AddDissolveTarget(target, cardUI.CardImage);
        AddDissolveTarget(target, cardUI.CardFrameImage);
        return target;
    }

    private static HandCardUI FindCardUI(HandUIManager handUIManager, CardData card)
    {
        if (card == null)
            return null;

        foreach (var cardUI in handUIManager.Cards)
        {
            if (cardUI == null)
                continue;

            if (ReferenceEquals(cardUI.CardData, card))
                return cardUI;
        }

        return null;
    }

    private static void ArrangeRemainingHandCards(HandUIManager handUIManager)
    {
        HandFanLayout fanLayout = null;
        var cards = handUIManager.Cards;
        for (int i = 0; i < cards.Count; i++)
        {
            HandCardUI cardUI = cards[i];
            if (cardUI == null || cardUI.transform.parent == null)
                continue;

            if (fanLayout == null)
                fanLayout = cardUI.transform.parent.GetComponent<HandFanLayout>();

            cardUI.SaveOriginalTransform();
        }

        if (fanLayout != null)
            fanLayout.ArrangeCards();

        for (int i = 0; i < cards.Count; i++)
        {
            HandCardUI cardUI = cards[i];
            if (cardUI != null && cardUI.transform.parent != null)
                cardUI.SaveOriginalTransform();
        }
    }

    private void CaptureGraphics(Target target)
    {
        foreach (var graphic in target.CardUI.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic == null || !graphic.enabled)
                continue;

            graphic.raycastTarget = false;
            target.Graphics.Add(new GraphicState
            {
                Graphic = graphic,
                OriginalColor = graphic.color
            });
        }
    }

    private void AddDissolveTarget(Target target, Image image)
    {
        if (image == null)
            return;

        Shader shader = GetDissolveShader();
        if (shader == null || !shader.isSupported)
            return;

        var material = new Material(shader)
        {
            name = "CardDismantleDissolve_Runtime"
        };
        material.SetFloat("_DissolveAmount", 0f);
        material.SetFloat("_EdgeWidth", edgeWidth);
        material.SetColor("_EdgeColor", edgeColor);
        material.SetFloat("_NoiseScale", Mathf.Max(0.01f, noiseScale));
        material.SetFloat("_ShardCount", Mathf.Max(1f, shardCount));
        material.SetFloat("_VerticalSplitStrength", verticalSplitStrength);
        material.SetFloat("_UpwardBias", upwardBias);

        target.Dissolves.Add(new DissolveTarget
        {
            Image = image,
            OriginalMaterial = image.material,
            RuntimeMaterial = material
        });
        image.material = material;
    }

    private static Shader GetDissolveShader()
    {
        if (dissolveShader == null)
            dissolveShader = Shader.Find(DissolveShaderName);

        return dissolveShader;
    }

    private void UpdateTarget(Target target, float elapsed)
    {
        if (target == null || target.Rect == null)
            return;

        float localTime = elapsed - target.Delay;
        if (localTime < 0f)
        {
            SetAlpha(target, 1f);
            return;
        }

        float t = Mathf.Clamp01(localTime / Mathf.Max(0.01f, duration));
        float riseT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, risePortion, t));
        float dissolveT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(risePortion, 1f, t));
        float lift = Mathf.Lerp(0f, liftDistance, riseT);
        float scale = 1f + Mathf.Sin(riseT * Mathf.PI) * scaleBoost;

        target.Rect.anchoredPosition = target.BasePosition + new Vector2(0f, lift);
        target.Rect.localRotation = Quaternion.Lerp(target.BaseRotation, Quaternion.identity, riseT);
        target.Rect.localScale = target.BaseScale * scale;

        if (t < risePortion)
        {
            SetAlpha(target, 1f);
            SetDissolve(target, 0f);
            return;
        }

        SetAlpha(target, 1f - Mathf.SmoothStep(0.88f, 1f, dissolveT));
        SetDissolve(target, dissolveT);
    }

    private void SetDissolve(Target target, float dissolveAmount)
    {
        for (int i = 0; i < target.Dissolves.Count; i++)
        {
            Material material = target.Dissolves[i].RuntimeMaterial;
            if (material == null)
                continue;

            material.SetFloat("_DissolveAmount", dissolveAmount);
            material.SetFloat("_EdgeWidth", edgeWidth);
            material.SetColor("_EdgeColor", edgeColor);
            material.SetFloat("_NoiseScale", Mathf.Max(0.01f, noiseScale));
            material.SetFloat("_ShardCount", Mathf.Max(1f, shardCount));
            material.SetFloat("_VerticalSplitStrength", verticalSplitStrength);
            material.SetFloat("_UpwardBias", upwardBias);
        }
    }

    private static void SetAlpha(Target target, float alphaScale)
    {
        for (int i = 0; i < target.Graphics.Count; i++)
        {
            GraphicState state = target.Graphics[i];
            if (state.Graphic == null)
                continue;

            Color color = state.OriginalColor;
            color.a *= Mathf.Clamp01(alphaScale);
            state.Graphic.color = color;
        }
    }

    private static void CompleteTarget(Target target)
    {
        if (target == null)
            return;

        for (int i = 0; i < target.Dissolves.Count; i++)
        {
            DissolveTarget dissolve = target.Dissolves[i];
            if (dissolve.Image != null)
                dissolve.Image.material = dissolve.OriginalMaterial;
            if (dissolve.RuntimeMaterial != null)
                Destroy(dissolve.RuntimeMaterial);
        }

        if (target.CardUI != null)
            Destroy(target.CardUI.gameObject);
    }
}
