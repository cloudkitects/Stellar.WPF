using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using Stellar.WPF.Document;
using Stellar.WPF.Utilities;

namespace Stellar.WPF.Rendering;

/// <summary>
/// Red-black tree composite for collapsed sections and their height.
/// </summary>
internal sealed class CollapsedSectionsTree : ILineTracker, IDisposable
{
    #region fields and props
    private readonly Document.Document? document;
    private CollapsedSectionsNode? root;
    private WeakLineTracker? weakLineTracker;

    /// <summary>
    /// Recursion tri-state for updating parent node data.
    /// </summary>
    private enum BubbleUpMode
    {
        None,
        IfRequired,
        Full
    }

    /// <summary>
    /// Default font size-based line height, set by the text view.
    /// </summary>
    private double defaultLineHeight;

    /// <summary>
    /// Default font size-based line height, set by the text view.
    /// </summary>
    /// <remarks>Propagated to collapsed sections and selectively to child nodes.</remarks>
    public double DefaultLineHeight
    {
        get => defaultLineHeight;
        set
        {
            double oldValue = defaultLineHeight;

            if (oldValue == value)
            {
                return;
            }

            defaultLineHeight = value;

            foreach (var node in AllNodes)
            {
                if (node.collapsedSections.height == oldValue)
                {
                    node.collapsedSections.height = value;

                    UpdateData(node, BubbleUpMode.IfRequired);
                }
            }
        }
    }

    /// <summary>
    /// A flag preventing infinite loops while merging nodes in the tree. 
    /// </summary>
    private bool removing;

    private List<CollapsedSectionsNode> nodesToCheckForMerging;

    public bool IsDisposed => root == null;
    #endregion

    #region constructor
    public CollapsedSectionsTree(Document.Document document, double defaultLineHeight)
    {
        this.document = document;

        weakLineTracker = WeakLineTracker.Register(document, this);

        DefaultLineHeight = defaultLineHeight;

        Rebuild();
    }

    public void Dispose()
    {
        weakLineTracker?.Unregister();

        root = null;
        weakLineTracker = null;
    }

    private CollapsedSectionsNode GetNode(Line line)
    {
        return GetNodeAt(line.Number - 1);
    }
    #endregion

    #region ILineTracker
    void ILineTracker.AfterChange(DocumentChangeEventArgs e)
    {
    }

    void ILineTracker.ResetLength(Line line, int newLength)
    {
    }
    #endregion

    #region build/rebuild
    /// <summary>
    /// Rebuild the tree. O(n)
    /// </summary>
    public void Rebuild()
    {
        foreach (var section in GetAllCollapsedSections())
        {
            section.Start = null!;
            section.End = null!;
        }

        var nodes = new CollapsedSectionsNode[document!.LineCount];
        var lineNumber = 0;

        foreach (var line in document.Lines)
        {
            nodes[lineNumber++] = new CollapsedSectionsNode(line, defaultLineHeight);
        }

        Debug.Assert(nodes.Length > 0);
        
        var height = LineTree.GetHeight(nodes.Length);

        Debug.WriteLine("HeightTree will have height: " + height);

        root = Build(nodes, 0, nodes.Length, height);
        root.color = BLACK;
#if DEBUG
        CheckProperties();
#endif
    }

    /// <summary>
    /// build a tree from a list of nodes
    /// </summary>
    private CollapsedSectionsNode Build(CollapsedSectionsNode[] nodes, int start, int end, int subtreeHeight)
    {
        Debug.Assert(start <= end);

        if (start == end)
        {
            return null!;
        }
        
        var middle = (start + end) / 2;
        
        var node = nodes[middle];

        node.left = Build(nodes, start, middle, subtreeHeight - 1);
        node.right = Build(nodes, middle + 1, end, subtreeHeight - 1);
        
        if (node.left is not null)
        {
            node.left.parent = node;
        }

        if (node.right is not null)
        {
            node.right.parent = node;
        }

        if (subtreeHeight == 1)
        {
            node.color = RED;
        }

        UpdateData(node, BubbleUpMode.None);
        
        return node;
    }
    #endregion

