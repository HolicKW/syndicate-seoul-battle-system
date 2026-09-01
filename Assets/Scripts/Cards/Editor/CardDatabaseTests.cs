using System.Linq;
using NUnit.Framework;

public class CardDatabaseTests
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

    [Test]
    public void SpecialCard_LoadsById_ButIsExcludedFromCollectibleCards()
    {
        CardDatabase database = CardDatabase.Instance;

        Assert.IsNotNull(database.GetById("STATUS_RANSOMWARE"));
        Assert.IsFalse(database.GetAll().Any(card => card != null && card.id == "STATUS_RANSOMWARE"));
        Assert.IsTrue(database.GetAllSpecial().Any(card => card != null && card.id == "STATUS_RANSOMWARE"));
    }
}
