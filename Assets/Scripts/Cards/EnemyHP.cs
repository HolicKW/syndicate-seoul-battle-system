using System;
using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 적의 HP를 관리하는 컴포넌트.
/// HP 데이터는 EntityState가 소유하며, 이 컴포넌트는 UI 갱신만 담당한다.
/// </summary>
public class EnemyHP : MonoBehaviour
{
    [Header("설정")]
    [SerializeField] private int maxHP = 50;

    [Header("UI")]
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private Image hpFillImage;
    [SerializeField] private float hpFillSmoothDuration = 0.25f;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text shieldText;
    [SerializeField] private GameObject shieldPanel;
    [SerializeField] private SpriteRenderer profileRenderer;

    // EntityState 바인딩 (2단계 리팩터링)
    private EntityState state;
    private bool deathFired;
    private Coroutine hpFillRoutine;

    // -- 하위 호환 프로퍼티 (BattleEngine 도입 전까지 유지) --
    public int CurrentHP => state != null ? state.hp : 0;
    public bool IsDead => state != null ? state.IsDead : false;

    /// <summary>
    /// 적 사망 시 호출되는 이벤트
    /// </summary>
    public event Action OnDeath;

    /// <summary>
    /// HP 변동 시 호출되는 이벤트 (현재HP)
    /// </summary>
    public event Action<int> OnHPChanged;

    void Start()
    {
        if (profileRenderer == null)
            profileRenderer = GetComponent<SpriteRenderer>();

        // EntityState가 아직 바인딩되지 않은 경우 임시 생성 (독립 실행 보호)
        if (state == null)
            BindState(EntityState.Create(maxHP));
    }

    /// <summary>
    /// EntityState를 바인딩한다. BattleEngine이 초기화 시 호출한다.
    /// </summary>
    public void BindState(EntityState entityState)
    {
        state = entityState;
        if (state != null)
            maxHP = state.maxHp;

        OnHPChanged?.Invoke(state.hp);
        UpdateUI(immediateFill: true);
        TryNotifyDeath();
    }

    public bool SetProfileSprite(Sprite sprite)
    {
        if (profileRenderer == null)
            profileRenderer = GetComponent<SpriteRenderer>();

        if (profileRenderer == null || sprite == null)
            return false;

        profileRenderer.sprite = sprite;
        return true;
    }

    public void SetDisplayName(string displayName)
    {
        if (nameText == null || string.IsNullOrWhiteSpace(displayName))
            return;

        nameText.text = displayName;
    }

    /// <summary>
    /// state.hp를 읽어 UI를 갱신한다.
    /// BattleEngine에 의해 EntityState가 직접 변경된 후 호출된다.
    /// </summary>
    public void SyncFromState()
    {
        OnHPChanged?.Invoke(state.hp);
        UpdateUI(immediateFill: false);

        TryNotifyDeath();
    }

    /// <summary>
    /// HP 초기화 (전투 재시작용)
    /// </summary>
    public void ResetHP(int newMaxHP = -1)
    {
        if (newMaxHP > 0)
        {
            maxHP = newMaxHP;
            state.maxHp = newMaxHP;
        }

        state.hp = state.maxHp;
        OnHPChanged?.Invoke(state.hp);
        UpdateUI(immediateFill: true);
    }

    private void UpdateUI(bool immediateFill)
    {
        if (hpText != null)
            hpText.text = $"{state.hp}";

        if (hpFillImage != null)
        {
            float targetFill = GetTargetFillAmount();
            if (immediateFill || hpFillSmoothDuration <= 0f || !gameObject.activeInHierarchy)
            {
                SetFillImmediate(targetFill);
            }
            else if (!Mathf.Approximately(hpFillImage.fillAmount, targetFill))
            {
                if (hpFillRoutine != null)
                    StopCoroutine(hpFillRoutine);

                hpFillRoutine = StartCoroutine(AnimateHpFill(targetFill));
            }
        }

        // 실드 표시는 BattleStatusUI(enemyStatusUI)가 전담.
        // EnemyHP에서 별도로 표시하면 동일 값이 두 번 렌더링됨.
    }

    private float GetTargetFillAmount()
    {
        return state.maxHp > 0 ? Mathf.Clamp01((float)state.hp / state.maxHp) : 0f;
    }

    private void SetFillImmediate(float fillAmount)
    {
        if (hpFillRoutine != null)
        {
            StopCoroutine(hpFillRoutine);
            hpFillRoutine = null;
        }

        hpFillImage.fillAmount = fillAmount;
    }

    private IEnumerator AnimateHpFill(float targetFill)
    {
        float startFill = hpFillImage.fillAmount;
        float elapsed = 0f;

        while (elapsed < hpFillSmoothDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / hpFillSmoothDuration);
            hpFillImage.fillAmount = Mathf.Lerp(startFill, targetFill, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        hpFillImage.fillAmount = targetFill;
        hpFillRoutine = null;
    }

    private void TryNotifyDeath()
    {
        // 사망 감지 (BattleEngine 경로에서도 OnDeath가 발동하도록)
        if (state.IsDead && !deathFired)
        {
            deathFired = true;
            OnDeath?.Invoke();
        }
    }
}
