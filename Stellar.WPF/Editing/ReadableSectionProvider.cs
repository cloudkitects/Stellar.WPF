using System.Collections.Generic;
using System.Linq;

using Stellar.WPF.Document;

namespace Stellar.WPF.Editing;

/// <summary>
/// Non-editable <see cref="ISectionProvider"/> implementation.
/// </summary>
internal class ReadableSectionProvider : ISectionProvider
{
    public static readonly ReadableSectionProvider Instance = new();

    public bool CanInsert(int offset) => false;

    public IEnumerable<ISegment> GetDeletableSegments(ISegment segment) => Enumerable.Empty<ISegment>();
}