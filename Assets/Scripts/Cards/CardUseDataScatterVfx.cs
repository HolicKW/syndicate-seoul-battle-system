using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays a single shader-based dissolve on top of a cloned card UI (artwork + frame together).
/// The original card is hidden immediately, the clone fades/dissolves out underneath,
/// and the effect commits gameplay at the configured point in the sequence.
/// </summary>
public class CardUseDataScatterVfx : MonoBehaviour
{
    public enum CloneVisibility
    {
        ArtworkOnly,
        ArtworkAndText,
    }

    [Header("Timing")]
    [Tooltip("이 시점(0~1)에서 게임플레이 효과를 커밋한다.")]
    [SerializeField, Range(0f, 1f)] private float commitNormalizedTime = 0.25f;

    [Header("Motion")]
    [SerializeField] private float liftDistance = 18f;
    [SerializeField] private float scaleBoost = 0.035f;

    [Header("Blend")]
    [SerializeField, Range(0f, 1f)] private float sourceFadeStart = 0.08f;
    [SerializeField, Range(0f, 1f)] private float sourceFadeEnd = 0.78f;
    [SerializeField, Range(0f, 1f)] private float minSourceAlpha = 0f;

    [Header("Artwork Dissolve")]
    [SerializeField] private bool useArtworkDissolve = true;
    [SerializeField, Range(0f, 1f)] private float artworkDissolveStart = 0.04f;
    [SerializeField, Range(0f, 1f)] private float artworkDissolveEnd = 1f;
    [SerializeField, Range(0f, 0.2f)] private float artworkDissolveEdgeWidth = 0.045f;
    [SerializeField] private Color artworkDissolveEdgeColor = new Color(0.35f, 0.92f, 1f, 1f);
    [SerializeField] private float artworkDissolveNoiseScale = 18f;
    [SerializeField, Range(0f, 1f)] private float artworkDissolveRightToLeftBias = 0.72f;

    private const string ArtworkDissolveShaderName = "UI/Card Artwork Dissolve";
    private static Shader artworkDissolveShader;


    private sealed class GraphicState
    {
        public Graphic Graphic;
        public Color OriginalColor;
    }

    public IEnumerator Play(HandCardUI sourceCard, CardData card, Canvas rootCanvas, Action onCommit)
    {
        yield return Play(sourceCard, card, rootCanvas, onCommit, CloneVisibility.ArtworkOnly);
    }

    public IEnumerator Play(HandCardUI sourceCard, CardData card, Canvas rootCanvas, Action onCommit, CloneVisibility cloneVisibility)
    {
        if (sourceCard == null)
        {
            onCommit?.Invoke();
            yield break;
        }

        Canvas canvas = rootCanvas != null ? rootCanvas : sourceCard.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            onCommit?.Invoke();
            yield break;
        }

        var sourceGroup = sourceCard.GetComponent<CanvasGroup>();
        if (sourceGroup == null)
            sourceGroup = sourceCard.gameObject.AddComponent<CanvasGroup>();
        sourceGroup.alpha = 0f;
        sourceGroup.blocksRaycasts = false;

        GameObject clone = Instantiate(sourceCard.gameObject, canvas.transform, true);
        clone.name = $"CardUseFrameVfx_{card?.id ?? "Card"}";
        clone.transform.SetAsLastSibling();

        Transform handParent = sourceCard.transform.parent;
        sourceCard.gameObject.SetActive(false);

        var fanLayout = handParent != null ? handParent.GetComponent<HandFanLayout>() : null;
        if (fanLayout != null)
        {
            fanLayout.ArrangeCards();
            foreach (Transform child in handParent)
            {
                var siblingCard = child.GetComponent<HandCardUI>();
                if (siblingCard != null)
                    siblingCard.SaveOriginalTransform();
            }
        }

        var cloneCard = clone.GetComponent<HandCardUI>();
        Image artworkImage = cloneCard != null ? cloneCard.CardImage : null;
        Image frameImage = cloneCard != null ? cloneCard.CardFrameImage : null;
        if (cloneCard != null)
            cloneCard.enabled = false;
        ConfigureCloneGraphics(clone, artworkImage, frameImage, cloneVisibility);

        var cloneRect = clone.GetComponent<RectTransform>();
        if (cloneRect == null)
        {
            onCommit?.Invoke();
            RestoreSourceIfStillAlive(sourceGroup);
            Destroy(clone);
            yield break;
        }

        var cloneGroup = clone.GetComponent<CanvasGroup>();
        if (cloneGroup == null)
            cloneGroup = clone.AddComponent<CanvasGroup>();
        cloneGroup.alpha = 1f;
        cloneGroup.blocksRaycasts = false;
        cloneGroup.interactable = false;

        foreach (var graphic in clone.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;

        List<GraphicState> cloneGraphics = CaptureGraphicStates(clone);
        // 카드 전체(아트워크 + 테두리)를 하나의 디졸브 셰이더로 소멸시킨다.
        // (기존에는 테두리만 cardUseFrame 스프라이트 시퀀스로 따로 처리했음)
        List<Material> dissolveMaterials = new List<Material>();
        AddDissolveMaterial(dissolveMaterials, artworkImage);
        AddDissolveMaterial(dissolveMaterials, frameImage);

        // 소멸 디졸브 재생 시간(초)
        float duration = 1.2f;

        Vector2 basePosition = cloneRect.anchoredPosition;
        Vector3 baseScale = cloneRect.localScale;
        Quaternion baseRotation = cloneRect.localRotation;

        // 프레임은 별도 스프라이트 애니메이션 없이 아래 디졸브 셰이더로 함께 소멸시킨다.
        // 잔존하는 frame Animator가 m_Sprite 를 덮어쓰지 않도록 비활성화.
        Animator frameAnimator = frameImage != null ? frameImage.GetComponent<Animator>() : null;
        if (frameAnimator != null)
            frameAnimator.enabled = false;

        bool committed = false;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);

