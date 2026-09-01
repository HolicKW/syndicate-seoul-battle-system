using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 덱을 생성하는 팩토리.
/// BattleInitializer에서 분리된 순수 정적 클래스 - MonoBehaviour/Inspector 의존 없음.
/// </summary>
public static class StarterDeckFactory
{
    [System.Serializable]
    private class StarterDeckEntry { public string id; public int count; }
    [System.Serializable]
    private class StarterDeckJson  { public int maxCards; public StarterDeckEntry[] cards; }

    // ===================================================
    //  Public API
    // ===================================================

    /// <summary>
    /// 카드 ID 목록으로 CardData 리스트를 생성한다. (BattleSceneData.PlayerDeckIds 용)
    /// </summary>
    public static List<CardData> BuildFromIds(List<string> ids)
    {
        var result = new List<CardData>(ids.Count);
        foreach (var id in ids)
        {
            var card = CardDatabase.Instance.GetById(id);
            if (card == null)
            {
                Debug.LogWarning($"[StarterDeckFactory] 덱 빌더: 카드 '{id}' 없음, 건너뜁니다.");
                continue;
            }
            result.Add(card.Clone());
        }
        return result;
    }

    /// <summary>
    /// PlayerDeck이 없거나 비어있을 때 사용하는 기본 덱.
    /// 1) starterDeck.json 우선 로드
    /// 2) 실패 시 하드코딩된 기본 덱 사용
    /// </summary>
    // [임시 테스트] 발버둥 확인용 — 폴백 덱을 최소 장수로 제한한다. 확인 후 원복할 것.
    private const int TestFallbackDeckSize = 10;

    private static List<CardData> TrimForStruggleTest(List<CardData> deck)
    {
        if (deck != null && deck.Count > TestFallbackDeckSize)
            deck.RemoveRange(TestFallbackDeckSize, deck.Count - TestFallbackDeckSize);
        return deck;
    }

    // [임시 테스트] 폴백 덱을 도박(러시안룰렛 팩) 전용으로 구성한다. 도박 연출 확인용.
    // 확인 후 이 블록을 제거하면 기존 starterDeck.json/하드코딩 폴백으로 돌아간다.
    private const int GambleFallbackDeckSize = 20;

    private static List<CardData> BuildGambleOnlyDeck()
    {
        if (CardDatabase.Instance == null || !CardDatabase.Instance.IsLoaded) return null;

        var pool = new List<CardData>();
        foreach (var card in CardDatabase.Instance.GetAll())
            if (card.type != CardType.Core && card.pack == "russian_roulette")
                pool.Add(card);

        if (pool.Count == 0) return null;

        int size = Mathf.Max(Deck.MinDeckSize, GambleFallbackDeckSize);
        var deck = new List<CardData>(size);
        for (int i = 0; i < size; i++)
            deck.Add(pool[i % pool.Count].Clone());

        Debug.Log($"[StarterDeckFactory] [임시] 도박 전용 폴백 덱 구성: {deck.Count}장 (풀 {pool.Count}종)");
        return deck;
    }

