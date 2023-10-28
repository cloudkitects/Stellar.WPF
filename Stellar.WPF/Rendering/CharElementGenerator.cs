using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;

using Stellar.WPF.Document;
using Stellar.WPF.Utilities;

namespace Stellar.WPF.Rendering;

/// <summary>
/// Element generator that displays · for spaces and » for tabs and control characters in a box.
/// </summary>
/// <remarks>
/// This element generator is present in every TextView by default; features are enabled via
/// <see cref="TextEditorOptions"/>.
/// </remarks>
sealed class CharElementGenerator : VisualLineGenerator, IElementGenerator
{
    /// <summary>
    /// Whether to show · for spaces.
    /// </summary>
    public bool ShowSpaces { get; set; }

    /// <summary>
    /// Whether to show » for tabs.
    /// </summary>
    public bool ShowTabs { get; set; }

    /// <summary>
    /// Whether to show a box with the hex code for control characters.
    /// </summary>
    public bool ShowControlCharacterBox { get; set; }

    /// <summary>
    /// Creates a new SingleCharacterElementGenerator instance.
    /// </summary>
    public CharElementGenerator()
    {
        ShowSpaces = true;
        ShowTabs = true;
        ShowControlCharacterBox = true;
    }

    void IElementGenerator.FetchOptions(TextEditorOptions options)
    {
        ShowSpaces = options.ShowSpaces;
        ShowTabs = options.ShowTabs;
        ShowControlCharacterBox = options.ShowControlCharacterBox;
    }

    public override int GetFirstInterestedOffset(int startOffset)
    {
        var endLine = Context!.VisualLine.LastLine;
        
        var relevantText = Context.GetText(startOffset, endLine.EndOffset - startOffset);

        for (var i = 0; i < relevantText.Count; i++)
        {
            var token = relevantText.Text[relevantText.Offset + i];

            if ((token == ' ' && ShowSpaces) ||
                (token == '\t' && ShowTabs) ||
                (char.IsControl(token) && ShowControlCharacterBox))
            {
                return startOffset + i;
            }
        }

        return -1;
    }

    public override VisualLineElement ConstructElement(int offset)
    {
        var token = Context!.Document.GetCharAt(offset);

        if (token == ' ' && ShowSpaces)
        {
            return new SpaceTextElement(Context.TextView.cachedElements.GetTextForNonPrintableCharacter("\u00B7", Context));
        }
        
        if (token == '\t' && ShowTabs)
        {
            return new TabTextElement(Context.TextView.cachedElements.GetTextForNonPrintableCharacter("\u00BB", Context));
        }
        
        if (char.IsControl(token) && ShowControlCharacterBox)
        {
            var p = new VisualLineTextRunProperties(Context.GlobalTextRunProperties);

            p.SetForegroundBrush(Brushes.White);
            
            var textFormatter = TextFormatterFactory.Create(Context.TextView);
            var text = FormattedTextElement.PrepareText(textFormatter, token.GetName(), p);
            
            return new SpecialCharacterBoxElement(text);
        }
        
        return null!;
    }

    sealed class SpaceTextElement : FormattedTextElement
    {
        public SpaceTextElement(TextLine textLine) : base(textLine, 1)
        {
            BreakBefore = LineBreakCondition.BreakPossible;
            BreakAfter = LineBreakCondition.BreakDesired;
        }

        public override int GetNextCaretPosition(int visualColumn, LogicalDirection direction, CaretPositioningMode mode)
        {
            if (mode == CaretPositioningMode.Normal || mode == CaretPositioningMode.EveryCodepoint)
            {
                return base.GetNextCaretPosition(visualColumn, direction, mode);
            }
            
            return -1;
        }

        public override bool IsWhitespace(int visualColumn) => true;
    }

    sealed class TabTextElement : VisualLineElement
    {
        internal readonly TextLine text;

        public TabTextElement(TextLine text) : base(2, 1)
        {
            this.text = text;
        }

