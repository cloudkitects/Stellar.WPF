using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.TextFormatting;

using Stellar.WPF.Document;
using Stellar.WPF.Rendering;
using Stellar.WPF.Utilities;

namespace Stellar.WPF.Editing;

static class CaretNavigationCommandHandler
{
    /// <summary>
    /// Creates a new <see cref="TextAreaInputHandler"/> for the text area.
    /// </summary>
    public static TextAreaInputHandler Create(TextArea textArea)
    {
        var handler = new TextAreaInputHandler(textArea);

        handler.CommandBindings.AddRange(CommandBindings);
        handler.InputBindings.AddRange(InputBindings);

        return handler;
    }

    static readonly List<CommandBinding> CommandBindings = new();
    static readonly List<InputBinding> InputBindings = new();

    static void AddBinding(ICommand command, ModifierKeys modifiers, Key key, ExecutedRoutedEventHandler handler)
    {
        CommandBindings.Add(new CommandBinding(command, handler));
        InputBindings.Add(TextAreaDefaultInputHandler.CreateFrozenKeyBinding(command, modifiers, key));
    }

    static CaretNavigationCommandHandler()
    {
        const ModifierKeys None = ModifierKeys.None;
        const ModifierKeys Ctrl = ModifierKeys.Control;
        const ModifierKeys Shift = ModifierKeys.Shift;
        const ModifierKeys Alt = ModifierKeys.Alt;

        AddBinding(EditingCommands.MoveLeftByCharacter, None, Key.Left, OnMoveCaret(CaretMovementType.CharLeft));
        AddBinding(EditingCommands.SelectLeftByCharacter, Shift, Key.Left, OnMoveCaretExtendSelection(CaretMovementType.CharLeft));
        AddBinding(BoxSelection.LeftByCharacter, Alt | Shift, Key.Left, OnMoveCaretBoxSelection(CaretMovementType.CharLeft));
        AddBinding(EditingCommands.MoveRightByCharacter, None, Key.Right, OnMoveCaret(CaretMovementType.CharRight));
        AddBinding(EditingCommands.SelectRightByCharacter, Shift, Key.Right, OnMoveCaretExtendSelection(CaretMovementType.CharRight));
        AddBinding(BoxSelection.RightByCharacter, Alt | Shift, Key.Right, OnMoveCaretBoxSelection(CaretMovementType.CharRight));

        AddBinding(EditingCommands.MoveLeftByWord, Ctrl, Key.Left, OnMoveCaret(CaretMovementType.WordLeft));
        AddBinding(EditingCommands.SelectLeftByWord, Ctrl | Shift, Key.Left, OnMoveCaretExtendSelection(CaretMovementType.WordLeft));
        AddBinding(BoxSelection.LeftByWord, Ctrl | Alt | Shift, Key.Left, OnMoveCaretBoxSelection(CaretMovementType.WordLeft));
        AddBinding(EditingCommands.MoveRightByWord, Ctrl, Key.Right, OnMoveCaret(CaretMovementType.WordRight));
        AddBinding(EditingCommands.SelectRightByWord, Ctrl | Shift, Key.Right, OnMoveCaretExtendSelection(CaretMovementType.WordRight));
        AddBinding(BoxSelection.RightByWord, Ctrl | Alt | Shift, Key.Right, OnMoveCaretBoxSelection(CaretMovementType.WordRight));

        AddBinding(EditingCommands.MoveUpByLine, None, Key.Up, OnMoveCaret(CaretMovementType.LineUp));
        AddBinding(EditingCommands.SelectUpByLine, Shift, Key.Up, OnMoveCaretExtendSelection(CaretMovementType.LineUp));
        AddBinding(BoxSelection.UpByLine, Alt | Shift, Key.Up, OnMoveCaretBoxSelection(CaretMovementType.LineUp));
        AddBinding(EditingCommands.MoveDownByLine, None, Key.Down, OnMoveCaret(CaretMovementType.LineDown));
        AddBinding(EditingCommands.SelectDownByLine, Shift, Key.Down, OnMoveCaretExtendSelection(CaretMovementType.LineDown));
        AddBinding(BoxSelection.DownByLine, Alt | Shift, Key.Down, OnMoveCaretBoxSelection(CaretMovementType.LineDown));

        AddBinding(EditingCommands.MoveDownByPage, None, Key.PageDown, OnMoveCaret(CaretMovementType.PageDown));
        AddBinding(EditingCommands.SelectDownByPage, Shift, Key.PageDown, OnMoveCaretExtendSelection(CaretMovementType.PageDown));
        AddBinding(EditingCommands.MoveUpByPage, None, Key.PageUp, OnMoveCaret(CaretMovementType.PageUp));
        AddBinding(EditingCommands.SelectUpByPage, Shift, Key.PageUp, OnMoveCaretExtendSelection(CaretMovementType.PageUp));

        AddBinding(EditingCommands.MoveToLineStart, None, Key.Home, OnMoveCaret(CaretMovementType.LineStart));
        AddBinding(EditingCommands.SelectToLineStart, Shift, Key.Home, OnMoveCaretExtendSelection(CaretMovementType.LineStart));
        AddBinding(BoxSelection.ToLineStart, Alt | Shift, Key.Home, OnMoveCaretBoxSelection(CaretMovementType.LineStart));
        AddBinding(EditingCommands.MoveToLineEnd, None, Key.End, OnMoveCaret(CaretMovementType.LineEnd));
        AddBinding(EditingCommands.SelectToLineEnd, Shift, Key.End, OnMoveCaretExtendSelection(CaretMovementType.LineEnd));
        AddBinding(BoxSelection.ToLineEnd, Alt | Shift, Key.End, OnMoveCaretBoxSelection(CaretMovementType.LineEnd));

        AddBinding(EditingCommands.MoveToDocumentStart, Ctrl, Key.Home, OnMoveCaret(CaretMovementType.DocumentStart));
        AddBinding(EditingCommands.SelectToDocumentStart, Ctrl | Shift, Key.Home, OnMoveCaretExtendSelection(CaretMovementType.DocumentStart));
        AddBinding(EditingCommands.MoveToDocumentEnd, Ctrl, Key.End, OnMoveCaret(CaretMovementType.DocumentEnd));
        AddBinding(EditingCommands.SelectToDocumentEnd, Ctrl | Shift, Key.End, OnMoveCaretExtendSelection(CaretMovementType.DocumentEnd));

        CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, OnSelectAll));

        TextAreaDefaultInputHandler.WorkaroundWPFMemoryLeak(InputBindings);
    }

    static void OnSelectAll(object target, ExecutedRoutedEventArgs args)
    {
        var textArea = GetTextArea(target);

        if (textArea is not null && textArea.Document is not null)
        {
            args.Handled = true;

            textArea.Caret.Offset = textArea.Document.TextLength;
            textArea.Selection = Selection.Create(textArea, 0, textArea.Document.TextLength);
        }
    }

    static TextArea? GetTextArea(object target)
    {
        return target as TextArea;
    }

    static ExecutedRoutedEventHandler OnMoveCaret(CaretMovementType direction)
    {
        return (target, args) =>
        {
            var textArea = GetTextArea(target);

            if (textArea is not null && textArea.Document is not null)
            {
                args.Handled = true;

                textArea.ClearSelection();

                MoveCaret(textArea, direction);

                textArea.Caret.BringCaretToView();
            }
        };
    }

    static ExecutedRoutedEventHandler OnMoveCaretExtendSelection(CaretMovementType direction)
    {
        return (target, args) =>
        {
            var textArea = GetTextArea(target);

            if (textArea is not null && textArea.Document is not null)
            {
                args.Handled = true;

                var oldPosition = textArea.Caret.Position;

                MoveCaret(textArea, direction);

                textArea.Selection = textArea.Selection.StartSelectionOrSetEndpoint(oldPosition, textArea.Caret.Position);
                textArea.Caret.BringCaretToView();
            }
        };
    }

    static ExecutedRoutedEventHandler OnMoveCaretBoxSelection(CaretMovementType direction)
    {
        return (target, args) =>
        {
            var textArea = GetTextArea(target);

            if (textArea is not null && textArea.Document is not null)
            {
                args.Handled = true;

                // First, convert the selection into a rectangle selection
                // (this is required so that virtual space gets enabled for the caret movement)
                if (textArea.Options.EnableRectangularSelection && !(textArea.Selection is BoxSelection))
                {
                    if (textArea.Selection.IsEmpty)
                    {
                        textArea.Selection = new BoxSelection(textArea, textArea.Caret.Position, textArea.Caret.Position);
                    }
                    else
                    {
                        // Convert normal selection to rectangle selection
                        textArea.Selection = new BoxSelection(textArea, textArea.Selection.StartPosition, textArea.Caret.Position);
                    }
                }

                // Now move the caret and extend the selection
                var oldPosition = textArea.Caret.Position;

                MoveCaret(textArea, direction);

                textArea.Selection = textArea.Selection.StartSelectionOrSetEndpoint(oldPosition, textArea.Caret.Position);
                textArea.Caret.BringCaretToView();
            }
        };
    }

    #region Caret movement
    internal static void MoveCaret(TextArea textArea, CaretMovementType direction)
    {
        var desiredXPos = textArea.Caret.DesiredXPos;

        if (textArea.FlowDirection == FlowDirection.RightToLeft)
        {
            direction = direction switch
            {
                CaretMovementType.CharLeft => CaretMovementType.CharRight,
                CaretMovementType.CharRight => CaretMovementType.CharLeft,
                CaretMovementType.WordRight => CaretMovementType.WordLeft,
                CaretMovementType.WordLeft => CaretMovementType.WordRight,
                _ => direction
            };
        }

        textArea.Caret.Position = GetNewCaretPosition(textArea.TextView, textArea.Caret.Position, direction, textArea.Selection.EnableVirtualSpace, ref desiredXPos);
        textArea.Caret.DesiredXPos = desiredXPos;
    }

    internal static TextViewPosition GetNewCaretPosition(TextView textView, TextViewPosition caretPosition, CaretMovementType direction, bool enableVirtualSpace, ref double desiredXPos)
    {
        switch (direction)
        {
            case CaretMovementType.None:
                return caretPosition;

            case CaretMovementType.DocumentStart:
                desiredXPos = double.NaN;
                return new TextViewPosition(0, 0);

            case CaretMovementType.DocumentEnd:
                desiredXPos = double.NaN;
                return new TextViewPosition(textView.Document.GetLocation(textView.Document.TextLength));

            default:
                break;
        }

        var caretLine = textView.Document.GetLineByNumber(caretPosition.Line);
        var visualLine = textView.GetOrConstructVisualLine(caretLine);
        var textLine = visualLine.GetTextLine(caretPosition.VisualColumn, caretPosition.IsAtEndOfLine);

        switch (direction)
        {
            case CaretMovementType.CharLeft:
                desiredXPos = double.NaN;

                // do not move caret to previous line in virtual space
                if (caretPosition.VisualColumn == 0 && enableVirtualSpace)
                {
                    return caretPosition;
                }

                return GetPrevCaretPosition(textView, caretPosition, visualLine, CaretPositioningMode.Normal, enableVirtualSpace);

            case CaretMovementType.Backspace:
                desiredXPos = double.NaN;
                return GetPrevCaretPosition(textView, caretPosition, visualLine, CaretPositioningMode.EveryCodepoint, enableVirtualSpace);

            case CaretMovementType.CharRight:
                desiredXPos = double.NaN;
                return GetNextCaretPosition(textView, caretPosition, visualLine, CaretPositioningMode.Normal, enableVirtualSpace);

            case CaretMovementType.WordLeft:
                desiredXPos = double.NaN;
                return GetPrevCaretPosition(textView, caretPosition, visualLine, CaretPositioningMode.WordStart, enableVirtualSpace);

            case CaretMovementType.WordRight:
                desiredXPos = double.NaN;
                return GetNextCaretPosition(textView, caretPosition, visualLine, CaretPositioningMode.WordStart, enableVirtualSpace);

            case CaretMovementType.LineUp:
            case CaretMovementType.LineDown:
            case CaretMovementType.PageUp:
            case CaretMovementType.PageDown:
                return GetUpDownCaretPosition(textView, caretPosition, direction, visualLine, textLine, enableVirtualSpace, ref desiredXPos);

            case CaretMovementType.LineStart:
                desiredXPos = double.NaN;
                return GetStartOfLineCaretPosition(caretPosition.VisualColumn, visualLine, textLine, enableVirtualSpace);

            case CaretMovementType.LineEnd:
                desiredXPos = double.NaN;
                return GetEndOfLineCaretPosition(visualLine, textLine);

            default:
                throw new NotSupportedException(direction.ToString());
        }
    }
    #endregion

    #region Home/End
    static TextViewPosition GetStartOfLineCaretPosition(int oldCol, VisualLine line, TextLine textLine, bool enableVirtualSpace)
    {
        var newCol = line.GetTextLineStartColumn(textLine);

        if (newCol == 0)
        {
            newCol = line.GetNextCaretPosition(newCol - 1, LogicalDirection.Forward, CaretPositioningMode.WordStart, enableVirtualSpace);
        }

        if (newCol < 0)
        {
            throw new InvalidOperationException("Could not find a valid caret position in the line");
        }
        // when the caret is already at the start of the text, jump to start before whitespace
        if (newCol == oldCol)
        {
            newCol = 0;
        }

        return line.GetTextViewPosition(newCol);
    }

    static TextViewPosition GetEndOfLineCaretPosition(VisualLine visualLine, TextLine textLine)
    {
        var newCol = visualLine.GetTextLineStartColumn(textLine) + textLine.Length - textLine.NewlineLength;

        var pos = visualLine.GetTextViewPosition(newCol);

        pos.IsAtEndOfLine = true;

        return pos;
    }
    #endregion

    #region By-character / By-word movement
    static TextViewPosition GetNextCaretPosition(TextView textView, TextViewPosition caretPosition, VisualLine visualLine, CaretPositioningMode mode, bool enableVirtualSpace)
    {
        var pos = visualLine.GetNextCaretPosition(caretPosition.VisualColumn, LogicalDirection.Forward, mode, enableVirtualSpace);

        if (pos >= 0)
        {
            return visualLine.GetTextViewPosition(pos);
        }
        // move to start of next line
        var nextLine = visualLine.LastLine.NextLine;

        if (nextLine is not null)
        {
            var line = textView.GetOrConstructVisualLine(nextLine);

            pos = line.GetNextCaretPosition(-1, LogicalDirection.Forward, mode, enableVirtualSpace);

            if (pos < 0)
            {
                throw new InvalidOperationException("Could not find a valid caret position in the line");
            }

            return line.GetTextViewPosition(pos);
        }

        // at end of document
        Debug.Assert(visualLine.LastLine.Offset + visualLine.LastLine.Length == textView.Document.TextLength);

        return new TextViewPosition(textView.Document.GetLocation(textView.Document.TextLength));
    }

    static TextViewPosition GetPrevCaretPosition(TextView textView, TextViewPosition caretPosition, VisualLine visualLine, CaretPositioningMode mode, bool enableVirtualSpace)
    {
        var pos = visualLine.GetNextCaretPosition(caretPosition.VisualColumn, LogicalDirection.Backward, mode, enableVirtualSpace);

        if (pos >= 0)
        {
            return visualLine.GetTextViewPosition(pos);
        }

        // move to end of previous line
        var previousLine = visualLine.FirstLine.PreviousLine;

        if (previousLine is not null)
        {
            var line = textView.GetOrConstructVisualLine(previousLine);

            pos = line.GetNextCaretPosition(line.VisualLength + 1, LogicalDirection.Backward, mode, enableVirtualSpace);

            if (pos < 0)
            {
                throw new InvalidOperationException("Could not find a valid caret position in the line");
            }

            return line.GetTextViewPosition(pos);
        }

        // at start of document
        Debug.Assert(visualLine.FirstLine.Offset == 0);

        return new TextViewPosition(0, 0);
    }
    #endregion

    #region Line+Page up/down
    static TextViewPosition GetUpDownCaretPosition(TextView textView, TextViewPosition caretPosition, CaretMovementType direction, VisualLine visualLine, TextLine textLine, bool enableVirtualSpace, ref double xPos)
    {
        // moving up/down happens using the desired visual X position
        if (double.IsNaN(xPos))
        {
            xPos = visualLine.GetTextLineVisualXPosition(textLine, caretPosition.VisualColumn);
        }

        // now find the TextLine+VisualLine where the caret will end up in
        var targetVisualLine = visualLine;
        var textLineIndex = visualLine.TextLines.IndexOf(textLine);

        TextLine targetLine;

        switch (direction)
        {
            case CaretMovementType.LineUp:
                {
                    // Move up: move to the previous TextLine in the same visual line
                    // or move to the last TextLine of the previous visual line
                    var prevLineNumber = visualLine.FirstLine.Number - 1;

                    if (textLineIndex > 0)
                    {
                        targetLine = visualLine.TextLines[textLineIndex - 1];
                    }
                    else if (prevLineNumber >= 1)
                    {
                        var prevLine = textView.Document.GetLineByNumber(prevLineNumber);

                        targetVisualLine = textView.GetOrConstructVisualLine(prevLine);
                        targetLine = targetVisualLine.TextLines[targetVisualLine.TextLines.Count - 1];
                    }
                    else
                    {
                        targetLine = null!;
                    }

                    break;
                }
            case CaretMovementType.LineDown:
                {
                    // Move down: move to the next TextLine in the same visual line
                    // or move to the first TextLine of the next visual line
                    var nextLineNumber = visualLine.LastLine.Number + 1;

                    if (textLineIndex < visualLine.TextLines.Count - 1)
                    {
                        targetLine = visualLine.TextLines[textLineIndex + 1];
                    }
                    else if (nextLineNumber <= textView.Document.LineCount)
                    {
                        var nextLine = textView.Document.GetLineByNumber(nextLineNumber);

                        targetVisualLine = textView.GetOrConstructVisualLine(nextLine);
                        targetLine = targetVisualLine.TextLines[0];
                    }
                    else
                    {
                        targetLine = null!;
                    }

                    break;
                }
            case CaretMovementType.PageUp:
            case CaretMovementType.PageDown:
                {
                    // Page up/down: find the target line using its visual position
                    var y = visualLine.GetTextLineVisualYPosition(textLine, VisualYPosition.Middle);

                    if (direction == CaretMovementType.PageUp)
                    {
                        y -= textView.RenderSize.Height;
                    }
                    else
                    {
                        y += textView.RenderSize.Height;
                    }

                    var newLine = textView.GetDocumentLineByVisualTop(y);

                    targetVisualLine = textView.GetOrConstructVisualLine(newLine);
                    targetLine = targetVisualLine.GetTextLineByVisualY(y);

                    break;
                }
            default:
                throw new NotSupportedException(direction.ToString());
        }
        
        if (targetLine is not null)
        {
            var y = targetVisualLine.GetTextLineVisualYPosition(targetLine, VisualYPosition.Middle);
            var newCol = targetVisualLine.GetVisualColumn(new Point(xPos, y), enableVirtualSpace);

            // prevent wrapping to the next line; TODO: could 'IsAtEnd' help here?
            var targetLineStartCol = targetVisualLine.GetTextLineStartColumn(targetLine);

            if (newCol >= targetLineStartCol + targetLine.Length)
            {
                if (newCol <= targetVisualLine.VisualLength)
                {
                    newCol = targetLineStartCol + targetLine.Length - 1;
                }
            }

            return targetVisualLine.GetTextViewPosition(newCol);
        }

        return caretPosition;
    }
    #endregion
}
