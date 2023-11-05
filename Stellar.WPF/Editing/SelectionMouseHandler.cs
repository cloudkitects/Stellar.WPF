using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;

using Stellar.WPF.Document;
using Stellar.WPF.Rendering;
using Stellar.WPF.Utilities;

namespace Stellar.WPF.Editing
{
    /// <summary>
    /// Handles selection of text using the mouse.
    /// </summary>
    internal sealed class SelectionMouseHandler : ITextAreaInputHandler
    {
        private readonly TextArea textArea;
        private MouseSelectionMode mode;
        private AnchorSegment? startWord;
        private Point possibleDragStartMousePos;

        #region Constructor + Attach + Detach
        internal SelectionMouseHandler(TextArea textArea)
        {
            this.textArea = textArea ?? throw new ArgumentNullException(nameof(textArea));
        }

        static SelectionMouseHandler()
        {
            EventManager.RegisterClassHandler(typeof(TextArea), Mouse.LostMouseCaptureEvent, new MouseEventHandler(OnLostMouseCapture));
        }

        private static void OnLostMouseCapture(object sender, MouseEventArgs e)
        {
            var textArea = (TextArea)sender;

            if (Mouse.Captured != textArea)
            {
                if (textArea.DefaultInputHandler.MouseSelection is SelectionMouseHandler handler)
                {
                    handler.mode = MouseSelectionMode.None;
                }
            }
        }

        TextArea ITextAreaInputHandler.TextArea => textArea;

        void ITextAreaInputHandler.Attach()
        {
            textArea.MouseLeftButtonDown += textArea_MouseLeftButtonDown;
            textArea.MouseMove += textArea_MouseMove;
            textArea.MouseLeftButtonUp += textArea_MouseLeftButtonUp;
            textArea.QueryCursor += textArea_QueryCursor;
            textArea.DocumentChanged += textArea_DocumentChanged;
            textArea.OptionChanged += textArea_OptionChanged;

            enableTextDragDrop = textArea.Options.EnableTextDragDrop;

            if (enableTextDragDrop)
            {
                AttachDragDrop();
            }
        }

        void ITextAreaInputHandler.Detach()
        {
            mode = MouseSelectionMode.None;
            textArea.MouseLeftButtonDown -= textArea_MouseLeftButtonDown;
            textArea.MouseMove -= textArea_MouseMove;
            textArea.MouseLeftButtonUp -= textArea_MouseLeftButtonUp;
            textArea.QueryCursor -= textArea_QueryCursor;
            textArea.DocumentChanged -= textArea_DocumentChanged;
            textArea.OptionChanged -= textArea_OptionChanged;

            if (enableTextDragDrop)
            {
                DetachDragDrop();
            }
        }

        private void AttachDragDrop()
        {
            textArea.AllowDrop = true;
            textArea.GiveFeedback += textArea_GiveFeedback;
            textArea.QueryContinueDrag += textArea_QueryContinueDrag;
            textArea.DragEnter += textArea_DragEnter;
            textArea.DragOver += textArea_DragOver;
            textArea.DragLeave += textArea_DragLeave;
            textArea.Drop += textArea_Drop;
        }

        private void DetachDragDrop()
        {
            textArea.AllowDrop = false;
            textArea.GiveFeedback -= textArea_GiveFeedback;
            textArea.QueryContinueDrag -= textArea_QueryContinueDrag;
            textArea.DragEnter -= textArea_DragEnter;
            textArea.DragOver -= textArea_DragOver;
            textArea.DragLeave -= textArea_DragLeave;
            textArea.Drop -= textArea_Drop;
        }

        private bool enableTextDragDrop;

        private void textArea_OptionChanged(object? sender, PropertyChangedEventArgs e)
        {
            var enable = textArea.Options.EnableTextDragDrop;

            if (enable != enableTextDragDrop)
            {
                enableTextDragDrop = enable;

                if (enable)
                {
                    AttachDragDrop();
                }
                else
                {
                    DetachDragDrop();
                }
            }
        }

        private void textArea_DocumentChanged(object? sender, EventArgs e)
        {
            if (mode != MouseSelectionMode.None)
            {
                mode = MouseSelectionMode.None;

                textArea.ReleaseMouseCapture();
            }

            startWord = null;
        }
        #endregion

