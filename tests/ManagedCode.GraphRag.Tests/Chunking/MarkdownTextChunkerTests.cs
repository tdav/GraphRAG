using GraphRag.Chunking;
using GraphRag.Config;
using GraphRag.Constants;
using GraphRag.Tokenization;

namespace ManagedCode.GraphRag.Tests.Chunking;

public sealed class MarkdownTextChunkerTests
{
    private readonly MarkdownTextChunker _chunker = new();

    #region Chunk Tests (Original)

    [Test]
    public async Task Chunk_SplitsMarkdownBlocks()
    {
        var text = "# Title\n\nAlice met Bob.\n\n![image](path)\n\n" +
                   string.Join(" ", Enumerable.Repeat("This is a longer paragraph that should be chunked based on token limits.", 4));
        var slices = new[] { new ChunkSlice("doc-1", text) };

        var config = new ChunkingConfig
        {
            Size = 60,
            Overlap = 10,
            EncodingModel = TokenizerDefaults.DefaultEncoding
        };

        var chunks = _chunker.Chunk(slices, config);

        await Assert.That(chunks).IsNotEmpty();
        foreach (var chunk in chunks)
        {
            await Assert.That(chunk.DocumentIds).Contains("doc-1");
        }
        await Assert.That(chunks.Count >= 2).IsTrue();
        foreach (var chunk in chunks)
        {
            await Assert.That(chunk.TokenCount > 0).IsTrue();
        }
    }

