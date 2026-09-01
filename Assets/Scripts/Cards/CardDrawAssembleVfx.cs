using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays a single shader-based dissolve-in (artwork + frame together) on newly drawn hand cards.
/// </summary>
public class CardDrawAssembleVfx : MonoBehaviour
{
    [Header("Frames")]
    [SerializeField] private string frameResourcesPath = "Images/CardUseFrames";
    [SerializeField] private float framesPerSecond = 24f;
    [SerializeField] private float fallbackDuration = 0.28f;

    [Header("Motion")]
    [SerializeField] private float settleDistance = 18f;
    [SerializeField] private float startScaleBoost = 0.035f;

    [Header("Blend")]
    [SerializeField, Range(0f, 1f)] private float minArtworkAlpha = 0.05f;
    [SerializeField, Range(0f, 1f)] private float extraGraphicsRevealStart = 0.18f;
    [SerializeField, Range(0f, 1f)] private float extraGraphicsRevealEnd = 0.72f;

    [Header("Artwork Dissolve")]
    [SerializeField] private bool useArtworkDissolve = true;
    [SerializeField, Range(0f, 1f)] private float artworkDissolveStart = 0f;
    [SerializeField, Range(0f, 1f)] private float artworkDissolveEnd = 1f;
    [SerializeField, Range(0f, 0.2f)] private float artworkDissolveEdgeWidth = 0.045f;
    [SerializeField] private Color artworkDissolveEdgeColor = new Color(0.35f, 0.92f, 1f, 1f);
    [SerializeField] private float artworkDissolveNoiseScale = 18f;
    [SerializeField, Range(0f, 1f)] private float artworkDissolveDirectionBias = 0.72f;

    private const string ArtworkDissolveShaderName = "UI/Card Artwork Dissolve";
    private static Shader artworkDissolveShader;
    private static readonly Dictionary<string, Sprite[]> FrameCache = new Dictionary<string, Sprite[]>(StringComparer.Ordinal);
    private Sprite[] loadedFrames = Array.Empty<Sprite>();

    private sealed class GraphicState
    {
        public Graphic Graphic;
        public Color OriginalColor;
        public bool OriginalEnabled;
    }

    private sealed class DissolveTarget
    {
        public Image Image;
        public Material OriginalMaterial;
        public Material RuntimeMaterial;
    }

    public float EstimatedDuration => GetDuration(loadedFrames);

    public void ConfigureExtraGraphicsReveal(float revealStart, float revealEnd)
    {
        extraGraphicsRevealStart = Mathf.Clamp01(revealStart);
        extraGraphicsRevealEnd = Mathf.Clamp01(Mathf.Max(extraGraphicsRevealStart, revealEnd));
    }

    public void ConfigureForCardPackReveal(float revealStart, float revealEnd)
        => ConfigureForCardPackReveal(revealStart, revealEnd, framesPerSecond, fallbackDuration);

