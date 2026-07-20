using GraphRag.Covariates;

namespace ManagedCode.GraphRag.Tests.Covariates;

public sealed class CovariateRecordTests
{
    [Test]
    public async Task CovariateRecord_StoresProperties()
    {
        var record = new CovariateRecord(
            "id",
            10,
            "claim",
            "type",
            "description",
            "subject",
            "object",
            "status",
            "2024-01-01",
            "2024-01-02",
            "source",
            "text-unit");

        await Assert.That(record.Id).IsEqualTo("id");
        await Assert.That(record.SubjectId).IsEqualTo("subject");
        await Assert.That(record.TextUnitId).IsEqualTo("text-unit");
    }
}
