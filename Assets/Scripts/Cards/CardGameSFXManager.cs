using UnityEngine;

/// <summary>
/// CardGame 씬 전용 SFX 매니저.
/// 모든 전투/카드 관련 효과음을 한 곳에서 설정하고 재생한다.
/// </summary>
public class CardGameSFXManager : MonoBehaviour
{
    public static CardGameSFXManager Instance { get; private set; }

    [Header("Basic Click")]
    [SerializeField] private AudioClip basicClickClip;
    [SerializeField, Range(0f, 1f)] private float basicClickVolume = 1f;

    [Header("Card Grab")]
    [SerializeField] private AudioClip cardGrabClip;
    [SerializeField, Range(0f, 1f)] private float cardGrabVolume = 1f;

    [Header("Card Use")]
    [SerializeField] private AudioClip cardUseClip;
    [SerializeField, Range(0f, 1f)] private float cardUseVolume = 0.328f;

    [Header("Mulligan Card Click")]
    [SerializeField] private AudioClip mulliganCardClickClip;
    [SerializeField, Range(0f, 1f)] private float mulliganCardClickVolume = 1f;

    [Header("Mulligan Confirm")]
    [SerializeField] private AudioClip mulliganConfirmClip;
    [SerializeField, Range(0f, 1f)] private float mulliganConfirmVolume = 0.3f;

    [Header("Banner")]
    [SerializeField] private AudioClip bannerClip;
    [SerializeField, Range(0f, 1f)] private float bannerVolume = 1f;

    [Header("Shield Gain")]
    [SerializeField] private AudioClip shieldGainClip;
    [SerializeField, Range(0f, 1f)] private float shieldGainVolume = 1f;

    [Header("Damage Hit")]
    [SerializeField] private AudioClip damageHitClip;
    [SerializeField, Range(0f, 1f)] private float damageHitVolume = 0.5f;

    [Header("Heavy Damage Hit")]
    [SerializeField] private AudioClip heavyDamageHitClip;
    [SerializeField, Range(0f, 1f)] private float heavyDamageHitVolume = 0.6f;

    [Header("Gamble Result (비우면 기존 클립으로 폴백)")]
    [SerializeField] private AudioClip gambleSuccessClip;
    [SerializeField, Range(0f, 1f)] private float gambleSuccessVolume = 0.9f;
    [SerializeField] private AudioClip gambleFailClip;
    [SerializeField, Range(0f, 1f)] private float gambleFailVolume = 0.7f;

    private const int HeavyDamageThreshold = 100;

    private AudioSource audioSource;

    private const string BasicClickPath = "Audio/SFX/CardGame/UI/\uAE30\uBCF8 \uD074\uB9AD\uC74C";
    private const string CardGrabPath = "Audio/SFX/CardGame/Card/\uCE74\uB4DC \uC7A1\uC744\uC2DC";
    private const string CardUsePath = "Audio/SFX/CardGame/Card/\uCE74\uB4DC \uC0AC\uC6A9";
    private const string MulliganCardClickPath = "Audio/SFX/CardGame/Card/\uCE74\uB4DC \uD074\uB9AD\uC74C";
    private const string MulliganConfirmPath = "Audio/SFX/CardGame/UI/\uD655\uC778 \uBC84\uD2BC \uD074\uB9AD";
    private const string BannerPath = "Audio/SFX/CardGame/UI/\uBC30\uB108 \uCD9C\uB825";
    private const string ShieldGainPath = "Audio/SFX/CardGame/Combat/\uC2E4\uB4DC \uD68D\uB4DD\uC2DC sfx";
    private const string DamageHitPath = "Audio/SFX/CardGame/Combat/\uD0C0\uACA9\uC74C";
    private const string HeavyDamageHitPath = "Audio/SFX/CardGame/Combat/\uAC15\uD55C\uD0C0\uACA9\uC74C";
    private const string GambleSuccessPath = "Audio/SFX/CardGame/Combat/\uB3C4\uBC15 \uC131\uACF5";
    private const string GambleFailPath = "Audio/SFX/CardGame/Combat/\uB3C4\uBC15 \uC2E4\uD328";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.loop = false;
        audioSource.playOnAwake = false;
        SoundManager.GetOrCreate().ApplySfxSource(audioSource);

