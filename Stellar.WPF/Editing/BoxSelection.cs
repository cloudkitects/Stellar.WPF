using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;

using Stellar.WPF.Document;

namespace Stellar.WPF.Editing
{
    /// <summary>
    /// Rectangular (box) selection.
    /// </summary>
    public sealed class BoxSelection : Selection
    {
        #region Commands
        /// <summary>
        /// Expands the selection left by one character, creating a rectangular selection.
        /// Key gesture: Alt+Shift+Left
        /// </summary>
        public static readonly RoutedUICommand LeftByCharacter = Command("BoxSelectLeftByCharacter");

        /// <summary>
        /// Expands the selection right by one character, creating a rectangular selection.
        /// Key gesture: Alt+Shift+Right
        /// </summary>
        public static readonly RoutedUICommand RightByCharacter = Command("BoxSelectRightByCharacter");

        /// <summary>
        /// Expands the selection left by one word, creating a rectangular selection.
        /// Key gesture: Ctrl+Alt+Shift+Left
        /// </summary>
        public static readonly RoutedUICommand LeftByWord = Command("BoxSelectLeftByWord");

        /// <summary>
        /// Expands the selection right by one word, creating a rectangular selection.
        /// Key gesture: Ctrl+Alt+Shift+Right
        /// </summary>
        public static readonly RoutedUICommand RightByWord = Command("BoxSelectRightByWord");

        /// <summary>
        /// Expands the selection up by one line, creating a rectangular selection.
        /// Key gesture: Alt+Shift+Up
        /// </summary>
        public static readonly RoutedUICommand UpByLine = Command("BoxSelectUpByLine");

        /// <summary>
        /// Expands the selection down by one line, creating a rectangular selection.
        /// Key gesture: Alt+Shift+Down
        /// </summary>
        public static readonly RoutedUICommand DownByLine = Command("BoxSelectDownByLine");

        /// <summary>
        /// Expands the selection to the start of the line, creating a rectangular selection.
        /// Key gesture: Alt+Shift+Home
        /// </summary>
        public static readonly RoutedUICommand ToLineStart = Command("BoxSelectToLineStart");

        /// <summary>
        /// Expands the selection to the end of the line, creating a rectangular selection.
        /// Key gesture: Alt+Shift+End
        /// </summary>
        public static readonly RoutedUICommand ToLineEnd = Command("BoxSelectToLineEnd");

        private static RoutedUICommand Command(string name) => new(name, name, typeof(BoxSelection));
        #endregion

        #region fields and props
        private Document.Document? document;

        /// <summary>
        /// start,end instances
        /// </summary>
        private readonly int sLine, eLine;
        private readonly double sX, eX;
        private readonly int TLOffset, BROffset;
        private readonly TextViewPosition sPos, ePos;
        
        private readonly List<SelectionSegment> segments = new();
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new rectangular selection.
        /// </summary>
        public BoxSelection(TextArea textArea, TextViewPosition start, TextViewPosition end)
            : base(textArea)
        {
            InitDocument();

            sLine = start.Line;
            eLine = end.Line;
            sX = GetXPos(textArea, start);
            eX = GetXPos(textArea, end);
            
            CalculateSegments();
            
            TLOffset = segments.First().StartOffset;
            BROffset = segments.Last().EndOffset;

            this.sPos = start;
            this.ePos = end;
        }

        private BoxSelection(TextArea textArea, int startLine, double startXPos, TextViewPosition end)
            : base(textArea)
        {
            InitDocument();
            
            this.sLine = startLine;
            
            eLine = end.Line;
            
            this.sX = startXPos;
            
            eX = GetXPos(textArea, end);
            
            CalculateSegments();
            
            TLOffset = segments.First().StartOffset;
            BROffset = segments.Last().EndOffset;

            sPos = GetStart();
            
            this.ePos = end;
        }

        private BoxSelection(TextArea textArea, TextViewPosition start, int endLine, double endXPos)
            : base(textArea)
        {
            InitDocument();
            
            sLine = start.Line;
            
            this.eLine = endLine;
            
            sX = GetXPos(textArea, start);
            
            this.eX = endXPos;
            
            CalculateSegments();
            
            TLOffset = segments.First().StartOffset;
            BROffset = segments.Last().EndOffset;

            this.sPos = start;
            
            ePos = GetEnd();
        }

        private void InitDocument() => document = textArea.Document ?? throw new InvalidOperationException("The text area document is null");

        private static double GetXPos(TextArea textArea, TextViewPosition pos)
        {
            var documentLine = textArea.Document.GetLineByNumber(pos.Line);
            var visualLine = textArea.TextView.GetOrConstructVisualLine(documentLine);
            var column = visualLine.ValidateVisualColumn(pos, true);
            var textLine = visualLine.GetTextLine(column, pos.IsAtEndOfLine);
            
            return visualLine.GetTextLineVisualXPosition(textLine, column);
        }