    public void ConfigureForCardPackReveal(float revealStart, float revealEnd, float revealFramesPerSecond, float revealFallbackDuration)
    {
        ConfigureExtraGraphicsReveal(revealStart, revealEnd);
        framesPerSecond = Mathf.Max(1f, revealFramesPerSecond);
        fallbackDuration = Mathf.Max(0.01f, revealFallbackDuration);
        artworkDissolveStart = extraGraphicsRevealStart;
        artworkDissolveEnd = extraGraphicsRevealEnd;
    }

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        loadedFrames = LoadFrameSprites();
    }

    public IEnumerator Play(HandCardUI targetCard, float startDelay = 0f)
    {
        if (targetCard == null)
            yield break;

        RectTransform cardRect = targetCard.RT;
        if (cardRect == null)
            yield break;

        var cardGroup = targetCard.GetComponent<CanvasGroup>();
        if (cardGroup == null)
            cardGroup = targetCard.gameObject.AddComponent<CanvasGroup>();

        bool originalCardEnabled = targetCard.enabled;
        bool originalBlocksRaycasts = cardGroup.blocksRaycasts;
        bool originalInteractable = cardGroup.interactable;
        float originalGroupAlpha = Mathf.Approximately(cardGroup.alpha, 0f) ? 1f : cardGroup.alpha;
        bool wasHiddenForDraw = Mathf.Approximately(cardGroup.alpha, 0f)
            && !originalBlocksRaycasts
            && !originalInteractable;

        targetCard.SetHovered(false);
        targetCard.enabled = false;
        cardGroup.blocksRaycasts = false;
        cardGroup.interactable = false;
        cardGroup.alpha = 1f;

        List<GraphicState> graphicStates = CaptureGraphicStates(targetCard.gameObject);
        Image artworkImage = targetCard.CardImage;
        bool artworkOnlyStart = artworkImage != null && artworkImage.sprite != null;
        // 카드 전체(아트워크 + 테두리)를 하나의 디졸브 셰이더로 등장시킨다.
        // (기존에는 테두리만 cardUseFrame 스프라이트 시퀀스 Animator로 따로 처리했음)
        List<DissolveTarget> dissolveTargets = new List<DissolveTarget>();
        AddDissolveTarget(dissolveTargets, artworkImage);
        AddDissolveTarget(dissolveTargets, targetCard.CardFrameImage);

        // 연출 전, 물리 연출 모션 설정 및 준비

        Vector2 basePosition = cardRect.anchoredPosition;
        Vector3 baseScale = cardRect.localScale;
        Quaternion baseRotation = cardRect.localRotation;
        float duration = GetDuration(loadedFrames);

        cardRect.anchoredPosition = basePosition + new Vector2(0f, settleDistance);
        cardRect.localScale = baseScale * (1f + startScaleBoost);
        cardRect.localRotation = baseRotation;

        if (startDelay > 0f)
        {
            HideGraphicsImmediately(graphicStates);
            yield return new WaitForSeconds(startDelay);
        }

        PrepareInitialGraphics(graphicStates, artworkImage, artworkOnlyStart);

        // 프레임은 별도 스프라이트 애니메이션 없이 위 디졸브 셰이더로 함께 등장시킨다.
        // 잔존 frame Animator가 m_Sprite 를 덮어쓰지 않도록 비활성화.
        if (targetCard.CardFrameImage != null)
        {
            Animator frameAnimator = targetCard.CardFrameImage.GetComponent<Animator>();
            if (frameAnimator != null)
                frameAnimator.enabled = false;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float settleT = Mathf.SmoothStep(0f, 1f, normalizedTime);

            cardRect.anchoredPosition = basePosition + new Vector2(0f, Mathf.Lerp(settleDistance, 0f, settleT));
            cardRect.localScale = baseScale * Mathf.Lerp(1f + startScaleBoost, 1f, settleT);
            cardRect.localRotation = baseRotation;

            UpdateGraphics(graphicStates, artworkImage, artworkOnlyStart, normalizedTime);
            for (int i = 0; i < dissolveTargets.Count; i++)
                UpdateArtworkDissolve(dissolveTargets[i].RuntimeMaterial, normalizedTime);

            yield return null;
        }

        RestoreDissolveTargets(dissolveTargets);

        // 런타임 애니메이터 스프라이트 간섭 방지를 위해 애니메이터를 끄고 알파 복구
        if (targetCard.CardFrameImage != null)
        {
            Animator frameAnimator = targetCard.CardFrameImage.GetComponent<Animator>();
            if (frameAnimator != null)
            {
                frameAnimator.enabled = false;
            }
            // 컬러 알파와 CanvasRenderer 알파를 모두 완전 불투명(1f)으로 확실히 복구
            Color frameColor = targetCard.CardFrameImage.color;
            frameColor.a = 1f;
            targetCard.CardFrameImage.color = frameColor;

            var canvasRenderer = targetCard.CardFrameImage.GetComponent<CanvasRenderer>();
            if (canvasRenderer != null)
            {
                canvasRenderer.SetAlpha(1f);
            }
        }

        RestoreGraphics(graphicStates);

        cardRect.anchoredPosition = basePosition;
        cardRect.localScale = baseScale;
        cardRect.localRotation = baseRotation;
        cardGroup.blocksRaycasts = wasHiddenForDraw || originalBlocksRaycasts;
        cardGroup.interactable = wasHiddenForDraw || originalInteractable;
        cardGroup.alpha = originalGroupAlpha;
        targetCard.enabled = originalCardEnabled;
        targetCard.SaveOriginalTransform();
    }

    /// <summary>
    /// Play의 역재생 — 카드가 서서히 사라지는 소멸 연출.
    /// Reveal 클립을 Speed=-1로 끝 지점부터 역재생하고,
    /// 디졸브와 그래픽 알파를 대칭적으로 페이드아웃합니다.
    /// </summary>
    public IEnumerator PlayHide(HandCardUI targetCard, float startDelay = 0f)
    {
        if (targetCard == null)
            yield break;

        RectTransform cardRect = targetCard.RT;
        if (cardRect == null)
            yield break;

        var cardGroup = targetCard.GetComponent<CanvasGroup>();
        if (cardGroup == null)
            cardGroup = targetCard.gameObject.AddComponent<CanvasGroup>();

        targetCard.SetHovered(false);
        targetCard.enabled = false;
        cardGroup.blocksRaycasts = false;
        cardGroup.interactable = false;

        List<GraphicState> graphicStates = CaptureGraphicStates(targetCard.gameObject);
        Image artworkImage = targetCard.CardImage;
        Material originalArtworkMaterial = artworkImage != null ? artworkImage.material : null;
        Material artworkDissolveMaterial = null;

        if (useArtworkDissolve && artworkImage != null)
        {
            Shader shader = GetArtworkDissolveShader();
            if (shader != null && shader.isSupported)
            {
                artworkDissolveMaterial = new Material(shader)
                {
                    name = "CardArtworkHideDissolve_Runtime"
                };
                artworkDissolveMaterial.SetFloat("_DissolveAmount", 0f);
                artworkDissolveMaterial.SetFloat("_EdgeWidth", artworkDissolveEdgeWidth);
                artworkDissolveMaterial.SetColor("_EdgeColor", artworkDissolveEdgeColor);
                artworkDissolveMaterial.SetFloat("_NoiseScale", Mathf.Max(0.01f, artworkDissolveNoiseScale));
                artworkDissolveMaterial.SetFloat("_RightToLeftBias", artworkDissolveDirectionBias);
                artworkImage.material = artworkDissolveMaterial;
            }
        }

        Vector2 basePosition = cardRect.anchoredPosition;
        Vector3 baseScale = cardRect.localScale;
        Quaternion baseRotation = cardRect.localRotation;
        
        float duration = GetDuration(loadedFrames);

        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        // 카드 프레임의 독립된 Hide 애니메이션 트리거 작동!
        if (targetCard.CardFrameImage != null)
        {
            Animator frameAnimator = targetCard.CardFrameImage.GetComponent<Animator>();
            if (frameAnimator != null)
            {
                frameAnimator.enabled = true; // 안전하게 애니메이터 재활성화
                frameAnimator.SetTrigger("Hide"); // Hide 트리거 날림!
            }
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float t = Mathf.SmoothStep(0f, 1f, normalizedTime);

            // 등장의 역순: 위로 떠오르며 약간 확대
            cardRect.anchoredPosition = basePosition + new Vector2(0f, Mathf.Lerp(0f, settleDistance, t));
            cardRect.localScale = baseScale * Mathf.Lerp(1f, 1f + startScaleBoost, t);
            cardRect.localRotation = baseRotation;

            // 그래픽 페이드아웃
            foreach (var state in graphicStates)
            {
                if (state?.Graphic == null || !state.OriginalEnabled)
                    continue;

                Color color = state.OriginalColor;
                color.a = state.OriginalColor.a * (1f - t);
                state.Graphic.color = color;
            }

            // 일러스트 디졸브 아웃 (0 → 1)
            if (artworkDissolveMaterial != null)
            {
                float dissolveT = Mathf.InverseLerp(artworkDissolveStart, artworkDissolveEnd, normalizedTime);
                dissolveT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(dissolveT));
                artworkDissolveMaterial.SetFloat("_DissolveAmount", dissolveT);
            }

            yield return null;
        }

        if (artworkImage != null)
            artworkImage.material = originalArtworkMaterial;
        if (artworkDissolveMaterial != null)
            Destroy(artworkDissolveMaterial);

        cardGroup.alpha = 0f;
    }

    private static void HideGraphicsImmediately(List<GraphicState> states)
    {
        foreach (var state in states)
        {
            if (state?.Graphic == null)
                continue;

            state.Graphic.enabled = state.OriginalEnabled;

            Color color = state.OriginalColor;
            color.a = 0f;
            state.Graphic.color = color;
        }
    }

    private static List<GraphicState> CaptureGraphicStates(GameObject cardObject)
    {
        var states = new List<GraphicState>();

        foreach (var graphic in cardObject.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic == null)
                continue;

            states.Add(new GraphicState
            {
                Graphic = graphic,
                OriginalColor = graphic.color,
                OriginalEnabled = graphic.enabled
            });
        }

        return states;
    }

    private void PrepareInitialGraphics(List<GraphicState> states, Image artworkImage, bool artworkOnlyStart)
    {
        foreach (var state in states)
        {
            if (state?.Graphic == null)
                continue;

            if (!state.OriginalEnabled)
            {
                state.Graphic.enabled = false;
                continue;
            }

            state.Graphic.enabled = true;

            Color color = state.OriginalColor;
            if (artworkOnlyStart && state.Graphic == artworkImage)
            {
                color.a = state.OriginalColor.a * minArtworkAlpha;
            }
            else
            {
                color.a = 0f;
            }

            state.Graphic.color = color;
        }
    }

    private void UpdateGraphics(List<GraphicState> states, Image artworkImage, bool artworkOnlyStart, float normalizedTime)
    {
        float artworkT = Mathf.SmoothStep(0f, 1f, normalizedTime);
        float extraT = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(extraGraphicsRevealStart, extraGraphicsRevealEnd, normalizedTime));

        foreach (var state in states)
        {
            if (state?.Graphic == null)
                continue;

            if (!state.OriginalEnabled)
            {
                state.Graphic.enabled = false;
                continue;
            }

            state.Graphic.enabled = true;

            Color color = state.OriginalColor;
            if (artworkOnlyStart && state.Graphic == artworkImage)
            {
                color.a = state.OriginalColor.a * Mathf.Lerp(minArtworkAlpha, 1f, artworkT);
            }
            else
            {
                color.a = state.OriginalColor.a * (artworkOnlyStart ? extraT : artworkT);
            }

            state.Graphic.color = color;
        }
    }

    /// <summary>
    /// 주어진 Image 에 등장용 디졸브 셰이더 머티리얼(시작 _DissolveAmount=1, 즉 보이지 않는 상태)을
    /// 적용하고 리스트에 등록한다. 아트워크와 테두리를 동일한 디졸브로 함께 등장시키기 위해 사용한다.
    /// </summary>
    private void AddDissolveTarget(List<DissolveTarget> targets, Image targetImage)
    {
        if (!useArtworkDissolve || targetImage == null)
            return;

        Shader shader = GetArtworkDissolveShader();
        if (shader == null || !shader.isSupported)
            return;

        Material material = new Material(shader)
        {
            name = "CardArtworkAssembleDissolve_Runtime"
        };
        material.SetFloat("_DissolveAmount", 1f);
        material.SetFloat("_EdgeWidth", artworkDissolveEdgeWidth);
        material.SetColor("_EdgeColor", artworkDissolveEdgeColor);
        material.SetFloat("_NoiseScale", Mathf.Max(0.01f, artworkDissolveNoiseScale));
        material.SetFloat("_RightToLeftBias", artworkDissolveDirectionBias);

        targets.Add(new DissolveTarget
        {
            Image = targetImage,
            OriginalMaterial = targetImage.material,
            RuntimeMaterial = material
        });
        targetImage.material = material;
    }

    private static void RestoreDissolveTargets(List<DissolveTarget> targets)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            DissolveTarget target = targets[i];
            if (target.Image != null)
                target.Image.material = target.OriginalMaterial;
            if (target.RuntimeMaterial != null)
                Destroy(target.RuntimeMaterial);
        }
    }

    private static Shader GetArtworkDissolveShader()
    {
        if (artworkDissolveShader == null)
            artworkDissolveShader = Shader.Find(ArtworkDissolveShaderName);

        return artworkDissolveShader;
    }

    private void UpdateArtworkDissolve(Material material, float normalizedTime)
    {
        if (material == null)
            return;

        float dissolveT = Mathf.InverseLerp(artworkDissolveStart, artworkDissolveEnd, normalizedTime);
        dissolveT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(dissolveT));
        material.SetFloat("_DissolveAmount", 1f - dissolveT);
        material.SetFloat("_EdgeWidth", artworkDissolveEdgeWidth);
        material.SetColor("_EdgeColor", artworkDissolveEdgeColor);
        material.SetFloat("_NoiseScale", Mathf.Max(0.01f, artworkDissolveNoiseScale));
        material.SetFloat("_RightToLeftBias", artworkDissolveDirectionBias);
    }

    private static void RestoreGraphics(List<GraphicState> states)
    {
        foreach (var state in states)
        {
            if (state?.Graphic == null)
                continue;

            state.Graphic.enabled = state.OriginalEnabled;
            state.Graphic.color = state.OriginalColor;
        }
    }



    private Sprite[] LoadFrameSprites()
    {
        if (string.IsNullOrWhiteSpace(frameResourcesPath))
            return Array.Empty<Sprite>();

        if (FrameCache.TryGetValue(frameResourcesPath, out Sprite[] cached))
            return cached;

        Sprite[] sprites = Resources.LoadAll<Sprite>(frameResourcesPath);
        Array.Sort(sprites, CompareSpritesByFrameName);
        FrameCache[frameResourcesPath] = sprites;
        return sprites;
    }

    private float GetDuration(Sprite[] frames)
    {
        if (frames == null || frames.Length == 0)
            return fallbackDuration;

        return Mathf.Max((frames.Length - 1) / Mathf.Max(1f, framesPerSecond), fallbackDuration);
    }

    private static int CompareSpritesByFrameName(Sprite left, Sprite right)
        => CompareFrameNames(left != null ? left.name : string.Empty, right != null ? right.name : string.Empty);

    private static int CompareFrameNames(string left, string right)
    {
        int leftNumber = ExtractTrailingNumber(left);
        int rightNumber = ExtractTrailingNumber(right);
        if (leftNumber != rightNumber)
            return leftNumber.CompareTo(rightNumber);

        return string.CompareOrdinal(left, right);
    }

    private static int ExtractTrailingNumber(string name)
    {
        if (string.IsNullOrEmpty(name))
            return int.MinValue;

        int index = name.Length - 1;
        while (index >= 0 && char.IsDigit(name[index]))
            index--;

        if (index == name.Length - 1)
            return int.MinValue;

        string digits = name.Substring(index + 1);
        return int.TryParse(digits, out int value) ? value : int.MinValue;
    }
}
