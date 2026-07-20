using Npgsql;

namespace ManagedCode.GraphRag.Tests.Storage.Postgres;

public sealed class NpgsqlExtensionsTests
{
    [Test]
    public async Task UseAge_ReturnsSameMapper()
    {
#pragma warning disable CS0618
        var mapper = NpgsqlConnection.GlobalTypeMapper;
        var returned = mapper.UseAge();
#pragma warning restore CS0618
        await Assert.That(returned).IsSameReferenceAs(mapper);
    }
}
