using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System;

namespace Stellar.WPF.Document;

/// <summary>
/// An augmented red-black tree of document lines implementing
/// most operations in O(logN).
/// </summary>
/// <remarks>
/// The tree is never empty, it initially contains an empty line.
/// </remarks>
internal sealed class LineTree : IList<Line>
{
    #region fields and props
    internal const bool RED = true;
    internal const bool BLACK = false;

    private readonly Document document;
    private Line root;

    public int LineCount => root.lineCount;
    #endregion

    #region constructor
    public LineTree(Document document)
    {
        this.document = document;

        Line emptyLine = new(document);

        root = emptyLine.Init();
    }
    #endregion

    #region rotation callbacks
    internal static void UpdateNodeData(Line line)
    {
        var totalCount = 1;
        var totalLength = line.TextLength;

        if (line.left is not null)
        {
            totalCount += line.left.lineCount;
            totalLength += line.left.totalLength;
        }

        if (line.right is not null)
        {
            totalCount += line.right.lineCount;
            totalLength += line.right.totalLength;
        }

        if (totalCount != line.lineCount || totalLength != line.totalLength)
        {
            line.lineCount = totalCount;
            line.totalLength = totalLength;

            if (line?.parent is not null)
            {
                UpdateNodeData(line.parent);
            }
        }
    }
    #endregion

    #region tree building
    /// <summary>
    /// Build a tree from lines.
    /// </summary>
    private Line BuildTree(Line[] lines, int start, int end, int height)
    {
        Debug.Assert(start <= end);

        if (start == end)
        {
            return null!;
        }

        var mid = (start + end) / 2;

        var line = lines[mid];

        line.left = BuildTree(lines, start, mid, height - 1);
        line.right = BuildTree(lines, mid + 1, end, height - 1);

        if (line.left is not null)
        {
            line.left.parent = line;
        }

        if (line.right is not null)
        {
            line.right.parent = line;
        }

        if (height == 1)
        {
            line.color = RED;
        }

        UpdateNodeData(line);

        return line;
    }

    internal static int GetHeight(int size)
    {
        return size == 0
            ? 0
            : GetHeight(size / 2) + 1;
    }

    /// <summary>
    /// Rebuild the tree, in O(n).
    /// </summary>
    public void RebuildTree(List<Line> lines)
    {
        var tree = new Line[lines.Count];

        for (int i = 0; i < lines.Count; i++)
        {
            tree[i] = lines[i].Init();
        }

        Debug.Assert(tree.Length > 0);

        // now build the corresponding balanced tree
        var height = GetHeight(tree.Length);

        Debug.WriteLine("line tree height: " + height);

        root = BuildTree(tree, 0, tree.Length, height);

        root.color = BLACK;
#if DEBUG
        ValidateData();
#endif
    }
    #endregion

    #region properties to/from line
    public Line? LineAt(int index)
    {
        Debug.Assert(index >= 0);
        Debug.Assert(index < root.lineCount);

        var line = root;

        while (true)
        {
            if (line?.left is not null && index < line?.left.lineCount)
            {
                line = line?.left;
            }
            else
            {
                if (line?.left is not null)
                {
                    index -= line.left.lineCount;
                }

                if (index == 0)
                {
                    return line;
                }

                index--;

                line = line?.right;
            }
        }
    }

    internal static int IndexOf(Line node)
    {
        var index = node.left is not null
            ? node.left.lineCount
            : 0;

        while (node.parent is not null)
        {
            if (node == node.parent.right)
            {
                if (node.parent.left is not null)
                {
                    index += node.parent.left.lineCount;
                }

                index++;
            }

            node = node.parent;
        }

        return index;
    }

    public Line LineBy(int offset)
    {
        Debug.Assert(offset >= 0);
        Debug.Assert(offset <= root.totalLength);

        if (offset == root.totalLength)
        {
            return root.RightMost;
        }

        var line = root;

        while (true)
        {
            if (line?.left is not null && offset < line?.left.totalLength)
            {
                line = line?.left;
            }
            else
            {
                if (line?.left is not null)
                {
                    offset -= line.left.totalLength;
                }

                offset -= line.TextLength;

                if (offset < 0)
                {
                    return line;
                }

                line = line?.right;
            }
        }
    }

