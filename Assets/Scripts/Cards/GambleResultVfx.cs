using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도박 카드 결과를 화면 중앙 홀로 패널로 연출한다. 사이버네틱 톤의
/// "확률 연산(PROBABILITY OVERRIDE)" 통합 연출 — 모든 도박 핸들러 공통.
///
/// UIGlitchOverlay 셰이더를 재사용해 글리치를 표현하며, 패널/텍스트/머티리얼은
/// 미지정 시 런타임에 생성한다(코드 폴백). 빠른 카드 템포를 고려해 전체
/// 시퀀스를 0.5초 내외로 압축했다. 모든 타이밍은 인스펙터에서 조정 가능.
///
/// BattleInitializer가 BattleEngine.OnGambleResult를 구독해 Play()를 호출한다.
/// </summary>
public class GambleResultVfx : MonoBehaviour
{
    [Header("Timing (초)")]
    [SerializeField] private float calcDuration = 0.30f;   // 연산(스크램블)
    [SerializeField] private float lockDuration = 0.07f;   // 확정 플래시
    [SerializeField] private float revealDuration = 0.45f; // 성공/실패 표시
    [SerializeField] private float fadeDuration = 0.14f;   // 소멸
    [SerializeField] private float rerollHold = 0.22f;     // 재굴림 정지(긴장)

    [Header("Color")]
    [SerializeField] private Color successColor = new Color(0.10f, 0.95f, 1f);
    [SerializeField] private Color failColor = new Color(1f, 0.25f, 0.30f);
    [SerializeField] private Color savedColor = new Color(1f, 0.82f, 0.25f);
    [SerializeField] private Color diceColor = new Color(0.65f, 0.85f, 1f);

    [Header("Layout")]
    [SerializeField] private Vector2 panelSize = new Vector2(760f, 240f);
    [SerializeField] private float failShakeMagnitude = 14f;

    private RectTransform root;
    private CanvasGroup canvasGroup;
    private Image background;
    private Image accentTop;
    private Image accentBottom;
    private Image glitchOverlay;
    private Material glitchMaterial;
    private TMP_Text label;
    private TMP_Text subLabel;

    private Coroutine playRoutine;
    private static readonly int GlitchStrengthId = Shader.PropertyToID("_GlitchStrength");

    /// <summary>
    /// 도박 결과 연출을 재생한다. 재생 중이면 기존 연출을 중단하고 최신 결과를 표시한다
    /// (러시안룰렛 등 다중 굴림에서 연출이 쌓이지 않도록).
    /// </summary>
    public void Play(GambleResultInfo info, Canvas canvas)
    {
        if (canvas == null) return;
        EnsureHierarchy(canvas);
        if (root == null) return;

        if (playRoutine != null) StopCoroutine(playRoutine);
        playRoutine = StartCoroutine(PlayRoutine(info));
    }

    private IEnumerator PlayRoutine(GambleResultInfo info)
    {
        bool failLike = !info.Success && !info.IsDice && info.Phase != GamblePhase.Softened;
        Color resultColor = info.IsDice ? diceColor
            : info.Success ? successColor
            : info.Phase == GamblePhase.Softened ? savedColor : failColor;

        root.gameObject.SetActive(true);
        root.SetAsLastSibling();
        root.anchoredPosition = Vector2.zero;
        root.localScale = Vector3.one;
        canvasGroup.alpha = 1f;

        // 재굴림: 짧은 적색 글리치 버스트 + "RE-CALC" 후 본 시퀀스로.
        if (info.Phase == GamblePhase.Reroll)
            yield return RerollIntro();

        // --- 1. 연산: 스케일 팝업 + 16진수 스크램블 + 글리치 ---
        SetAccent(new Color(0.6f, 0.7f, 0.8f));
        label.color = Color.white;
        label.text = "CALCULATING";
        float calcT = 0f;
        while (calcT < calcDuration)
        {
            calcT += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(calcT / calcDuration);
            root.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, k);
            SetGlitch(0.45f);
            subLabel.text = Scramble(info.ChancePct);
            yield return null;
        }

        // --- 2. 확정: 글리치 스파이크 ---
        root.localScale = Vector3.one;
        SetGlitch(0.9f);
        yield return WaitUnscaled(lockDuration);

        // --- 3. 표시: 결과 확정 ---
        if (info.IsDice) CardGameSFXManager.PlayGambleSuccess(); // 중립 효과음
        else if (info.Success) CardGameSFXManager.PlayGambleSuccess();
        else CardGameSFXManager.PlayGambleFail();

        SetAccent(resultColor);
        label.color = resultColor;
        label.text = ResultLabel(info);
        subLabel.color = resultColor;
        subLabel.text = info.IsDice ? "DICE ROLL" : $"{info.ChancePct:F0}%   LUCK {info.Luck}";