    #region insert/remove lines
    void ILineTracker.BeforeRemoving(Line line)
    {
        var node = GetNode(line);

        if (node.collapsedSections.sections is not null)
        {
            foreach (var section in node.collapsedSections.sections.ToArray())
            {
                if (section.Start == line && section.End == line)
                {
                    section.Start = null!;
                    section.End = null!;
                }
                else if (section.Start == line)
                {
                    Uncollapse(section);
                    
                    section.Start = line.NextLine;
                    
                    AddCollapsedSection(section, section.End.Number - section.Start.Number + 1);
                }
                else if (section.End == line)
                {
                    Uncollapse(section);
                    
                    section.End = line.PreviousLine;
                    
                    AddCollapsedSection(section, section.End.Number - section.Start.Number + 1);
                }
            }
        }
        BeginRemoval();

        RemoveNode(node);
        
        // prevent damage if removed line is in "nodesToCheckForMerging"
        node.collapsedSections.sections = null;
        
        EndRemoval();
    }

    void ILineTracker.AfterInserting(Line line, Line newLine)
    {
        InsertAfter(GetNode(line), newLine);
#if DEBUG
        CheckProperties();
#endif
    }

    private CollapsedSectionsNode InsertAfter(CollapsedSectionsNode node, Line newLine)
    {
        var newNode = new CollapsedSectionsNode(newLine, defaultLineHeight);

        if (node.right is null)
        {
            if (node.collapsedSections.sections is not null)
            {
                // copy all collapsedSections that do not end at node
                foreach (var section in node.collapsedSections.sections)
                {
                    if (section.End != node.line)
                    {
                        newNode.AddDirectlyCollapsed(section);
                    }
                }
            }
            
            InsertAsRight(node, newNode);
        }
        else
        {
            node = node.right.LeftMost;
            
            if (node.collapsedSections.sections is not null)
            {
                // copy all collapsedSections that do not start at node
                foreach (var section in node.collapsedSections.sections)
                {
                    if (section.Start != node.line)
                    {
                        newNode.AddDirectlyCollapsed(section);
                    }
                }
            }

            InsertAsLeft(node, newNode);
        }

        return newNode;
    }
    #endregion

    #region rotation callbacks
    private static void UpdateAfterChildrenChange(CollapsedSectionsNode node)
    {
        UpdateData(node, BubbleUpMode.IfRequired);
    }

    /// <summary>
    /// Update node data, namely, its node count and height.
    /// </summary>
    /// <param name="node">The node to update.</param>
    /// <param name="mode">The bubble up mode, i.e., whether to update the parent node.</param>
    private static void UpdateData(CollapsedSectionsNode node, BubbleUpMode mode)
    {
        var c = 1;
        var h = node.collapsedSections.Height;

        if (node.left is not null)
        {
            c += node.left.count;
            h += node.left.height;
        }

        if (node.right is not null)
        {
            c += node.right.count;
            h += node.right.height;
        }
        
        if (node.IsDirectlyCollapsed)
        {
            h = 0;
        }

        if (c != node.count || !h.Nears(node.height) || mode == BubbleUpMode.Full)
        {
            node.count = c;
            node.height = h;
            
            if (node.parent is not null && mode != BubbleUpMode.None)
            {
                UpdateData(node.parent, mode);
            }
        }
    }

    /// <summary>
    /// Swap collapsing and collapsing sections after rotating the node left,
    /// merge them if possible and bubble up the update.
    /// </summary>
    private void UpdateAfterRotateLeft(CollapsedSectionsNode node)
    {
        var papas = node.parent.collapsingSections;
        var these = node.collapsingSections;

        // move these to the new parent
        node.parent.collapsingSections = these;
        node.collapsingSections = null!;

        // fan out the new parent's
        if (papas is not null)
        {
            foreach (var section in papas)
            {
                node.parent.right?.AddDirectlyCollapsed(section);
                
                node.parent.collapsedSections.AddDirectlyCollapsed(section);
                
                node.right?.AddDirectlyCollapsed(section);
            }
        }

        MergeCollapsedSections(node);

        UpdateAfterChildrenChange(node);
    }

    /// <summary>
    /// Swap collapsing and collapsing sections after rotating the node right,
    /// merge them if possible and bubble up the update.
    /// </summary>
    private void UpdateAfterRotateRight(CollapsedSectionsNode node)
    {
        var papas = node.parent.collapsingSections;
        var these = node.collapsingSections;
        
        // move these to the new parent
        node.parent.collapsingSections = these;
        node.collapsingSections = null!;

        // fan out the new parent's
        if (papas is not null)
        {
            foreach (CollapsedSection cs in papas)
            {
                node.parent.left?.AddDirectlyCollapsed(cs);

                node.parent.collapsedSections.AddDirectlyCollapsed(cs);
                node.left?.AddDirectlyCollapsed(cs);
            }
        }

        MergeCollapsedSections(node);

        UpdateAfterChildrenChange(node);
    }

