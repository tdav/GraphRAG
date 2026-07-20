using GraphRag.Chunking;
using GraphRag.Config;
using GraphRag.Constants;
using GraphRag.Tokenization;

namespace ManagedCode.GraphRag.Tests.Chunking;

public sealed class TokenTextChunkerTests
{
    private readonly TokenTextChunker _chunker = new();
    private readonly ChunkingConfig _defaultConfig = new()
    {
        Size = 40,
        Overlap = 10,
        EncodingModel = TokenizerDefaults.DefaultEncoding
    };

    [Test]
    public async Task Chunk_RespectsTokenBudget()
    {
        var tokenizer = TokenizerRegistry.GetTokenizer(TokenizerDefaults.DefaultEncoding);
        const string baseSentence = "Alice met Bob at the conference and shared insights.";
        var text = string.Join(' ', Enumerable.Repeat(baseSentence, 16));
        var slices = new[] { new ChunkSlice("doc-1", text) };

        var config = new ChunkingConfig
        {
            Size = 40,
            Overlap = 10,
            EncodingModel = TokenizerDefaults.DefaultEncoding
        };

        var totalTokens = tokenizer.EncodeToIds(text).Count;
        var chunks = _chunker.Chunk(slices, config);

        await Assert.That(chunks).IsNotEmpty();
        foreach (var chunk in chunks)
        {
            await Assert.That(chunk.DocumentIds).Contains("doc-1");
            await Assert.That(chunk.TokenCount <= config.Size).IsTrue();
            await Assert.That(string.IsNullOrWhiteSpace(chunk.Text)).IsFalse();
        }

        if (totalTokens > config.Size)
        {
            await Assert.That(chunks.Count > 1).IsTrue();
        }
    }

    [Test]
    public async Task Chunk_CombinesDocumentIdentifiersAcrossSlices()
    {
        var slices = new[]
        {
            new ChunkSlice("doc-1", string.Join(' ', Enumerable.Repeat("First slice carries shared content.", 4))),
            new ChunkSlice("doc-2", string.Join(' ', Enumerable.Repeat("Second slice enriches the narrative.", 4)))
        };

        var config = new ChunkingConfig
        {
            Size = 50,
            Overlap = 10,
            EncodingModel = TokenizerDefaults.DefaultModel
        };

        var chunks = _chunker.Chunk(slices, config);

        await Assert.That(chunks).IsNotEmpty();
        await Assert.That(chunks.Any(chunk => chunk.DocumentIds.Contains("doc-1"))).IsTrue();
        await Assert.That(chunks.Any(chunk => chunk.DocumentIds.Contains("doc-2"))).IsTrue();
    }

    [Test]
    public async Task Chunk_OverlapProducesSharedTokensBetweenAdjacentChunks()
    {
        var tokenizer = TokenizerRegistry.GetTokenizer(TokenizerDefaults.DefaultEncoding);
        const string text = "The quick brown fox jumps over the lazy dog and continues running through the forest until it reaches the river where it stops to drink some water.";
        var slices = new[] { new ChunkSlice("doc-1", text) };

        var config = new ChunkingConfig
        {
            Size = 20,
            Overlap = 5,
            EncodingModel = TokenizerDefaults.DefaultEncoding
        };

        var chunks = _chunker.Chunk(slices, config);

        await Assert.That(chunks.Count >= 2).IsTrue();

        for (var i = 0; i < chunks.Count - 1; i++)
        {
            var currentChunkTokens = tokenizer.EncodeToIds(chunks[i].Text);
            var nextChunkTokens = tokenizer.EncodeToIds(chunks[i + 1].Text);

            var lastTokensOfCurrent = currentChunkTokens.TakeLast(config.Overlap).ToArray();
            var firstTokensOfNext = nextChunkTokens.Take(config.Overlap).ToArray();

            await Assert.That(firstTokensOfNext).IsEquivalentTo(lastTokensOfCurrent);
        }
    }

    [Test]
    public async Task Chunk_EmptySlicesReturnsEmptyResult()
    {
        var slices = Array.Empty<ChunkSlice>();

        var chunks = _chunker.Chunk(slices, _defaultConfig);

        await Assert.That(chunks).IsEmpty();
    }

    [Test]
    public async Task Chunk_SlicesWithEmptyTextReturnsEmptyResult()
    {
        var slices = new[] { new ChunkSlice("doc-1", string.Empty) };

        var chunks = _chunker.Chunk(slices, _defaultConfig);

        await Assert.That(chunks).IsEmpty();
    }

    [Test]
    public async Task Chunk_NullSlicesThrowsArgumentNullException()
    {
        await Assert.That(() => _chunker.Chunk(null!, _defaultConfig)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Chunk_NullConfigThrowsArgumentNullException()
    {
        var slices = new[] { new ChunkSlice("doc-1", "Some text") };

        await Assert.That(() => _chunker.Chunk(slices, null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Chunk_ZeroOverlapProducesNonOverlappingChunks()
    {
        var tokenizer = TokenizerRegistry.GetTokenizer(TokenizerDefaults.DefaultEncoding);
        const string text = "The quick brown fox jumps over the lazy dog and continues running through the forest until it reaches the river.";
        var slices = new[] { new ChunkSlice("doc-1", text) };

        var config = new ChunkingConfig
        {
            Size = 15,
            Overlap = 0,
            EncodingModel = TokenizerDefaults.DefaultEncoding
        };

        var chunks = _chunker.Chunk(slices, config);
        await Assert.That(chunks.Count >= 2).IsTrue();

        var allChunkTokens = chunks
            .SelectMany(c => tokenizer.EncodeToIds(c.Text))
            .ToList();

        var originalTokens = tokenizer.EncodeToIds(text);

        await Assert.That(allChunkTokens.Count).IsEqualTo(originalTokens.Count);
    }

    [Test]
    public async Task Chunk_InputSmallerThanChunkSizeReturnsSingleChunk()
    {
        const string shortText = "Hello world";
        var slices = new[] { new ChunkSlice("doc-1", shortText) };

        var config = new ChunkingConfig
        {
            Size = 100,
            Overlap = 10,
            EncodingModel = TokenizerDefaults.DefaultEncoding
        };

        var chunks = _chunker.Chunk(slices, config);

        await Assert.That(chunks).HasSingleItem();
        await Assert.That(chunks[0].Text).IsEqualTo(shortText);
    }

    [Test]
    public async Task Chunk_ExactBoundaryProducesExpectedChunkCount()
    {
        var tokenizer = TokenizerRegistry.GetTokenizer(TokenizerDefaults.DefaultEncoding);

        const int chunkSize = 10;
        const int overlap = 2;
        const int step = chunkSize - overlap;

        var targetTokenCount = step * 3 + overlap;
        var words = Enumerable.Range(0, targetTokenCount * 2).Select(i => "word").ToArray();
        var text = string.Join(" ", words);

        var actualTokens = tokenizer.EncodeToIds(text);
        var slices = new[] { new ChunkSlice("doc-1", text) };

        var config = new ChunkingConfig
        {
            Size = chunkSize,
            Overlap = overlap,
            EncodingModel = TokenizerDefaults.DefaultEncoding
        };

        var chunks = _chunker.Chunk(slices, config);

        await Assert.That(chunks.Count >= 2).IsTrue();
        foreach (var chunk in chunks.SkipLast(1))
        {
            await Assert.That(chunk.TokenCount).IsEqualTo(chunkSize);
        }
    }
}
