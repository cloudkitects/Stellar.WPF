using System.Collections.Generic;
using System.Diagnostics;

namespace Stellar.WPF.Rendering;

/// <summary>
/// A composite wrapping the list of collapsed sections,
/// the total height--0 if directly collapsed--and actions on it.
/// </summary>
struct CollapsedSections
{
    internal double height;
    internal List<CollapsedSection>? sections;

    internal readonly bool IsDirectlyCollapsed => sections is not null;

    /// <summary>
    /// Returns 0 if the line is directly collapsed, otherwise, returns <see cref="height"/>.
    /// </summary>
    internal readonly double Height => IsDirectlyCollapsed ? 0 : height;

    internal CollapsedSections(double height)
    {
        sections = null;
        this.height = height;
    }

    internal void AddDirectlyCollapsed(CollapsedSection section)
	{
		sections ??= new List<CollapsedSection>();
		
        sections.Add(section);
	}

	internal void RemoveDirectlyCollapsed(CollapsedSection section)
	{
		Debug.Assert(sections!.Contains(section));
		
        sections.Remove(section);
		
        if (sections.Count == 0)
        {
            sections = null;
        }
    }
}