    /// <summary>
    /// Bequeath collapsing sections.
    /// </summary>
    /// <remarks>A node is removed when its successor is removed.
    /// It must bequeath all its collapsing sections before that.</remarks>
    /// <param name="node">The node being removed.</param>
    private void BequeathSections(CollapsedSectionsNode node)
    {
        Debug.Assert(node.left is null || node.right is null);

        var sections = node.collapsingSections;
        
        if (sections is not null)
        {
            var child = node.left ?? node.right;
            
            if (child is not null)
            {
                foreach (var section in sections)
                {
                    child.AddDirectlyCollapsed(section);
                }
            }
        }
        
        if (node.parent is not null)
        {
            MergeCollapsedSections(node.parent);
        }
    }

    /// <summary>
    /// Bequeath collapsing sections.
    /// </summary>
    /// <remarks>A node is removed when its successor replaces it.
    /// It must bequeath all its collapsing sections before that.</remarks>
    /// <param name="node">The node being replaced.</param>
    private void BequeathSections(CollapsedSectionsNode node, CollapsedSectionsNode newNode, CollapsedSectionsNode newNodeParent)
    {
        Debug.Assert(node is not null && newNode is not null);

        while (newNodeParent != node)
        {
            if (newNodeParent.collapsingSections is not null)
            {
                foreach (var section in newNodeParent.collapsingSections)
                {
                    newNode.collapsedSections.AddDirectlyCollapsed(section);
                }
            }

            newNodeParent = newNodeParent.parent;
        }

        if (newNode.collapsingSections is not null)
        {
            foreach (var section in newNode.collapsingSections)
            {
                newNode.collapsedSections.AddDirectlyCollapsed(section);
            }
        }

        newNode.collapsingSections = node.collapsingSections;

        MergeCollapsedSections(newNode);
    }

    private void BeginRemoval()
    {
        Debug.Assert(!removing);
        
        nodesToCheckForMerging ??= new List<CollapsedSectionsNode>();
        
        removing = true;
    }

    private void EndRemoval()
    {
        Debug.Assert(removing);

        removing = false;
        
        foreach (var node in nodesToCheckForMerging)
        {
            MergeCollapsedSections(node);
        }

        nodesToCheckForMerging.Clear();
    }

    /// <summary>
    /// Merge collapsed sections when possible, i.e., when a collapsed section
    /// of this node is reported as contained by a child's collapsing section.
    /// </summary>
    /// <param name="node">The node from which to start merging.</param>
    /// <remarks>The merge--if any--is bubbled up.</remarks>
    private void MergeCollapsedSections(CollapsedSectionsNode node)
    {
        Debug.Assert(node is not null);

        if (removing)
        {
            nodesToCheckForMerging.Add(node);

            return;
        }

        // now check if we need to merge collapsedSections together
        bool merged = false;
        var sections = node.collapsedSections.sections;
        
        if (sections is not null)
        {
            for (var i = sections.Count - 1; i >= 0; i--)
            {
                var section = sections[i];

                if (section.Start == node.line || section.End == node.line)
                {
                    continue;
                }

                if (node.left is null || (node.left.collapsingSections is not null && node.left.collapsingSections.Contains(section)))
                {
                    if (node.right is null || (node.right.collapsingSections is not null && node.right.collapsingSections.Contains(section)))
                    {
                        // merge--all children of node contain the section
                        node.left?.RemoveDirectlyCollapsed(section);
                        node.right?.RemoveDirectlyCollapsed(section);

                        sections.RemoveAt(i);
                        
                        node.AddDirectlyCollapsed(section);
                        
                        merged = true;
                    }
                }
            }
            
            if (sections.Count == 0)
            {
                node.collapsedSections.sections = null;
            }
        }

        if (merged && node.parent is not null)
        {
            MergeCollapsedSections(node.parent);
        }
    }
    #endregion

    #region GetNodeBy... / Get...FromNode
    private CollapsedSectionsNode GetNodeAt(int index)
    {
        Debug.Assert(index >= 0);
        Debug.Assert(index < root.count);
        CollapsedSectionsNode node = root;
        while (true)
        {
            if (node.left != null && index < node.left.count)
            {
                node = node.left;
            }
            else
            {
                if (node.left is not null)
                {
                    index -= node.left.count;
                }
                if (index == 0)
                {
                    return node;
                }

                index--;
                node = node.right;
            }
        }
    }

