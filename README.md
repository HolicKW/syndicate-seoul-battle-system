# 신디게이트: 서울 — 카드 배틀 시스템

> Unity 6 기반 하이브리드 전략 게임의 전투 엔진 및 전투 UI 설계·구현
> 담당: 함대영 ([@HolicKW](https://github.com/HolicKW))

`C#` `Unity 6000.3.4f1` `URP` `NUnit` — 카드 245장 · 이펙트 핸들러 151종 · 단위 테스트 104개 · 코드 21,800줄

---

## 이 저장소에 대하여

팀 프로젝트 **신디게이트: 서울**에서 제가 단독으로 설계·구현한 **카드 배틀 시스템**을
포트폴리오 용도로 분리한 **코드 열람용 저장소**입니다.

**포함된 것**

| 경로 | 내용 |
|---|---|
| `Assets/Scripts/Battle/` | 전투 엔진 — 이펙트 인터프리터, 턴 매니저, 코어 매니저, 적 AI |
| `Assets/Scripts/Cards/` | 전투 UI/UX — 손패, 툴팁, 멀리건, 로그, VFX |
| `Assets/Scripts/Cards/Editor/` | 단위 테스트 11개 파일 / 104 케이스 |
| `Assets/Scripts/CardData.cs` | 카드 데이터 모델 + 동적 설명문 치환기 |
| `Assets/Resources/cards.json` | 카드 데이터베이스 245장 |

**포함되지 않은 것** — 아트·음원·프리팹·씬 파일, 그리고 제가 담당하지 않은 도시 경영 시스템.
따라서 **이 저장소만으로는 빌드·실행되지 않습니다.** 코드와 설계를 읽기 위한 스냅샷입니다.

**저작권 및 기여 고지**

원본 프로젝트는 4인 팀 저작물입니다. 이 저장소의 파일은 `git blame` 기준으로
제 작성 비율이 99~100%인 것만 선별했습니다. 다음 파일에는 팀원 기여가 일부 포함되어 있습니다.

| 파일 | 팀원 기여 |
|---|---|
| `Assets/Scripts/Cards/BattleInitializer.cs` | kimgyeran 9줄 |
| `Assets/Scripts/CardData.cs` | jjh8998 8줄 |
| `Assets/Scripts/BattleSceneData.cs` | jjh8998 1줄 |

원본 저장소: [jjh8998/NewWorld](https://github.com/jjh8998/NewWorld)

---

## 1. 프로젝트 개요

| 항목 | 내용 |
|---|---|
| 프로젝트명 | 신디게이트: 서울 (내부 리포지토리명 NewWorld) |
| 장르 | 덱빌딩 카드 배틀 + 턴제 도시 경영 하이브리드 |
| 엔진 / 언어 | Unity 6000.3.4f1 (URP), C# |
| 기간 | 2026.02 ~ 2026.06 (약 5개월) |
| 팀 구성 | 4인 (경영 시스템 2인 / 전투 시스템 1인 / 데이터·리서치 1인) |
| 나의 역할 | **카드 배틀 시스템 전담 설계·구현 + 전투 UI/UX** |
| 기여도 | 머지 제외 커밋 104 / 218 (약 48%) |

게임은 두 개의 축으로 구성된다. 플레이어는 도시를 경영해 자원과 카드를 확보하고, 다른 세력과의 분쟁은
카드 배틀로 해소한다. 내가 맡은 부분은 **후자 전체** — 전투 규칙 엔진, 카드 효과 처리기, 적 AI,
그리고 플레이어가 실제로 만지는 전투 화면 전부다.

---

## 2. 담당 범위

### 2.1 전투 엔진 (`Assets/Scripts/Battle/`)

| 모듈 | 역할 |
|---|---|
| `BattleEngine.cs` | 전투 오케스트레이터. 카드 사용 흐름, 데미지·드로우·해체, 프로토콜 판정, 코어 트리거 |
| `EffectInterpreter.cs` (+ 10개 partial 파일) | 문자열 타입 → 핸들러 디스패치. **등록 핸들러 151종** |
| `TurnManager.cs` | 턴 시퀀싱. 에너지 회복, 카운터 초기화, 지속 피해, 지연 실행 큐 소화 |
| `CoreManager.cs` (+ 3개 partial) | 지속 효과(코어) 카드의 트리거 관리 — turnStart / turnEnd / cardPlayed / dismantle / immediate |
| `EntityState.cs` | 플레이어·적이 공유하는 대칭 상태 모델 |
| `EnemyAI.cs` + `CardEvaluator.cs` | Big Five 성격 파라미터 기반 전략 선택 + 카드 기대값 평가 |
| `DeckController.cs` / `CostCalculator.cs` / `ProtocolResolver.cs` | 덱 영역 조작, 코스트 보정, 프로토콜 조건 판정 |

### 2.2 전투 UI/UX (`Assets/Scripts/Cards/`)

손패 부채꼴 레이아웃, 카드 드래그·호버, 멀리건 패널, 키워드 툴팁, 전투 로그,
해체 선택 UI, 상태 아이콘 툴팁, 그리고 5종의 연출 VFX 컴포넌트.

### 2.3 카드 데이터베이스 (`Assets/Resources/cards.json`)

| 지표 | 값 |
|---|---|
| 총 카드 수 | **245장** |
| 카드 팩 | 7종 (base / overclock / dismantle / network / biohazard / russian_roulette / IP) |
| 카드 타입 | Attack 86 · Skill 120 · Core 39 |
| 등급 | Common 44 · Rare 82 · Epic 58 · Unique 40 · Legendary 21 |
| 사용 중인 효과 타입 | **128종** |
| 키워드 | 14종 (해체·바이러스·도박·네트워크·재구축·프로토콜·부식·오버클럭 등) |

---

## 3. 아키텍처 설계

### 3.1 데이터 주도 이펙트 시스템

카드는 코드가 아니라 **JSON 데이터**다. 카드 한 장을 추가할 때 C# 파일을 건드리지 않는 것이 설계 목표였다.

```json
{
  "id": "BASE_002",
  "cardName": "연속 타격",
  "type": "Attack", "rarity": "Common", "tier": 1, "cost": 1,
  "effects": [ { "type": "damage", "value": 3, "hits": 2 } ],
  "description": "적에게 {d:3}의 피해를 2회 줍니다."
}
```

`EffectInterpreter`는 `type` 문자열을 키로 하는 `Dictionary<string, Action<EffectContext>>`
레지스트리다. 실행은 데이터 순회일 뿐이고, 로직은 전부 핸들러 안에 격리된다.

```
CardData.effects (JSON)
        │
        ▼
  ExecuteAll(effects, ctx)   ── 순차 실행, 재귀 깊이 10 제한
        │
        ▼
  handlers[effect.type](ctx) ── 151개 핸들러 중 디스패치
        │
        ├─ 즉시 실행    → EntityState 변경
        └─ 지연 등록    → deferredActions 큐 (turnEnd / nextTurnStart)
```

핸들러는 복잡도에 따라 **4개 티어**로 나누고, 티어와 카드 팩 기준으로 partial class 파일을 분리했다.

```
EffectInterpreter.cs                    594줄  ── 레지스트리 + 공유 헬퍼
├─ .CoreHandlers.cs        Tier 1       456줄  damage / shield / modifyStat
├─ .BasicHandlers.cs       Tier 2       242줄  heal / draw / energy
├─ .OverclockHandlers.cs   Tier 3        78줄
├─ .DismantleHandlers.cs   Tier 3       295줄
├─ .BiohazardHandlers.cs   Tier 3       156줄
├─ .GambleHandlers.cs      Tier 3       148줄
├─ .NetworkHandlers.cs     Tier 3        85줄
├─ .SpecialHandlers.cs     Tier 4       464줄
├─ .DeferredHandlers.cs    지연 실행      66줄
└─ .AliasHandlers.cs       래퍼/별칭     291줄
```

**설계 의도**: 파일 하나가 800줄을 넘지 않고, 새 카드 팩을 추가할 때 기존 파일을 열지 않아도 되게 했다.
바이오해저드 팩 40장을 추가할 때 실제로 `BiohazardHandlers.cs` 하나만 새로 만들었다.

### 3.2 대칭 엔티티 모델

플레이어와 적이 **완전히 같은 `EntityState`를 쓴다.** 적 전용 로직이 존재하지 않는다.

```csharp
public class EntityState
{
    public int hp, maxHp, shield, energy;
    public int strength, weakness, virus, corrosion;
    public List<CardData> drawPile, hand, voidPile, activeCores;
    public EntityState opponent;   // 상호 참조
    public TurnCounters Turn;      // 턴 한정 카운터 (3.3 참조)
}
```

이 결정의 실질적 이득:

- 적 AI가 플레이어와 **동일한 `BattleEngine.PlayCard()`** 를 호출한다. AI용 별도 효과 처리기가 없다.
- `bypassShield`, `retaliate` 같은 효과가 양방향으로 자동 성립한다. 방향별 분기가 필요 없다.
- 개발자 모드에서 **플레이어가 적 진영을 조작하는 방어전**을 추가할 때, 진영 참조만 바꿔 끼웠다.

### 3.3 턴 카운터를 struct로 격리

전투 규칙에는 "이번 턴에만 유효한" 상태가 많다. 이번 턴 자해량, 도박 성공 횟수, 다음 공격 보너스,
관통 플래그… 개발 중반에 이런 필드가 **30개를 넘어섰다.**

**문제**: 새 카운터를 추가할 때마다 `ResetTurnCounters()`에 초기화 한 줄을 추가해야 했고,
그걸 빠뜨리면 *다음 턴까지 버프가 새는* 버그가 났다. 재현이 어렵고 리뷰로도 잘 안 걸린다.

**해결**: 턴 한정 상태를 전부 `TurnCounters` **struct**로 옮기고, 리셋을 한 줄로 만들었다.

```csharp
public struct TurnCounters
{
    public int selfDamageThisTurn, cardsPlayedThisTurn, gambleSuccessThisTurn;
    public int nextAttackBonus, evadeNextHits, retaliateOnHitDamage;
    public bool armorPierceNextAttack, invincibleThisTurn, preventNextSelfDamage;
    // ... 총 30여 개
}

public void ResetTurnCounters() => Turn = default;
```

struct의 `default` 대입은 모든 필드를 0/false로 되돌린다. **필드를 추가하면 리셋이 자동으로 보장된다.**
이후 이 계열 버그가 나오지 않았다.

---

## 4. 기술적 문제 해결

### 4.1 카드 한 장의 버그가 전투 전체를 죽이던 문제

**상황** — 245장 중 한 장의 핸들러에서 `NullReferenceException`이 나면 `PlayCard()` 콜스택 전체가
끊어졌다. 턴이 넘어가지 않고, 입력도 안 먹고, 플레이어는 게임을 재시작하는 수밖에 없었다.
카드 수가 200장을 넘어가면서 이 위험이 실질적으로 커졌다.

**해결** — 디스패치 지점에서 핸들러를 **격리 실행**하고, 실패를 전투 로그로 승격시켰다.

```csharp
if (!handlers.TryGetValue(ctx.Effect.type, out var handler))
{
    Debug.LogWarning($"[EffectInterpreter] 미등록 핸들러: {ctx.Effect.type}");
    BattleLogger.Log(BattleLogType.Warning, $"미등록 핸들러: {ctx.Effect.type}");
    return;
}

try { handler(ctx); }
catch (Exception ex)
{
    Debug.LogError($"[EffectInterpreter] 핸들러 '{ctx.Effect.type}' 실행 중 예외: {ex}");
    BattleLogger.Log(BattleLogType.Warning, $"핸들러 '{ctx.Effect.type}' 예외: {ex.Message}");
}
```

**결과** — 문제 카드는 "효과가 발동하지 않은 카드" 한 장으로 끝나고 전투는 계속된다.
더 중요한 건 진단 쪽이었다. 미등록 핸들러가 인게임 로그에 뜨니까, **기획자가 카드 JSON에 오타를 냈을 때
개발자를 부르지 않고 본인이 확인**할 수 있게 됐다.

> 예외를 삼키는 대신 로그로 승격시킨 이유: 조용한 실패는 밸런싱 중 "이 카드 왜 약하지?" 로 위장한다.
> 플레이어에게 보이는 로그에 남겨야 테스트 플레이 중에 발견된다.

### 4.2 카드가 카드를 부르는 순환 참조

**상황** — 해체(dismantle) 계열 카드는 다른 카드를 파괴하며 그 카드의 효과를 유발한다.
재구축(rebuild)은 파괴된 카드를 되살린다. 이 둘을 조합한 덱에서 **A가 B를 부르고 B가 A를 부르는**
무한 재귀가 발생해 Unity 에디터가 스택 오버플로로 죽었다.

**해결** — 효과 실행 컨텍스트에 깊이를 넣고, 실행 경로 전체에 한도를 걸었다.

```csharp
public void ExecuteAll(List<CardEffect> effects, EffectContext ctx)
{
    ctx.Depth++;
    if (ctx.Depth > 10) { Debug.LogWarning("재귀 깊이 초과. 순환 참조 의심."); ctx.Depth--; return; }
    try {
        foreach (var effect in effects) {
            if (ctx.Engine != null && ctx.Engine.IsBattleEnded) break;   // 전투 종료 시 즉시 중단
            ctx.Effect = effect;
            Execute(ctx);
            if (ctx.AbortRemainingEffects) { ctx.AbortRemainingEffects = false; break; }
        }
    } finally { ctx.Depth--; }
}
```

세 가지를 같이 넣었다.
`Depth` 한도(순환 차단), `IsBattleEnded` 검사(적이 죽은 뒤 남은 효과가 실행되며 나던 버그 차단),
`AbortRemainingEffects` 플래그(조건 분기 효과가 후속 효과를 취소할 수 있게).
적 AI 쪽에도 같은 이유로 턴당 카드 사용 30회 안전 한도를 뒀다.

### 4.3 양쪽 덱이 소진되면 전투가 끝나지 않던 문제

**상황** — 이 게임은 덱을 소모하는 구조라 장기전에서 **양쪽 드로우 파일이 동시에 비는** 상황이 나온다.
그러면 아무도 카드를 못 내고, 데미지가 발생하지 않고, 턴만 무한히 넘어갔다.
승패 판정이 HP 기준이라 전투가 영원히 안 끝났다.

**해결** — 규칙으로 풀었다. 코드에 타임아웃을 거는 대신 **게임 디자인으로 교착을 해소**했다.

드로우할 카드가 없으면 손패에 특수 카드 `STATUS_STRUGGLE`("발버둥")이 들어온다. 이 카드는 아무 효과가
없고, **턴 시작 시 손패에 남아 있으면 최대 체력의 10% 피해**를 준다.

```csharp
private const string StruggleCardId = "STATUS_STRUGGLE";
private const int StrugglePercentDamage = 10;

int damage = Mathf.Max(1, Mathf.RoundToInt(entity.maxHp * (StrugglePercentDamage / 100f)));
```

**결과** — 교착 상태는 **최대 10턴 안에 반드시 승부가 난다.** HP가 높은 쪽이 이기므로 결과도 납득 가능하다.
그리고 이 규칙은 그 자체로 전략이 됐다. "상대 덱을 먼저 태우고 체력을 남긴다"는 덱 아키타입이
의도치 않게 생겨났고, 그대로 유지했다.

> 무한 루프를 `if (turn > 100) return Draw;` 같은 방어 코드로 막을 수도 있었다.
> 규칙으로 푼 쪽을 택한 건, 플레이어가 화면에서 원인을 읽을 수 있어야 한다고 판단해서다.
> 발버둥 카드는 손패에 보이고, 툴팁에 이유가 적혀 있다.

### 4.4 카드 설명과 실제 수치가 어긋나던 문제

**상황** — 카드에 "6의 피해"라고 적혀 있는데, 힘 +5 버프를 받으면 실제로는 11이 나간다.
플레이어는 카드 텍스트를 믿고 계산했다가 매번 틀렸다. 오버클럭 스택·해체 누적처럼
**전투 중 변하는 값에 비례하는 카드**에서 특히 심했다.

**해결** — 설명문에 플레이스홀더 태그를 두고, 표시 시점에 실제 값으로 치환하는 시스템을 만들었다.

```
JSON:  "적에게 {d:6}의 피해를 줍니다."
       "[오버클럭] 적에게 {d:20}+(스택x20)의 피해를 줍니다."
                       ↓  CardData.cs — 표시 직전 해석
화면:  "적에게 11의 피해를 줍니다."          (힘 +5 반영)
       "적에게 80의 피해를 줍니다."          (현재 스택 3 반영)
```

정규식 기반 치환기를 `CardData.cs`에 두고, 힘/약화 보정·스택 곱연산·해체 누적 카운트 등
패턴별로 해석 규칙을 정의했다. 데이터에는 기준값만 적고, **실제 수치는 런타임에 계산된다.**

**검증** — 이 로직은 표시 전용이라 눈으로 잡기 어려워서 단위 테스트로 고정했다
(`CardDataDescriptionTests.cs`, 7개 케이스). 힘 버프 반영, 스택 곱연산, 누적 카운트,
버프가 없을 때 기준값 그대로 표시되는 케이스를 각각 검증한다.

### 4.5 밸런싱 가능한 적 AI

**상황** — 초기 적 AI는 하드코딩된 우선순위였다("공격 카드부터, 없으면 방어"). 적마다 다른 성격을 주려면
분기가 늘어나고, 밸런싱하려면 매번 코드를 고쳐야 했다.

**해결** — 평가와 정책을 분리했다.

- `CardEvaluator` — 카드 한 장을 **10개 축의 기대값**으로 환산한다 (damage, shield, heal, draw,
  statusBuff, statusDebuff, energy, discard, coreLongTerm, unknownValue). 카드 효과 리스트를 재귀
  순회하며 합산하고, 오버클럭 스택 같은 현재 상태도 반영한다.
- `EnemyAI` — 전략별 **가중치 벡터**를 평가값에 곱해 최종 점수를 낸다. 가중치는
  `EnemyAIProfileSO` ScriptableObject로 빼서 **에디터에서 인스펙터로 조정**한다.
- 전략(Aggressive / Defensive / Balanced / Random)은 적 캐릭터의 **Big Five 성격 파라미터**에서
  파생된다. 경영 씬에서 설정된 상대 세력 CEO의 성격이 전투 AI 성향으로 이어진다.

**결과** — AI 밸런싱이 코드 수정에서 **에셋 값 조정**으로 내려왔다. 그리고 두 시스템이 연결되면서,
경영 씬에서 "공격적인 CEO"로 소개된 상대가 전투에서도 실제로 공격적으로 나온다.

---

## 5. UI/UX 구현

### 5.1 손패 부채꼴 레이아웃

하스스톤·슬레이 더 스파이어 계열의 손패 감각을 목표로 `HandFanLayout`을 직접 구현했다.
카드 수에 따라 각도와 간격이 동적으로 조정되고, 가운데가 높고 양끝이 낮은 원호를 그린다.

```csharp
float t = ((float)i / (count - 1)) * 2f - 1f;                    // -1 ~ 0 ~ +1
float y = baseYOffset + (1f - t * t) * arcHeight;                // 포물선 → 중앙이 최고점
float angle = Mathf.Lerp(totalAngle / 2f, -totalAngle / 2f, ...); // 좌우 기울기
rt.SetSiblingIndex(i);                                            // 렌더 순서 = 왼→오
```

전체 각도에 상한(`maxTotalAngle`)을 둬서 카드가 12장 넘게 쌓여도 화면을 벗어나지 않는다.
모든 파라미터는 `[SerializeField]` + `[Tooltip]`으로 노출해 인스펙터에서 감각적으로 조정했다.

### 5.2 키워드 툴팁 자동 생성

14종 키워드(해체·바이러스·프로토콜·오버클럭 등)는 신규 플레이어에게 전부 낯설다.
카드마다 설명을 수동으로 적으면 **키워드 규칙이 바뀔 때 245장을 전부 고쳐야 한다.**

`KeywordTooltipBuilder`는 카드의 `keywords` 배열을 읽어 툴팁 섹션을 자동 생성한다.
키워드 정의는 한 곳에만 있고, 규칙이 바뀌면 여기만 고친다. 9개 단위 테스트로 출력 형식을 고정했다.

### 5.3 조건 충족 시각 피드백

이 게임의 카드는 조건부 효과가 많다("바이러스가 3 이상이면 추가 피해"). 플레이어가 조건 충족 여부를
매번 계산하게 두면 카드를 읽는 비용이 너무 커진다.

`HandCardUI`에 **조건 충족 오라(aura)** 를 넣었다. 현재 상태로 조건이 만족되는 카드는 손패에서
청록색으로 맥동한다. 계산 없이 눈으로 "지금 이 카드가 세다"를 읽을 수 있다.

```csharp
[SerializeField] private Color conditionMetAuraColor = new Color(0.34f, 0.95f, 1f, 0.55f);
[SerializeField] private float conditionMetPulseSpeed = 2.4f;
[SerializeField] private float conditionMetScalePulse = 0.035f;
```

### 5.4 그 외 전투 UI

| 컴포넌트 | 목적 |
|---|---|
| `MulliganUI` (441줄) | 시작 손패 교체. 선택 카드 강조·확정 흐름 |
| `BattleLogUI` (507줄) | 전투 로그. 카드명 클릭 시 해당 카드 상세로 점프 |
| `DismantleSelectUI` | 해체 대상 직접 선택 모드 |
| `AssistStatusBarUI` | 경영 씬의 지원 건물 버프를 전투 화면에 표시 |
| `CardDrawAssembleVfx` 외 4종 | 드로우·사용·해체·도박 결과·턴 전환 연출 |

**연출 원칙** — 모든 VFX는 게임 규칙에 대응한다. 도박 카드가 실패하면 화면에 글리치가 끼고,
해체는 카드가 데이터 조각으로 흩어진다. 사이버펑크 세계관의 "시스템을 해킹한다"는 정체성을
연출 언어로 일관되게 유지하려 했다.

---

## 6. 게임 디자인

전투 시스템 규칙을 직접 설계했다. 코드와 기획이 분리되지 않았던 게 오히려 이 프로젝트의 장점이었다.

### 6.1 6개 카드 팩 = 6개 덱 아키타입

| 팩 | 핵심 메커니즘 | 플레이 감각 |
|---|---|---|
| **BASE** | 기본 공격·방어 | 안정적, 학습용 |
| **오버클럭** | 스택을 쌓아 배율 증폭, 대신 자해 | 고위험 고수익 |
| **해체** | 자기 카드를 파괴해 즉발 이득, 재구축으로 회수 | 자원 순환 |
| **네트워크** | 연속 사용 시 프로토콜 발동 | 콤보 빌드 |
| **바이오해저드** | 바이러스·부식 누적 지속 피해 | 장기전 |
| **러시안 룰렛** | 확률 판정, 행운/불운 스택 | 도박 |

각 팩은 40장씩이고, **팩 안에서 티어 1→5로 올라가며 같은 메커니즘이 심화**된다.
플레이어가 한 팩을 골라 파면 그 축으로 덱이 자연스럽게 수렴하도록 설계했다.

### 6.2 카드 등급 분포

Common 44 / Rare 82 / Epic 58 / Unique 40 / Legendary 21.
Rare를 가장 두껍게 둔 건 의도적이다. 중간 등급이 얇으면 **초반 덱과 완성 덱 사이에 공백**이 생기고,
플레이어가 "언젠가 나올 좋은 카드"만 기다리게 된다. 성장 곡선이 계속 이어지도록 중간을 채웠다.

### 6.3 위험을 감수하는 선택의 설계

오버클럭은 자해하고, 도박은 실패하고, 해체는 자기 카드를 태운다.
세 팩 모두 **이득에 반드시 비용이 붙는다.** 플레이어가 매 턴 "지금 감수할 것인가"를 판단하게 만드는 게
전투 설계의 중심이었다. 발버둥 카드(4.3)도 같은 원리다 — 교착을 처벌이 아니라 **압박**으로 표현했다.

---

## 7. 테스트와 품질 관리

Unity Test Framework(NUnit) 기반 **에디터 모드 단위 테스트 104개**를 작성했다.

| 테스트 파일 | 케이스 | 대상 |
|---|---|---|
| `EffectHandlerTests.cs` | 44 | 이펙트 핸들러 동작 (데미지·실드·조건 분기·스케일링) |
| `TurnManagerTests.cs` | 12 | 턴 시퀀스, 카운터 초기화, 지연 실행 타이밍 |
| `BattleEngineRetaliationTests.cs` | 11 | 반격 처리 |
| `KeywordTooltipBuilderTests.cs` | 9 | 툴팁 출력 형식 |
| `DeckManagerTests.cs` | 8 | 덱 조작·리셔플 |
| `CardDataDescriptionTests.cs` | 7 | 동적 설명문 치환 |
| `CoreManagerTests.cs` | 6 | 코어 트리거 |
| 그 외 4개 파일 | 7 | 카드 DB 로딩, 버프 요약, 덱 감염, 특허 카드 연동 |

**테스트를 어디에 걸었는가** — 커버리지 숫자보다 *어디서 버그가 났었는지*를 기준으로 골랐다.
이펙트 핸들러(카드 수만큼 조합이 폭발), 턴 카운터(리셋 누락 이력), 설명문 치환(눈으로 검증 불가) 세 곳에
집중했다. 반대로 UI 레이아웃 같은 시각 요소는 테스트하지 않고 에디터에서 직접 확인했다.

**코드 리뷰 체크리스트** — 이 프로젝트에서 반복해 나온 실수를 문서화해 `CLAUDE.md`에 규칙으로 남겼다.

- 참조하는 `EntityState` 필드·핸들러 메서드가 실제로 존재하는가
- 별칭 핸들러가 위임 대상의 해당 mode/filter를 실제로 지원하는가
- `ScalingData` source 문자열이 `GetScalingSourceValue()`에 등록되어 있는가
- 지연 실행 이펙트의 timing 문자열이 `TurnManager`의 처리 타이밍과 일치하는가

문자열 키 기반 디스패치는 유연한 대신 **컴파일러가 오타를 못 잡는다.** 그 대가를 체크리스트와
런타임 경고 로그(4.1)로 메웠다.

---

## 8. 회고

### 잘한 결정

**데이터 주도 설계를 초기에 확정한 것.** 카드 245장 중 200장 이상이 C# 코드 수정 없이 JSON만으로 추가됐다.
프로젝트 후반 밸런싱 단계에서 하루에 수십 장의 수치를 조정했는데, 컴파일 대기 없이 돌릴 수 있었다.

**대칭 엔티티 모델.** "적은 특별하지 않다"는 제약을 초기에 걸어둔 덕에, 개발자 모드 방어전 같은
후반 요구사항이 진영 참조 교체만으로 끝났다.

**교착 상태를 규칙으로 푼 것.** 방어 코드 대신 게임 규칙(발버둥)으로 해결하니
버그 수정이 콘텐츠가 됐다.

### 아쉬운 점

**`BattleInitializer.cs`가 1557줄까지 커졌다.** 전투 초기화·UI 생성·멀리건 처리·이벤트 바인딩이
한 클래스에 몰려 있다. 초기에는 "초기화니까 한 곳에"가 자연스러웠는데, 기능이 붙을수록 여기로 모였다.
`EffectInterpreter`처럼 초기에 partial 분리 기준을 정해뒀어야 했다.

**문자열 키 디스패치의 타입 안전성.** 유연성을 얻은 대신 오타를 런타임까지 못 잡는다.
다시 만든다면 효과 타입을 코드 생성으로 상수화하거나, 빌드 시점에 JSON을 검증하는
에디터 스크립트를 먼저 만들었을 것이다.

### 배운 것

가장 크게 남은 건 **"확장을 예상해 구조를 잡는 것"과 "지금 필요 없는 걸 미리 만드는 것"의 차이**다.
이펙트 레지스트리는 카드가 30장일 때 이미 과했지만 245장에서 값을 했다.
반면 초기에 만든 몇몇 추상화는 끝까지 구현체가 하나뿐이었다.
기준은 결국 "이 축으로 반복해서 늘어날 것을 아는가"였다.

---

## 부록: 파일 인덱스

```
Assets/Scripts/Battle/
├── BattleEngine.cs                 614   전투 오케스트레이터
├── TurnManager.cs                  574   턴 시퀀싱
├── EffectInterpreter.cs            594   핸들러 레지스트리 (+ partial 10개)
├── CoreManager.cs                  266   지속 효과 (+ partial 3개)
├── EntityState.cs                  249   대칭 상태 모델
├── CardEvaluator.cs                991   카드 기대값 평가
├── EnemyAI.cs                      332   전략 선택 AI
├── CardPlayProcessor.cs            263   카드 사용 파이프라인
├── DeckController.cs               117   덱 영역 조작
├── CostCalculator.cs               121   코스트 보정
└── ProtocolResolver.cs              72   프로토콜 판정

Assets/Scripts/Cards/
├── HandCardUI.cs                   526   카드 드래그·호버·조건 오라
├── BattleLogUI.cs                  507   전투 로그
├── MulliganUI.cs                   441   멀리건
├── KeywordTooltipBuilder.cs        259   키워드 툴팁 생성
├── HandFanLayout.cs                 71   부채꼴 레이아웃
└── *Vfx.cs (5종)                  1672   전투 연출

Assets/Scripts/Cards/Editor/        단위 테스트 11개 파일 / 104 케이스
Assets/Resources/cards.json         카드 245장
```
