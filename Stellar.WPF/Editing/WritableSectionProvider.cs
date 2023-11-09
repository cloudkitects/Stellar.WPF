using Stellar.WPF.Document;
using System.Collections.Generic;
using System;

using Stellar.WPF.Utilities;

namespace Stellar.WPF.Editing;

/// <summary>
/// Fully editable <see cref="ISectionProvider"/> implementation.
/// </summary>
sealed class WritableSectionProvider : ISectionProvider
{
    public static readonly WritableSectionProvider Instance = new();

    public bool CanInsert(int offset) => true;

    public IEnumerable<ISegment> GetDeletableSegments(ISegment segment) => (segment ?? throw new ArgumentNullException(nameof(segment))).ToEnumerable();
}