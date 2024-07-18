using System;
using System.ComponentModel;
using System.Reflection;

namespace Stellar.WPF;

/// <summary>
/// A container for text editor options.
/// </summary>
[Serializable]
public class TextEditorOptions : INotifyPropertyChanged
{
    #region constructor
    /// <summary>
    /// Initializes an empty instance of TextEditorOptions.
    /// </summary>
    public TextEditorOptions()
    {
    }

    /// <summary>
    /// Initializes a new instance of TextEditorOptions by copying all values
    /// from <paramref name="options"/> to the new instance.
    /// </summary>
    public TextEditorOptions(TextEditorOptions options)
    {
        var fields = typeof(TextEditorOptions).GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

        foreach (var field in fields)
        {
            if (!field.IsNotSerialized)
            {
                field.SetValue(this, field.GetValue(options));
            }
        }
    }
    #endregion

    #region property changed handling
    /// <inheritdoc/>
    [field: NonSerialized]
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    /// <param name="propertyName">The name of the changed property.</param>
    protected void OnPropertyChanged(string propertyName)
    {
        OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        PropertyChanged?.Invoke(this, e);
    }
    #endregion

    #region non-printables display
    bool showSpaces;
    bool showTabs;
    bool showEndOfLine;
    bool showControlCharacterBox = true;

    /// <summary>
    /// Gets/Sets whether to show · for spaces.
    /// </summary>
    /// <remarks>The default value is <c>false</c>.</remarks>
    [DefaultValue(false)]
    public virtual bool ShowSpaces
    {
        get => showSpaces;
        set
        {
            if (showSpaces != value)
            {
                showSpaces = value;

                OnPropertyChanged(nameof(ShowSpaces));
            }
        }
    }

    /// <summary>
    /// Gets/Sets whether to show » for tabs.
    /// </summary>
    /// <remarks>The default value is <c>false</c>.</remarks>
    [DefaultValue(false)]
    public virtual bool ShowTabs
    {
        get => showTabs;
        set
        {
            if (showTabs != value)
            {
                showTabs = value;
                OnPropertyChanged(nameof(ShowTabs));
            }
        }
    }

    /// <summary>
    /// Gets/Sets whether to show ¶ at the end of lines.
    /// </summary>
    /// <remarks>The default value is <c>false</c>.</remarks>
    [DefaultValue(false)]
    public virtual bool ShowEndOfLine
    {
        get => showEndOfLine;
        set
        {
            if (showEndOfLine != value)
            {
                showEndOfLine = value;
                OnPropertyChanged(nameof(ShowEndOfLine));
            }
        }
    }

    /// <summary>
    /// Gets/Sets whether to show a box with the hex code for control characters.
    /// </summary>
    /// <remarks>The default value is <c>true</c>.</remarks>
    [DefaultValue(true)]
    public virtual bool ShowControlCharacterBox
    {
        get => showControlCharacterBox;
        set
        {
            if (showControlCharacterBox != value)
            {
                showControlCharacterBox = value;

                OnPropertyChanged(nameof(ShowControlCharacterBox));
            }
        }
    }
    #endregion

    #region hyperlinks display
    bool enableHyperlinks = true;
    bool enableEmailHyperlinks = true;
    bool requireControlClick = true;

    /// <summary>
    /// Gets/Sets whether to enable clickable hyperlinks in the editor.
    /// </summary>
    /// <remarks>The default value is <c>true</c>.</remarks>
    [DefaultValue(true)]
    public virtual bool EnableHyperlinks
    {
        get => enableHyperlinks;
        set
        {
            if (enableHyperlinks != value)
            {
                enableHyperlinks = value;

                OnPropertyChanged(nameof(EnableHyperlinks));
            }
        }
    }

    /// <summary>
    /// Gets/Sets whether to enable clickable hyperlinks for e-mail addresses in the editor.
    /// </summary>
    /// <remarks>The default value is <c>true</c>.</remarks>
    [DefaultValue(true)]
    public virtual bool EnableEmailHyperlinks
    {
        get => enableEmailHyperlinks;
        set
        {
            if (enableEmailHyperlinks != value)
            {
                enableEmailHyperlinks = value;

                OnPropertyChanged("EnableEMailHyperlinks");
            }
        }
    }