    private CollapsedSectionsNode GetNodeByVisualPosition(double position)
    {
        CollapsedSectionsNode node = root;
        while (true)
        {
            double positionAfterLeft = position;
            if (node.left is not null)
            {
                positionAfterLeft -= node.left.height;
                if (positionAfterLeft < 0)
                {
                    // Descend into left
                    node = node.left;
                    continue;
                }
            }
            double positionBeforeRight = positionAfterLeft - node.collapsedSections.Height;
            if (positionBeforeRight < 0)
            {
                // Found the correct node
                return node;
            }
            if (node.right == null || node.right.height == 0)
            {
                // Can happen when position>node.totalHeight,
                // i.e. at the end of the document, or due to rounding errors in previous loop iterations.

                // If node.lineNode isn't collapsed, return that.
                // Also return node.lineNode if there is no previous node that we could return instead.
                if (node.collapsedSections.Height > 0 || node.left == null)
                {
                    return node;
                }
                // Otherwise, descend into left (find the last non-collapsed node)
                node = node.left;
            }
            else
            {
                // Descend into right
                position = positionBeforeRight;
                node = node.right;
            }
        }
    }

    private static double GetVisualPositionFromNode(CollapsedSectionsNode node)
    {
        double position = (node.left != null) ? node.left.height : 0;
        while (node.parent != null)
        {
            if (node.IsDirectlyCollapsed)
            {
                position = 0;
            }

            if (node == node.parent.right)
            {
                if (node.parent.left is not null)
                {
                    position += node.parent.left.height;
                }

                position += node.parent.collapsedSections.Height;
            }
            node = node.parent;
        }
        return position;
    }
    #endregion

    #region Public methods
    public Line GetLineByNumber(int number)
    {
        return GetNodeAt(number - 1).line;
    }

    public Line GetLineByVisualPosition(double position)
    {
        return GetNodeByVisualPosition(position).line;
    }

    public double GetVisualPosition(Line line)
    {
        return GetVisualPositionFromNode(GetNode(line));
    }

    public double GetHeight(Line line)
    {
        return GetNode(line).collapsedSections.height;
    }

    public void SetHeight(Line line, double val)
    {
        var node = GetNode(line);
        node.collapsedSections.height = val;
        UpdateAfterChildrenChange(node);
    }

    public bool GetIsCollapsed(int lineNumber)
    {
        var node = GetNodeAt(lineNumber - 1);
        return node.collapsedSections.IsDirectlyCollapsed || GetIsCollapedFromNode(node);
    }

    /// <summary>
    /// Collapses the specified text section.
    /// Runtime: O(log n)
    /// </summary>
    public CollapsedSection CollapseText(Line start, Line end)
    {
        if (!document.Lines.Contains(start))
        {
            throw new ArgumentException("Line is not part of this document", "start");
        }

        if (!document.Lines.Contains(end))
        {
            throw new ArgumentException("Line is not part of this document", "end");
        }

        int length = end.Number - start.Number + 1;
        if (length < 0)
        {
            throw new ArgumentException("start must be a line before end");
        }

        CollapsedSection section = new CollapsedSection(this, start, end);
        AddCollapsedSection(section, length);
#if DEBUG
        CheckProperties();
#endif
        return section;
    }
    #endregion

    #region LineCount & TotalHeight
    public int LineCount => root.count;

    public double TotalHeight => root.height;
    #endregion

    #region GetAllCollapsedSections
    private IEnumerable<CollapsedSectionsNode> AllNodes
    {
        get
        {
            if (root is not null)
            {
                CollapsedSectionsNode node = root.LeftMost;
                while (node != null)
                {
                    yield return node;
                    node = node.Successor;
                }
            }
        }
    }

    internal IEnumerable<CollapsedSection> GetAllCollapsedSections()
    {
        List<CollapsedSection> emptyCSList = new List<CollapsedSection>();
        return System.Linq.Enumerable.Distinct(
            System.Linq.Enumerable.SelectMany(
                AllNodes, node => System.Linq.Enumerable.Concat(node.collapsedSections.sections ?? emptyCSList,
                                                                node.collapsingSections ?? emptyCSList)
            ));
    }
    #endregion

