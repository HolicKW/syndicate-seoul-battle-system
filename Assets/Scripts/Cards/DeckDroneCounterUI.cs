using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DeckDroneCounterUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text countText;
    [SerializeField] private Image animationImage;

    [Header("Animation")]
    [SerializeField] private string animationResourcePath = "Images/CardBackground/deckBuilder";
    [SerializeField] private int animationColumns = 6;
    [SerializeField] private int animationRows = 5;
    [SerializeField] private float framesPerSecond = 6f;
    [SerializeField] private bool pingPongLoop = true;

    [Header("Count Style")]
    [SerializeField] private Color countColor = new Color(0.68f, 0.96f, 1f, 1f);
    [SerializeField] private Color countOutlineColor = new Color(0.02f, 0.22f, 0.35f, 0.95f);
    [SerializeField] private Vector2 countOutlineDistance = new Vector2(1.5f, -1.5f);
    [SerializeField] private float countFontSize = 30f;

    private readonly List<Sprite> animationFrames = new List<Sprite>();
    private Coroutine animationRoutine;

    private void OnValidate()
    {
        TryAutoBind();
        ApplyCountTextStyle();

        if (!Application.isPlaying)
            ShowPreviewFrame();
    }

    private void Awake()
    {
        TryAutoBind();
        ApplyCountTextStyle();
        ShowPreviewFrame();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
            StartAnimation();
    }

    private void OnDisable()
    {
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }
    }

    public void SetCount(int count)
    {
        if (countText != null)
            countText.text = count.ToString();
    }

    private void TryAutoBind()
    {
        if (animationImage == null)
            animationImage = GetComponent<Image>();

        if (countText == null)
            countText = GetComponentInChildren<TMP_Text>(true);
    }

    private void ApplyCountTextStyle()
    {
        if (countText == null)
            return;

        countText.color = countColor;
        countText.fontStyle = FontStyles.Bold;
        countText.alignment = TextAlignmentOptions.Center;
        countText.fontSize = countFontSize;
        countText.enableAutoSizing = true;
        countText.fontSizeMin = 18f;
        countText.fontSizeMax = countFontSize;
        countText.raycastTarget = false;

        var outline = countText.GetComponent<Outline>();
        if (outline == null)
            outline = countText.gameObject.AddComponent<Outline>();

        outline.effectColor = countOutlineColor;
        outline.effectDistance = countOutlineDistance;
        outline.useGraphicAlpha = true;
    }

    private void ShowPreviewFrame()
    {
        EnsureFramesLoaded();

        if (animationImage == null || animationFrames.Count == 0)
            return;

        animationImage.color = Color.white;
        animationImage.preserveAspect = true;
        animationImage.sprite = animationFrames[0];
    }

    private void StartAnimation()
    {
        EnsureFramesLoaded();

        if (animationImage == null || animationFrames.Count == 0)
            return;

        animationImage.color = Color.white;
        animationImage.preserveAspect = true;

        if (animationRoutine == null)
            animationRoutine = StartCoroutine(PlayAnimationLoop());
    }

    private void EnsureFramesLoaded()
    {
        if (animationFrames.Count > 0)
            return;

        var slicedSprites = Resources.LoadAll<Sprite>(animationResourcePath);
        if (slicedSprites != null && slicedSprites.Length > 1)
        {
            System.Array.Sort(slicedSprites, CompareSpriteNames);
            animationFrames.AddRange(slicedSprites);
            return;
        }

        var texture = Resources.Load<Texture2D>(animationResourcePath);
        if (texture == null)
        {
            Debug.LogWarning($"[DeckDroneCounterUI] Resources/{animationResourcePath} animation image was not found.");
            return;
        }

        int columns = Mathf.Max(1, animationColumns);
        int rows = Mathf.Max(1, animationRows);
        float frameWidth = texture.width / (float)columns;
        float frameHeight = texture.height / (float)rows;

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                var rect = new Rect(
                    column * frameWidth,
                    texture.height - ((row + 1) * frameHeight),
                    frameWidth,
                    frameHeight);

                var sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f);
                sprite.name = $"deckBuilder_{row * columns + column}";
                animationFrames.Add(sprite);
            }
        }
    }

    private IEnumerator PlayAnimationLoop()
    {
        var wait = new WaitForSeconds(1f / Mathf.Max(1f, framesPerSecond));
        int index = 0;
        int direction = 1;

        while (animationFrames.Count > 0)
        {
            if (animationImage != null)
                animationImage.sprite = animationFrames[index];

            if (pingPongLoop && animationFrames.Count > 1)
            {
                index += direction;
                if (index >= animationFrames.Count)
                {
                    direction = -1;
                    index = animationFrames.Count - 2;
                }
                else if (index < 0)
                {
                    direction = 1;
                    index = 1;
                }
            }
            else
            {
                index = (index + 1) % animationFrames.Count;
            }

            yield return wait;
        }

        animationRoutine = null;
    }

    private static int CompareSpriteNames(Sprite a, Sprite b)
    {
        int aIndex = ExtractSpriteIndex(a);
        int bIndex = ExtractSpriteIndex(b);
        if (aIndex != bIndex)
            return aIndex.CompareTo(bIndex);

        string aName = a != null ? a.name : string.Empty;
        string bName = b != null ? b.name : string.Empty;
        return string.CompareOrdinal(aName, bName);
    }

    private static int ExtractSpriteIndex(Sprite sprite)
    {
        if (sprite == null || string.IsNullOrEmpty(sprite.name))
            return int.MaxValue;

        int separatorIndex = sprite.name.LastIndexOf('_');
        if (separatorIndex >= 0 && separatorIndex + 1 < sprite.name.Length
            && int.TryParse(sprite.name.Substring(separatorIndex + 1), out int parsedIndex))
        {
            return parsedIndex;
        }

        return int.MaxValue;
    }
}
