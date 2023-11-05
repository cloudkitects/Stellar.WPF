using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Stellar.WPF.Document;

namespace Stellar.WPF.Editing;

sealed class EmptySelection : Selection
{
    public EmptySelection(TextArea textArea) : base(textArea)
    {
    }

    public override Selection UpdateOnDocumentChange(DocumentChangeEventArgs e) => this;

    public override TextViewPosition StartPosition => new(Location.Empty);

    public override TextViewPosition EndPosition => new(Location.Empty);

    public override ISegment SurroundingSegment => null!;

    public override Selection SetEndpoint(TextViewPosition endPosition) => throw new NotSupportedException();

    public override Selection StartSelectionOrSetEndpoint(TextViewPosition startPosition, TextViewPosition endPosition)
    {
        _ = textArea.Document ?? throw new InvalidOperationException("The text area document is null");

        return Create(textArea, startPosition, endPosition);
    }

    public override IEnumerable<SelectionSegment> Segments => Array.Empty<SelectionSegment>();

    public override string GetText() => string.Empty;

    public override void ReplaceSelectionWithText(string newText)
    {
        newText = AddSpacesIfRequired(
            newText ?? throw new ArgumentNullException(nameof(newText)),
            textArea.Caret.Position,
            textArea.Caret.Position);

        if (newText.Length > 0)
        {
            if (textArea.EditableSectionProvider.CanInsert(textArea.Caret.Offset))
            {
                textArea.Document.Insert(textArea.Caret.Offset, newText);
            }
        }
        
        textArea.Caret.VisualColumn = -1;
    }

    public override int Length => 0;

    // reference equality will do as there's only one EmptySelection per text area.
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);

    public override bool Equals(object obj) => this == obj;
}
