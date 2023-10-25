using System;
using System.Collections.Generic;

using Stellar.WPF.Document;

namespace Stellar.WPF.Highlighting;

/// <summary>
/// Event handler for <see cref="IHighlighter.HighlightingStateChanged"/>
/// </summary>
public delegate void HighlightingStateChangedEventHandler(int fromLineNumber, int toLineNumber);

/// <summary>
/// A contract for highlighting a document.
/// </summary>
/// <remarks>Used by the <see cref="Colorizer"/> to register highlighters as a TextView service.</remarks>
public interface IHighlighter : IDisposable
{
    /// <summary>
    /// The underlying document.
    /// </summary>
    IDocument Document { get; }

    /// <summary>
    /// The stack of active styles (styles associated with active spans) up to the
    /// end of the specified line. 
    /// </summary>
    /// <remarks>
    /// GetStyles(0) returns an empty stack.
    /// GetStyles(1) returns the styles of the second line.
    /// Elements are returned in inside-out order (first element is the color of the innermost span).
    /// </remarks>
    IEnumerable<Style> GetStyles(int line);

    /// <summary>
    /// Highlights the specified document line.
    /// </summary>
    /// <returns>A <see cref="StyledLine"/> line object that represents the highlighted sections.</returns>
    StyledLine HighlightLine(int line);

    /// <summary>
    /// Enforces a highlighting state update (triggering the HighlightingStateChanged event if necessary)
    /// for all lines up to (and inclusive) the specified line.
    /// </summary>
    void UpdateHighlightingState(int line);

    /// <summary>
    /// Notification when the highlighter detects that the highlighting state at the
    /// <b>beginning</b> of the specified lines has changed.
    /// </summary>
    /// <remarks>
    /// <c>fromLine</c> and <c>toLine</c> are both inclusive, and a
    /// single-line change is represented by <c>fromLine == toLine</c>.
    /// 
    /// Highlighting of line X raises this event for line X + 1 if the highlighting
    /// state at the end of X has changed from its previous state.
    /// This event can also be raised after changes to external data (e.g., semantic information).
    ///
    /// Implementers should make sure not to raise this event if state doesn't change for [X,Y] when
    /// there are no document changes between the start of X and the start of Y > X. See
    /// <see cref="HighlightingColorizer.OnHighlightStateChanged"/> for more details.
    /// </remarks>
    event HighlightingStateChangedEventHandler HighlightingStateChanged;

    /// <summary>
    /// Opens a group of <see cref="HighlightLine"/> calls.
    /// </summary>
    /// <remarks>
    /// Calling this method before calling <see cref="HighlightLine"/>, is not required
    /// but can make highlighting much more performant in some cases, e.g., when re-using a resolver within
    /// a highlighting group. The group is closed by either a <see cref="EndHighlighting"/> or a
    /// <see cref="IDisposable.Dispose"/> call. Nested groups are not allowed.
    /// </remarks>
    void BeginHighlighting();

    /// <summary>
    /// Closes the currently opened group of <see cref="HighlightLine"/> calls.
    /// </summary>
    /// <seealso cref="BeginHighlighting"/>.
    void EndHighlighting();

    /// <summary>
    /// Retrieves the style with the specified name, or null if none matching the name is found.
    /// </summary>
    Style GetStyle(string name);

    /// <summary>
    /// The default text style.
    /// </summary>
    Style DefaultStyle { get; }
}