    public static List<CardData> BuildDefault()
    {
        var gambleDeck = BuildGambleOnlyDeck();
        if (gambleDeck != null) return gambleDeck;

        var jsonDeck = LoadFromJson();
        if (jsonDeck != null) return TrimForStruggleTest(jsonDeck);

        // (카드 ID, 장수)
        var starterIds = new (string id, int count)[]
        {
            ("DM_001", 1),  // 고철 던지기   (ATK t1 1코, dismantle)
            ("DM_002", 1),  // 맹목적 사격   (ATK t1 1코, extract)
            ("DM_003", 1),  // 분해 타격     (ATK t1 2코, dismantle)
            ("DM_004", 1),  // 스크랩 소드   (ATK t1 1코, extract)
            ("DM_005", 1),  // 불법 복제     (SKL t1 1코, dismantle)
            ("DM_006", 1),  // 데이터 백업   (SKL t1 1코, extract)
            ("DM_007", 1),  // 임시 땜빵     (SKL t1 1코, rebuild)
            ("DM_008", 1),  // 부품 색출     (SKL t1 2코, dismantle)
            ("DM_009", 1),  // 긴급 탈출     (SKL t1 2코)
            ("DM_010", 1),  // 불량 배터리   (SKL t1 0코, dismantle)
            ("DM_011", 1),  // 재활용 프레스 (ATK t2 2코, rebuild)
            ("DM_012", 1),  // 폐기물 폭격   (ATK t2 2코, dismantle)
            ("DM_013", 1),  // 백도어 찌르기 (ATK t2 1코, extract)
            ("DM_014", 1),  // 나노머신 톱   (ATK t2 1코, rebuild)
            ("DM_015", 1),  // 과부하 색출   (SKL t2 1코, dismantle)
            ("DM_016", 1),  // 불연속 회피   (SKL t2 2코, extract)
            ("DM_017", 1),  // 메모리 최적화 (SKL t2 2코, dismantle)
            ("DM_018", 1),  // 부품 조립     (SKL t2 0코)
            ("DM_019", 1),  // 방화벽 재구축 (SKL t2 2코, extract)
            ("DM_021", 1),  // 리사이클 빔   (ATK t3 2코, rebuildAccum)
            ("DM_022", 1),  // 데이터 파쇄   (ATK t3 1코, dismantle)
            ("DM_023", 1),  // 쓰레기통 투척 (ATK t3 2코, dismantle)
            ("DM_024", 1),  // 블랙마켓 거래 (SKL t3 1코, dismantle)
            ("DM_025", 1),  // 무한 동력 수배 (SKL t3 0코)
            ("DM_026", 1),  // 강제 데이터 덤프 (SKL t3 1코, dismantle)
            ("DM_027", 1),  // 예비품 가동   (SKL t3 1코, rebuild)
            ("DM_028", 1),  // 스크랩 실드   (SKL t3 2코, dismantle)
        };

        var result = new List<CardData>();
        foreach (var (id, count) in starterIds)
        {
            var card = CardDatabase.Instance.GetById(id);
            if (card == null)
            {
                Debug.LogWarning($"[StarterDeckFactory] 스타터 카드 '{id}' 없음, 건너뜁니다.");
                continue;
            }
            for (int i = 0; i < count; i++)
                result.Add(card.Clone());
        }

        // 폴백 1: tier 1~2 카드로 채우기
        if (result.Count == 0 && CardDatabase.Instance.IsLoaded)
        {
            Debug.LogWarning("[StarterDeckFactory] 스타터 카드 로드 실패. tier 1~2 카드로 대체합니다.");
            foreach (var card in CardDatabase.Instance.GetAll())
            {
                if (card.tier <= 2 && card.type != CardType.Core && result.Count < 20)
                    result.Add(card.Clone());
            }
        }

        // 폴백 2: 전체 카드에서 20장
        if (result.Count == 0 && CardDatabase.Instance.IsLoaded)
        {
            Debug.LogWarning("[StarterDeckFactory] tier 1~2 카드 없음. 전체 카드에서 20장 사용합니다.");
            int taken = 0;
            foreach (var card in CardDatabase.Instance.GetAll())
            {
                if (card.type != CardType.Core && taken < 20)
                {
                    result.Add(card.Clone());
                    taken++;
                }
            }
        }

        TrimForStruggleTest(result);
        Debug.Log($"[StarterDeckFactory] 스타터 덱 구성 완료: {result.Count}장");
        return result;
    }

    // ===================================================
    //  Private helpers
    // ===================================================

    private static List<CardData> LoadFromJson()
    {
        var asset = Resources.Load<TextAsset>("starterDeck");
        if (asset == null) return null;

        try
        {
            var data = Newtonsoft.Json.JsonConvert.DeserializeObject<StarterDeckJson>(asset.text);
            if (data?.cards == null || data.cards.Length == 0) return null;

            var result = new List<CardData>();
            foreach (var entry in data.cards)
            {
                var card = CardDatabase.Instance.GetById(entry.id);
                if (card == null)
                {
                    Debug.LogWarning($"[StarterDeckFactory] starterDeck.json: 카드 '{entry.id}' 없음, 건너뜁니다.");
                    continue;
                }
                for (int i = 0; i < entry.count; i++)
                    result.Add(card.Clone());
            }

            if (result.Count > 0)
            {
                Debug.Log($"[StarterDeckFactory] starterDeck.json에서 스타터 덱 로드: {result.Count}장");
                return result;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[StarterDeckFactory] starterDeck.json 파싱 실패: {e.Message}");
        }
        return null;
    }
}
