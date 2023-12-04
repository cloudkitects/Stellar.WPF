using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

using Stellar.WPF.Document;
using Stellar.WPF.Rendering;
using Stellar.WPF.Utilities;

namespace Stellar.WPF.Editing
{
    /// <summary>
    /// Helper class with caret-related methods.
    /// </summary>
    public sealed class Caret
    {
        private readonly TextArea textArea;
        private readonly TextView textView;
        private readonly CaretLayer caretAdorner;
        private bool visible;

        internal Caret(TextArea textArea)
        {
            this.textArea = textArea;
            textView = textArea.TextView;
            position = new TextViewPosition(1, 1, 0);

            caretAdorner = new CaretLayer(textArea);
            textView.InsertLayer(caretAdorner, KnownLayer.Caret, LayerInsertionPosition.Replace);
            textView.VisualLinesChanged += TextView_VisualLinesChanged;
            textView.ScrollOffsetChanged += TextView_ScrollOffsetChanged;
        }

        internal void UpdateIfVisible()
        {
            if (visible)
            {
                Show();
            }
        }

        private void TextView_VisualLinesChanged(object sender, EventArgs e)
        {
            if (visible)
            {
                Show();
            }
            // required because the visual columns might have changed if the
            // element generators did something differently than on the last run
            // (e.g. a FoldingSection was collapsed)
            InvalidateVisualColumn();
        }

        private void TextView_ScrollOffsetChanged(object sender, EventArgs e)
        {
            caretAdorner?.InvalidateVisual();
        }

        private double desiredXPos = double.NaN;
        private TextViewPosition position;

        /// <summary>
        /// Gets/Sets the position of the caret.
        /// Retrieving this property will validate the visual column (which can be expensive).
        /// Use the <see cref="Location"/> property instead if you don't need the visual column.
        /// </summary>
        public TextViewPosition Position
        {
            get
            {
                ValidateVisualColumn();
                return position;
            }
            set
            {
                if (position != value)
                {
                    position = value;

                    storedCaretOffset = -1;

                    ValidatePosition();
                    
                    InvalidateVisualColumn();
                    
                    RaisePositionChanged();
                    
                    Log("Caret position changed to " + value);
                    
                    if (visible)
                    {
                        Show();
                    }
                }
            }
        }

        /// <summary>
        /// Gets the caret position without validating it.
        /// </summary>
        internal TextViewPosition NonValidatedPosition
        {
            get
            {
                return position;
            }
        }

        /// <summary>
        /// Gets/Sets the location of the caret.
        /// The getter of this property is faster than <see cref="Position"/> because it doesn't have
        /// to validate the visual column.
        /// </summary>
        public Location Location
        {
            get => position.Location;
            set => Position = new TextViewPosition(value);
        }

        /// <summary>
        /// Gets/Sets the caret line.
        /// </summary>
        public int Line
        {
            get => position.Line;
            set => Position = new TextViewPosition(value, position.Column);
        }

        /// <summary>
        /// Gets/Sets the caret column.
        /// </summary>
        public int Column
        {
            get => position.Column;
            set => Position = new TextViewPosition(position.Line, value);
        }

        /// <summary>
        /// Gets/Sets the caret visual column.
        /// </summary>
        public int VisualColumn
        {
            get
            {
                ValidateVisualColumn();

                return position.VisualColumn;
            }
            set => Position = new TextViewPosition(position.Line, position.Column, value);
        }

        private bool isInVirtualSpace;

        /// <summary>
        /// Gets whether the caret is in virtual space.
        /// </summary>
        public bool IsInVirtualSpace
        {
            get
            {
                ValidateVisualColumn();

                return isInVirtualSpace;
            }
        }

        private int storedCaretOffset;

        internal void OnDocumentChanging()
        {
            storedCaretOffset = Offset;
            
            InvalidateVisualColumn();
        }