        private void CalculateSegments()
        {
            var nextLine = document?.GetLineByNumber(Math.Min(sLine, eLine));

            do
            {
                var line = textArea.TextView.GetOrConstructVisualLine(nextLine!);

                var sCol = line.GetVisualColumn(new Point(sX, 0), true);
                var eCol = line.GetVisualColumn(new Point(eX, 0), true);
                
                var baseOffset = line.FirstLine.Offset;
                
                var sOffset = baseOffset + line.GetRelativeOffset(sCol);
                var eOffset = baseOffset + line.GetRelativeOffset(eCol);
                
                segments.Add(new SelectionSegment(sOffset, sCol, eOffset, eCol));

                nextLine = line.LastLine.NextLine;
            }
            while (nextLine is not null && nextLine.Number <= Math.Max(sLine, eLine));
        }

        private TextViewPosition GetStart()
        {
            var segment = (sLine < eLine ? segments.First() : segments.Last());

            return sX < eX
                ? new TextViewPosition(document!.GetLocation(segment.StartOffset), segment.StartVisualColumn)
                : new TextViewPosition(document!.GetLocation(segment.EndOffset), segment.EndVisualColumn);
        }

        private TextViewPosition GetEnd()
        {
            var segment = (sLine < eLine ? segments.Last() : segments.First());

            return sX < eX
                ? new TextViewPosition(document!.GetLocation(segment.EndOffset), segment.EndVisualColumn)
                : new TextViewPosition(document!.GetLocation(segment.StartOffset), segment.StartVisualColumn);
        }
        #endregion

        /// <inheritdoc/>
        public override string GetText()
        {
            var b = new StringBuilder();

            foreach (var segment in Segments)
            {
                if (b.Length > 0)
                {
                    b.AppendLine();
                }

                b.Append(document!.GetText(segment));
            }

            return b.ToString();
        }

        /// <inheritdoc/>
        public override Selection StartSelectionOrSetEndpoint(TextViewPosition startPosition, TextViewPosition endPosition) => SetEndpoint(endPosition);

        /// <inheritdoc/>
        public override int Length => Segments.Sum(s => s.Length);

        /// <inheritdoc/>
        public override bool EnableVirtualSpace => true;

        /// <inheritdoc/>
        public override ISegment SurroundingSegment => new SimpleSegment(TLOffset, BROffset - TLOffset);

        /// <inheritdoc/>
        public override IEnumerable<SelectionSegment> Segments => segments;

        /// <inheritdoc/>
        public override TextViewPosition StartPosition => sPos;

        /// <inheritdoc/>
        public override TextViewPosition EndPosition => ePos;

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is BoxSelection other &&
                other.textArea == textArea &&
                other.TLOffset == TLOffset && other.BROffset == BROffset &&
                other.sLine == sLine && other.eLine == eLine &&
                other.sX == sX && other.eX == eX;
        }

        /// <inheritdoc/>
        public override int GetHashCode() => TLOffset ^ BROffset;

        /// <inheritdoc/>
        public override Selection SetEndpoint(TextViewPosition endPosition) => new BoxSelection(textArea, sLine, sX, endPosition);

        private int GetVisualColumnFromXPos(int number, double x)
        {
            var line = textArea.TextView.GetOrConstructVisualLine(textArea.Document.GetLineByNumber(number));
            
            return line.GetVisualColumn(new Point(x, 0), true);
        }

        /// <inheritdoc/>
        public override Selection UpdateOnDocumentChange(DocumentChangeEventArgs e)
        {
            var sLocation = textArea.Document.GetLocation(e.ComputeOffset(TLOffset, AnchorMovementType.AfterInsertion));
            var eLocation = textArea.Document.GetLocation(e.ComputeOffset(BROffset, AnchorMovementType.BeforeInsertion));

            return new BoxSelection(textArea,
                new TextViewPosition(sLocation, GetVisualColumnFromXPos(sLocation.Line, sX)),
                new TextViewPosition(eLocation, GetVisualColumnFromXPos(eLocation.Line, eX)));
        }

