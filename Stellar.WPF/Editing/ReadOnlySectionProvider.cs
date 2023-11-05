using System.Collections.Generic;
using System.Linq;

using Stellar.WPF.Document;

namespace Stellar.WPF.Editing;

/// <summary>
/// Non-editable <see cref="IEditableSectionProvider"/> implementation.
/// </summary>
internal class ReadOnlySectionProvider : IEditableSectionProvider
{
    public static readonly ReadOnlySectionProvider Instance = new();

    public bool CanInsert(int offset) => false;

    public IEnumerable<ISegment> GetDeletableSegments(ISegment segment) => Enumerable.Empty<ISegment>();
}