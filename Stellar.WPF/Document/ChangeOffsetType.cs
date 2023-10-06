namespace Stellar.WPF.Document
{
    /// <summary>
    /// Contains predefined offset change mapping types.
    /// </summary>
    public enum ChangeOffsetType
    {
        /// <summary>
        /// Anchors in front of the replaced region stay in front, anchors after the replaced region stay in the back.
        /// Anchors in the middle of the removed region will be deleted and move depending on their movement type
        /// if they survive deletion.
        /// </summary>
        /// <remarks>
        /// The default implementation of DocumentChangeEventArgs will work without an
        /// OffsetChanges instance and this value. This is implemented as OffsetChanges with a single entry
        /// describing the replace operation.
        /// </remarks>
        Default,

        /// <summary>
        /// Old text is removed before new text is inserted.
        /// Anchors immediately in front (or after) the replaced region may move to the other side of the insertion,
        /// depending on their movement type.
        /// </summary>
        /// <remarks>
        /// This is implemented as OffsetChanges with two entries: the removal, and the insertion.
        /// </remarks>
        RemoveThenInsert,

        /// <summary>
        /// Replace text character-by-character.
        /// Anchors keep their position inside the replaced text. Anchors after the replaced region will move accordingly if
        /// the replacement text has a different length than the replaced text. If the new text is shorter, anchors inside the
        /// old text that would end up behind the replacement text will be moved to the end of the replacement text.
        /// </summary>
        /// <remarks>
        /// Growing text is implemented by replacing the last character in the replaced text with itself and the additional
        /// text segment: a simple insertion of the additional text would move anchors immediately after the replaced text
        /// into the replacement text if the movement type is 'before insertion'.
        /// Shrinking text is implemented by removing the text segment that's too long but in a special mode that
        /// causes anchors to always survive irrespective of their <see cref="Anchor.SurviveDeletion"/> setting.
        /// If the text keeps its old size, this is implemented as OffsetChanges.Empty.
        /// </remarks>
        ReplaceCharacters,

        /// <summary>
        /// Same as Default but anchors with Default <see cref="Anchor.MovementType"/> stay in front of the insertion instead
        /// of moving behind it.
        /// </summary>
        KeepAnchorsInFront
    }
}