        #region Dropping text
        private void textArea_DragEnter(object sender, DragEventArgs e)
        {
            try
            {
                e.Effects = GetEffect(e);

                textArea.Caret.Show();
            }
            catch (Exception ex)
            {
                OnDragException(ex);
            }
        }

        private void textArea_DragOver(object sender, DragEventArgs e)
        {
            try
            {
                e.Effects = GetEffect(e);
            }
            catch (Exception ex)
            {
                OnDragException(ex);
            }
        }

        private DragDropEffects GetEffect(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.UnicodeText, true))
            {
                e.Handled = true;
                int offset = GetOffsetFromMousePosition(e.GetPosition(textArea.TextView), out int visualColumn, out bool isAtEndOfLine);
                if (offset >= 0)
                {
                    textArea.Caret.Position = new TextViewPosition(textArea.Document.GetLocation(offset), visualColumn) { IsAtEndOfLine = isAtEndOfLine };
                    textArea.Caret.DesiredXPos = double.NaN;
                    if (textArea.EditableSectionProvider.CanInsert(offset))
                    {
                        return (e.AllowedEffects & DragDropEffects.Move) == DragDropEffects.Move &&
                            (e.KeyStates & DragDropKeyStates.ControlKey) != DragDropKeyStates.ControlKey
                            ? DragDropEffects.Move
                            : e.AllowedEffects & DragDropEffects.Copy;
                    }
                }
            }
            
