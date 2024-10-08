﻿using System;

namespace Stellar.WPF.Document;

/// <summary>
/// A text anchor references an offset (a position between two characters in a text) that is
/// updated when text is inserted/removed in front of it.
/// </summary>
/// <remarks>
/// <para>Use the <see cref="ITextAnchor.Offset"/> property to get the offset from a text anchor.
/// Use the <see cref="IDocument.CreateAnchor"/> method to create an anchor from an offset.
/// </para>
/// <para>
/// The document automatically updates all text anchors using weak references so that the
/// garbage collector eats the anchors when they're no longer needed.
/// </para>
/// <para>The document also retrieves and updates a large number of anchors without looking
/// at each individually, taking O(logN).</para>
/// </remarks>
/// <example>
/// Usage:
/// <code>TextAnchor anchor = document.CreateAnchor(offset);
/// ChangeMyDocument();
/// int newOffset = anchor.Offset;
/// </code>
/// </example>
public interface ITextAnchor
{
    /// <summary>
    /// The text location of this anchor.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when trying to get the Offset from a deleted anchor.</exception>
    Location Location { get; }

    /// <summary>
    /// The anchor's offset.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when trying to get the Offset from a deleted anchor.</exception>
    int Offset { get; } // TODO: rename as Position

    /// <summary>
    /// How the anchor moves.
    /// </summary>
    /// <remarks>Anchor movement is ambiguous if text is inserted exactly at the anchor's location.
    /// Does the anchor stay before the inserted text, or does it move after it?
    /// This property determines which of these two options the anchor will use.
    /// The default value is <see cref="AnchorMovementType.Default"/>.</remarks>
    AnchorMovementType MovementType { get; set; }

    /// <summary>
    /// <para>
    /// Whether the anchor survives deletion of the text containing it.
    /// </para><para>
    /// <c>false</c>: The anchor is deleted when the a selection that includes the anchor is deleted.
    /// <c>true</c>: The anchor is not deleted.
    /// </para>
    /// </summary>
    /// <remarks><inheritdoc cref="IsDeleted" /></remarks>
    bool SurviveDeletion { get; set; }

    /// <summary>
    /// Whether the anchor was deleted.
    /// </summary>
    /// <remarks>
    /// <para>When a piece of text containing an anchor is removed, then that anchor will be deleted.
    /// First, the <see cref="IsDeleted"/> property is set to true on all deleted anchors,
    /// then the <see cref="Deleted"/> events are raised.
    /// You cannot retrieve the offset from an anchor that has been deleted.</para>
    /// <para>This deletion behavior might be useful when using anchors for building a bookmark feature,
    /// but in other cases you want to still be able to use the anchor. For those cases, set
    /// <c><see cref="SurviveDeletion"/> = true</c>.</para>
    /// </remarks>
    bool IsDeleted { get; }

    /// <summary>
    /// Occurs after the anchor was deleted.
    /// </summary>
    /// <remarks>
    /// <inheritdoc cref="IsDeleted" />
    /// <para>Triggered only while code holds a reference to the TextAnchor object, given the
    /// weak reference nature of text anchors.
    /// </para>
    /// </remarks>
    event EventHandler Deleted;

    /// <summary>
    /// The anchor's line number.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when trying to get the Offset from a deleted anchor.</exception>
    int Line { get; }

    /// <summary>
    /// The anchor's column number (position within the line).
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when trying to get the Offset from a deleted anchor.</exception>
    int Column { get; }
}