        /// <inheritdoc/>
        public override void ReplaceSelectionWithText(string text)
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            using (textArea.Document.RunUpdate())
            {
                var start = new TextViewPosition(document!.GetLocation(TLOffset), GetVisualColumnFromXPos(sLine, sX));
                var end = new TextViewPosition(document.GetLocation(BROffset), GetVisualColumnFromXPos(eLine, eX));
                
                int insertionLength;
                int totalInsertionLength = 0;
                int firstInsertionLength = 0;
                int editOffset = Math.Min(TLOffset, BROffset);
                
                TextViewPosition pos;
                
                if (NewLineFinder.Next(text, 0) == SimpleSegment.Invalid)
                {
                    // insert same text into every line
                    foreach (var segment in Segments.Reverse())
                    {
                        ReplaceSingleLineText(textArea, segment, text, out insertionLength);
                        
                        totalInsertionLength += insertionLength;
                        firstInsertionLength  = insertionLength;
                    }

                    pos = new TextViewPosition(document.GetLocation(editOffset + firstInsertionLength));

                    textArea.Selection = new BoxSelection(textArea, pos, Math.Max(sLine, eLine), GetXPos(textArea, pos));
                }
                else
                {
                    var lines = text.Split(NewLineFinder.NewlineStrings, segments.Count, StringSplitOptions.None);
                    
                    for (var i = lines.Length - 1; i >= 0; i--)
                    {
                        ReplaceSingleLineText(textArea, segments[i], lines[i], out insertionLength);
                        
                        firstInsertionLength = insertionLength;
                    }

                    pos = new TextViewPosition(document.GetLocation(editOffset + firstInsertionLength));
                    
                    textArea.ClearSelection();
                }

                textArea.Caret.Position = textArea.TextView.GetPosition(new Point(GetXPos(textArea, pos), textArea.TextView.GetVisualTopByDocumentLine(Math.Max(sLine, eLine)))).GetValueOrDefault();
            }
        }

        private void ReplaceSingleLineText(TextArea textArea, SelectionSegment lineSegment, string newText, out int insertionLength)
        {
            if (lineSegment.Length == 0)
            {
                if (newText.Length > 0 && textArea.EditableSectionProvider.CanInsert(lineSegment.StartOffset))
                {
                    newText = AddSpacesIfRequired(newText, new TextViewPosition(document!.GetLocation(lineSegment.StartOffset), lineSegment.StartVisualColumn), new TextViewPosition(document.GetLocation(lineSegment.EndOffset), lineSegment.EndVisualColumn));

                    textArea.Document.Insert(lineSegment.StartOffset, newText);
                }
            }
            else
            {
                var segmentsToDelete = textArea.GetDeletableSegments(lineSegment);

                for (var i = segmentsToDelete.Length - 1; i >= 0; i--)
                {
                    if (i == segmentsToDelete.Length - 1)
                    {
                        if (segmentsToDelete[i].Offset == SurroundingSegment.Offset && segmentsToDelete[i].Length == SurroundingSegment.Length)
                        {
                            newText = AddSpacesIfRequired(newText, new TextViewPosition(document!.GetLocation(lineSegment.StartOffset), lineSegment.StartVisualColumn), new TextViewPosition(document.GetLocation(lineSegment.EndOffset), lineSegment.EndVisualColumn));
                        }

                        textArea.Document.Replace(segmentsToDelete[i], newText);
                    }
                    else
                    {
                        textArea.Document.Remove(segmentsToDelete[i]);
                    }
                }
            }

            insertionLength = newText.Length;
        }

        /// <summary>
        /// Performs a rectangular paste operation.
        /// </summary>
        public static bool PerformRectangularPaste(TextArea textArea, TextViewPosition startPosition, string text, bool selectInsertedText)
        {
            if (textArea is null)
            {
                throw new ArgumentNullException(nameof(textArea));
            }

            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var newLineCount = text.Count(c => c == '\n'); // might not work in all cases, but single \r line endings are really rare today

            var endLocation = new Location(startPosition.Line + newLineCount, startPosition.Column);

            if (endLocation.Line <= textArea.Document.LineCount)
            {
                var endOffset = textArea.Document.GetOffset(endLocation);

                if (textArea.Selection.EnableVirtualSpace || textArea.Document.GetLocation(endOffset) == endLocation)
                {
                    BoxSelection rsel = new(textArea, startPosition, endLocation.Line, GetXPos(textArea, startPosition));
                    
                    rsel.ReplaceSelectionWithText(text);
                    
                    if (selectInsertedText && textArea.Selection is BoxSelection selection)
                    {
                        BoxSelection sel = selection;
                        textArea.Selection = new BoxSelection(textArea, startPosition, sel.eLine, sel.eX);
                    }
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets the name of the entry in the DataObject that signals rectangle selections.
        /// </summary>
        public const string BoxSelectionDataType = "BoxSelection";

        /// <inheritdoc/>
        public override DataObject CreateDataObject(TextArea textArea)
        {
            var data = base.CreateDataObject(textArea);

            if (EditingCommandHandler.ConfirmDataFormat(textArea, data, BoxSelectionDataType))
            {
                var isRectangle = new MemoryStream(1);
                
                isRectangle.WriteByte(1);
                
                data.SetData(BoxSelectionDataType, isRectangle, false);
            }

            return data;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// It's possible that ToString() gets called on old (invalid) selections, e.g. for "change from... to..." debug message
        /// make sure we don't crash even when the desired locations don't exist anymore.
        /// </remarks>
        public override string ToString() => $"[BoxSelection {sLine} {TLOffset} {sX} to {eLine} {BROffset} {eX}]";
    }
}
