using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

using Stellar.WPF.Document;

namespace Stellar.WPF.Editing;

/// <summary>
/// Contains the predefined input handlers.
/// </summary>
public class TextAreaDefaultInputHandler : TextAreaInputHandler
{
    /// <summary>
    /// Gets the caret navigation input handler.
    /// </summary>
    public TextAreaInputHandler CaretNavigation { get; private set; }

    /// <summary>
    /// Gets the editing input handler.
    /// </summary>
    public TextAreaInputHandler Editing { get; private set; }

    /// <summary>
    /// Gets the mouse selection input handler.
    /// </summary>
    public ITextAreaInputHandler MouseSelection { get; private set; }

    /// <summary>
    /// Creates a new TextAreaDefaultInputHandler instance.
    /// </summary>
    public TextAreaDefaultInputHandler(TextArea textArea) : base(textArea)
    {
        NestedInputHandlers.Add(CaretNavigation = CaretNavigationCommandHandler.Create(textArea));
        NestedInputHandlers.Add(Editing = EditingCommandHandler.Create(textArea));
        NestedInputHandlers.Add(MouseSelection = new SelectionMouseHandler(textArea));

        CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo, ExecuteUndo, CanExecuteUndo));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo, ExecuteRedo, CanExecuteRedo));
    }

    internal static KeyBinding CreateFrozenKeyBinding(ICommand command, ModifierKeys modifiers, Key key)
    {
        var keyBinding = new KeyBinding(command, key, modifiers);

        // freeze the key binding to share it between multiple editor instances
        if ((object)keyBinding is Freezable freezable)
        {
            freezable.Freeze();
        }

        return keyBinding;
    }

    /// <summary>
    // KeyBinding retains a reference to whichever UIElement it is used in first.
    // Using a dummy element for this purpose ensures we don't leak a real
    // editor with a potentially large document.
    /// </summary>
    internal static void WorkaroundWPFMemoryLeak(List<InputBinding> inputBindings)
    {
        var dummyElement = new UIElement();
        
        dummyElement.InputBindings.AddRange(inputBindings);
    }

    #region Undo / Redo
    UndoStack GetUndoStack()
    {
        var document = TextArea.Document;

        return document is null
            ? null!
            : document.UndoStack;
    }

    void ExecuteUndo(object sender, ExecutedRoutedEventArgs e)
    {
        var undoStack = GetUndoStack();
        
        if (undoStack is not null)
        {
            if (undoStack.CanUndo)
            {
                undoStack.Undo();

                TextArea.Caret.BringCaretToView();
            }

            e.Handled = true;
        }
    }

    void CanExecuteUndo(object sender, CanExecuteRoutedEventArgs e)
    {
        var undoStack = GetUndoStack();
        
        if (undoStack is not null)
        {
            e.Handled = true;
            e.CanExecute = undoStack.CanUndo;
        }
    }

    void ExecuteRedo(object sender, ExecutedRoutedEventArgs e)
    {
        var undoStack = GetUndoStack();
        if (undoStack is not null)
        {
            if (undoStack.CanRedo)
            {
                undoStack.Redo();

                TextArea.Caret.BringCaretToView();
            }

            e.Handled = true;
        }
    }

    void CanExecuteRedo(object sender, CanExecuteRoutedEventArgs e)
    {
        var undoStack = GetUndoStack();
        
        if (undoStack is not null)
        {
            e.Handled = true;
            e.CanExecute = undoStack.CanRedo;
        }
    }
    #endregion
}
