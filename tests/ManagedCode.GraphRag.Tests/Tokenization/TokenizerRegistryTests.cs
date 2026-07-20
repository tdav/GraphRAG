using GraphRag.Constants;
using GraphRag.Tokenization;

namespace ManagedCode.GraphRag.Tests.Tokenization;

public sealed class TokenizerRegistryTests
{
    [Test]
    public async Task GetTokenizer_DefaultsToPreferredEncoding()
    {
        var defaultTokenizer = TokenizerRegistry.GetTokenizer();
        var explicitTokenizer = TokenizerRegistry.GetTokenizer(TokenizerDefaults.DefaultEncoding);

        await Assert.That(TokenizerRegistry.GetTokenizer()).IsSameReferenceAs(explicitTokenizer);
        await Assert.That(explicitTokenizer).IsSameReferenceAs(defaultTokenizer);
        await Assert.That(defaultTokenizer.CountTokens("GraphRAG validates default encoding selection."))
            .IsEqualTo(explicitTokenizer.CountTokens("GraphRAG validates default encoding selection."));
    }

    [Test]
    public async Task GetTokenizer_FallsBackForUnknownModel()
    {
        var fallbackTokenizer = TokenizerRegistry.GetTokenizer(TokenizerDefaults.DefaultModel);
        var unknownTokenizer = TokenizerRegistry.GetTokenizer("unknown-model-name");

        var sample = "Fallback tokens should match GPT-4 encoding.";
        await Assert.That(unknownTokenizer.CountTokens(sample)).IsEqualTo(fallbackTokenizer.CountTokens(sample));
    }
}