            if (!committed && normalizedTime >= commitNormalizedTime)
            {
                committed = true;
                onCommit?.Invoke();
            }

            float lift = Mathf.SmoothStep(0f, liftDistance, Mathf.Clamp01(normalizedTime / 0.5f));
            float scalePulse = Mathf.Sin(normalizedTime * Mathf.PI);
            cloneRect.anchoredPosition = basePosition + new Vector2(0f, lift);
            cloneRect.localRotation = Quaternion.Lerp(baseRotation, Quaternion.identity, Mathf.Clamp01(normalizedTime / 0.18f));
            cloneRect.localScale = baseScale * Mathf.Lerp(1f, 1f + scaleBoost, scalePulse);

            UpdateCloneFade(cloneGraphics, normalizedTime, cloneVisibility);
            for (int i = 0; i < dissolveMaterials.Count; i++)
                UpdateArtworkDissolve(dissolveMaterials[i], normalizedTime);

            yield return null;
        }

        if (!committed)
            onCommit?.Invoke();

        RestoreSourceIfStillAlive(sourceGroup);
        for (int i = 0; i < dissolveMaterials.Count; i++)
        {
            if (dissolveMaterials[i] != null)
                Destroy(dissolveMaterials[i]);
        }
        Destroy(clone);
    }

    private List<GraphicState> CaptureGraphicStates(GameObject clone)
    {
        var states = new List<GraphicState>();
        foreach (var graphic in clone.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic == null || !graphic.enabled)
                continue;

            states.Add(new GraphicState
            {
                Graphic = graphic,
                OriginalColor = graphic.color
            });
        }
        return states;
    }

    private static void ConfigureCloneGraphics(GameObject clone, Image artworkImage, Image frameImage, CloneVisibility cloneVisibility)
    {
        foreach (var graphic in clone.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic == null)
                continue;

            bool keepGraphic = graphic == artworkImage || (frameImage != null && graphic == frameImage);
            if (cloneVisibility == CloneVisibility.ArtworkAndText)
                keepGraphic |= graphic is TMP_Text;

            graphic.enabled = keepGraphic;
        }
    }

    private void UpdateCloneFade(List<GraphicState> graphics, float normalizedTime, CloneVisibility cloneVisibility)
    {
        float alphaScale;
        if (cloneVisibility == CloneVisibility.ArtworkOnly)
        {
            alphaScale = 1f - Mathf.SmoothStep(0f, 1f, normalizedTime);
        }
        else
        {
            float fadeT = Mathf.InverseLerp(sourceFadeStart, sourceFadeEnd, normalizedTime);
            alphaScale = Mathf.Lerp(1f, minSourceAlpha, Mathf.SmoothStep(0f, 1f, fadeT));
        }

        foreach (var state in graphics)
        {
            if (state?.Graphic == null)
                continue;

            Color color = state.OriginalColor;
            color.a = state.OriginalColor.a * alphaScale;
            state.Graphic.color = color;
        }
    }

    /// <summary>
    /// 주어진 Image 에 디졸브 셰이더 머티리얼을 적용하고 리스트에 등록한다.
    /// 아트워크와 테두리를 동일한 디졸브로 함께 소멸시키기 위해 사용한다.
    /// </summary>
    private void AddDissolveMaterial(List<Material> materials, Image targetImage)
    {
        if (!useArtworkDissolve || targetImage == null)
            return;

        Shader shader = GetArtworkDissolveShader();
        if (shader == null || !shader.isSupported)
            return;

        Material material = new Material(shader)
        {
            name = "CardArtworkDissolve_Runtime"
        };
        material.SetFloat("_DissolveAmount", 0f);
        material.SetFloat("_EdgeWidth", artworkDissolveEdgeWidth);
        material.SetColor("_EdgeColor", artworkDissolveEdgeColor);
        material.SetFloat("_NoiseScale", Mathf.Max(0.01f, artworkDissolveNoiseScale));
        material.SetFloat("_RightToLeftBias", artworkDissolveRightToLeftBias);
        targetImage.material = material;
        materials.Add(material);
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
        material.SetFloat("_DissolveAmount", dissolveT);
        material.SetFloat("_EdgeWidth", artworkDissolveEdgeWidth);
        material.SetColor("_EdgeColor", artworkDissolveEdgeColor);
        material.SetFloat("_NoiseScale", Mathf.Max(0.01f, artworkDissolveNoiseScale));
        material.SetFloat("_RightToLeftBias", artworkDissolveRightToLeftBias);
    }

    private static void RestoreSourceIfStillAlive(CanvasGroup sourceGroup)
    {
        if (sourceGroup == null)
            return;

        sourceGroup.gameObject.SetActive(true);
        sourceGroup.alpha = 1f;
        sourceGroup.blocksRaycasts = true;
    }
}
