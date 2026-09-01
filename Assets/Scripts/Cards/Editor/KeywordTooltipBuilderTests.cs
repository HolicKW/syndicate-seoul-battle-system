using System.Collections.Generic;
using NUnit.Framework;

[TestFixture]
public class KeywordTooltipBuilderTests
{
    [Test]
    public void BuildSection_IncludesDynamicKeywordValues()
    {
        var card = new CardData
        {
            keywords = new List<string>
            {
                "overload",
                "rebuild",
                "accumulation",
                "gamble",
                "virus",
                "corrosion",
                "반격",
                "일시적",
            },
            overclockThreshold = 4,
            rebuildCount = 2,
            accumulationTarget = 6,
            effects = new List<CardEffect>
            {
                new CardEffect { type = "gamble", chance = 0.4f },
                new CardEffect { type = "virus", value = 3 },
                new CardEffect { type = "corrosion", value = 2 },
                new CardEffect { type = "retaliateOnHit", value = 10 },
            },
        };

        string section = KeywordTooltipBuilder.BuildSection(card);

        StringAssert.Contains("과부하(4)", section);
        StringAssert.Contains("재구축(2)", section);
        StringAssert.Contains("축적(6)", section);
        StringAssert.Contains("도박(40%)", section);
        StringAssert.Contains("바이러스(3)", section);
        StringAssert.Contains("부식(2)", section);
        StringAssert.Contains("반격(10)", section);
        StringAssert.Contains("일시적", section);
    }

    [Test]
    public void BuildSection_IgnoresRebuildAccumKeyword()
    {
        var card = new CardData
        {
            keywords = new List<string> { "rebuildAccum" },
        };

        Assert.AreEqual(string.Empty, KeywordTooltipBuilder.BuildSection(card));
    }

    [Test]
    public void BuildSection_OmitsValueWhenKeywordHasNoDirectAmount()
    {
        var card = new CardData
        {
            keywords = new List<string> { "virus" },
        };

        string section = KeywordTooltipBuilder.BuildSection(card);

        StringAssert.Contains("바이러스:", section);
        Assert.IsFalse(section.Contains("바이러스(N)"));
    }

    [Test]
    public void BuildSection_FormatsOverdriveAll()
    {
        var card = new CardData
        {
            keywords = new List<string> { "overdrive" },
            effects = new List<CardEffect>
            {
                new CardEffect { type = "consumeVirus", countAll = true },
            },
        };

        StringAssert.Contains("폭주(전부)", KeywordTooltipBuilder.BuildSection(card));
        Assert.IsFalse(KeywordTooltipBuilder.BuildSection(card).Contains("적의 바이러스를"));
    }

    [Test]
    public void BuildSection_FormatsOverdriveFromMeltToxinThreshold()
    {
        var card = new CardData
        {
            keywords = new List<string> { "overdrive" },
            virusThreshold = 6,
            effects = new List<CardEffect>
            {
                new CardEffect { type = "meltToxin", threshold = 6, value = 10 },
            },
        };

        StringAssert.Contains("폭주(6)", KeywordTooltipBuilder.BuildSection(card));
        Assert.IsFalse(KeywordTooltipBuilder.BuildSection(card).Contains("적의 바이러스를"));
    }

    [Test]
    public void BuildLinkedDescription_WrapsKeywordLabelsWithTmpLinks()
    {
        var card = new CardData
        {
            keywords = new List<string> { "network", "protocol", "dismantle" },
            description = "[네트워크] 피해를 줍니다. [프로토콜] 이전에 카드를 해체했다면 추가 효과.",
        };

        string description = KeywordTooltipBuilder.BuildLinkedDescription(card, card.description);

        StringAssert.Contains("<link=\"network\"><b>네트워크</b></link>", description);
        StringAssert.Contains("<link=\"protocol\"><b>프로토콜</b></link>", description);
        StringAssert.Contains("<link=\"dismantle\"><b>해체</b></link>", description);
    }

    [Test]
    public void BuildLinkedDescription_WrapsKeywordLabelsEvenWhenNotInKeywordList()
    {
        var card = new CardData
        {
            keywords = new List<string>(),
            description = "덱에서 [추출] 카드 1장을 찾아 손패에 추가합니다.",
        };

        string description = KeywordTooltipBuilder.BuildLinkedDescription(card, card.description);

        StringAssert.Contains("<link=\"extract\"><b>추출</b></link>", description);
    }

    [Test]
    public void BuildLinkedDescription_WrapsOverflowText()
    {
        var card = new CardData
        {
            description = "자신에게 오버플로우(1)를 부여합니다.",
        };

        string description = KeywordTooltipBuilder.BuildLinkedDescription(card, card.description);

        StringAssert.Contains("<link=\"overflow\"><b>오버플로우</b></link>", description);
    }

    [Test]
    public void BuildLinkedDescription_WrapsStrengthAndWeaknessText()
    {
        var card = new CardData
        {
            description = "내게 힘을 2 부여하고, 적에게 약화를 1 부여합니다.",
            effects = new List<CardEffect>
            {
                new CardEffect { type = "strength", value = 2 },
                new CardEffect { type = "weakness", value = 1 },
            },
        };

        string description = KeywordTooltipBuilder.BuildLinkedDescription(card, card.description);

        StringAssert.Contains("<link=\"strength\"><b>힘</b></link>", description);
        StringAssert.Contains("<link=\"weakness\"><b>약화</b></link>", description);
    }
}
