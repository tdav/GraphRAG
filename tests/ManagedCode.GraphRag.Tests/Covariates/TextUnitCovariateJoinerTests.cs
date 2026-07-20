using GraphRag.Covariates;
using GraphRag.Data;

namespace ManagedCode.GraphRag.Tests.Covariates;

public sealed class TextUnitCovariateJoinerTests
{
    [Test]
    public async Task Attach_MergesCovariateIdentifiersOntoTextUnits()
    {
        var textUnits = new[]
        {
            new TextUnitRecord
            {
                Id = "unit-1",
                Text = "Alpha",
                DocumentIds = new[] { "doc-1" },
                TokenCount = 12,
                CovariateIds = Array.Empty<string>()
            },
            new TextUnitRecord
            {
                Id = "unit-2",
                Text = "Beta",
                DocumentIds = new[] { "doc-2" },
                TokenCount = 15,
                CovariateIds = new[] { "existing" }
            },
            new TextUnitRecord
            {
                Id = "unit-3",
                Text = "Gamma",
                DocumentIds = new[] { "doc-3" },
                TokenCount = 9
            }
        };

        var covariates = new[]
        {
            new CovariateRecord("cov-1", 0, "claim", "fraud", "", "entity-1", null, "OPEN", null, null, null, "unit-1"),
            new CovariateRecord("cov-2", 1, "claim", "fraud", "", "entity-1", null, "OPEN", null, null, null, "unit-1"),
            new CovariateRecord("cov-3", 2, "claim", "audit", "", "entity-2", null, "OPEN", null, null, null, "unit-2")
        };

        var updated = TextUnitCovariateJoiner.Attach(textUnits, covariates);

        var firstMatches = updated.Where(unit => unit.Id == "unit-1").ToList();
        await Assert.That(firstMatches).HasSingleItem();
        var first = firstMatches.Single();
        await Assert.That(first.CovariateIds).IsEquivalentTo(new[] { "cov-1", "cov-2" });

        var secondMatches = updated.Where(unit => unit.Id == "unit-2").ToList();
        await Assert.That(secondMatches).HasSingleItem();
        var second = secondMatches.Single();
        await Assert.That(second.CovariateIds).IsEquivalentTo(new[] { "cov-3", "existing" });

        var thirdMatches = updated.Where(unit => unit.Id == "unit-3").ToList();
        await Assert.That(thirdMatches).HasSingleItem();
        var third = thirdMatches.Single();
        await Assert.That(third.CovariateIds).IsEmpty();
    }
}