            return DragDropEffects.None;
        }

        private void textArea_DragLeave(object sender, DragEventArgs e)
        {
            try
            {
                e.Handled = true;

                if (!textArea.IsKeyboardFocusWithin)
                {
                    textArea.Caret.Hide();
                }
            }
            catch (Exception ex)
            {
                OnDragException(ex);
            }
        }

        private void textArea_Drop(object sender, DragEventArgs e)
        {
            try
            {
                var effect = GetEffect(e);
                
                e.Effects = effect;
                
                if (effect != DragDropEffects.None)
                {
                    var start = textArea.Caret.Offset;

                    if (mode == MouseSelectionMode.Drag && textArea.Selection.Contains(start))
                    {
                        Debug.WriteLine("Drop: did not drop: drop target is inside selection");
                        
                        e.Effects = DragDropEffects.None;
                    }
                    else
                    {
                        Debug.WriteLine("Drop: insert at " + start);

                        var pastingEventArgs = new DataObjectPastingEventArgs(e.Data, true, DataFormats.UnicodeText);
                        
                        textArea.RaiseEvent(pastingEventArgs);
                        
                        if (pastingEventArgs.CommandCancelled)
                        {
                            return;
                        }

                        var text = EditingCommandHandler.GetTextToPaste(pastingEventArgs, textArea);
                        
                        if (text is null)
                        {
                            return;
                        }

                        var rectangular = pastingEventArgs.DataObject.GetDataPresent(BoxSelection.BoxSelectionDataType);

                        // Mark the undo group with the currentDragDescriptor, if the drag
                        // is originating from the same control. This allows combining
                        // the undo groups when text is moved.
                        textArea.Document.UndoStack.StartUndoGroup(currentDragDescriptor);
                        
                        try
                        {
                            if (!rectangular || !BoxSelection.PerformRectangularPaste(textArea, textArea.Caret.Position, text, true))
                            {
                                textArea.Document.Insert(start, text);
                                
                                textArea.Selection = Selection.Create(textArea, start, start + text.Length);
                            }
                        }
                        finally
                        {
                            textArea.Document.UndoStack.EndUndoGroup();
                        }
                    }
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                OnDragException(ex);
            }
        }

        private void OnDragException(Exception exception)
        {
            // give apps a chance to defer or ignore
            textArea.Dispatcher.BeginInvoke(
                DispatcherPriority.Send,
                new Action(delegate
                {
                    throw new DragDropException("Exception during drag'n'drop", exception);
                }));
        }

        private void textArea_GiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            try
            {
                e.UseDefaultCursors = true;
                e.Handled = true;
            }
            catch (Exception ex)
            {
                OnDragException(ex);
            }
        }

        private void textArea_QueryContinueDrag(object sender, QueryContinueDragEventArgs e)
        {
            try
            {
                if (e.EscapePressed)
                {
                    e.Action = DragAction.Cancel;
                }
                else if ((e.KeyStates & DragDropKeyStates.LeftMouseButton) != DragDropKeyStates.LeftMouseButton)
                {
                    e.Action = DragAction.Drop;
                }
                else
                {
                    e.Action = DragAction.Continue;
                }
                e.Handled = true;
            }
            catch (Exception ex)
            {
                OnDragException(ex);
            }
        }
        #endregion

        #region Start Drag
        private object currentDragDescriptor;

        private void StartDrag()
        {
            // mouse capture and Drag'n'Drop doesn't mix
            textArea.ReleaseMouseCapture();

            // prevent nested StartDrag calls
            mode = MouseSelectionMode.Drag;

            var dataObject = textArea.Selection.CreateDataObject(textArea);
            var allowedEffects = DragDropEffects.All;
            var deleteOnMove = textArea.Selection.Segments.Select(s => new AnchorSegment(textArea.Document, s)).ToList();
            
            foreach (ISegment s in deleteOnMove)
            {
                ISegment[] result = textArea.GetDeletableSegments(s);
                if (result.Length != 1 || result[0].Offset != s.Offset || result[0].EndOffset != s.EndOffset)
                {
                    allowedEffects &= ~DragDropEffects.Move;
                }
            }

            var copyingEventArgs = new DataObjectCopyingEventArgs(dataObject, true);
            textArea.RaiseEvent(copyingEventArgs);
            if (copyingEventArgs.CommandCancelled)
            {
                return;
            }

            object dragDescriptor = new object();
            currentDragDescriptor = dragDescriptor;

            DragDropEffects resultEffect;
            using (textArea.AllowCaretOutsideSelection())
            {
                var oldCaretPosition = textArea.Caret.Position;
                try
                {
                    Debug.WriteLine("DoDragDrop with allowedEffects=" + allowedEffects);
                    resultEffect = DragDrop.DoDragDrop(textArea, dataObject, allowedEffects);
                    Debug.WriteLine("DoDragDrop done, resultEffect=" + resultEffect);
                }
                catch (COMException ex)
                {
                    // ignore COM errors - don't crash on badly implemented drop targets
                    Debug.WriteLine("DoDragDrop failed: " + ex.ToString());
                    return;
                }
                if (resultEffect == DragDropEffects.None)
                {
                    // reset caret if drag was aborted
                    textArea.Caret.Position = oldCaretPosition;
                }
            }

            currentDragDescriptor = null;

            if (deleteOnMove != null && resultEffect == DragDropEffects.Move && (allowedEffects & DragDropEffects.Move) == DragDropEffects.Move)
            {
                bool draggedInsideSingleDocument = (dragDescriptor == textArea.Document.UndoStack.LastGroupDescriptor);
                if (draggedInsideSingleDocument)
                {
                    textArea.Document.UndoStack.StartContinuedUndoGroup(null);
                }

                textArea.Document.BeginUpdate();
                try
                {
                    foreach (ISegment s in deleteOnMove)
                    {
                        textArea.Document.Remove(s.Offset, s.Length);
                    }
                }
                finally
                {
                    textArea.Document.EndUpdate();
                    if (draggedInsideSingleDocument)
                    {
                        textArea.Document.UndoStack.EndUndoGroup();
                    }
                }
            }
        }
        #endregion

        #region QueryCursor
        // provide the IBeam Cursor for the text area
        private void textArea_QueryCursor(object sender, QueryCursorEventArgs e)
        {
            if (!e.Handled)
            {
                if (mode != MouseSelectionMode.None)
                {
                    // during selection, use IBeam cursor even outside the text area
                    e.Cursor = Cursors.IBeam;
                    e.Handled = true;
                }
                else if (textArea.TextView.VisualLinesValid)
                {
                    // Only query the cursor if the visual lines are valid.
                    // If they are invalid, the cursor will get re-queried when the visual lines
                    // get refreshed.
                    Point p = e.GetPosition(textArea.TextView);
                    if (p.X >= 0 && p.Y >= 0 && p.X <= textArea.TextView.ActualWidth && p.Y <= textArea.TextView.ActualHeight)
                    {
                        int offset = GetOffsetFromMousePosition(e, out int visualColumn, out bool isAtEndOfLine);
                        if (enableTextDragDrop && textArea.Selection.Contains(offset))
                        {
                            e.Cursor = Cursors.Arrow;
                        }
                        else
                        {
                            e.Cursor = Cursors.IBeam;
                        }

                        e.Handled = true;
                    }
                }
            }
        }
        #endregion

        #region LeftButtonDown
        private void textArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            mode = MouseSelectionMode.None;

            // don't enter into selection mode when there's no document attached
            if (textArea.Document is null)
            {
                return;
            }

            if (!e.Handled && e.ChangedButton == MouseButton.Left)
            {
                var modifiers = Keyboard.Modifiers;
                var shift = (modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                
                if (enableTextDragDrop && e.ClickCount == 1 && !shift)
                {
                    var offset = GetOffsetFromMousePosition(e, out int _, out bool _);

                    if (textArea.Selection.Contains(offset))
                    {
                        if (textArea.CaptureMouse())
                        {
                            mode = MouseSelectionMode.DragStart;
                            possibleDragStartMousePos = e.GetPosition(textArea);
                        }
                        
                        e.Handled = true;
                        
                        return;
                    }
                }

                var oldPosition = textArea.Caret.Position;
                
                SetCaretOffsetToMousePosition(e);

                if (!shift)
                {
                    textArea.ClearSelection();
                }
                if (textArea.CaptureMouse())
                {
                    if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt && textArea.Options.EnableRectangularSelection)
                    {
                        mode = MouseSelectionMode.Rectangular;
                        if (shift && textArea.Selection is BoxSelection)
                        {
                            textArea.Selection = textArea.Selection.StartSelectionOrSetEndpoint(oldPosition, textArea.Caret.Position);
                        }
                    }
                    else if (e.ClickCount == 1 && ((modifiers & ModifierKeys.Control) == 0))
                    {
                        mode = MouseSelectionMode.Normal;

                        if (shift && !(textArea.Selection is BoxSelection))
                        {
                            textArea.Selection = textArea.Selection.StartSelectionOrSetEndpoint(oldPosition, textArea.Caret.Position);
                        }
                    }
                    else
                    {
                        SimpleSegment startWord;
                        
                        if (e.ClickCount == 3)
                        {
                            mode = MouseSelectionMode.WholeLine;
                            startWord = GetLineAtMousePosition(e);
                        }
                        else
                        {
                            mode = MouseSelectionMode.WholeWord;
                            startWord = GetWordAtMousePosition(e);
                        }
                        
                        if (startWord == SimpleSegment.Invalid)
                        {
                            mode = MouseSelectionMode.None;
                            textArea.ReleaseMouseCapture();
                        
                            return;
                        }

                        if (shift && !textArea.Selection.IsEmpty)
                        {
                            if (startWord.Offset < textArea.Selection.SurroundingSegment.Offset)
                            {
                                textArea.Selection = textArea.Selection.SetEndpoint(new TextViewPosition(textArea.Document.GetLocation(startWord.Offset)));
                            }
                            else if (startWord.EndOffset > textArea.Selection.SurroundingSegment.EndOffset)
                            {
                                textArea.Selection = textArea.Selection.SetEndpoint(new TextViewPosition(textArea.Document.GetLocation(startWord.EndOffset)));
                            }
                            
                            this.startWord = new AnchorSegment(textArea.Document, textArea.Selection.SurroundingSegment);
                        }
                        else
                        {
                            textArea.Selection = Selection.Create(textArea, startWord.Offset, startWord.EndOffset);
                            
                            this.startWord = new AnchorSegment(textArea.Document, startWord.Offset, startWord.Length);
                        }
                    }
                }
            }

            e.Handled = true;
        }

        public MouseSelectionMode MouseSelectionMode
        {
            get => mode;
            set
            {
                if (mode == value)
                {
                    return;
                }

                if (value == MouseSelectionMode.None)
                {
                    mode = MouseSelectionMode.None;

                    textArea.ReleaseMouseCapture();
                }
                else if (textArea.CaptureMouse())
                {
                    mode = value switch
                    {
                        MouseSelectionMode.Normal or MouseSelectionMode.Rectangular => value,
                        _ => throw new NotImplementedException("Programmatically starting mouse selection is only supported for normal and rectangular selections."),
                    };
                }
            }
        }
        #endregion

        #region Mouse Position <-> Text coordinates
        private SimpleSegment GetWordAtMousePosition(MouseEventArgs e)
        {
            var textView = textArea.TextView;

            if (textView == null)
            {
                return SimpleSegment.Invalid;
            }

            var pos = e.GetPosition(textView);

            pos.Y = pos.Y.Rectify(0, textView.ActualHeight);

            pos += textView.ScrollOffset;

            var line = textView.GetVisualLineFromVisualTop(pos.Y);

            if (line is null)
            {
                return SimpleSegment.Invalid;
            }

            var visualColumn = line.GetVisualColumn(pos, textArea.Selection.EnableVirtualSpace);
            var wordStartVC = line.GetNextCaretPosition(visualColumn + 1, LogicalDirection.Backward, CaretPositioningMode.WordStartOrSymbol, textArea.Selection.EnableVirtualSpace);

            if (wordStartVC == -1)
            {
                wordStartVC = 0;
            }

            var wordEndVC = line.GetNextCaretPosition(wordStartVC, LogicalDirection.Forward, CaretPositioningMode.WordBorderOrSymbol, textArea.Selection.EnableVirtualSpace);

            if (wordEndVC == -1)
            {
                wordEndVC = line.VisualLength;
            }

            var relOffset = line.FirstLine.Offset;
            var wordStartOffset = line.GetRelativeOffset(wordStartVC) + relOffset;
            var wordEndOffset = line.GetRelativeOffset(wordEndVC) + relOffset;

            return new SimpleSegment(wordStartOffset, wordEndOffset - wordStartOffset);
        }

        private SimpleSegment GetLineAtMousePosition(MouseEventArgs e)
        {
            var textView = textArea.TextView;

            if (textView is null)
            {
                return SimpleSegment.Invalid;
            }

            var pos = e.GetPosition(textView);

            pos.Y = pos.Y.Rectify(0, textView.ActualHeight);

            pos += textView.ScrollOffset;

            var line = textView.GetVisualLineFromVisualTop(pos.Y);

            if (line is null)
            {
                return SimpleSegment.Invalid;
            }

            return new SimpleSegment(line.StartOffset, line.LastLine.EndOffset - line.StartOffset);
        }

        private int GetOffsetFromMousePosition(MouseEventArgs e, out int visualColumn, out bool isAtEndOfLine)
        {
            return GetOffsetFromMousePosition(e.GetPosition(textArea.TextView), out visualColumn, out isAtEndOfLine);
        }

        private int GetOffsetFromMousePosition(Point positionRelativeToTextView, out int visualColumn, out bool isAtEndOfLine)
        {
            visualColumn = 0;
            var textView = textArea.TextView;
            var pos = positionRelativeToTextView;

            pos.Y = pos.Y.Rectify(0, textView.ActualHeight);

            pos += textView.ScrollOffset;
            
            if (pos.Y >= textView.DocumentHeight)
            {
                pos.Y = textView.DocumentHeight - VisualExtensions.Epsilon;
            }

            var line = textView.GetVisualLineFromVisualTop(pos.Y);

            if (line is null)
            {
                isAtEndOfLine = false;
                return -1;
            }
            
            visualColumn = line.GetVisualColumn(pos, textArea.Selection.EnableVirtualSpace, out isAtEndOfLine);
                
            return line.GetRelativeOffset(visualColumn) + line.FirstLine.Offset;
        }

        private int GetOffsetFromMousePositionFirstTextLineOnly(Point positionRelativeToTextView, out int visualColumn)
        {
            visualColumn = 0;
            TextView textView = textArea.TextView;
            Point pos = positionRelativeToTextView;
            if (pos.Y < 0)
            {
                pos.Y = 0;
            }

            if (pos.Y > textView.ActualHeight)
            {
                pos.Y = textView.ActualHeight;
            }

            pos += textView.ScrollOffset;
            if (pos.Y >= textView.DocumentHeight)
            {
                pos.Y = textView.DocumentHeight - VisualExtensions.Epsilon;
            }

            VisualLine line = textView.GetVisualLineFromVisualTop(pos.Y);
            if (line != null)
            {
                visualColumn = line.GetVisualColumn(line.TextLines.First(), pos.X, textArea.Selection.EnableVirtualSpace);
                return line.GetRelativeOffset(visualColumn) + line.FirstLine.Offset;
            }
            return -1;
        }
        #endregion

        #region MouseMove
        private void textArea_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }

            if (mode == MouseSelectionMode.Normal || mode == MouseSelectionMode.WholeWord || mode == MouseSelectionMode.WholeLine || mode == MouseSelectionMode.Rectangular)
            {
                e.Handled = true;
                if (textArea.TextView.VisualLinesValid)
                {
                    // If the visual lines are not valid, don't extend the selection.
                    // Extending the selection forces a VisualLine refresh, and it is sufficient
                    // to do that on MouseUp, we don't have to do it every MouseMove.
                    ExtendSelectionToMouse(e);
                }
            }
            else if (mode == MouseSelectionMode.DragStart)
            {
                e.Handled = true;
                Vector mouseMovement = e.GetPosition(textArea) - possibleDragStartMousePos;
                if (Math.Abs(mouseMovement.X) > SystemParameters.MinimumHorizontalDragDistance
                    || Math.Abs(mouseMovement.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    StartDrag();
                }
            }
        }
        #endregion

        #region ExtendSelection
        private void SetCaretOffsetToMousePosition(MouseEventArgs e)
        {
            SetCaretOffsetToMousePosition(e, null);
        }

        private void SetCaretOffsetToMousePosition(MouseEventArgs e, ISegment allowedSegment)
        {
            int visualColumn;
            bool isAtEndOfLine;
            int offset;
            if (mode == MouseSelectionMode.Rectangular)
            {
                offset = GetOffsetFromMousePositionFirstTextLineOnly(e.GetPosition(textArea.TextView), out visualColumn);
                isAtEndOfLine = true;
            }
            else
            {
                offset = GetOffsetFromMousePosition(e, out visualColumn, out isAtEndOfLine);
            }
            if (allowedSegment != null)
            {
                offset = offset.Rectify(allowedSegment.Offset, allowedSegment.EndOffset);
            }
            if (offset >= 0)
            {
                textArea.Caret.Position = new TextViewPosition(textArea.Document.GetLocation(offset), visualColumn) { IsAtEndOfLine = isAtEndOfLine };
                textArea.Caret.DesiredXPos = double.NaN;
            }
        }

        private void ExtendSelectionToMouse(MouseEventArgs e)
        {
            TextViewPosition oldPosition = textArea.Caret.Position;
            if (mode == MouseSelectionMode.Normal || mode == MouseSelectionMode.Rectangular)
            {
                SetCaretOffsetToMousePosition(e);
                if (mode == MouseSelectionMode.Normal && textArea.Selection is BoxSelection)
                {
                    textArea.Selection = new SimpleSelection(textArea, oldPosition, textArea.Caret.Position);
                }
                else if (mode == MouseSelectionMode.Rectangular && !(textArea.Selection is BoxSelection))
                {
                    textArea.Selection = new BoxSelection(textArea, oldPosition, textArea.Caret.Position);
                }
                else
                {
                    textArea.Selection = textArea.Selection.StartSelectionOrSetEndpoint(oldPosition, textArea.Caret.Position);
                }
            }
            else if (mode == MouseSelectionMode.WholeWord || mode == MouseSelectionMode.WholeLine)
            {
                var newWord = (mode == MouseSelectionMode.WholeLine) ? GetLineAtMousePosition(e) : GetWordAtMousePosition(e);
                if (newWord != SimpleSegment.Invalid)
                {
                    textArea.Selection = Selection.Create(textArea,
                                                          Math.Min(newWord.Offset, startWord.Offset),
                                                          Math.Max(newWord.EndOffset, startWord.EndOffset));
                    // moves caret to start or end of selection
                    if (newWord.Offset < startWord.Offset)
                    {
                        textArea.Caret.Offset = newWord.Offset;
                    }
                    else
                    {
                        textArea.Caret.Offset = Math.Max(newWord.EndOffset, startWord.EndOffset);
                    }
                }
            }
            textArea.Caret.BringCaretToView(5.0);
        }
        #endregion

        #region MouseLeftButtonUp
        private void textArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (mode == MouseSelectionMode.None || e.Handled)
            {
                return;
            }

            e.Handled = true;
            if (mode == MouseSelectionMode.DragStart)
            {
                // -> this was not a drag start (mouse didn't move after mousedown)
                SetCaretOffsetToMousePosition(e);
                textArea.ClearSelection();
            }
            else if (mode == MouseSelectionMode.Normal || mode == MouseSelectionMode.WholeWord || mode == MouseSelectionMode.WholeLine || mode == MouseSelectionMode.Rectangular)
            {
                ExtendSelectionToMouse(e);
            }
            mode = MouseSelectionMode.None;
            textArea.ReleaseMouseCapture();
        }
        #endregion
    }
}