    internal static int OffsetOf(Line line)
    {
        int offset = line?.left is not null
            ? line.left.totalLength
            : 0;

        while (line?.parent is not null)
        {
            if (line == line.parent.right)
            {
                if (line.parent.left is not null)
                {
                    offset += line.parent.left.totalLength;
                }

                offset += line.parent.TextLength;
            }

            line = line.parent;
        }
        return offset;
    }
    #endregion

    #region validate data
#if DEBUG
    [Conditional("DATACONSISTENCYTEST")]
    internal void ValidateData()
    {
        Debug.Assert(root.totalLength == document.TextLength);

        int blackCount = -1;

        ValidateData(root);
        ValidateTree(root, null!, RED, 0, ref blackCount);
    }

    private void ValidateData(Line node)
    {
        int totalCount = 1;
        int totalLength = node.TextLength;

        if (node.left is not null)
        {
            ValidateData(node.left);

            totalCount += node.left.lineCount;
            totalLength += node.left.totalLength;
        }

        if (node.right is not null)
        {
            ValidateData(node.right);

            totalCount += node.right.lineCount;
            totalLength += node.right.totalLength;
        }

        Debug.Assert(node.lineCount == totalCount);
        Debug.Assert(node.totalLength == totalLength);
    }

    /// <summary>
    /// Validate the (sub) tree starting at node satisfies all requirements:
    /// 1. Every node is either red or black.
	/// 2. All leaves (NIL nodes) are black.
    /// 3. No red node has a red child.
	/// 4. Every red node has a black parent.
	/// 5. Every path from a given node to any of its leaves goes through the same number of black nodes.
    /// </summary>
    private void ValidateTree(Line node, Line parent, bool parentColor, int blackCount, ref int expectedBlackCount)
    {
        if (node == null)
        {
            return;
        }

        Debug.Assert(node.parent == parent);

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
            if (expectedBlackCount == -1)
            {
                expectedBlackCount = blackCount;
            }
            else
            {
                Debug.Assert(expectedBlackCount == blackCount);
            }
        }
        ValidateTree(node.left!, node, node.color, blackCount, ref expectedBlackCount);
        ValidateTree(node.right!, node, node.color, blackCount, ref expectedBlackCount);
    }

    public string GetTreeAsString()
    {
        StringBuilder b = new();
        AppendTreeToString(root, b, 0);
        return b.ToString();
    }

    private static void AppendTreeToString(Line node, StringBuilder b, int indent)
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

    #region Insert/Remove lines
    public void RemoveLine(Line line)
    {
        RemoveNode(line);
        line.isDeleted = true;
    }

    public Line InsertLineAfter(Line line, int totalLength)
    {
        Line newLine = new(document)
        {
            TextLength = totalLength
        };

        InsertAfter(line, newLine);
        return newLine;
    }

    private void InsertAfter(Line node, Line newLine)
    {
        var newNode = newLine.Init();

        if (node.right == null)
        {
            InsertAsRight(node, newNode);
        }
        else
        {
            InsertAsLeft(node.right.LeftMost, newNode);
        }
    }
    #endregion

    #region red/black tree
    private void InsertAsLeft(Line parentNode, Line newNode)
    {
        Debug.Assert(parentNode.left == null);

        parentNode.left = newNode;
        newNode.parent = parentNode;
        newNode.color = RED;

        UpdateNodeData(parentNode);
        Rebalance(newNode);
    }

    private void InsertAsRight(Line parentNode, Line newNode)
    {
        Debug.Assert(parentNode.right == null);
        parentNode.right = newNode;
        newNode.parent = parentNode;
        newNode.color = RED;
        UpdateNodeData(parentNode);
        Rebalance(newNode);
    }

    /// <summary>
    /// Rebalance the tree after inserting a node.
    /// </summary>
    /// <param name="node">The inserted node.</param>
    private void Rebalance(Line node)
    {
        Debug.Assert(node is not null);
        Debug.Assert(node.color == RED);
        Debug.Assert(node.left == null || node.left.color == BLACK);
        Debug.Assert(node.right == null || node.right.color == BLACK);

        var parent = node.parent;

        if (parent is null)
        {
            // inserting at root, the tree remains balanced
            node.color = BLACK;
            return;
        }
        if (parent.color == BLACK)
        {
            // red node placed correctly, the tree remains balanced
            return;
        }
        // parentNode is red, so there is a conflict here!

        // the parent is RED, so it must have a parent
        var grandpa = parent.parent;
        var uncle = Sibling(parent);

        if (uncle is not null && uncle.color == RED)
        {
            parent.color = BLACK;
            uncle.color = BLACK;
            grandpa.color = RED;

            Rebalance(grandpa);

            return;
        }

        // parent is red but uncle is black
        if (node == parent.right && parent == grandpa.left)
        {
            RotateLeft(parent);

            node = node.left!;
        }
        else if (node == parent.left && parent == grandpa.right)
        {
            RotateRight(parent);

            node = node.right!;
        }

        // rearrange kinship and recolor
        parent = node.parent;
        parent.color = BLACK;

        grandpa = parent.parent;
        grandpa.color = RED;

        // rotate once more
        if (node == parent.left && parent == grandpa.left)
        {
            RotateRight(grandpa);
        }
        else
        {
            Debug.Assert(node == parent.right && parent == grandpa.right, "node kinship is guaranteed by the first rotation");

            RotateLeft(grandpa);
        }
    }

    /// <summary>
    /// Remove a node from the tree, replacing it with
    /// it's successor, and trickling down recursively
    /// </summary>
    private void RemoveNode(Line node)
    {
        if (node.left is not null && node.right is not null)
        {
            var leftMost = node.right.LeftMost;

            RemoveNode(leftMost);

            ReplaceNode(node, leftMost);

            leftMost.left = node.left;

            if (leftMost.left is not null)
            {
                leftMost.left.parent = leftMost;
            }

            leftMost.right = node.right;

            if (leftMost.right is not null)
            {
                leftMost.right.parent = leftMost;
            }

            leftMost.color = node.color;

            UpdateNodeData(leftMost);

            if (leftMost.parent is not null)
            {
                UpdateNodeData(leftMost.parent);
            }

            return;
        }

        // either node.left or node.right is null
        var parent = node.parent;
        var child = node.left ?? node.right;

        ReplaceNode(node, child!);

        if (parent is not null)
        {
            UpdateNodeData(parent);
        }

        if (node.color == BLACK)
        {
            if (child is not null && child.color == RED)
            {
                child.color = BLACK;
            }
            else
            {
                Rebalance(child!, parent!);
            }
        }
    }

    /// <summary>
    /// Rebalance the tree after removing a node.
    /// </summary>
    private void Rebalance(Line node, Line parent)
    {
        Debug.Assert(node is null || node.parent == parent);

        if (parent is null)
        {
            return;
        }

        // warning: node may be null
        var sibling = Sibling(node!, parent);

        if (sibling.color == RED)
        {
            parent.color = RED;
            sibling.color = BLACK;

            if (node == parent.left)
            {
                RotateLeft(parent);
            }
            else
            {
                RotateRight(parent);
            }

            sibling = Sibling(node!, parent);
        }

        if (parent.color == BLACK &&
            sibling.color == BLACK &&
            GetColor(sibling.left) == BLACK &&
            GetColor(sibling.right) == BLACK)
        {
            sibling.color = RED;

            Rebalance(parent, parent.parent);

            return;
        }

        if (parent.color == RED &&
            sibling.color == BLACK &&
            GetColor(sibling.left) == BLACK &&
            GetColor(sibling.right) == BLACK)
        {
            sibling.color = RED;
            parent.color = BLACK;

            return;
        }

        if (node == parent.left &&
            sibling.color == BLACK &&
            GetColor(sibling.left) == RED &&
            GetColor(sibling.right) == BLACK)
        {
            sibling.color = RED;
            sibling.left.color = BLACK;

            RotateRight(sibling);
        }
        else if (node == parent.right &&
            sibling.color == BLACK &&
            GetColor(sibling.right) == RED &&
            GetColor(sibling.left) == BLACK)
        {
            sibling.color = RED;
            sibling.right.color = BLACK;

            RotateLeft(sibling);
        }

        sibling = Sibling(node!, parent);

        sibling.color = parent.color;
        parent.color = BLACK;

        if (node == parent.left)
        {
            if (sibling.right is not null)
            {
                Debug.Assert(sibling.right.color == RED);

                sibling.right.color = BLACK;
            }

            RotateLeft(parent);
        }
        else
        {
            if (sibling.left is not null)
            {
                Debug.Assert(sibling.left.color == RED);

                sibling.left.color = BLACK;
            }

            RotateRight(parent);
        }
    }

    private void ReplaceNode(Line node, Line newNode)
    {
        if (node.parent == null)
        {
            Debug.Assert(node == root);

            root = newNode;
        }
        else
        {
            if (node == node.parent.left)
            {
                node.parent.left = newNode;
            }
            else
            {
                node.parent.right = newNode;
            }
        }

        if (newNode is not null)
        {
            newNode.parent = node.parent!;
        }

        node.parent = null!;
    }

    private void RotateLeft(Line p)
    {
        var q = p.right;

        Debug.Assert(q is not null);
        Debug.Assert(q.parent == p);

        // set q to be the new root
        ReplaceNode(p, q);

        p.right = q.left;

        if (p.right is not null)
        {
            p.right.parent = p;
        }

        q.left = p;
        p.parent = q;

        UpdateNodeData(p);
    }

    private void RotateRight(Line p)
    {
        var q = p.left;

        Debug.Assert(q is not null);
        Debug.Assert(q.parent == p);

        // set q to be the new root
        ReplaceNode(p, q);

        p.left = q.right;

        if (p.left is not null)
        {
            p.left.parent = p;
        }

        q.right = p;
        p.parent = q;

        UpdateNodeData(p);
    }

    private static Line Sibling(Line node)
    {
        if (node == node.parent!.left)
        {
            return node.parent.right!;
        }

        return node.parent.left!;
    }

    private static Line Sibling(Line node, Line parent)
    {
        Debug.Assert(node is null || node.parent == parent);

        if (node == parent.left)
        {
            return parent.right!;
        }

        return parent.left!;
    }

    private static bool GetColor(Line node)
    {
        return node is not null && node.color;
    }
    #endregion

    #region IList
    Line IList<Line>.this[int index]
    {
        get
        {
            document.VerifyAccess();

            return LineAt(index);
        }
        set
        {
            throw new NotSupportedException();
        }
    }

    int IList<Line>.IndexOf(Line item)
    {
        document.VerifyAccess();

        if (item is null || item.IsDeleted)
        {
            return -1;
        }

        var index = item.Number - 1;

        return index < LineCount && LineAt(index) == item
            ? index
            : -1;
    }

    void IList<Line>.Insert(int index, Line item)
    {
        throw new NotSupportedException();
    }

    void IList<Line>.RemoveAt(int index)
    {
        throw new NotSupportedException();
    }
    #endregion

    #region ICollection
    int ICollection<Line>.Count
    {
        get
        {
            document.VerifyAccess();

            return LineCount;
        }
    }

    bool ICollection<Line>.IsReadOnly => true;

    void ICollection<Line>.Add(Line item)
    {
        throw new NotSupportedException();
    }

    void ICollection<Line>.Clear()
    {
        throw new NotSupportedException();
    }

    bool ICollection<Line>.Contains(Line item)
    {
        IList<Line> self = this;

        return self.IndexOf(item) >= 0;
    }

    void ICollection<Line>.CopyTo(Line[] array, int arrayIndex)
    {
        if (array is null)
        {
            throw new ArgumentNullException(nameof(array));
        }
        if (array.Length < LineCount)
        {
            throw new ArgumentException("The array is too small", nameof(array));
        }

        if (arrayIndex < 0 || arrayIndex + LineCount > array.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex, "Value must be between 0 and " + (array.Length - LineCount));
        }

        foreach (var line in this)
        {
            array[arrayIndex++] = line;
        }
    }

    bool ICollection<Line>.Remove(Line item)
    {
        throw new NotSupportedException();
    }
    #endregion

    #region IEnumerator
    public IEnumerator<Line> GetEnumerator()
    {
        document.VerifyAccess();
        return Enumerate();
    }

    private IEnumerator<Line> Enumerate()
    {
        document.VerifyAccess();

        var line = root.LeftMost;

        while (line is not null)
        {
            yield return line;

            line = line?.NextLine;
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
    #endregion
}