        internal void OnDocumentChanged(DocumentChangeEventArgs e)
        {
            InvalidateVisualColumn();
            
            if (storedCaretOffset >= 0)
            {
                // If the caret is at the end of a selection, we don't expand the selection if something
                // is inserted at the end. Thus we also need to keep the caret in front of the insertion.
                AnchorMovementType caretMovementType = !textArea.Selection.IsEmpty && storedCaretOffset == textArea.Selection.SurroundingSegment.EndOffset
                    ? AnchorMovementType.BeforeInsertion
                    : AnchorMovementType.Default;
                var newCaretOffset = e.ComputeOffset(storedCaretOffset, caretMovementType);
                var document = textArea.Document;
                
                if (document is not null)
                {
                    // keep visual column
                    Position = new TextViewPosition(document.GetLocation(newCaretOffset), position.VisualColumn);
                }
            }
            storedCaretOffset = -1;
        }

        /// <summary>
        /// Gets/Sets the caret offset.
        /// Setting the caret offset has the side effect of setting the <see cref="DesiredXPos"/> to NaN.
        /// </summary>
        public int Offset
        {
            get
            {
                var document = textArea.Document;

                return document is null
                    ? 0
                    : document.GetOffset(position.Location);
            }
            set
            {
                var document = textArea.Document;
                
                if (document is not null)
                {
                    Position = new TextViewPosition(document.GetLocation(value));
                    DesiredXPos = double.NaN;
                }
            }
        }

        /// <summary>
        /// Gets/Sets the desired x-position of the caret, in device-independent pixels.
        /// This property is NaN if the caret has no desired position.
        /// </summary>
        public double DesiredXPos
        {
            get => desiredXPos;
            set => desiredXPos = value;
        }

        private void ValidatePosition()
        {
            if (position.Line < 1)
            {
                position.Line = 1;
            }

            if (position.Column < 1)
            {
                position.Column = 1;
            }

            if (position.VisualColumn < -1)
            {
                position.VisualColumn = -1;
            }

            var document = textArea.Document;
            if (document is not null)
            {
                if (position.Line > document.LineCount)
                {
                    position.Line = document.LineCount;
                    position.Column = document.GetLineByNumber(position.Line).Length + 1;
                    position.VisualColumn = -1;
                }
                else
                {
                    var line = document.GetLineByNumber(position.Line);

                    if (position.Column > line.Length + 1)
                    {
                        position.Column = line.Length + 1;
                        position.VisualColumn = -1;
                    }
                }
            }
        }

        /// <summary>
        /// Event raised when the caret position has changed.
        /// If the caret position is changed inside a document update (between BeginUpdate/EndUpdate calls),
        /// the PositionChanged event is raised only once at the end of the document update.
        /// </summary>
        public event EventHandler PositionChanged;

        private bool raisePositionChangedOnUpdateFinished;

        private void RaisePositionChanged()
        {
            if (textArea.Document is not null && textArea.Document.IsUpdating)
            {
                raisePositionChangedOnUpdateFinished = true;
            }
            else
            {
                if (PositionChanged is not null)
                {
                    PositionChanged(this, EventArgs.Empty);
                }
            }
        }

        internal void OnDocumentUpdateFinished()
        {
            if (raisePositionChangedOnUpdateFinished)
            {
                if (PositionChanged is not null)
                {
                    PositionChanged(this, EventArgs.Empty);
                }
            }
        }

        private bool visualColumnValid;

        private void ValidateVisualColumn()
        {
            if (!visualColumnValid)
            {
                var document = textArea.Document;
                if (document is not null)
                {
                    Debug.WriteLine("Explicit validation of caret column");
                    var documentLine = document.GetLineByNumber(position.Line);
                    RevalidateVisualColumn(textView.GetOrConstructVisualLine(documentLine));
                }
            }
        }

        private void InvalidateVisualColumn()
        {
            visualColumnValid = false;
        }

