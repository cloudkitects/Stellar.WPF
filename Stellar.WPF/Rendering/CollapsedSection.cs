using Stellar.WPF.Document;

namespace Stellar.WPF.Rendering;

/// <summary>
/// A section of collapsed lines.
/// </summary>
public sealed class CollapsedSection
{
    private Line start, end;
    private readonly CollapsedSectionsTree heightTree;

#if DEBUG
    internal string ID;
    private static int nextId;
#else
		const string ID = "";
#endif

    internal CollapsedSection(CollapsedSectionsTree heightTree, Line start, Line end)
    {
        this.heightTree = heightTree;
        this.start = start;
        this.end = end;
#if DEBUG
        unchecked
        {
            ID = $"{nextId++}";
        }
#endif
    }

    /// <summary>
    /// Gets if the document line is collapsed.
    /// This property initially is true and turns to false when uncollapsing the section.
    /// </summary>
    public bool IsCollapsed => start is not null;

    /// <summary>
    /// Gets the start line of the section.
    /// When the section is uncollapsed or the text containing it is deleted,
    /// this property returns null.
    /// </summary>
    public Line Start
    {
        get => start;
        internal set => start = value;
    }

    /// <summary>
    /// Gets the end line of the section.
    /// When the section is uncollapsed or the text containing it is deleted,
    /// this property returns null.
    /// </summary>
    public Line End
    {
        get => end;
        internal set => end = value;
    }

    /// <summary>
    /// Uncollapses the section.
    /// This causes the Start and End properties to be set to null!
    /// Does nothing if the section is already uncollapsed.
    /// </summary>
    public void Uncollapse()
    {
        if (start is null)
        {
            return;
        }

        if (!heightTree.IsDisposed)
        {
            heightTree.Uncollapse(this);
#if DEBUG
            heightTree.CheckProperties();
#endif
        }

        start = null!;
        end = null!;
    }

    /// <summary>
    /// Gets a string representation of the collapsed section.
    /// </summary>
    public override string ToString()
    {
        return $"[CollapsedSection ID={ID} Start={(start is not null ? start.Number : "null")} End={(end is not null ? end.Number : "null")}]";
    }
}
