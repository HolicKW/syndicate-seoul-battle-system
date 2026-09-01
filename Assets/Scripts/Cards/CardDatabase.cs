using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// JSON 파일에서 카드 데이터를 로드하고 관리하는 싱글톤 데이터베이스.
/// Resources/cards.json에서 생산 가능한 일반 카드를, Resources/specialCards.json에서 전투용 특수 카드를 읽어온다.
/// </summary>
public class CardDatabase
{
    private static CardDatabase instance;
    public static CardDatabase Instance
    {
        get
        {
            if (instance == null)
                instance = new CardDatabase();
            return instance;
        }
    }

    /// <summary>
    /// 플레이모드 진입 시 싱글톤을 리셋하여 JSON을 다시 로드한다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        instance = null;
    }

    /// <summary>
    /// JSON 직렬화용 래퍼 클래스
    /// </summary>
    private class CardCollection
    {
        public List<CardData> cards;
    }

    private readonly Dictionary<string, CardData> cardMap = new Dictionary<string, CardData>();
    private readonly Dictionary<string, CardData> specialCardMap = new Dictionary<string, CardData>();
    private readonly List<CardData> allCards = new List<CardData>();
    private readonly List<CardData> specialCards = new List<CardData>();
    private bool isLoaded;

    private CardDatabase()
    {
        Load();
    }

    /// <summary>
    /// Resources/cards.json에서 일반 카드를 로드하고, 있으면 Resources/specialCards.json에서 특수 카드를 추가 로드한다.
    /// </summary>
    public void Load()
    {
        cardMap.Clear();
        specialCardMap.Clear();
        allCards.Clear();
        specialCards.Clear();

        var settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };

        if (!TryLoadCardCollection("cards", settings, required: true, out CardCollection collection))
        {
            isLoaded = false;
            return;
        }

        AddCards(collection.cards, cardMap, allCards, "일반");

        if (TryLoadCardCollection("specialCards", settings, required: false, out CardCollection specialCollection))
            AddCards(specialCollection.cards, specialCardMap, specialCards, "특수");

        isLoaded = true;
        Debug.Log($"[CardDatabase] 일반 {allCards.Count}장, 특수 {specialCards.Count}장의 카드를 로드했습니다.");
    }

    public bool IsLoaded => isLoaded;

    public IReadOnlyList<CardData> GetAll() => allCards;

    public IReadOnlyList<CardData> GetAllSpecial() => specialCards;

    public CardData GetById(string id)
    {
        if (id != null && cardMap.TryGetValue(id, out var card))
            return card;
        if (id != null && specialCardMap.TryGetValue(id, out var specialCard))
            return specialCard;
        return null;
    }

    public List<CardData> GetByType(CardType type)
    {
        return allCards.Where(c => c.type == type).ToList();
    }

    public List<CardData> GetByRarity(CardRarity rarity)
    {
        return allCards.Where(c => c.rarity == rarity).ToList();
    }

    /// <summary>
    /// 특정 키워드를 가진 카드를 필터링
    /// </summary>
    public List<CardData> GetByKeyword(string keyword)
    {
        return allCards.Concat(specialCards).Where(c => c.HasKeyword(keyword)).ToList();
    }

    /// <summary>
    /// 특정 팩의 카드를 필터링
    /// </summary>
    public List<CardData> GetByPack(string pack)
    {
        return allCards.Concat(specialCards).Where(c => c.pack == pack).ToList();
    }

    /// <summary>
    /// 여러 ID로 카드 목록 조회 (덱 구성용)
    /// </summary>
    public List<CardData> GetByIds(IEnumerable<string> ids)
    {
        var result = new List<CardData>();
        foreach (var id in ids)
        {
            var card = GetById(id);
            if (card != null)
                result.Add(card);
            else
                Debug.LogWarning($"[CardDatabase] ID '{id}'에 해당하는 카드를 찾을 수 없습니다.");
        }
        return result;
    }

    private static bool TryLoadCardCollection(string resourcePath, JsonSerializerSettings settings, bool required, out CardCollection collection)
    {
        collection = null;

        var textAsset = Resources.Load<TextAsset>(resourcePath);
        if (textAsset == null)
        {
            if (required)
                Debug.LogError($"[CardDatabase] Resources/{resourcePath}.json 파일을 찾을 수 없습니다.");
            return false;
        }

        try
        {
            collection = JsonConvert.DeserializeObject<CardCollection>(textAsset.text, settings);
        }
        catch (JsonException ex)
        {
            Debug.LogError($"[CardDatabase] Resources/{resourcePath}.json 파싱 실패: {ex.Message}");
            return false;
        }

        if (collection == null || collection.cards == null)
        {
            Debug.LogError($"[CardDatabase] Resources/{resourcePath}.json 구조가 올바르지 않습니다.");
            return false;
        }

        return true;
    }

    private static void AddCards(List<CardData> source, Dictionary<string, CardData> targetMap, List<CardData> targetList, string sourceLabel)
    {
        foreach (var card in source)
        {
            if (string.IsNullOrEmpty(card.id))
            {
                Debug.LogWarning($"[CardDatabase] {sourceLabel} 카드 중 id가 비어있는 카드를 건너뜁니다.");
                continue;
            }

            if (targetMap.ContainsKey(card.id))
                Debug.LogWarning($"[CardDatabase] {sourceLabel} 카드 중복 id '{card.id}' 발견. 나중 항목으로 덮어씁니다.");

            targetMap[card.id] = card;
            targetList.Add(card);
        }
    }

#if UNITY_EDITOR
    public static void ResetInstance()
    {
        instance = null;
    }
#endif
}
