using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class PatentCardLinkTests
{
    [SetUp]
    public void SetUp()
    {
        CardDatabase.ResetInstance();
    }

    [TearDown]
    public void TearDown()
    {
        CardDatabase.ResetInstance();
    }

    private static ResearchDatabaseSO LoadResearchDatabase()
    {
        ResearchDatabaseSO database = ScriptableObject.CreateInstance<ResearchDatabaseSO>();
        database.LoadCSV();
        Assert.IsNotEmpty(database.allResearches, "연구 CSV 로드 실패");
        return database;
    }

    [Test]
    public void EveryPatentResearch_HasGrantCardId_ThatExistsInCardDatabase()
    {
        ResearchDatabaseSO database = LoadResearchDatabase();
        CardDatabase cardDatabase = CardDatabase.Instance;

        int patentCount = 0;
        foreach (ResearchData research in database.allResearches)
        {
            if (research == null || !research.isPatentResearch)
                continue;

            patentCount++;
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(research.grantCardId),
                $"특허 연구 {research.id}({research.name})에 GrantCardID가 없습니다.");

            CardData card = cardDatabase.GetById(research.grantCardId);
            Assert.IsNotNull(
                card,
                $"특허 연구 {research.id}의 GrantCardID '{research.grantCardId}'가 카드 DB에 없습니다.");
        }

        Assert.Greater(patentCount, 0, "특허 연구가 CSV에 없습니다.");
    }

    [Test]
    public void PatentGrantCardIds_AreUnique()
    {
        ResearchDatabaseSO database = LoadResearchDatabase();

        HashSet<string> seenCardIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (ResearchData research in database.allResearches)
        {
            if (research == null || string.IsNullOrWhiteSpace(research.grantCardId))
                continue;

            Assert.IsTrue(
                seenCardIds.Add(research.grantCardId.Trim()),
                $"GrantCardID '{research.grantCardId}'가 여러 연구에 중복 매핑되어 있습니다. (연구 {research.id})");
        }
    }

    [Test]
    public void NonPatentResearch_DoesNotGrantCards()
    {
        ResearchDatabaseSO database = LoadResearchDatabase();

        foreach (ResearchData research in database.allResearches)
        {
            if (research == null || research.isPatentResearch)
                continue;

            Assert.IsTrue(
                string.IsNullOrWhiteSpace(research.grantCardId),
                $"일반 연구 {research.id}에 GrantCardID '{research.grantCardId}'가 설정되어 있습니다. 카드 지급은 특허 연구 전용입니다.");
        }
    }
}
