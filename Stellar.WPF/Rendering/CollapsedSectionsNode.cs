using System.Collections.Generic;
using System.Diagnostics;

using Stellar.WPF.Document;

namespace Stellar.WPF.Rendering;

/// <summary>
/// A node in the text view's height tree.
/// </summary>
sealed class CollapsedSectionsNode
{
    #region fields and props
    internal readonly Line? line;
    internal CollapsedSectionsNode? left, right, parent;
    internal bool color;

    /// <summary>
    /// List of the sections held collapsed by this node and maintaining a height shortcut.
    /// </summary>
    internal CollapsedSections collapsedSections;

    /// <summary>
    /// List of the sections that hold this node collapsed.
    /// Invariants:
    /// For each line in the collapsing section range, exactly one ancestor contains the collapsing section. 
    /// A collapsing section is contained byt this node, this+left or this+right.
    /// The start and end of a collapsing section always contain the collapsed section in this node.
    /// </summary>
    internal List<CollapsedSection>? collapsingSections;

    /// <summary>
    /// The number of lines in this node and its child nodes.
    /// Invariant:
    /// count = 1 + left.count + right.count
    /// </summary>
    internal int count;

    /// <summary>
    /// The total height of this node and its child nodes, excluding directly collapsed nodes.
    /// Invariant:
    /// height = collapsed sections Height + left.height + right.height
    /// </summary>
    internal double height;


    internal CollapsedSectionsNode LeftMost
    {
        get
        {
            var node = this;

            while (node.left is not null)
            {
                node = node.left;
            }

            return node;
        }
    }

    internal CollapsedSectionsNode RightMost
    {
        get
        {
            var node = this;

            while (node.right is not null)
            {
                node = node.right;
            }

            return node;
        }
    }

    /// <summary>
    /// Gets the inorder successor of the node.
    /// </summary>
    internal CollapsedSectionsNode Successor
    {
        get
        {
            if (right is not null)
            {
                return right.LeftMost;
            }
            
            var node = this;
            CollapsedSectionsNode oldNode;

            do
            {
                oldNode = node;
                node = node.parent;
            }
            while (node is not null && node.right == oldNode);
            
            return node!;
        }
    }

    internal bool IsDirectlyCollapsed
    {
        get
        {
            return collapsingSections is not null;
        }
    }
    #endregion

    #region constructors
    internal CollapsedSectionsNode()
    {
    }

    internal CollapsedSectionsNode(Line documentLine, double height)
    {
        line = documentLine;
        count = 1;
        collapsedSections = new CollapsedSections(height);
        this.height = height;
    }
    #endregion

    #region methods
    internal void AddDirectlyCollapsed(CollapsedSection section)
    {
        if (collapsingSections == null)
        {
            collapsingSections = new List<CollapsedSection>();
            height = 0;
        }

        Debug.Assert(!collapsingSections.Contains(section));
        
        collapsingSections.Add(section);
    }


    internal void RemoveDirectlyCollapsed(CollapsedSection section)
    {
        Debug.Assert(collapsingSections!.Contains(section));
        
        collapsingSections.Remove(section);
        
        if (collapsingSections.Count == 0)
        {
            collapsingSections = null!;

            height = collapsedSections.Height;
            
            if (left is not null)
            {
                height += left.height;
            }

            if (right is not null)
            {
                height += right.height;
            }
        }
    }

#if DEBUG
    public override string ToString()
    {
        var cs = GetCollapsedSections(collapsingSections!);
        var lcs = GetCollapsedSections(collapsedSections.sections!);

        return $"[HeightTreeNode {line.Number} CollapsedSections={cs} Line.CollapsedSections={lcs} Line.Height={collapsedSections.height} height={height}]";
    }

    static string GetCollapsedSections(List<CollapsedSection> list)
    {
        return list is null
            ? "{}":
            $"{{{string.Join(",", list.ConvertAll(cs => cs.ID).ToArray())}}}";
    }
#endif
    #endregion
}
