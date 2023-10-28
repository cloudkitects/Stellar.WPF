﻿using System;
using System.ComponentModel;

namespace Stellar.WPF;

/// <summary>
/// Represents a text editor control (<see cref="TextEditor"/>, <see cref="TextArea"/>
/// or <see cref="TextView"/>).
/// </summary>
public interface ITextEditorComponent : IServiceProvider
{
    /// <summary>
    /// Gets the document being edited.
    /// </summary>
    Document.Document Document { get; }

    /// <summary>
    /// Occurs when the Document property changes (when the text editor is connected to another
    /// document - not when the document content changes).
    /// </summary>
    event EventHandler DocumentChanged;

    /// <summary>
    /// Gets the options of the text editor.
    /// </summary>
    TextEditorOptions Options { get; }

    /// <summary>
    /// Occurs when the Options property changes, or when an option inside the current option list
    /// changes.
    /// </summary>
    event PropertyChangedEventHandler OptionChanged;
}