    #region CheckProperties
#if DEBUG
    [Conditional("DATACONSISTENCYTEST")]
    internal void CheckProperties()
    {
        CheckProperties(root);

        foreach (CollapsedSection cs in GetAllCollapsedSections())
        {
            Debug.Assert(GetNode(cs.Start).collapsedSections.sections.Contains(cs));
            Debug.Assert(GetNode(cs.End).collapsedSections.sections.Contains(cs));
            int endLine = cs.End.Number;
            for (int i = cs.Start.Number; i <= endLine; i++)
            {
                CheckIsInSection(cs, GetLineByNumber(i));
            }
        }

        // check red-black property:
        int blackCount = -1;
        CheckNodeProperties(root, null, RED, 0, ref blackCount);
    }

    private void CheckIsInSection(CollapsedSection cs, Line line)
    {
        CollapsedSectionsNode node = GetNode(line);
        if (node.collapsedSections.sections != null && node.collapsedSections.sections.Contains(cs))
        {
            return;
        }

        while (node != null)
        {
            if (node.collapsingSections != null && node.collapsingSections.Contains(cs))
            {
                return;
            }

            node = node.parent;
        }
        throw new InvalidOperationException(cs + " not found for line " + line);
    }

    private void CheckProperties(CollapsedSectionsNode node)
    {
        int totalCount = 1;
        double totalHeight = node.collapsedSections.Height;
        if (node.collapsedSections.IsDirectlyCollapsed)
        {
            Debug.Assert(node.collapsedSections.sections.Count > 0);
        }

        if (node.left != null)
        {
            CheckProperties(node.left);
            totalCount += node.left.count;
            totalHeight += node.left.height;

            CheckAllContainedIn(node.left.collapsingSections, node.collapsedSections.sections);
        }
        if (node.right != null)
        {
            CheckProperties(node.right);
            totalCount += node.right.count;
            totalHeight += node.right.height;

            CheckAllContainedIn(node.right.collapsingSections, node.collapsedSections.sections);
        }
        if (node.left != null && node.right != null)
        {
            if (node.left.collapsingSections != null && node.right.collapsingSections != null)
            {
                var intersection = System.Linq.Enumerable.Intersect(node.left.collapsingSections, node.right.collapsingSections);
                Debug.Assert(System.Linq.Enumerable.Count(intersection) == 0);
            }
        }
        if (node.IsDirectlyCollapsed)
        {
            Debug.Assert(node.collapsingSections.Count > 0);
            totalHeight = 0;
        }
        Debug.Assert(node.count == totalCount);
        Debug.Assert(node.height.Nears(totalHeight));
    }

    /// <summary>
    /// Checks that all elements in list1 are contained in list2.
    /// </summary>
    private static void CheckAllContainedIn(IEnumerable<CollapsedSection> list1, ICollection<CollapsedSection> list2)
    {
        list1 ??= new List<CollapsedSection>();

        list2 ??= new List<CollapsedSection>();

        foreach (CollapsedSection cs in list1)
        {
            Debug.Assert(list2.Contains(cs));
        }
    }

