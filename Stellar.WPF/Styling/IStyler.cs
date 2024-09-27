﻿using System;
using System.Collections.Generic;

using Stellar.WPF.Document;

namespace Stellar.WPF.Styling;

/// <summary>
/// Event handler for <see cref="IStyler.StylingStateChanged"/>
/// </summary>
public delegate void StylingStateChangedEventHandler(int fromLineNumber, int toLineNumber);

/// <summary>
/// A contract for styling a document.
/// </summary>
/// <remarks>Used by the <see cref="StyledDocumentRenderer"/> to register stylers as a TextView service.</remarks>
public interface IStyler : IDisposable
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
    /// Styles the specified document line.
    /// </summary>
    /// <returns>A <see cref="StyledLine"/> line object that represents the styled sections.</returns>
    StyledLine StyleLine(int line);

    /// <summary>
    /// Enforces a styling state update (triggering the StylingStateChanged event if necessary)
    /// for all lines up to (and inclusive) the specified line.
    /// </summary>
    void UpdateStylingState(int line);

    /// <summary>
    /// Notification when the styler detects that the styling state at the
    /// <b>beginning</b> of the specified lines has changed.
    /// </summary>
    /// <remarks>
    /// <c>fromLine</c> and <c>toLine</c> are both inclusive, and a
    /// single-line change is represented by <c>fromLine == toLine</c>.
    /// 
    /// Styling of line X raises this event for line X + 1 if the styling
    /// state at the end of X has changed from its previous state.
    /// This event can also be raised after changes to external data (e.g., semantic information).
    ///
    /// Implementers should make sure not to raise this event if state doesn't change for [X,Y] when
    /// there are no document changes between the start of X and the start of Y > X. See
    /// <see cref="HighlightingColorizer.OnHighlightStateChanged"/> for more details.
    /// </remarks>
    event StylingStateChangedEventHandler StylingStateChanged;

    /// <summary>
    /// Opens a group of <see cref="StyleLine"/> calls.
    /// </summary>
    /// <remarks>
    /// Calling this method before calling <see cref="StyleLine"/>, is not required
    /// but can make styling much more performant in some cases, e.g., when re-using a resolver within
    /// a styling group. The group is closed by either a <see cref="EndStyling"/> or a
    /// <see cref="IDisposable.Dispose"/> call. Nested groups are not allowed.
    /// </remarks>
    void BeginStyling();

    /// <summary>
    /// Closes the currently opened group of <see cref="StyleLine"/> calls.
    /// </summary>
    /// <seealso cref="BeginStyling"/>.
    void EndStyling();

    /// <summary>
    /// The default text style.
    /// </summary>
    Style DefaultStyle { get; }
}
