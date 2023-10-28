using System;
using System.Windows.Media.TextFormatting;
using System.Windows.Media;
using System.Windows;

using Stellar.WPF.Utilities;

namespace Stellar.WPF.Rendering;

/// <summary>
/// Formatted text (as opposed to normal document text), a base class for
/// various VisualLineElements like newline markers or collapsed folding sections.
/// </summary>
public class FormattedTextElement : VisualLineElement
{
    internal readonly FormattedText? formattedText;
    internal string? text;
    internal TextLine? textLine;

    /// <summary>
    /// Creates a new FormattedTextElement that displays the specified text
    /// and occupies the specified length in the document.
    /// </summary>
    public FormattedTextElement(string text, int documentLength) : base(1, documentLength)
    {
        this.text = text ?? throw new ArgumentNullException(nameof(text));
        
        BreakBefore = LineBreakCondition.BreakPossible;
        BreakAfter = LineBreakCondition.BreakPossible;
    }

    /// <summary>
    /// Creates a new FormattedTextElement that displays the specified text
    /// and occupies the specified length in the document.
    /// </summary>
    public FormattedTextElement(TextLine text, int documentLength) : base(1, documentLength)
    {
        textLine = text ?? throw new ArgumentNullException(nameof(text));
        BreakBefore = LineBreakCondition.BreakPossible;
        BreakAfter = LineBreakCondition.BreakPossible;
    }

    /// <summary>
    /// Creates a new FormattedTextElement that displays the specified text
    /// and occupies the specified length in the document.
    /// </summary>
    public FormattedTextElement(FormattedText text, int documentLength) : base(1, documentLength)
    {
        formattedText = text ?? throw new ArgumentNullException(nameof(text));
        BreakBefore = LineBreakCondition.BreakPossible;
        BreakAfter = LineBreakCondition.BreakPossible;
    }

    /// <summary>
    /// Gets/sets the line break condition before the element.
    /// The default is 'BreakPossible'.
    /// </summary>
    public LineBreakCondition BreakBefore { get; set; }

    /// <summary>
    /// Gets/sets the line break condition after the element.
    /// The default is 'BreakPossible'.
    /// </summary>
    public LineBreakCondition BreakAfter { get; set; }

    /// <inheritdoc/>
    public override TextRun CreateTextRun(int startVisualColumn, ITextRunContext context)
    {
        if (textLine == null)
        {
            var formatter = TextFormatterFactory.Create(context.TextView);
            
            textLine = PrepareText(formatter, text!, TextRunProperties!);
            
            text = null!;
        }
        
        return new FormattedTextRun(this, TextRunProperties!);
    }

    /// <summary>
    /// Constructs a TextLine from a simple text.
    /// </summary>
    public static TextLine PrepareText(TextFormatter formatter, string text, TextRunProperties properties)
    {
        return (formatter ?? throw new ArgumentNullException(nameof(formatter)))
            .FormatLine(
                new SimpleTextSource(
                    text ?? throw new ArgumentNullException(nameof(text)),
                    properties ?? throw new ArgumentNullException(nameof(properties))),
                0,
                32000,
                new VisualLineTextParagraphProperties
                {
                    defaultTextRunProperties = properties,
                    textWrapping = TextWrapping.NoWrap,
                    tabSize = 40
                },
                null);
    }
}