    /// <summary>
    /// Gets/Sets whether the user needs to press Control to click hyperlinks.
    /// The default value is true.
    /// </summary>
    /// <remarks>The default value is <c>true</c>.</remarks>
    [DefaultValue(true)]
    public virtual bool RequireControlClick
    {
        get => requireControlClick;
        set
        {
            if (requireControlClick != value)
            {
                requireControlClick = value;

                OnPropertyChanged(nameof(requireControlClick));
            }
        }
    }
    #endregion

    #region tab options
    int tabSize = 4;
    bool convertTabsToSpaces;

    /// <summary>
    /// Gets/Sets the width of one indentation unit.
    /// </summary>
    /// <remarks>The default value is 4. Too large a value (~100K) is known to cause
    /// WPF to crash internally later--or sooner for larger fonts (~10K).
    /// </remarks>
    [DefaultValue(4)]
    public virtual int TabSize
    {
        get => tabSize;
        set
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), $"{nameof(TabSize)} {value} < 1");
            }
            if (value > 255)
            {
                throw new ArgumentOutOfRangeException(nameof(value), $"{nameof(TabSize)} {value} > 255");
            }

            if (tabSize != value)
            {
                tabSize = value;
                
                OnPropertyChanged(nameof(TabSize));
                OnPropertyChanged(nameof(TabString));
            }
        }
    }

    /// <summary>
    /// Gets/Sets whether to use spaces for indentation instead of tabs.
    /// </summary>
    /// <remarks>The default value is <c>false</c>.</remarks>
    [DefaultValue(false)]
    public virtual bool ConvertTabsToSpaces
    {
        get => convertTabsToSpaces;
        set
        {
            if (convertTabsToSpaces != value)
            {
                convertTabsToSpaces = value;

                OnPropertyChanged(nameof(ConvertTabsToSpaces));
                OnPropertyChanged(nameof(TabString));
            }
        }
    }

    /// <summary>
    /// Gets the text used for indentation.
    /// </summary>
    [Browsable(false)]
    public string TabString => GetTabString(1);

    /// <summary>
    /// Gets text required to indent from the specified <paramref name="column"/> to the next indentation level.
    /// </summary>
    public virtual string GetTabString(int column)
    {
        if (column < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(column), $"{column} < 1");
        }

        var tabSize = TabSize;

        if (ConvertTabsToSpaces)
        {
            return new string(' ', tabSize - ((column - 1) % tabSize));
        }
        
        return "\t";
    }
    #endregion

    #region miscellaneous
    bool cutCopyWholeLine = true;
    bool allowScrollBelowDocument;
    double wordWrapIndentation = 0;
    bool inheritWordWrapIndentation = true;
    bool enableRectangularSelection = true;
    bool enableTextDragDrop = true;
    bool enableVirtualSpace;
    bool enableImeSupport = true;
    bool showColumnRuler = false;
    int columnRulerPosition = 80;
    bool highlightCurrentLine = false;
    bool hideCursorWhileTyping = true;
    bool allowToggleOverstrikeMode = false;

    /// <summary>
    /// Whether copying without a selection copies the whole current line.
    /// </summary>
    [DefaultValue(true)]
    public virtual bool CutCopyWholeLine
    {
        get => cutCopyWholeLine;
        set
        {
            if (cutCopyWholeLine != value)
            {
                cutCopyWholeLine = value;
                
                OnPropertyChanged(nameof(CutCopyWholeLine));
            }
        }
    }

    /// <summary>
    /// Whether the user can scroll below the bottom of the document, recommended
    /// when using folding.
    /// </summary>
    [DefaultValue(false)]
    public virtual bool AllowScrollBelowDocument
    {
        get => allowScrollBelowDocument;
        set
        {
            if (allowScrollBelowDocument != value)
            {
                allowScrollBelowDocument = value;
                
                OnPropertyChanged(nameof(AllowScrollBelowDocument));
            }
        }
    }

    /// <summary>
    /// Gets/Sets the indentation used for all lines except the first when word-wrapping.
    /// The default value is 0.
    /// </summary>
    [DefaultValue(0.0)]
    public virtual double WordWrapIndentation
    {
        get => wordWrapIndentation;
        set
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "value must be a number and different from infinity");
            }

            if (value != wordWrapIndentation)
            {
                wordWrapIndentation = value;

                OnPropertyChanged(nameof(WordWrapIndentation));
            }
        }
    }

    /// <summary>
    /// Whether indentation is inherited from the first line when word-wrapping.
    /// </summary>
    /// <remarks>When combined with <see cref="WordWrapIndentation"/>, the inherited
    /// indentation is added to the word wrap indentation.</remarks>
    [DefaultValue(true)]
    public virtual bool InheritWordWrapIndentation
    {
        get => inheritWordWrapIndentation;
        set
        {
            if (value != inheritWordWrapIndentation)
            {
                inheritWordWrapIndentation = value;

                OnPropertyChanged(nameof(InheritWordWrapIndentation));
            }
        }
    }

    /// <summary>
    /// Enables rectangular selection (press ALT and select a rectangle).
    /// </summary>
    [DefaultValue(true)]
    public bool EnableRectangularSelection
    {
        get => enableRectangularSelection;
        set
        {
            if (enableRectangularSelection != value)
            {
                enableRectangularSelection = value;

                OnPropertyChanged(nameof(EnableRectangularSelection));
            }
        }
    }

    /// <summary>
    /// Enable dragging text within the text area.
    /// </summary>
    [DefaultValue(true)]
    public bool EnableTextDragDrop
    {
        get => enableTextDragDrop;
        set
        {
            if (enableTextDragDrop != value)
            {
                enableTextDragDrop = value;
                
                OnPropertyChanged(nameof(EnableTextDragDrop));
            }
        }
    }

    /// <summary>
    /// Gets/Sets whether the user can set the caret behind the line ending
    /// (into "virtual space").
    /// Note that virtual space is always used (independent from this setting)
    /// when doing rectangle selections.
    /// </summary>
    [DefaultValue(false)]
    public virtual bool EnableVirtualSpace
    {
        get => enableVirtualSpace;
        set
        {
            if (enableVirtualSpace != value)
            {
                enableVirtualSpace = value;
                
                OnPropertyChanged(nameof(EnableVirtualSpace));
            }
        }
    }

    /// <summary>
    /// Gets/Sets whether the support for Input Method Editors (IME)
    /// for non-alphanumeric scripts (Chinese, Japanese, Korean, ...) is enabled.
    /// </summary>
    [DefaultValue(true)]
    public virtual bool EnableImeSupport
    {
        get => enableImeSupport;
        set
        {
            if (enableImeSupport != value)
            {
                enableImeSupport = value;
                
                OnPropertyChanged(nameof(EnableImeSupport));
            }
        }
    }

    /// <summary>
    /// Gets/Sets whether the column ruler should be shown.
    /// </summary>
    [DefaultValue(false)]
    public virtual bool ShowColumnRuler
    {
        get => showColumnRuler;
        set
        {
            if (showColumnRuler != value)
            {
                showColumnRuler = value;
                
                OnPropertyChanged(nameof(ShowColumnRuler));
            }
        }
    }

    /// <summary>
    /// Gets/Sets where the column ruler should be shown.
    /// </summary>
    [DefaultValue(80)]
    public virtual int ColumnRulerPosition
    {
        get => columnRulerPosition;
        set
        {
            if (columnRulerPosition != value)
            {
                columnRulerPosition = value;
                
                OnPropertyChanged(nameof(ColumnRulerPosition));
            }
        }
    }

    /// <summary>
    /// Gets/Sets if current line should be shown.
    /// </summary>
    [DefaultValue(false)]
    public virtual bool HighlightCurrentLine
    {
        get => highlightCurrentLine;
        set
        {
            if (highlightCurrentLine != value)
            {
                highlightCurrentLine = value;
                
                OnPropertyChanged(nameof(HighlightCurrentLine));
            }
        }
    }

    /// <summary>
    /// Gets/Sets if mouse cursor should be hidden while user is typing.
    /// </summary>
    [DefaultValue(true)]
    public bool HideCursorWhileTyping
    {
        get => hideCursorWhileTyping;
        set
        {
            if (hideCursorWhileTyping != value)
            {
                hideCursorWhileTyping = value;
                
                OnPropertyChanged(nameof(HideCursorWhileTyping));
            }
        }
    }

    /// <summary>
    /// Gets/Sets if the user is allowed to enable/disable overstrike mode.
    /// </summary>
    [DefaultValue(false)]
    public bool AllowToggleOverstrikeMode
    {
        get => allowToggleOverstrikeMode;
        set
        {
            if (allowToggleOverstrikeMode != value)
            {
                allowToggleOverstrikeMode = value;
                
                OnPropertyChanged(nameof(AllowToggleOverstrikeMode));
            }
        }
    }
    #endregion
}