        /// <summary>
        /// Validates the visual column of the caret using the specified visual line.
        /// The visual line must contain the caret offset.
        /// </summary>
        private void RevalidateVisualColumn(VisualLine visualLine)
        {
            if (visualLine == null)
            {
                throw new ArgumentNullException("visualLine");
            }

            // mark column as validated
            visualColumnValid = true;

            var caretOffset = textView.Document.GetOffset(position.Location);
            var firstDocumentLineOffset = visualLine.FirstLine.Offset;

            position.VisualColumn = visualLine.ValidateVisualColumn(position, textArea.Selection.EnableVirtualSpace);

            // search possible caret positions
            var newVisualColumnForwards = visualLine.GetNextCaretPosition(position.VisualColumn - 1, LogicalDirection.Forward, CaretPositioningMode.Normal, textArea.Selection.EnableVirtualSpace);
            
            // If position.VisualColumn was valid, we're done with validation.
            if (newVisualColumnForwards != position.VisualColumn)
            {
                // also search backwards so that we can pick the better match
                int newVisualColumnBackwards = visualLine.GetNextCaretPosition(position.VisualColumn + 1, LogicalDirection.Backward, CaretPositioningMode.Normal, textArea.Selection.EnableVirtualSpace);

                if (newVisualColumnForwards < 0 && newVisualColumnBackwards < 0)
                {
                    throw new InvalidOperationException("Invalid caret position");
                }

                // determine offsets for new visual column positions
                var newOffsetForwards = newVisualColumnForwards >= 0
                    ? visualLine.GetRelativeOffset(newVisualColumnForwards) + firstDocumentLineOffset
                    : -1;

                var newOffsetBackwards = newVisualColumnBackwards >= 0
                    ? visualLine.GetRelativeOffset(newVisualColumnBackwards) + firstDocumentLineOffset
                    : -1;

                int newVisualColumn, newOffset;
                
                // if there's only one valid position, use it
                if (newVisualColumnForwards < 0)
                {
                    newVisualColumn = newVisualColumnBackwards;
                    newOffset = newOffsetBackwards;
                }
                else if (newVisualColumnBackwards < 0)
                {
                    newVisualColumn = newVisualColumnForwards;
                    newOffset = newOffsetForwards;
                }
                else
                {
                    // two valid positions: find the better match
                    if (Math.Abs(newOffsetBackwards - caretOffset) < Math.Abs(newOffsetForwards - caretOffset))
                    {
                        // backwards is better
                        newVisualColumn = newVisualColumnBackwards;
                        newOffset = newOffsetBackwards;
                    }
                    else
                    {
                        // forwards is better
                        newVisualColumn = newVisualColumnForwards;
                        newOffset = newOffsetForwards;
                    }
                }

                Position = new TextViewPosition(textView.Document.GetLocation(newOffset), newVisualColumn);
            }

            isInVirtualSpace = (position.VisualColumn > visualLine.VisualLength);
        }

        private Rect CalcCaretRectangle(VisualLine visualLine)
        {
            if (!visualColumnValid)
            {
                RevalidateVisualColumn(visualLine);
            }

            var textLine = visualLine.GetTextLine(position.VisualColumn, position.IsAtEndOfLine);

            var x = visualLine.GetTextLineVisualXPosition(textLine, position.VisualColumn);
            var y = visualLine.GetTextLineVisualYPosition(textLine, VisualYPosition.TextTop);
            var h = visualLine.GetTextLineVisualYPosition(textLine, VisualYPosition.TextBottom) - y;
            var w = SystemParameters.CaretWidth;

            return new Rect(x, y, w, h);
        }

        private Rect CalcCaretOverstrikeRectangle(VisualLine visualLine)
        {
            if (!visualColumnValid)
            {
                RevalidateVisualColumn(visualLine);
            }

            var currentPos = position.VisualColumn;
            
            // The text being overwritten in overstrike mode is everything up to the next normal caret stop
            var nextPos = visualLine.GetNextCaretPosition(currentPos, LogicalDirection.Forward, CaretPositioningMode.Normal, true);

            var textLine = visualLine.GetTextLine(currentPos);

            Rect r;
            
            if (currentPos < visualLine.VisualLength)
            {
                // If the caret is within the text, use GetTextBounds() for the text being overwritten.
                // This is necessary to ensure the rectangle is calculated correctly in bidirectional text.
                var textBounds = textLine.GetTextBounds(currentPos, nextPos - currentPos)[0];
                
                r = textBounds.Rectangle;
                
                r.Y += visualLine.GetTextLineVisualYPosition(textLine, VisualYPosition.Top);
            }
            else
            {
                // If the caret is at the end of the line (or in virtual space),
                // use the visual X position of currentPos and nextPos (one or more of which will be in virtual space)
                var x = visualLine.GetTextLineVisualXPosition(textLine, currentPos);
                var w = visualLine.GetTextLineVisualXPosition(textLine, nextPos) - x;
                var y = visualLine.GetTextLineVisualYPosition(textLine, VisualYPosition.TextTop);
                var h = visualLine.GetTextLineVisualYPosition(textLine, VisualYPosition.TextBottom) - y;

                r = new Rect(x, y, w, h);
            }
            
            // ensure the caret is visible if it is too small (e.g., when in front of a zero-width character)
            r.Width = Math.Max(r.Width, SystemParameters.CaretWidth);
            
            return r;
        }

