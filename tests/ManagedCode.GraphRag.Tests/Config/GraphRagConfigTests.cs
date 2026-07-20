using GraphRag.Config;

namespace ManagedCode.GraphRag.Tests.Config;

public sealed class GraphRagConfigTests
{
    [Test]
    public async Task CacheConfig_UsesFileDefaults()
    {
        var cache = new CacheConfig();
        await Assert.That(cache.Type).IsEqualTo(CacheType.File);
        await Assert.That(cache.BaseDir).IsEqualTo("cache");
        await Assert.That(cache.ConnectionString).IsNull();
    }

    [Test]
    public async Task ExtractGraphNlpConfig_HasTextAnalyzerDefaults()
    {
        var config = new ExtractGraphNlpConfig();

        await Assert.That(config.NormalizeEdgeWeights).IsTrue();
        await Assert.That(config.TextAnalyzer.ExtractorType).IsEqualTo(NounPhraseExtractorType.RegexEnglish);
        await Assert.That(config.TextAnalyzer.ExcludeNouns).Contains("stuff");
        await Assert.That(config.TextAnalyzer.NounPhraseGrammars["PROPN,PROPN"]).IsEqualTo("PROPN");
        await Assert.That(config.ConcurrentRequests).IsEqualTo(25);
        await Assert.That(config.AsyncMode).IsEqualTo(AsyncType.Threaded);
    }

    [Test]
    public async Task ClaimExtractionConfig_ReadsPromptFromRoot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var promptPath = Path.Combine(tempRoot, "claims.txt");
        File.WriteAllText(promptPath, "Prompt Body");

        try
        {
            var config = new ClaimExtractionConfig
            {
                Prompt = "claims.txt",
                Description = "desc",
                MaxGleanings = 2,
                ModelId = "chat"
            };

            var strategy = config.GetResolvedStrategy(tempRoot);
            await Assert.That(strategy["model_id"]).IsEqualTo("chat");
            await Assert.That(strategy["extraction_prompt"]).IsEqualTo("Prompt Body");
            await Assert.That(strategy["claim_description"]).IsEqualTo("desc");
            await Assert.That(strategy["max_gleanings"]).IsEqualTo(2);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}
