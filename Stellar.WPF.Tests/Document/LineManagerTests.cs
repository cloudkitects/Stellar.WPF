namespace Stellar.WPF.Tests.Document;

using Stellar.WPF.Document;

using Document = WPF.Document.Document;

public class LineManagerTests
{
    private readonly Document document = new();

    #region helpers
    void AssertDocumentLinesAre(string expected)
    {
        var lines = expected
            .Replace("\r", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(lines.Length, document.LineCount);
        
        for (var i = 0; i < lines.Length; i++)
        {
            Assert.Equal(lines[i], document.GetText(document.Lines[i]));
        }
    }
    #endregion

    [Fact]
    public void ValidateNewDocumentProperties()
    {
        Assert.Equal(string.Empty, document.Text);
        Assert.Equal(0, document.TextLength);
        Assert.Equal(1, document.LineCount);
    }

    [Fact]
    public void ValidateClearedDocumentMembers()
    {
        document.Text = "Hello,\nWorld.";

        Assert.Equal(2, document.LineCount);
        
        var lines = document.Lines.ToArray();
        
        document.Text = string.Empty;
        
        Assert.Equal(string.Empty, document.Text);
        Assert.Equal(0, document.TextLength);
        Assert.Equal(1, document.LineCount);
        
        Assert.Same(lines[0], document.Lines.Single());
        
        Assert.False(lines[0].IsDeleted);
        Assert.True(lines[1].IsDeleted);
        
        Assert.Null(lines[0].NextLine);
        Assert.Null(lines[1].PreviousLine);
    }

    [Fact]
    public void ValidateEmptyDocumentLine()
    {
        Assert.Equal(1, document.Lines.Count);
        
        var lines = new List<Line>(document.Lines);
        
        Assert.Single(lines);
        
        var line = document.Lines[0];
        
        Assert.Same(line, lines[0]);
        
        Assert.Same(line, document.GetLineByNumber(1));
        Assert.Same(line, document.GetLineByOffset(0));

        Assert.Equal(0, line.Offset);
        Assert.Equal(0, line.EndOffset);
        Assert.Equal(0, line.TextLength);
        Assert.Equal(0, line.SeparatorLength); // last line
        Assert.Equal(0, line.Length); // the last two subtracted, so moot :) 
    }

    [Fact]
    public void IndexesLines()
    {
        var line = document.GetLineByNumber(1);
        
        Assert.Equal(0, document.Lines.IndexOf(line));
        
        var lineFromOtherDocument = new Document().GetLineByNumber(1);
        
        Assert.Equal(-1, document.Lines.IndexOf(lineFromOtherDocument));
        
        document.Text = "a\nb\nc";
        
        var middleLine = document.GetLineByNumber(2);
        
        Assert.Equal(1, document.Lines.IndexOf(middleLine));
        
        document.Remove(1, 3);
        
        Assert.True(middleLine.IsDeleted);
        Assert.Equal(-1, document.Lines.IndexOf(middleLine));
    }

    [Fact]
    public void ValidateLineAfterInsertingText()
    {
        document.Insert(0, "a");

        var line = document.GetLineByNumber(1);

        Assert.Equal("a", document.GetText(line));
    }

    [Fact]
    public void ValidateLineAfterInsertingNothing()
    {
        document.Insert(0, string.Empty);

        var line = document.GetLineByNumber(1);

        Assert.Equal(1, document.LineCount);
        Assert.Equal(0, document.TextLength);

        Assert.Empty(document.GetText(line));
    }

    [Fact]
    public void ValidateLineAfterSettingText()
    {
        document.Text = "a";

        var line = document.GetLineByNumber(1);

        Assert.Equal("a", document.GetText(line));
    }

    [Fact]
    public void ThrowsOnNullArgument()
    {
        Assert.Throws<ArgumentNullException>(() => document.Insert(0, (string)null!));
        Assert.Throws<ArgumentNullException>(() => document.Insert(0, (ITextSource)null!));
        Assert.Throws<ArgumentNullException>(() => document.Text = null!);
    }

    [Fact]
    public void RemovesNothing()
    {
        document.Remove(0, 0);

        Assert.Equal(1, document.LineCount);
        Assert.Equal(0, document.TextLength);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("a\nb", -1)]
    [InlineData("a\nb", 3)]
    public void GetCharAtThrows(string text, int index)
    {
        document.Text = text;

        Assert.Throws<ArgumentOutOfRangeException>(() => document.GetCharAt(index));
    }

    [Theory]
    [InlineData("a\nb", -1)]
    [InlineData("a\nb", 4)]
    public void ThrowsOnOutOfRangeArguments(string text, int index)
    {
        document.Text = text;

        Assert.Throws<ArgumentOutOfRangeException>(() => document.Insert(index, "c"));
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Remove(2, index));
        Assert.Throws<ArgumentOutOfRangeException>(() => document.GetLineByNumber(index));
        Assert.Throws<ArgumentOutOfRangeException>(() => document.GetLineByOffset(index));
    }

    [Theory]
    [InlineData("ai\rb\n", "b", "ai\rb\nb")]
    public void Inserts(string text, string insert, string expected)
    {
        document.Text = text;
        document.Insert(text.Length, insert);

        AssertDocumentLinesAre(expected);
    }

    [Theory]
    [InlineData("ai\rb\nb")]
    public void GetsCharAt(string text)
    {
        document.Text = text;

        var chars = text.ToCharArray();

        for (var i = 0; i < chars.Length; i++)
        {
            Assert.Equal(chars[i], document.GetCharAt(i));
        }
    }

    [Theory]
    [InlineData("mixed\rline\nseparator\r\ntext", 4, 4)]
    public void SeparatesLines(string text, int lineCount, int sepsLength)
    {
        document.Text = text;
        var i = 1;
        var separatorsLength = 0;

        Assert.Equal(lineCount, document.LineCount);

        foreach (var line in document.Lines)
        {
            var lineByNumber = document.GetLineByNumber(i);

            Assert.Equal(line, lineByNumber);
            Assert.Equal(i, line.Number);

            separatorsLength += lineByNumber.SeparatorLength;

            i++;
        }

        Assert.Equal(sepsLength, separatorsLength);
    }

    [Theory]
    [InlineData("mixed\rline\nseparator\r\ntext", 5, 1, "mixedline\nseparator\r\ntext")]
    public void Removes(string text, int offset, int length, string expected)
    {
        document.Text = text;
        document.Remove(offset, length);

        AssertDocumentLinesAre(expected);
    }

    [Theory]
    [InlineData("mixed\rline\nseparator\r\ntext", 20, 1, "s", "mixed\rline\nseparators\ntext")]
    public void Replaces(string text, int offset, int length, string replace, string expected)
    {
        document.Text = text;
        document.Replace(offset, length, replace);

        AssertDocumentLinesAre(expected);
    }

    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(1, 2, 1)]
    [InlineData(1, 6, 5)]
    [InlineData(1, 7, 6)]
    [InlineData(2, 1, 7)]
    [InlineData(2, 7, 13)]
    [InlineData(1, -1, 0)]
    [InlineData(1, -100, 0)]
    [InlineData(2, 0, 7)]
    [InlineData(2, -1, 7)]
    [InlineData(2, -100, 7)]
    [InlineData(1, 8, 6)]
    [InlineData(1, 100, 6)]
    [InlineData(2, 8, 13)]
    [InlineData(2, 1000, 13)]
    public void GetsOffset(int line, int col, int offset)
    {
        document.Text = "Hello,\nworld.";

        Assert.Equal(offset, document.GetOffset(line, col));
    }
}
