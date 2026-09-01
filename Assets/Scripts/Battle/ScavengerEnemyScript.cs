using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Early-game scavenger enemy setup.
/// Provides a rough tier-1 deck while delegating card choice and play order to EnemyAI.
/// </summary>
public class ScavengerEnemyScript : MonoBehaviour
{
    [SerializeField] private int enemyMaxHp = 30;

    [SerializeField] private List<string> scavengerDeckIds = new List<string>
    {
        "DM_004",
        "DM_004",
        "DM_004",
        "BASE_003",
        "BASE_003",
        "BIO_003",
        "BIO_003",
        "BASE_005",
        "BASE_005",
        "DM_006",
        "DM_006",
        "BASE_007",
        "BASE_008",
        "DM_010",
        "DM_004",
        "BASE_003",
        "BIO_003",
        "BASE_005",
        "DM_006",
        "BASE_007",
    };

    [SerializeField] private BigFivePersonality scavengerPersonality = new BigFivePersonality
    {
        openness = 40,
        conscientiousness = 30,
        extraversion = 75,
        agreeableness = 20,
        neuroticism = 55,
    };

    [SerializeField] private EnemyAIProfileSO aiProfile;

    public int EnemyMaxHp => Mathf.Max(1, enemyMaxHp);

    public List<string> GetDeckIds()
    {
        return new List<string>(scavengerDeckIds);
    }

    public List<CardData> BuildDeck()
    {
        List<CardData> source = CardDatabase.Instance.GetByIds(scavengerDeckIds);
        var deck = new List<CardData>(source.Count);
        foreach (CardData card in source)
        {
            if (card != null)
                deck.Add(card.Clone());
        }

        return deck;
    }

    public void ConfigureEnemyAI(EnemyAI enemyAI)
    {
        if (enemyAI == null)
            return;

        enemyAI.InitializePersonality(scavengerPersonality);
        enemyAI.InitializeWeights(aiProfile);
    }

    public int PlayTurn(EntityState scavenger, EntityState player)
    {
        EnemyAI enemyAI = EnemyAI.Instance;
        ConfigureEnemyAI(enemyAI);
        return enemyAI != null ? enemyAI.PlayTurn(scavenger, player) : 0;
    }
}
