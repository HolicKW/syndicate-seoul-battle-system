using UnityEngine;

/// <summary>
/// 전투 중 플레이어/적 스탯을 실시간 조정하는 디버그 패널 (IMGUI).
/// BattleEngine이 존재하는 씬에 아무 GameObject에나 붙이면 동작한다.
/// F1 키로 패널 토글.
/// </summary>
public class DebugStatPanel : MonoBehaviour
{
    private bool _visible = false;
    private Rect _windowRect = new Rect(10, 10, 320, 560);
    private bool _showEnemy = false;

    // 입력 버퍼 (string → int 파싱용)
    private string _hpBuf = "";
    private string _maxHpBuf = "";
    private string _shieldBuf = "";
    private string _energyBuf = "";
    private string _baseEnergyBuf = "";
    private string _strengthBuf = "";
    private string _weaknessBuf = "";
    private string _virusBuf = "";
    private string _corrosionBuf = "";
    private string _ocStacksBuf = "";
    private string _luckBuf = "";

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
            _visible = !_visible;
    }

    void OnGUI()
    {
        if (!_visible) return;
        _windowRect = GUI.Window(0, _windowRect, DrawWindow, "[DEBUG] 스탯 조정 패널  (F1 토글)");
    }

    private void DrawWindow(int id)
    {
        var engine = BattleEngine.Instance;
        if (engine == null)
        {
            GUILayout.Label("BattleEngine 없음");
            GUI.DragWindow();
            return;
        }

        // 대상 선택 탭
        GUILayout.BeginHorizontal();
        GUI.color = !_showEnemy ? Color.cyan : Color.white;
        if (GUILayout.Button("플레이어")) { _showEnemy = false; SyncBuffers(engine.Player); }
        GUI.color = _showEnemy ? Color.cyan : Color.white;
        if (GUILayout.Button("적")) { _showEnemy = true; SyncBuffers(engine.Enemy); }
        GUI.color = Color.white;
        GUILayout.EndHorizontal();

        var entity = _showEnemy ? engine.Enemy : engine.Player;

        GUILayout.Space(4);
        bool changed = false;

        changed |= DrawIntField("HP", ref _hpBuf, ref entity.hp);
        changed |= DrawIntField("최대 HP", ref _maxHpBuf, ref entity.maxHp);
        changed |= DrawIntField("실드", ref _shieldBuf, ref entity.shield);
        changed |= DrawIntField("에너지", ref _energyBuf, ref entity.energy);
        changed |= DrawIntField("기본 에너지", ref _baseEnergyBuf, ref entity.baseEnergy);

        GUILayout.Space(4);
        GUILayout.Label("-- 버프/디버프 --");
        changed |= DrawIntField("힘(Strength)", ref _strengthBuf, ref entity.strength);
        changed |= DrawIntField("약화(Weakness)", ref _weaknessBuf, ref entity.weakness);
        changed |= DrawIntField("바이러스", ref _virusBuf, ref entity.virus);
        changed |= DrawIntField("부식(Corrosion)", ref _corrosionBuf, ref entity.corrosion);

        GUILayout.Space(4);
        GUILayout.Label("-- 기타 --");
        changed |= DrawIntField("오버클럭 스택", ref _ocStacksBuf, ref entity.overclockStacks);
        changed |= DrawIntField("행운(Luck)", ref _luckBuf, ref entity.luck);

        GUILayout.Space(6);

        // 즉시 적용 버튼 행
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("HP 최대치로"))
        {
            entity.hp = entity.maxHp;
            changed = true;
        }
        if (GUILayout.Button("실드 초기화"))
        {
            entity.shield = 0;
            changed = true;
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("버프/디버프 초기화"))
        {
            entity.strength = 0;
            entity.weakness = 0;
            entity.virus = 0;
            entity.corrosion = 0;
            entity.overclockStacks = 0;
            SyncBuffers(entity);
            changed = true;
        }
        GUILayout.EndHorizontal();

        if (changed)
        {
            // hp가 maxHp를 초과하지 않도록 보정
            entity.hp = Mathf.Clamp(entity.hp, 0, entity.maxHp > 0 ? entity.maxHp : 9999);
            engine.NotifyStateChanged();
        }

        GUILayout.Space(4);
        GUILayout.Label($"[현재] HP:{entity.hp}/{entity.maxHp}  실드:{entity.shield}  에너지:{entity.energy}");

        GUI.DragWindow();
    }

    /// <summary>
    /// 레이블 + 텍스트필드 + 입력값 파싱 → ref int에 반영. 변경 여부 반환.
    /// </summary>
    private bool DrawIntField(string label, ref string buf, ref int value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(130));
        string next = GUILayout.TextField(buf, GUILayout.Width(70));
        GUILayout.EndHorizontal();

        if (next != buf)
        {
            buf = next;
            if (int.TryParse(buf, out int parsed) && parsed != value)
            {
                value = parsed;
                return true;
            }
        }
        return false;
    }

    /// <summary>엔티티의 현재 값을 입력 버퍼에 동기화.</summary>
    private void SyncBuffers(EntityState e)
    {
        _hpBuf = e.hp.ToString();
        _maxHpBuf = e.maxHp.ToString();
        _shieldBuf = e.shield.ToString();
        _energyBuf = e.energy.ToString();
        _baseEnergyBuf = e.baseEnergy.ToString();
        _strengthBuf = e.strength.ToString();
        _weaknessBuf = e.weakness.ToString();
        _virusBuf = e.virus.ToString();
        _corrosionBuf = e.corrosion.ToString();
        _ocStacksBuf = e.overclockStacks.ToString();
        _luckBuf = e.luck.ToString();
    }

    void OnEnable()
    {
        // 처음 패널 열 때 버퍼 초기화
        var engine = BattleEngine.Instance;
        if (engine?.Player != null)
            SyncBuffers(engine.Player);
    }
}