        /// <summary>
        /// Returns the caret rectangle. The coordinate system is in device-independent pixels from the top of the document.
        /// </summary>
        public Rect CalculateCaretRectangle()
        {
            if (textView is not null && textView.Document is not null)
            {
                var visualLine = textView.GetOrConstructVisualLine(textView.Document.GetLineByNumber(position.Line));

                return textArea.OverstrikeMode
                    ? CalcCaretOverstrikeRectangle(visualLine)
                    : CalcCaretRectangle(visualLine);
            }
            
            return Rect.Empty;
        }

        /// <summary>
        /// Minimum distance of the caret to the view border.
        /// </summary>
        internal const double MinimumDistanceToViewBorder = 30;

        /// <summary>
        /// Scrolls the text view so that the caret is visible.
        /// </summary>
        public void BringCaretToView()
        {
            BringCaretToView(MinimumDistanceToViewBorder);
        }

        internal void BringCaretToView(double border)
        {
            var rect = CalculateCaretRectangle();

            if (!rect.IsEmpty)
            {
                rect.Inflate(border, border);
                
                textView.MakeVisible(rect);
            }
        }

        /// <summary>
        /// Makes the caret visible and updates its on-screen position.
        /// </summary>
        public void Show()
        {
            Log("Caret.Show()");
            
            visible = true;
            
            if (!showScheduled)
            {
                showScheduled = true;
                textArea.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(ShowInternal));
            }
        }

        private bool showScheduled;
        private bool hasWin32Caret;

        private void ShowInternal()
        {
            showScheduled = false;

            // if show was scheduled but caret hidden in the meantime
            if (!visible)
            {
                return;
            }

            if (caretAdorner is not null && textView is not null)
            {
                var visualLine = textView!.GetVisualLine(position.Line);

                if (visualLine is not null)
                {
                    var rect = textArea.OverstrikeMode
                        ? CalcCaretOverstrikeRectangle(visualLine)
                        : CalcCaretRectangle(visualLine);

                    // Create Win32 caret so that Windows knows where our managed caret is. This is necessary for
                    // features like 'Follow text editing' in the Windows Magnifier.
                    if (!hasWin32Caret)
                    {
                        hasWin32Caret = Win32.CreateCaret(textView, rect.Size);
                    }
                    if (hasWin32Caret)
                    {
                        Win32.SetCaretPosition(textView, rect.Location - textView.ScrollOffset);
                    }
                    
                    caretAdorner.Show(rect);
                    textArea.ime.UpdateCompositionWindow();
                }
                else
                {
                    caretAdorner.Hide();
                }
            }
        }

        /// <summary>
        /// Makes the caret invisible.
        /// </summary>
        public void Hide()
        {
            Log("Caret.Hide()");

            visible = false;
            
            if (hasWin32Caret)
            {
                Win32.DestroyCaret();
                hasWin32Caret = false;
            }
            
            caretAdorner?.Hide();
        }

        [Conditional("DEBUG")]
        private static void Log(string text)
        {
            // commented out to make debug output less noisy - add back if there are any problems with the caret
            //Debug.WriteLine(text);
        }

        /// <summary>
        /// Gets/Sets the color of the caret.
        /// </summary>
        public Brush CaretBrush
        {
            get => caretAdorner.CaretBrush;
            set => caretAdorner.CaretBrush = value;
        }
    }
}