        public override TextRun CreateTextRun(int startVisualColumn, ITextRunContext context)
        {
            // the TabTextElement consists of two TextRuns:
            // first a TabGlyphRun, then TextCharacters '\t' to let WPF handle the tab indentation
            if (startVisualColumn == VisualColumn)
            {
                return new TabGlyphRun(this, TextRunProperties!);
            }
            
            if (startVisualColumn == VisualColumn + 1)
            {
                return new TextCharacters("\t", 0, 1, TextRunProperties);
            }
            
            throw new ArgumentOutOfRangeException(nameof(startVisualColumn));
        }

        public override int GetNextCaretPosition(int visualColumn, LogicalDirection direction, CaretPositioningMode mode)
        {
            if (mode == CaretPositioningMode.Normal || mode == CaretPositioningMode.EveryCodepoint)
            {
                return base.GetNextCaretPosition(visualColumn, direction, mode);
            }

            return -1;
        }

        public override bool IsWhitespace(int visualColumn) => true;
    }

    sealed class TabGlyphRun : TextEmbeddedObject
    {
        readonly TabTextElement element;
        readonly TextRunProperties properties;

        public TabGlyphRun(TabTextElement element, TextRunProperties properties)
        {
            this.properties = properties ?? throw new ArgumentNullException(nameof(properties));
            this.element = element;
        }

        public override LineBreakCondition BreakBefore => LineBreakCondition.BreakPossible;

        public override LineBreakCondition BreakAfter => LineBreakCondition.BreakRestrained;

        public override bool HasFixedSize => true;

        public override CharacterBufferReference CharacterBufferReference => new();

        public override int Length => 1;

        public override TextRunProperties Properties => properties;

        public override TextEmbeddedObjectMetrics Format(double remainingParagraphWidth) => new(Math.Min(0, element.text.WidthIncludingTrailingWhitespace - 1), element.text.Height, element.text.Baseline);

        public override Rect ComputeBoundingBox(bool rightToLeft, bool sideways) => new(0, 0, Math.Min(0, element.text.WidthIncludingTrailingWhitespace - 1), element.text.Height);

        public override void Draw(DrawingContext drawingContext, Point origin, bool rightToLeft, bool sideways)
        {
            origin.Y -= element.text.Baseline;

            element.text.Draw(drawingContext, origin, InvertAxes.None);
        }
    }

    sealed class SpecialCharacterBoxElement : FormattedTextElement
    {
        public SpecialCharacterBoxElement(TextLine text) : base(text, 1)
        {
        }

        public override TextRun CreateTextRun(int startVisualColumn, ITextRunContext context) => new SpecialCharacterTextRun(this, TextRunProperties!);
    }

    sealed class SpecialCharacterTextRun : FormattedTextRun
    {
        static readonly SolidColorBrush darkGrayBrush;

        static SpecialCharacterTextRun()
        {
            darkGrayBrush = new SolidColorBrush(Color.FromArgb(200, 128, 128, 128));

            darkGrayBrush.Freeze();
        }

        public SpecialCharacterTextRun(FormattedTextElement element, TextRunProperties properties)
            : base(element, properties)
        {
        }

        public override void Draw(DrawingContext drawingContext, Point origin, bool rightToLeft, bool sideways)
        {
            var newOrigin = new Point(origin.X + 1.5, origin.Y);
            var metrics = base.Format(double.PositiveInfinity);
            var rect = new Rect(newOrigin.X - 0.5, newOrigin.Y - metrics.Baseline, metrics.Width + 2, metrics.Height);
            
            drawingContext.DrawRoundedRectangle(darkGrayBrush, null, rect, 2.5, 2.5);
            
            base.Draw(drawingContext, newOrigin, rightToLeft, sideways);
        }

        public override TextEmbeddedObjectMetrics Format(double remainingParagraphWidth)
        {
            var metrics = base.Format(remainingParagraphWidth);

            return new TextEmbeddedObjectMetrics(metrics.Width + 3, metrics.Height, metrics.Baseline);
        }

        public override Rect ComputeBoundingBox(bool rightToLeft, bool sideways)
        {
            var rect = base.ComputeBoundingBox(rightToLeft, sideways);
            
            rect.Width += 3;
            
            return rect;
        }
    }
}