        float revealT = 0f;
        while (revealT < revealDuration)
        {
            revealT += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(revealT / revealDuration);

            if (!failLike)
            {
                // 성공: 약한 잔여 글리치 + 살짝 솟구치는 스케일 팝.
                SetGlitch(Mathf.Lerp(0.15f, 0f, k));
                float pop = 1f + 0.08f * Mathf.Sin(k * Mathf.PI);
                root.localScale = Vector3.one * pop;
            }
            else
            {
                // 실패: 강한 글리치 감쇠 + 좌우 셰이크.
                SetGlitch(Mathf.Lerp(0.8f, 0f, k));
                float shake = failShakeMagnitude * (1f - k);
                root.anchoredPosition = new Vector2(Random.Range(-shake, shake), Random.Range(-shake, shake) * 0.4f);
            }
            yield return null;
        }

        root.localScale = Vector3.one;
        root.anchoredPosition = Vector2.zero;
        SetGlitch(0f);

        // --- 4. 소멸 ---
        float fadeT = 0f;
        while (fadeT < fadeDuration)
        {
            fadeT += Time.unscaledDeltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(fadeT / fadeDuration);
            yield return null;
        }

        Hide();
        playRoutine = null;
    }

    private IEnumerator RerollIntro()
    {
        SetAccent(failColor);
        label.color = failColor;
        subLabel.color = failColor;
        subLabel.text = "OVERRIDE";
        float t = 0f;
        while (t < rerollHold)
        {
            t += Time.unscaledDeltaTime;
            // 점멸하는 RE-CALC.
            label.text = Mathf.FloorToInt(t * 18f) % 2 == 0 ? "RE-CALC" : "";
            SetGlitch(0.85f);
            yield return null;
        }
    }

    private static string ResultLabel(GambleResultInfo info)
    {
        if (info.IsDice) return $"⚀ {info.DiceValue}"; // 주사위 눈금(1~6)
        if (info.Success) return "SYNC OK";
        return info.Phase == GamblePhase.Softened ? "SAVED" : "ERROR";
    }

    private readonly StringBuilder scrambleBuilder = new StringBuilder(24);
    private string Scramble(float chancePct)
    {
        scrambleBuilder.Clear();
        for (int i = 0; i < 3; i++)
        {
            scrambleBuilder.Append("0x");
            scrambleBuilder.Append(Random.Range(0, 256).ToString("X2"));
            scrambleBuilder.Append(' ');
        }
        return scrambleBuilder.ToString();
    }

    private void SetGlitch(float strength)
    {
        if (glitchMaterial != null)
            glitchMaterial.SetFloat(GlitchStrengthId, Mathf.Clamp01(strength));
    }

    private void SetAccent(Color c)
    {
        if (accentTop != null) accentTop.color = c;
        if (accentBottom != null) accentBottom.color = c;
    }

    private void Hide()
    {
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (root != null) root.gameObject.SetActive(false);
    }

    private IEnumerator WaitUnscaled(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    // ===================================================
    //  런타임 계층 생성 (코드 폴백)
    // ===================================================

    private void EnsureHierarchy(Canvas canvas)
    {
        if (root != null) return;

        var go = new GameObject("GambleResultVfx_Panel",
            typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(canvas.transform, false);

        root = go.GetComponent<RectTransform>();
        Center(root, panelSize);
        canvasGroup = go.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        background = CreateImage("BG", root, new Color(0.02f, 0.04f, 0.07f, 0.78f));
        Stretch(background.rectTransform);

        accentTop = CreateImage("AccentTop", root, Color.white);
        AnchorBar(accentTop.rectTransform, true);
        accentBottom = CreateImage("AccentBottom", root, Color.white);
        AnchorBar(accentBottom.rectTransform, false);

        glitchOverlay = CreateImage("Glitch", root, Color.white);
        Stretch(glitchOverlay.rectTransform);
        glitchOverlay.raycastTarget = false;
        glitchMaterial = BuildGlitchMaterial();
        if (glitchMaterial != null) glitchOverlay.material = glitchMaterial;

        label = CreateText("Label", root, 84f, new Vector2(0f, 22f));
        subLabel = CreateText("Sub", root, 34f, new Vector2(0f, -64f));

        Hide();
    }

    private Material BuildGlitchMaterial()
    {
        Shader sh = Shader.Find("UI/Glitch Overlay");
        if (sh == null)
        {
            Debug.LogWarning("[GambleResultVfx] 'UI/Glitch Overlay' 셰이더를 찾지 못했습니다. 글리치 없이 진행합니다.");
            return null;
        }
        var mat = new Material(sh);
        mat.SetFloat(GlitchStrengthId, 0f);
        return mat;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TMP_Text CreateText(string name, Transform parent, float fontSize, Vector2 anchoredPos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = false;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.raycastTarget = false;
        tmp.text = string.Empty;
        var rt = tmp.rectTransform;
        Stretch(rt);
        rt.anchoredPosition = anchoredPos;
        return tmp;
    }

    private static void Center(RectTransform rt, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void AnchorBar(RectTransform rt, bool top)
    {
        rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
        rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
        rt.sizeDelta = new Vector2(0f, 6f);
        rt.anchoredPosition = Vector2.zero;
    }
}