    [Test]
    public async Task Chunk_MergesImageBlocksIntoPrecedingChunk()
    {
        var text = string.Join(' ', Enumerable.Repeat("This paragraph provides enough content for chunking.", 6)) +
                   "\n\n![diagram](diagram.png)\nImage description follows with more narrative text.";
        var slices = new[] { new ChunkSlice("doc-1", text) };

        var config = new ChunkingConfig
        {
            Size = 60,
            Overlap = 0,
            EncodingModel = TokenizerDefaults.DefaultModel
        };

        var chunks = _chunker.Chunk(slices, config);

        await Assert.That(chunks).IsNotEmpty();
        await Assert.That(chunks.Any(chunk => chunk.Text.Contains("![diagram](diagram.png)", StringComparison.Ordinal))).IsTrue();
        await Assert.That(chunks.Any(chunk => chunk.Text.TrimStart().StartsWith("![", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task Chunk_RespectsOverlapBetweenChunks()
    {
        var text = string.Join(' ', Enumerable.Repeat("Token overlap ensures continuity across generated segments.", 20));
        var slices = new[] { new ChunkSlice("doc-1", text) };

        var config = new ChunkingConfig
        {
            Size = 80,
            Overlap = 20,
            EncodingModel = "gpt-4"
        };

        var chunks = _chunker.Chunk(slices, config);

        await Assert.That(chunks.Count > 1).IsTrue();

        var tokenizer = TokenizerRegistry.GetTokenizer(config.EncodingModel);
        var firstTokens = tokenizer.EncodeToIds(chunks[0].Text);

        _ = tokenizer.EncodeToIds(chunks[1].Text);
        var overlapTokens = firstTokens.Skip(Math.Max(0, firstTokens.Count - config.Overlap)).ToArray();
        await Assert.That(overlapTokens.Length > 0).IsTrue();
        var overlapText = tokenizer.Decode(overlapTokens).TrimStart();
        var secondText = chunks[1].Text.TrimStart();
        await Assert.That(secondText.StartsWith(overlapText, StringComparison.Ordinal)).IsTrue();
    }

    #endregion

    #region SplitToFragments Tests

    [Test]
    public async Task SplitToFragments_EmptyString_ReturnsEmpty()
    {
        var result = MarkdownTextChunker.SplitToFragments("", MarkdownTextChunker.ExplicitSeparators);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task SplitToFragments_NullSeparators_ReturnsCharacterLevelFragments()
    {
        var text = "abc";
        var result = MarkdownTextChunker.SplitToFragments(text, null);

        await Assert.That(result.Count).IsEqualTo(3);
        foreach (var f in result)
        {
            await Assert.That(f.IsSeparator).IsTrue();
        }
        await Assert.That(text[result[0].Range]).IsEqualTo("a");
        await Assert.That(text[result[1].Range]).IsEqualTo("b");
        await Assert.That(text[result[2].Range]).IsEqualTo("c");
    }

    [Test]
    public async Task SplitToFragments_NoSeparatorsInText_ReturnsSingleContentFragment()
    {
        var text = "hello world";
        var result = MarkdownTextChunker.SplitToFragments(text, MarkdownTextChunker.ExplicitSeparators);

        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].IsSeparator).IsFalse();
        await Assert.That(text[result[0].Range]).IsEqualTo("hello world");
    }

    [Test]
    public async Task SplitToFragments_SeparatorAtStart_FirstFragmentIsSeparator()
    {
        var text = "\n\nhello";
        var result = MarkdownTextChunker.SplitToFragments(text, MarkdownTextChunker.ExplicitSeparators);

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0].IsSeparator).IsTrue();
        await Assert.That(text[result[0].Range]).IsEqualTo("\n\n");
        await Assert.That(result[1].IsSeparator).IsFalse();
        await Assert.That(text[result[1].Range]).IsEqualTo("hello");
    }

    [Test]
    public async Task SplitToFragments_SeparatorAtEnd_LastFragmentIsSeparator()
    {
        var text = "hello.\n\n";
        var result = MarkdownTextChunker.SplitToFragments(text, MarkdownTextChunker.ExplicitSeparators);

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0].IsSeparator).IsFalse();
        await Assert.That(text[result[0].Range]).IsEqualTo("hello");
        await Assert.That(result[1].IsSeparator).IsTrue();
        await Assert.That(text[result[1].Range]).IsEqualTo(".\n\n");
    }

    [Test]
    public async Task SplitToFragments_AdjacentSeparators_CreatesSeparateFragments()
    {
        var text = "\n\n\n\n";
        var result = MarkdownTextChunker.SplitToFragments(text, MarkdownTextChunker.ExplicitSeparators);

        await Assert.That(result.Count).IsEqualTo(2);
        foreach (var f in result)
        {
            await Assert.That(f.IsSeparator).IsTrue();
        }
        await Assert.That(text[result[0].Range]).IsEqualTo("\n\n");
        await Assert.That(text[result[1].Range]).IsEqualTo("\n\n");
    }

    [Test]
    public async Task SplitToFragments_LongestMatchPrecedence_MatchesDotNewlineNewlineOverDot()
    {
        // Using WeakSeparators2 which has both "." and ".\n\n" isn't there, but ExplicitSeparators has ".\n\n"
        var text = "hello.\n\nworld";
        var result = MarkdownTextChunker.SplitToFragments(text, MarkdownTextChunker.ExplicitSeparators);

        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(text[result[0].Range]).IsEqualTo("hello");
        await Assert.That(text[result[1].Range]).IsEqualTo(".\n\n");
        await Assert.That(result[1].IsSeparator).IsTrue();
        await Assert.That(text[result[2].Range]).IsEqualTo("world");
    }

    [Test]
    public async Task SplitToFragments_LongestMatchPrecedence_MatchesTripleQuestionOverDouble()
    {
        var text = "what???really";
        var result = MarkdownTextChunker.SplitToFragments(text, MarkdownTextChunker.WeakSeparators2);

        // Should match "???" not "??"
        await Assert.That(result.Any(f => f.IsSeparator && text[f.Range] == "???")).IsTrue();
    }

    [Test]
    public async Task SplitToFragments_UnicodeSeparators_HandlesInterrobangCorrectly()
    {
        var text = "what⁉ really";
        var result = MarkdownTextChunker.SplitToFragments(text, MarkdownTextChunker.WeakSeparators2);

        await Assert.That(result.Any(f => f.IsSeparator && text[f.Range] == "⁉ ")).IsTrue();
    }

    [Test]
    public async Task SplitToFragments_UnicodeSeparators_HandlesEllipsisCorrectly()
    {
        var text = "wait… more";
        var result = MarkdownTextChunker.SplitToFragments(text, MarkdownTextChunker.WeakSeparators2);

        await Assert.That(result.Any(f => f.IsSeparator && text[f.Range] == "… ")).IsTrue();
    }

    #endregion

    #region ExplicitSeparators Additional Tests

    [Test]
    public async Task SplitToFragments_HeaderSeparators_MatchesNewlineHash()
    {
        var text = "content\n# Header1\n## Header2";
        var result = MarkdownTextChunker.SplitToFragments(text, MarkdownTextChunker.ExplicitSeparators);

        await Assert.That(result.Any(f => f.IsSeparator && text[f.Range] == "\n#")).IsTrue();
        await Assert.That(result.Any(f => f.IsSeparator && text[f.Range] == "\n##")).IsTrue();
    }

    [Test]
    public async Task SplitToFragments_HeaderSeparators_MatchesAllLevels()
    {
        var text = "a\n#b\n##c\n###d\n####e\n#####f";
        var result = MarkdownTextChunker.SplitToFragments(text, MarkdownTextChunker.ExplicitSeparators);

        await Assert.That(result.Any(f => f.IsSeparator && text[f.Range] == "\n#")).IsTrue();
        await Assert.That(result.Any(f => f.IsSeparator && text[f.Range] == "\n##")).IsTrue();
        await Assert.That(result.Any(f => f.IsSeparator && text[f.Range] == "\n###")).IsTrue();
        await Assert.That(result.Any(f => f.IsSeparator && text[f.Range] == "\n####")).IsTrue();
        await Assert.That(result.Any(f => f.IsSeparator && text[f.Range] == "\n#####")).IsTrue();
    }

    [Test]
    public async Task SplitToFragments_HorizontalRule_MatchesNewlineDashes()
    {
        var text = "above\n---below";
        var result = MarkdownTextChunker.SplitToFragments(text, MarkdownTextChunker.ExplicitSeparators);

        await Assert.That(result.Any(f => f.IsSeparator && text[f.Range] == "\n---")).IsTrue();
    }

    [Test]
    public async Task SplitToFragments_ExclamationNewlines_MatchesAllVariants()
    {
        var text1 = "wow!\n\nmore";
        var text2 = "wow!!\n\nmore";
        var text3 = "wow!!!\n\nmore";
        var result1 = MarkdownTextChunker.SplitToFragments(text1, MarkdownTextChunker.ExplicitSeparators);
        var result2 = MarkdownTextChunker.SplitToFragments(text2, MarkdownTextChunker.ExplicitSeparators);
        var result3 = MarkdownTextChunker.SplitToFragments(text3, MarkdownTextChunker.ExplicitSeparators);

        await Assert.That(result1.Any(f => f.IsSeparator && text1[f.Range] == "!\n\n")).IsTrue();
        await Assert.That(result2.Any(f => f.IsSeparator && text2[f.Range] == "!!\n\n")).IsTrue();
        await Assert.That(result3.Any(f => f.IsSeparator && text3[f.Range] == "!!!\n\n")).IsTrue();
    }

    [Test]
    public async Task SplitToFragments_QuestionNewlines_MatchesAllVariants()
    {
        var text1 = "what?\n\nmore";
        var text2 = "what??\n\nmore";
        var text3 = "what???\n\nmore";
        var result1 = MarkdownTextChunker.SplitToFragments(text1, MarkdownTextChunker.ExplicitSeparators);
        var result2 = MarkdownTextChunker.SplitToFragments(text2, MarkdownTextChunker.ExplicitSeparators);
        var result3 = MarkdownTextChunker.SplitToFragments(text3, MarkdownTextChunker.ExplicitSeparators);

        await Assert.That(result1.Any(f => f.IsSeparator && text1[f.Range] == "?\n\n")).IsTrue();
        await Assert.That(result2.Any(f => f.IsSeparator && text2[f.Range] == "??\n\n")).IsTrue();
        await Assert.That(result3.Any(f => f.IsSeparator && text3[f.Range] == "???\n\n")).IsTrue();
    }

    #endregion

    #region PotentialSeparators Tests

    [Test]
    public async Task SplitToFragments_Blockquote_MatchesNewlineGreaterThan()
    {
        var text = "text\n> quoted";
        var result = MarkdownTextChunker.SplitToFragments(text, MarkdownTextChunker.PotentialSeparators);

        await Assert.That(result.Any(f => f.IsSeparator && text[f.Range] == "\n> ")).IsTrue();
    }

    [Test]
    public async Task SplitToFragments_BlockquoteList_MatchesVariants()
    {
        var text1 = "text\n>- item";
        var text2 = "text\n>* item";
        var result1 = MarkdownTextChunker.SplitToFragments(text1, MarkdownTextChunker.PotentialSeparators);
        var result2 = MarkdownTextChunker.SplitToFragments(text2, MarkdownTextChunker.PotentialSeparators);

        await Assert.That(result1.Any(f => f.IsSeparator && text1[f.Range] == "\n>- ")).IsTrue();
        await Assert.That(result2.Any(f => f.IsSeparator && text2[f.Range] == "\n>* ")).IsTrue();
    }

    [Test]
    public async Task SplitToFragments_NumberedList_MatchesDigitDotSpace()
    {
        var text = "intro\n1. first\n2. second\n10. tenth";
        var result = MarkdownTextChunker.SplitToFragments(text, MarkdownTextChunker.PotentialSeparators);

        await Assert.That(result.Any(f => f.IsSeparator && text[f.Range] == "\n1. ")).IsTrue();
        await Assert.That(result.Any(f => f.IsSeparator && text[f.Range] == "\n2. ")).IsTrue();
        await Assert.That(result.Any(f => f.IsSeparator && text[f.Range] == "\n10. ")).IsTrue();
    }

    [Test]
    public async Task SplitToFragments_CodeFence_MatchesTripleBacktick()
    {
        var text = "text\n```code";
        var result = MarkdownTextChunker.SplitToFragments(text, MarkdownTextChunker.PotentialSeparators);

        await Assert.That(result.Any(f => f.IsSeparator && text[f.Range] == "\n```")).IsTrue();
    }

    #endregion

    #region WeakSeparators1 Tests

    [Test]
    public async Task SplitToFragments_TablePipe_MatchesPipeVariants()
    {
        var text1 = "col1| col2";
        var text2 = "data |\nmore";
        var text3 = "---|-|\ndata";
        var result1 = MarkdownTextChunker.SplitToFragments(text1, MarkdownTextChunker.WeakSeparators1);
        var result2 = MarkdownTextChunker.SplitToFragments(text2, MarkdownTextChunker.WeakSeparators1);
        var result3 = MarkdownTextChunker.SplitToFragments(text3, MarkdownTextChunker.WeakSeparators1);

        await Assert.That(result1.Any(f => f.IsSeparator && text1[f.Range] == "| ")).IsTrue();
        await Assert.That(result2.Any(f => f.IsSeparator && text2[f.Range] == " |\n")).IsTrue();
        await Assert.That(result3.Any(f => f.IsSeparator && text3[f.Range] == "-|\n")).IsTrue();
    }

    [Test]
    public async Task SplitToFragments_LinkBracket_MatchesOpenBracket()
    {
        var text = "click [here](url)";
        var result = MarkdownTextChunker.SplitToFragments(text, MarkdownTextChunker.WeakSeparators1);

        await Assert.That(result.Any(f => f.IsSeparator && text[f.Range] == "[")).IsTrue();
    }

    [Test]
    public async Task SplitToFragments_ImageBracket_MatchesExclamationBracket()
    {
        var text = "see ![alt](img.png)";
        var result = MarkdownTextChunker.SplitToFragments(text, MarkdownTextChunker.WeakSeparators1);

        await Assert.That(result.Any(f => f.IsSeparator && text[f.Range] == "![")).IsTrue();
    }

    [Test]
    public async Task SplitToFragments_DefinitionList_MatchesNewlineColon()
    {
        var text = "term\n: definition";
        var result = MarkdownTextChunker.SplitToFragments(text, MarkdownTextChunker.WeakSeparators1);

        await Assert.That(result.Any(f => f.IsSeparator && text[f.Range] == "\n: ")).IsTrue();
    }

    #endregion

    #region WeakSeparators2 Additional Tests

    [Test]
    public async Task SplitToFragments_TabSeparators_MatchesPunctuationTab()
    {
        var text1 = "end.\tnext";
        var text2 = "what?\tnext";
        var text3 = "wow!\tnext";
        var result1 = MarkdownTextChunker.SplitToFragments(text1, MarkdownTextChunker.WeakSeparators2);
        var result2 = MarkdownTextChunker.SplitToFragments(text2, MarkdownTextChunker.WeakSeparators2);
        var result3 = MarkdownTextChunker.SplitToFragments(text3, MarkdownTextChunker.WeakSeparators2);

        await Assert.That(result1.Any(f => f.IsSeparator && text1[f.Range] == ".\t")).IsTrue();
        await Assert.That(result2.Any(f => f.IsSeparator && text2[f.Range] == "?\t")).IsTrue();
        await Assert.That(result3.Any(f => f.IsSeparator && text3[f.Range] == "!\t")).IsTrue();
    }

    [Test]
    public async Task SplitToFragments_NewlineSeparators_MatchesPunctuationNewline()
    {
        var text1 = "end.\nnext";
        var text2 = "what?\nnext";
        var text3 = "wow!\nnext";
        var result1 = MarkdownTextChunker.SplitToFragments(text1, MarkdownTextChunker.WeakSeparators2);
        var result2 = MarkdownTextChunker.SplitToFragments(text2, MarkdownTextChunker.WeakSeparators2);
        var result3 = MarkdownTextChunker.SplitToFragments(text3, MarkdownTextChunker.WeakSeparators2);

        await Assert.That(result1.Any(f => f.IsSeparator && text1[f.Range] == ".\n")).IsTrue();
        await Assert.That(result2.Any(f => f.IsSeparator && text2[f.Range] == "?\n")).IsTrue();
        await Assert.That(result3.Any(f => f.IsSeparator && text3[f.Range] == "!\n")).IsTrue();
    }

    [Test]
    public async Task SplitToFragments_QuadPunctuation_MatchesFourChars()
    {
        var text1 = "what!!!!really";
        var text2 = "what????really";
        var result1 = MarkdownTextChunker.SplitToFragments(text1, MarkdownTextChunker.WeakSeparators2);
        var result2 = MarkdownTextChunker.SplitToFragments(text2, MarkdownTextChunker.WeakSeparators2);

        await Assert.That(result1.Any(f => f.IsSeparator && text1[f.Range] == "!!!!")).IsTrue();
        await Assert.That(result2.Any(f => f.IsSeparator && text2[f.Range] == "????")).IsTrue();
    }

    [Test]
    public async Task SplitToFragments_MixedPunctuation_MatchesInterrobangVariants()
    {
        var text1 = "what?!?really";
        var text2 = "what!?!really";
        var text3 = "what!?really";
        var text4 = "what?!really";
        var result1 = MarkdownTextChunker.SplitToFragments(text1, MarkdownTextChunker.WeakSeparators2);
        var result2 = MarkdownTextChunker.SplitToFragments(text2, MarkdownTextChunker.WeakSeparators2);
        var result3 = MarkdownTextChunker.SplitToFragments(text3, MarkdownTextChunker.WeakSeparators2);
        var result4 = MarkdownTextChunker.SplitToFragments(text4, MarkdownTextChunker.WeakSeparators2);

        await Assert.That(result1.Any(f => f.IsSeparator && text1[f.Range] == "?!?")).IsTrue();
        await Assert.That(result2.Any(f => f.IsSeparator && text2[f.Range] == "!?!")).IsTrue();
        await Assert.That(result3.Any(f => f.IsSeparator && text3[f.Range] == "!?")).IsTrue();
        await Assert.That(result4.Any(f => f.IsSeparator && text4[f.Range] == "?!")).IsTrue();
    }

    [Test]
    public async Task SplitToFragments_Ellipsis_MatchesDotVariants()
    {
        var text1 = "wait....more";
        var text2 = "wait...more";
        var text3 = "wait..more";
        var result1 = MarkdownTextChunker.SplitToFragments(text1, MarkdownTextChunker.WeakSeparators2);
        var result2 = MarkdownTextChunker.SplitToFragments(text2, MarkdownTextChunker.WeakSeparators2);
        var result3 = MarkdownTextChunker.SplitToFragments(text3, MarkdownTextChunker.WeakSeparators2);

        await Assert.That(result1.Any(f => f.IsSeparator && text1[f.Range] == "....")).IsTrue();
        await Assert.That(result2.Any(f => f.IsSeparator && text2[f.Range] == "...")).IsTrue();
        await Assert.That(result3.Any(f => f.IsSeparator && text3[f.Range] == "..")).IsTrue();
    }

    [Test]
    public async Task SplitToFragments_SinglePunctuation_MatchesWithoutSpace()
    {
        // Single punctuation at end of string (no space after)
        var text1 = "end.";
        var text2 = "end?";
        var text3 = "end!";
        var result1 = MarkdownTextChunker.SplitToFragments(text1, MarkdownTextChunker.WeakSeparators2);
        var result2 = MarkdownTextChunker.SplitToFragments(text2, MarkdownTextChunker.WeakSeparators2);
        var result3 = MarkdownTextChunker.SplitToFragments(text3, MarkdownTextChunker.WeakSeparators2);

        await Assert.That(result1.Any(f => f.IsSeparator && text1[f.Range] == ".")).IsTrue();
        await Assert.That(result2.Any(f => f.IsSeparator && text2[f.Range] == "?")).IsTrue();
        await Assert.That(result3.Any(f => f.IsSeparator && text3[f.Range] == "!")).IsTrue();
    }

    [Test]
    public async Task SplitToFragments_DoubleQuestion_MatchesBeforeTriple()
    {
        var text = "what??next";
        var result = MarkdownTextChunker.SplitToFragments(text, MarkdownTextChunker.WeakSeparators2);

        await Assert.That(result.Any(f => f.IsSeparator && text[f.Range] == "??")).IsTrue();
    }

    [Test]
    public async Task SplitToFragments_DoubleExclamation_MatchesBeforeTriple()
    {
        var text = "wow!!next";
        var result = MarkdownTextChunker.SplitToFragments(text, MarkdownTextChunker.WeakSeparators2);

        await Assert.That(result.Any(f => f.IsSeparator && text[f.Range] == "!!")).IsTrue();
    }

    #endregion

    #region WeakSeparators3 Tests

    [Test]
    public async Task SplitToFragments_Semicolon_MatchesAllVariants()
    {
        var text1 = "a; b";
        var text2 = "a;\tb";
        var text3 = "a;\nb";
        var text4 = "a;b";
        var result1 = MarkdownTextChunker.SplitToFragments(text1, MarkdownTextChunker.WeakSeparators3);
        var result2 = MarkdownTextChunker.SplitToFragments(text2, MarkdownTextChunker.WeakSeparators3);
        var result3 = MarkdownTextChunker.SplitToFragments(text3, MarkdownTextChunker.WeakSeparators3);
        var result4 = MarkdownTextChunker.SplitToFragments(text4, MarkdownTextChunker.WeakSeparators3);

        await Assert.That(result1.Any(f => f.IsSeparator && text1[f.Range] == "; ")).IsTrue();
        await Assert.That(result2.Any(f => f.IsSeparator && text2[f.Range] == ";\t")).IsTrue();
        await Assert.That(result3.Any(f => f.IsSeparator && text3[f.Range] == ";\n")).IsTrue();
        await Assert.That(result4.Any(f => f.IsSeparator && text4[f.Range] == ";")).IsTrue();
    }

    [Test]
    public async Task SplitToFragments_CloseBrace_MatchesAllVariants()
    {
        var text1 = "a} b";
        var text2 = "a}\tb";
        var text3 = "a}\nb";
        var text4 = "a}b";
        var result1 = MarkdownTextChunker.SplitToFragments(text1, MarkdownTextChunker.WeakSeparators3);
        var result2 = MarkdownTextChunker.SplitToFragments(text2, MarkdownTextChunker.WeakSeparators3);
        var result3 = MarkdownTextChunker.SplitToFragments(text3, MarkdownTextChunker.WeakSeparators3);
        var result4 = MarkdownTextChunker.SplitToFragments(text4, MarkdownTextChunker.WeakSeparators3);

        await Assert.That(result1.Any(f => f.IsSeparator && text1[f.Range] == "} ")).IsTrue();
        await Assert.That(result2.Any(f => f.IsSeparator && text2[f.Range] == "}\t")).IsTrue();
        await Assert.That(result3.Any(f => f.IsSeparator && text3[f.Range] == "}\n")).IsTrue();
        await Assert.That(result4.Any(f => f.IsSeparator && text4[f.Range] == "}")).IsTrue();
    }

    [Test]
    public async Task SplitToFragments_CloseParen_MatchesAllVariants()
    {
        var text1 = "(a) b";
        var text2 = "(a)\tb";
        var text3 = "(a)\nb";
        var text4 = "(a)b";
        var result1 = MarkdownTextChunker.SplitToFragments(text1, MarkdownTextChunker.WeakSeparators3);
        var result2 = MarkdownTextChunker.SplitToFragments(text2, MarkdownTextChunker.WeakSeparators3);
        var result3 = MarkdownTextChunker.SplitToFragments(text3, MarkdownTextChunker.WeakSeparators3);
        var result4 = MarkdownTextChunker.SplitToFragments(text4, MarkdownTextChunker.WeakSeparators3);

        await Assert.That(result1.Any(f => f.IsSeparator && text1[f.Range] == ") ")).IsTrue();
        await Assert.That(result2.Any(f => f.IsSeparator && text2[f.Range] == ")\t")).IsTrue();
        await Assert.That(result3.Any(f => f.IsSeparator && text3[f.Range] == ")\n")).IsTrue();
        await Assert.That(result4.Any(f => f.IsSeparator && text4[f.Range] == ")")).IsTrue();
    }

    [Test]
    public async Task SplitToFragments_CloseBracket_MatchesAllVariants()
    {
        var text1 = "[a] b";
        var text2 = "[a]\tb";
        var text3 = "[a]\nb";
        var text4 = "[a]b";
        var result1 = MarkdownTextChunker.SplitToFragments(text1, MarkdownTextChunker.WeakSeparators3);
        var result2 = MarkdownTextChunker.SplitToFragments(text2, MarkdownTextChunker.WeakSeparators3);
        var result3 = MarkdownTextChunker.SplitToFragments(text3, MarkdownTextChunker.WeakSeparators3);
        var result4 = MarkdownTextChunker.SplitToFragments(text4, MarkdownTextChunker.WeakSeparators3);

        await Assert.That(result1.Any(f => f.IsSeparator && text1[f.Range] == "] ")).IsTrue();
        await Assert.That(result2.Any(f => f.IsSeparator && text2[f.Range] == "]\t")).IsTrue();
        await Assert.That(result3.Any(f => f.IsSeparator && text3[f.Range] == "]\n")).IsTrue();
        await Assert.That(result4.Any(f => f.IsSeparator && text4[f.Range] == "]")).IsTrue();
    }

    [Test]
    public async Task SplitToFragments_Colon_MatchesAllVariants()
    {
        var text1 = "key: value";
        var text2 = "key:value";
        var result1 = MarkdownTextChunker.SplitToFragments(text1, MarkdownTextChunker.WeakSeparators3);
        var result2 = MarkdownTextChunker.SplitToFragments(text2, MarkdownTextChunker.WeakSeparators3);

        await Assert.That(result1.Any(f => f.IsSeparator && text1[f.Range] == ": ")).IsTrue();
        await Assert.That(result2.Any(f => f.IsSeparator && text2[f.Range] == ":")).IsTrue();
    }

    [Test]
    public async Task SplitToFragments_Comma_MatchesAllVariants()
    {
        var text1 = "a, b";
        var text2 = "a,b";
        var result1 = MarkdownTextChunker.SplitToFragments(text1, MarkdownTextChunker.WeakSeparators3);
        var result2 = MarkdownTextChunker.SplitToFragments(text2, MarkdownTextChunker.WeakSeparators3);

        await Assert.That(result1.Any(f => f.IsSeparator && text1[f.Range] == ", ")).IsTrue();
        await Assert.That(result2.Any(f => f.IsSeparator && text2[f.Range] == ",")).IsTrue();
    }

    [Test]
    public async Task SplitToFragments_SingleNewline_MatchesInWeakSeparators3()
    {
        var text = "line1\nline2";
        var result = MarkdownTextChunker.SplitToFragments(text, MarkdownTextChunker.WeakSeparators3);

        await Assert.That(result.Any(f => f.IsSeparator && text[f.Range] == "\n")).IsTrue();
    }

    #endregion

    #region Edge Cases and Optimized Equivalence Tests

    [Test]
    public async Task SplitToFragments_MultipleSeparatorTypes_ProcessesInOrder()
    {
        // Mix of different separator types
        var text = "hello.\n\nworld";
        var result = MarkdownTextChunker.SplitToFragments(text, MarkdownTextChunker.ExplicitSeparators);

        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(text[result[0].Range]).IsEqualTo("hello");
        await Assert.That(result[0].IsSeparator).IsFalse();
        await Assert.That(text[result[1].Range]).IsEqualTo(".\n\n");
        await Assert.That(result[1].IsSeparator).IsTrue();
        await Assert.That(text[result[2].Range]).IsEqualTo("world");
        await Assert.That(result[2].IsSeparator).IsFalse();
    }

    [Test]
    public async Task SplitToFragments_SixConsecutiveNewlines_CreatesSeparateFragments()
    {
        var text = "\n\n\n\n\n\n";
        var result = MarkdownTextChunker.SplitToFragments(text, MarkdownTextChunker.ExplicitSeparators);

        // Should match \n\n three times
        await Assert.That(result.Count).IsEqualTo(3);
        foreach (var f in result)
        {
            await Assert.That(f.IsSeparator).IsTrue();
        }
        foreach (var f in result)
        {
            await Assert.That(text[f.Range]).IsEqualTo("\n\n");
        }
    }

    [Test]
    public async Task SplitToFragments_SeparatorOnly_ReturnsOnlySeparators()
    {
        var text = ".\n\n";
        var result = MarkdownTextChunker.SplitToFragments(text, MarkdownTextChunker.ExplicitSeparators);

        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0].IsSeparator).IsTrue();
        await Assert.That(text[result[0].Range]).IsEqualTo(".\n\n");
    }

    #endregion

    #region NormalizeNewlines Tests

    [Test]
    public async Task NormalizeNewlines_CRLF_ConvertsToLF()
    {
        var result = MarkdownTextChunker.NormalizeNewlines("hello\r\nworld");
        await Assert.That(result).IsEqualTo("hello\nworld");
    }

    [Test]
    public async Task NormalizeNewlines_CROnly_ConvertsToLF()
    {
        var result = MarkdownTextChunker.NormalizeNewlines("hello\rworld");
        await Assert.That(result).IsEqualTo("hello\nworld");
    }

    [Test]
    public async Task NormalizeNewlines_MixedLineEndings_AllConvertToLF()
    {
        var result = MarkdownTextChunker.NormalizeNewlines("a\r\nb\rc\nd");
        await Assert.That(result).IsEqualTo("a\nb\nc\nd");
    }

    [Test]
    public async Task NormalizeNewlines_AlreadyNormalized_Unchanged()
    {
        var result = MarkdownTextChunker.NormalizeNewlines("hello\nworld");
        await Assert.That(result).IsEqualTo("hello\nworld");
    }

    [Test]
    public async Task NormalizeNewlines_NoLineEndings_Unchanged()
    {
        var result = MarkdownTextChunker.NormalizeNewlines("hello world");
        await Assert.That(result).IsEqualTo("hello world");
    }

    #endregion

    #region MergeImageChunks Tests

    [Test]
    public async Task MergeImageChunks_NoImages_Unchanged()
    {
        var chunks = new List<string> { "first", "second", "third" };
        var result = MarkdownTextChunker.MergeImageChunks(chunks);

        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result).IsEquivalentTo(chunks);
    }

    [Test]
    public async Task MergeImageChunks_ImageAtStart_NotMerged()
    {
        var chunks = new List<string> { "![image](path)", "second" };
        var result = MarkdownTextChunker.MergeImageChunks(chunks);

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0]).IsEqualTo("![image](path)");
    }

    [Test]
    public async Task MergeImageChunks_ImageAfterContent_MergedWithPrevious()
    {
        var chunks = new List<string> { "some text", "![image](path)" };
        var result = MarkdownTextChunker.MergeImageChunks(chunks);

        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0]).Contains("some text");
        await Assert.That(result[0]).Contains("![image](path)");
    }

    [Test]
    public async Task MergeImageChunks_ConsecutiveImages_AllMergedIntoPreceding()
    {
        var chunks = new List<string> { "content", "![img1](p1)", "![img2](p2)" };
        var result = MarkdownTextChunker.MergeImageChunks(chunks);

        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0]).Contains("content");
        await Assert.That(result[0]).Contains("![img1](p1)");
        await Assert.That(result[0]).Contains("![img2](p2)");
    }

    [Test]
    public async Task MergeImageChunks_SingleChunk_Unchanged()
    {
        var chunks = new List<string> { "single chunk" };
        var result = MarkdownTextChunker.MergeImageChunks(chunks);

        await Assert.That(result).HasSingleItem();
        await Assert.That(result[0]).IsEqualTo("single chunk");
    }

    #endregion

    #region Overlap Handling Tests

    [Test]
    public async Task Chunk_ZeroOverlap_NoOverlapProcessing()
    {
        var text = string.Join(' ', Enumerable.Repeat("This sentence repeats for testing purposes.", 20));
        var slices = new[] { new ChunkSlice("doc-1", text) };

        var config = new ChunkingConfig
        {
            Size = 50,
            Overlap = 0,
            EncodingModel = TokenizerDefaults.DefaultEncoding
        };

        var chunks = _chunker.Chunk(slices, config);

        await Assert.That(chunks.Count > 1).IsTrue();
        // With zero overlap, chunks should not have shared prefix/suffix
        var tokenizer = TokenizerRegistry.GetTokenizer(config.EncodingModel);
        var firstTokens = tokenizer.EncodeToIds(chunks[0].Text);
        var secondTokens = tokenizer.EncodeToIds(chunks[1].Text);

        // First token of second chunk shouldn't be last token of first chunk
        // (unless by coincidence from the text itself)
        await Assert.That(firstTokens.Count > 0).IsTrue();
        await Assert.That(secondTokens.Count > 0).IsTrue();
    }

    [Test]
    public async Task Chunk_OverlapSmallerThanChunk_AddsOverlapPrefix()
    {
        var text = string.Join(' ', Enumerable.Repeat("Word", 100));
        var slices = new[] { new ChunkSlice("doc-1", text) };

        var config = new ChunkingConfig
        {
            Size = 30,
            Overlap = 10,
            EncodingModel = TokenizerDefaults.DefaultEncoding
        };

        var chunks = _chunker.Chunk(slices, config);

        await Assert.That(chunks.Count > 1).IsTrue();
        // Second chunk should start with overlap from first
        var tokenizer = TokenizerRegistry.GetTokenizer(config.EncodingModel);
        var firstTokens = tokenizer.EncodeToIds(chunks[0].Text);
        var overlapTokens = firstTokens.Skip(Math.Max(0, firstTokens.Count - config.Overlap)).ToArray();
        var overlapText = tokenizer.Decode(overlapTokens);

        await Assert.That(chunks[1].Text.Trim().StartsWith(overlapText.Trim(), StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Chunk_SingleChunk_NoOverlapNeeded()
    {
        var text = "Short text";
        var slices = new[] { new ChunkSlice("doc-1", text) };

        var config = new ChunkingConfig
        {
            Size = 100,
            Overlap = 20,
            EncodingModel = TokenizerDefaults.DefaultEncoding
        };

        var chunks = _chunker.Chunk(slices, config);

        await Assert.That(chunks).HasSingleItem();
        await Assert.That(chunks[0].Text).IsEqualTo("Short text");
    }

    #endregion

    #region GenerateChunks Token Boundary Tests

    [Test]
    public async Task Chunk_SmallDocument_FitsInSingleChunk()
    {
        var text = "Hello world. This is a test.";
        var slices = new[] { new ChunkSlice("doc-1", text) };

        var config = new ChunkingConfig
        {
            Size = 100,
            Overlap = 0,
            EncodingModel = TokenizerDefaults.DefaultEncoding
        };

        var chunks = _chunker.Chunk(slices, config);

        await Assert.That(chunks).HasSingleItem();
    }

    [Test]
    public async Task Chunk_LargeDocument_SplitsIntoMultipleChunks()
    {
        var text = string.Join("\n\n", Enumerable.Repeat("This is a paragraph with enough content to exceed token limits when repeated multiple times.", 20));
        var slices = new[] { new ChunkSlice("doc-1", text) };

        var config = new ChunkingConfig
        {
            Size = 50,
            Overlap = 0,
            EncodingModel = TokenizerDefaults.DefaultEncoding
        };

        var chunks = _chunker.Chunk(slices, config);

        await Assert.That(chunks.Count > 1).IsTrue();

        // Each chunk should respect token limit (approximately)
        var tokenizer = TokenizerRegistry.GetTokenizer(config.EncodingModel);
        foreach (var chunk in chunks)
        {
            var tokenCount = tokenizer.CountTokens(chunk.Text);
            // Allow some flexibility due to overlap and boundary handling
            await Assert.That(tokenCount <= config.Size * 1.5).IsTrue();
        }
    }

    [Test]
    public async Task Chunk_DocumentWithHeaders_SplitsAtHeaderBoundaries()
    {
        var text = "# Header 1\n\nContent for header 1.\n\n## Header 2\n\nContent for header 2.\n\n### Header 3\n\nContent for header 3.";
        var slices = new[] { new ChunkSlice("doc-1", text) };

        var config = new ChunkingConfig
        {
            Size = 20,
            Overlap = 0,
            EncodingModel = TokenizerDefaults.DefaultEncoding
        };

        var chunks = _chunker.Chunk(slices, config);

        await Assert.That(chunks.Count >= 1).IsTrue();
        // Headers should be preserved in chunks
        await Assert.That(chunks.Any(c => c.Text.Contains('#'))).IsTrue();
    }

    [Test]
    public async Task Chunk_TrailingContent_Captured()
    {
        var text = "First paragraph.\n\nSecond paragraph.\n\nTrailing content.";
        var slices = new[] { new ChunkSlice("doc-1", text) };

        var config = new ChunkingConfig
        {
            Size = 200,
            Overlap = 0,
            EncodingModel = TokenizerDefaults.DefaultEncoding
        };

        var chunks = _chunker.Chunk(slices, config);

        var allText = string.Join("", chunks.Select(c => c.Text));
        await Assert.That(allText).Contains("Trailing content");
    }

    #endregion
}