    /*
	1. A node is either red or black.
	2. The root is black.
	3. All leaves are black. (The leaves are the NIL children.)
	4. Both children of every red node are black. (So every red node must have a black parent.)
	5. Every simple path from a node to a descendant leaf contains the same number of black nodes. (Not counting the leaf node.)
	 */
    private void CheckNodeProperties(CollapsedSectionsNode node, CollapsedSectionsNode parentNode, bool parentColor, int blackCount, ref int expectedBlackCount)
    {
        if (node == null)
        {
            return;
        }

        Debug.Assert(node.parent == parentNode);

        if (parentColor == RED)
        {
            Debug.Assert(node.color == BLACK);
        }
        if (node.color == BLACK)
        {
            blackCount++;
        }
        if (node.left == null && node.right == null)
        {
            // node is a leaf node:
            if (expectedBlackCount == -1)
            {
                expectedBlackCount = blackCount;
            }
            else
            {
                Debug.Assert(expectedBlackCount == blackCount);
            }
        }
        CheckNodeProperties(node.left, node, node.color, blackCount, ref expectedBlackCount);
        CheckNodeProperties(node.right, node, node.color, blackCount, ref expectedBlackCount);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
    public string GetTreeAsString()
    {
        StringBuilder b = new StringBuilder();
        AppendTreeToString(root, b, 0);
        return b.ToString();
    }

    private static void AppendTreeToString(CollapsedSectionsNode node, StringBuilder b, int indent)
    {
        if (node.color == RED)
        {
            b.Append("RED   ");
        }
        else
        {
            b.Append("BLACK ");
        }

        b.AppendLine(node.ToString());
        indent += 2;
        if (node.left != null)
        {
            b.Append(' ', indent);
            b.Append("L: ");
            AppendTreeToString(node.left, b, indent);
        }
        if (node.right != null)
        {
            b.Append(' ', indent);
            b.Append("R: ");
            AppendTreeToString(node.right, b, indent);
        }
    }
#endif
    #endregion

    #region Red/Black Tree
    private const bool RED = true;
    private const bool BLACK = false;

    private void InsertAsLeft(CollapsedSectionsNode parentNode, CollapsedSectionsNode newNode)
    {
        Debug.Assert(parentNode.left == null);
        parentNode.left = newNode;
        newNode.parent = parentNode;
        newNode.color = RED;
        UpdateAfterChildrenChange(parentNode);
        FixTreeOnInsert(newNode);
    }

    private void InsertAsRight(CollapsedSectionsNode parentNode, CollapsedSectionsNode newNode)
    {
        Debug.Assert(parentNode.right == null);
        parentNode.right = newNode;
        newNode.parent = parentNode;
        newNode.color = RED;
        UpdateAfterChildrenChange(parentNode);
        FixTreeOnInsert(newNode);
    }

    private void FixTreeOnInsert(CollapsedSectionsNode node)
    {
        Debug.Assert(node != null);
        Debug.Assert(node.color == RED);
        Debug.Assert(node.left == null || node.left.color == BLACK);
        Debug.Assert(node.right == null || node.right.color == BLACK);

        CollapsedSectionsNode parentNode = node.parent;
        if (parentNode == null)
        {
            // we inserted in the root -> the node must be black
            // since this is a root node, making the node black increments the number of black nodes
            // on all paths by one, so it is still the same for all paths.
            node.color = BLACK;
            return;
        }
        if (parentNode.color == BLACK)
        {
            // if the parent node where we inserted was black, our red node is placed correctly.
            // since we inserted a red node, the number of black nodes on each path is unchanged
            // -> the tree is still balanced
            return;
        }
        // parentNode is red, so there is a conflict here!

        // because the root is black, parentNode is not the root -> there is a grandparent node
        CollapsedSectionsNode grandparentNode = parentNode.parent;
        CollapsedSectionsNode uncleNode = Sibling(parentNode);
        if (uncleNode != null && uncleNode.color == RED)
        {
            parentNode.color = BLACK;
            uncleNode.color = BLACK;
            grandparentNode.color = RED;
            FixTreeOnInsert(grandparentNode);
            return;
        }
        // now we know: parent is red but uncle is black
        // First rotation:
        if (node == parentNode.right && parentNode == grandparentNode.left)
        {
            RotateLeft(parentNode);
            node = node.left;
        }
        else if (node == parentNode.left && parentNode == grandparentNode.right)
        {
            RotateRight(parentNode);
            node = node.right;
        }
        // because node might have changed, reassign variables:
        parentNode = node.parent;
        grandparentNode = parentNode.parent;

        // Now recolor a bit:
        parentNode.color = BLACK;
        grandparentNode.color = RED;
        // Second rotation:
        if (node == parentNode.left && parentNode == grandparentNode.left)
        {
            RotateRight(grandparentNode);
        }
        else
        {
            // because of the first rotation, this is guaranteed:
            Debug.Assert(node == parentNode.right && parentNode == grandparentNode.right);
            RotateLeft(grandparentNode);
        }
    }

    private void RemoveNode(CollapsedSectionsNode removedNode)
    {
        if (removedNode.left != null && removedNode.right != null)
        {
            // replace removedNode with it's in-order successor

            CollapsedSectionsNode leftMost = removedNode.right.LeftMost;
            CollapsedSectionsNode parentOfLeftMost = leftMost.parent;
            RemoveNode(leftMost); // remove leftMost from its current location

            BequeathSections(removedNode, leftMost, parentOfLeftMost);
            // and overwrite the removedNode with it
            ReplaceNode(removedNode, leftMost);
            leftMost.left = removedNode.left;
            if (leftMost.left is not null)
            {
                leftMost.left.parent = leftMost;
            }

            leftMost.right = removedNode.right;
            if (leftMost.right is not null)
            {
                leftMost.right.parent = leftMost;
            }

            leftMost.color = removedNode.color;

            UpdateAfterChildrenChange(leftMost);
            if (leftMost.parent is not null)
            {
                UpdateAfterChildrenChange(leftMost.parent);
            }

            return;
        }

        // now either removedNode.left or removedNode.right is null
        // get the remaining child
        CollapsedSectionsNode parentNode = removedNode.parent;
        CollapsedSectionsNode childNode = removedNode.left ?? removedNode.right;
        BequeathSections(removedNode);
        ReplaceNode(removedNode, childNode);
        if (parentNode is not null)
        {
            UpdateAfterChildrenChange(parentNode);
        }

        if (removedNode.color == BLACK)
        {
            if (childNode != null && childNode.color == RED)
            {
                childNode.color = BLACK;
            }
            else
            {
                FixTreeOnDelete(childNode, parentNode);
            }
        }
    }

    private void FixTreeOnDelete(CollapsedSectionsNode node, CollapsedSectionsNode parentNode)
    {
        Debug.Assert(node == null || node.parent == parentNode);
        if (parentNode == null)
        {
            return;
        }

        // warning: node may be null
        CollapsedSectionsNode sibling = Sibling(node, parentNode);
        if (sibling.color == RED)
        {
            parentNode.color = RED;
            sibling.color = BLACK;
            if (node == parentNode.left)
            {
                RotateLeft(parentNode);
            }
            else
            {
                RotateRight(parentNode);
            }

            sibling = Sibling(node, parentNode); // update value of sibling after rotation
        }

        if (parentNode.color == BLACK
            && sibling.color == BLACK
            && GetColor(sibling.left) == BLACK
            && GetColor(sibling.right) == BLACK)
        {
            sibling.color = RED;
            FixTreeOnDelete(parentNode, parentNode.parent);
            return;
        }

        if (parentNode.color == RED
            && sibling.color == BLACK
            && GetColor(sibling.left) == BLACK
            && GetColor(sibling.right) == BLACK)
        {
            sibling.color = RED;
            parentNode.color = BLACK;
            return;
        }

        if (node == parentNode.left &&
            sibling.color == BLACK &&
            GetColor(sibling.left) == RED &&
            GetColor(sibling.right) == BLACK)
        {
            sibling.color = RED;
            sibling.left.color = BLACK;
            RotateRight(sibling);
        }
        else if (node == parentNode.right &&
                 sibling.color == BLACK &&
                 GetColor(sibling.right) == RED &&
                 GetColor(sibling.left) == BLACK)
        {
            sibling.color = RED;
            sibling.right.color = BLACK;
            RotateLeft(sibling);
        }
        sibling = Sibling(node, parentNode); // update value of sibling after rotation

        sibling.color = parentNode.color;
        parentNode.color = BLACK;
        if (node == parentNode.left)
        {
            if (sibling.right is not null)
            {
                Debug.Assert(sibling.right.color == RED);
                sibling.right.color = BLACK;
            }
            RotateLeft(parentNode);
        }
        else
        {
            if (sibling.left is not null)
            {
                Debug.Assert(sibling.left.color == RED);
                sibling.left.color = BLACK;
            }
            RotateRight(parentNode);
        }
    }

    private void ReplaceNode(CollapsedSectionsNode replacedNode, CollapsedSectionsNode newNode)
    {
        if (replacedNode.parent == null)
        {
            Debug.Assert(replacedNode == root);
            root = newNode;
        }
        else
        {
            if (replacedNode.parent.left == replacedNode)
            {
                replacedNode.parent.left = newNode;
            }
            else
            {
                replacedNode.parent.right = newNode;
            }
        }
        if (newNode is not null)
        {
            newNode.parent = replacedNode.parent;
        }
        replacedNode.parent = null;
    }

    private void RotateLeft(CollapsedSectionsNode p)
    {
        // let q be p's right child
        CollapsedSectionsNode q = p.right;
        Debug.Assert(q != null);
        Debug.Assert(q.parent == p);
        // set q to be the new root
        ReplaceNode(p, q);

        // set p's right child to be q's left child
        p.right = q.left;
        if (p.right is not null)
        {
            p.right.parent = p;
        }
        // set q's left child to be p
        q.left = p;
        p.parent = q;
        UpdateAfterRotateLeft(p);
    }

    private void RotateRight(CollapsedSectionsNode p)
    {
        // let q be p's left child
        CollapsedSectionsNode q = p.left;
        Debug.Assert(q != null);
        Debug.Assert(q.parent == p);
        // set q to be the new root
        ReplaceNode(p, q);

        // set p's left child to be q's right child
        p.left = q.right;
        if (p.left is not null)
        {
            p.left.parent = p;
        }
        // set q's right child to be p
        q.right = p;
        p.parent = q;
        UpdateAfterRotateRight(p);
    }

    private static CollapsedSectionsNode Sibling(CollapsedSectionsNode node)
    {
        if (node == node.parent.left)
        {
            return node.parent.right;
        }
        else
        {
            return node.parent.left;
        }
    }

    private static CollapsedSectionsNode Sibling(CollapsedSectionsNode node, CollapsedSectionsNode parentNode)
    {
        Debug.Assert(node == null || node.parent == parentNode);
        if (node == parentNode.left)
        {
            return parentNode.right;
        }
        else
        {
            return parentNode.left;
        }
    }

    private static bool GetColor(CollapsedSectionsNode node)
    {
        return node != null ? node.color : BLACK;
    }
    #endregion

    #region Collapsing support
    private static bool GetIsCollapedFromNode(CollapsedSectionsNode node)
    {
        while (node != null)
        {
            if (node.IsDirectlyCollapsed)
            {
                return true;
            }

            node = node.parent;
        }
        return false;
    }

    internal void AddCollapsedSection(CollapsedSection section, int sectionLength)
    {
        AddRemoveCollapsedSection(section, sectionLength, true);
    }

    private void AddRemoveCollapsedSection(CollapsedSection section, int sectionLength, bool add)
    {
        Debug.Assert(sectionLength > 0);

        CollapsedSectionsNode node = GetNode(section.Start);
        // Go up in the tree.
        while (true)
        {
            // Mark all middle nodes as collapsed
            if (add)
            {
                node.collapsedSections.AddDirectlyCollapsed(section);
            }
            else
            {
                node.collapsedSections.RemoveDirectlyCollapsed(section);
            }

            sectionLength -= 1;
            if (sectionLength == 0)
            {
                // we are done!
                Debug.Assert(node.line == section.End);
                break;
            }
            // Mark all right subtrees as collapsed.
            if (node.right is not null)
            {
                if (node.right.count < sectionLength)
                {
                    if (add)
                    {
                        node.right.AddDirectlyCollapsed(section);
                    }
                    else
                    {
                        node.right.RemoveDirectlyCollapsed(section);
                    }

                    sectionLength -= node.right.count;
                }
                else
                {
                    // mark partially into the right subtree: go down the right subtree.
                    AddRemoveCollapsedSectionDown(section, node.right, sectionLength, add);
                    break;
                }
            }
            // go up to the next node
            CollapsedSectionsNode parentNode = node.parent;
            Debug.Assert(parentNode != null);
            while (parentNode.right == node)
            {
                node = parentNode;
                parentNode = node.parent;
                Debug.Assert(parentNode != null);
            }
            node = parentNode;
        }
        UpdateData(GetNode(section.Start), BubbleUpMode.Full);
        UpdateData(GetNode(section.End), BubbleUpMode.Full);
    }

    private static void AddRemoveCollapsedSectionDown(CollapsedSection section, CollapsedSectionsNode node, int sectionLength, bool add)
    {
        while (true)
        {
            if (node.left is not null)
            {
                if (node.left.count < sectionLength)
                {
                    // mark left subtree
                    if (add)
                    {
                        node.left.AddDirectlyCollapsed(section);
                    }
                    else
                    {
                        node.left.RemoveDirectlyCollapsed(section);
                    }

                    sectionLength -= node.left.count;
                }
                else
                {
                    // mark only inside the left subtree
                    node = node.left;
                    Debug.Assert(node != null);
                    continue;
                }
            }
            if (add)
            {
                node.collapsedSections.AddDirectlyCollapsed(section);
            }
            else
            {
                node.collapsedSections.RemoveDirectlyCollapsed(section);
            }

            sectionLength -= 1;
            if (sectionLength == 0)
            {
                // done!
                Debug.Assert(node.line == section.End);
                break;
            }
            // mark inside right subtree:
            node = node.right;
            Debug.Assert(node != null);
        }
    }

    public void Uncollapse(CollapsedSection section)
    {
        int sectionLength = section.End.Number - section.Start.Number + 1;
        AddRemoveCollapsedSection(section, sectionLength, false);
        // do not call CheckProperties() in here - Uncollapse is also called during line removals
    }
    #endregion
}