        LoadFallbackClipsIfNeeded();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static void PlayBasicClick()
    {
        var manager = GetInstance();
        manager?.Play(manager.basicClickClip, manager.basicClickVolume);
    }

    public static void PlayCardGrab()
    {
        var manager = GetInstance();
        manager?.Play(manager.cardGrabClip, manager.cardGrabVolume);
    }

    public static void PlayCardUse()
    {
        var manager = GetInstance();
        manager?.Play(manager.cardUseClip, manager.cardUseVolume);
    }

    public static void PlayMulliganCardClick()
    {
        var manager = GetInstance();
        manager?.Play(manager.mulliganCardClickClip, manager.mulliganCardClickVolume);
    }

    public static void PlayMulliganConfirm()
    {
        var manager = GetInstance();
        manager?.Play(manager.mulliganConfirmClip, manager.mulliganConfirmVolume);
    }

    public static void PlayBanner()
    {
        var manager = GetInstance();
        manager?.Play(manager.bannerClip, manager.bannerVolume);
    }

    public static void PlayShieldGain()
    {
        var manager = GetInstance();
        manager?.Play(manager.shieldGainClip, manager.shieldGainVolume);
    }

    public static void PlayDamageHit(int damage)
    {
        var manager = GetInstance();
        if (manager == null) return;

        if (damage >= HeavyDamageThreshold)
            manager.Play(manager.heavyDamageHitClip, manager.heavyDamageHitVolume);
        else
            manager.Play(manager.damageHitClip, manager.damageHitVolume);
    }

    public static void PlayGambleSuccess()
    {
        var manager = GetInstance();
        if (manager == null) return;
        // 전용 클립이 없으면 실드 획득음으로 폴백(밝은 톤).
        AudioClip clip = manager.gambleSuccessClip != null ? manager.gambleSuccessClip : manager.shieldGainClip;
        manager.Play(clip, manager.gambleSuccessVolume);
    }

    public static void PlayGambleFail()
    {
        var manager = GetInstance();
        if (manager == null) return;
        // 전용 클립이 없으면 중립 UI음으로 폴백. (타격음으로 폴백하면 피해 없는
        // 도박 실패에도 타격음이 나서 오해를 주므로 사용하지 않는다.)
        AudioClip clip = manager.gambleFailClip != null ? manager.gambleFailClip : manager.basicClickClip;
        if (clip == null) return;
        manager.Play(clip, manager.gambleFailVolume);
    }

    private static CardGameSFXManager GetInstance()
    {
        if (Instance != null)
            return Instance;

        Instance = FindFirstObjectByType<CardGameSFXManager>();
        return Instance;
    }

    private void Play(AudioClip clip, float volume)
    {
        if (clip == null)
            return;

        SoundManager manager = SoundManager.GetOrCreate();
        if (manager != null)
        {
            manager.PlaySfx(clip, volume);
            return;
        }

        if (audioSource != null)
            audioSource.PlayOneShot(clip, volume);
    }

    private void LoadFallbackClipsIfNeeded()
    {
        if (basicClickClip == null)
            basicClickClip = Resources.Load<AudioClip>(BasicClickPath);
        if (cardGrabClip == null)
            cardGrabClip = Resources.Load<AudioClip>(CardGrabPath);
        if (cardUseClip == null)
            cardUseClip = Resources.Load<AudioClip>(CardUsePath);
        if (mulliganCardClickClip == null)
            mulliganCardClickClip = Resources.Load<AudioClip>(MulliganCardClickPath);
        if (mulliganConfirmClip == null)
            mulliganConfirmClip = Resources.Load<AudioClip>(MulliganConfirmPath);
        if (bannerClip == null)
            bannerClip = Resources.Load<AudioClip>(BannerPath);
        if (shieldGainClip == null)
            shieldGainClip = Resources.Load<AudioClip>(ShieldGainPath);
        if (damageHitClip == null)
            damageHitClip = Resources.Load<AudioClip>(DamageHitPath);
        if (heavyDamageHitClip == null)
            heavyDamageHitClip = Resources.Load<AudioClip>(HeavyDamageHitPath);
        if (gambleSuccessClip == null)
            gambleSuccessClip = Resources.Load<AudioClip>(GambleSuccessPath);
        if (gambleFailClip == null)
            gambleFailClip = Resources.Load<AudioClip>(GambleFailPath);
    }
}
