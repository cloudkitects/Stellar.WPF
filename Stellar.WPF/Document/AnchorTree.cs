using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Stellar.WPF.Utilities;

namespace Stellar.WPF.Document;

/// <summary>
/// An anchor node tree.
/// </summary>
/// <remarks>
/// Provision for (1) quickly updating all anchors' offsets whenever a document changes and (2) using weak references.

/// (1) is unaffordable at O(N); a linked list would also be slow (an offset lookup traverses the whole list).
/// An augmented red-black tree of anchor nodes with a (weakly referenced) anchor at the end is more efficient.

/// In essence, the tree holds a sorted list of anchors, where each anchor stores the distance to the previous anchor.
/// An node's next node is its successor, and the distance between them is the node's length. 
/// Distances are never negative, so anchors are always sorted by offset--the order of anchors at the same offset is undefined.

/// We're not using the tree for sorting: it's a binary tree with balancing red-black properties.

/// The augmentatation is provided by total length, the sum of all node lengths in a branch.
/// This allows computing an anchor's offset by walking up the list of parent nodes in O(logN).
/// </remarks>
internal sealed class AnchorTree
{
    private readonly Document document;
    private readonly List<AnchorNode> nodesBin = new();
    private AnchorNode? root;

    public AnchorTree(Document document)
    {
        this.document = document;
    }

    [Conditional("DEBUG")]
    private static void Log(string text)
    {
        Debug.WriteLine("anchor tree: " + text);
    }

    #region insert text
    private void InsertText(int offset, int length, bool defaultAnchorMovementIsBeforeInsertion)
    {
        if (length == 0 || root is null || offset > root.totalLength)
        {
            return;
        }

        // find the [beg, end) range of nodes placed exactly at offset
        if (offset == root.totalLength)
        {
            InsertText(FindTopPredecessor(root.RightMost)!, null!, length, defaultAnchorMovementIsBeforeInsertion);
        }
        else
        {
            var endNode = FindNode(ref offset);

            Debug.Assert(endNode?.length > 0);

            if (offset > 0)
            {
                // no nodes are exactly at offset
                endNode.length += length;

                UpdateNodeData(endNode);
            }
            else
            {
                InsertText(FindTopPredecessor(endNode.Predecessor)!, endNode, length, defaultAnchorMovementIsBeforeInsertion);
            }
        }

        EmptyBin();
    }

    private AnchorNode? FindTopPredecessor(AnchorNode? node)
    {
        while (node is not null && node.length == 0)
        {
            node = node.Predecessor;
        }

        return node ?? root?.LeftMost;
    }

    // Sorts the nodes in the [beg, end) range by movement type
    // and inserts the length between before and after insertion nodes
    private void InsertText(AnchorNode begNode, AnchorNode endNode, int length, bool defaultAnchorMovementIsBeforeInsertion)
    {
        Debug.Assert(begNode is not null);

        var beforeInsert = new List<AnchorNode>();
        var temp = begNode;

        // sort
        while (temp != endNode)
        {
            if (temp.Target is not Anchor anchor)
            {
                nodesBin.AddIfNotExists(temp);
            }
            else if (defaultAnchorMovementIsBeforeInsertion
                       ? anchor.MovementType != AnchorMovementType.AfterInsertion
                       : anchor.MovementType == AnchorMovementType.BeforeInsertion)
            {
                beforeInsert.Add(temp);
            }

            temp = temp.Successor;
        }

        // swap nodes with those in the before insert list
        temp = begNode;

        foreach (AnchorNode node in beforeInsert)
        {
            SwapAnchors(node, temp);

            temp = temp.Successor;
        }

        // temp now points to the first node after insert or the end node if there
        // is no after insert node at the offset, so add the length to temp
        if (temp is null)
        {
            Debug.Assert(endNode is null);
        }
        else
        {
            temp.length += length;

            UpdateNodeData(temp);
        }
    }

    /// <summary>
    /// Swaps the anchors stored in the two nodes.
    /// </summary>
    private void SwapAnchors(AnchorNode n1, AnchorNode n2)
    {
        if (n1 != n2)
        {
            var anchor1 = n1.Target as Anchor;
            var anchor2 = n2.Target as Anchor;

            if (anchor1 is null && anchor2 is null)
            {
                return;
            }

            n1.Target = anchor2;
            n2.Target = anchor1;

            if (anchor1 is null)
            {
                nodesBin.Remove(n1);
                nodesBin.AddIfNotExists(n2);

                anchor2!.node = n1;
            }
            else if (anchor2 is null)
            {
                nodesBin.Remove(n2);
                nodesBin.AddIfNotExists(n1);

                anchor1.node = n2;
            }
            else
            {
                anchor1.node = n2;
                anchor2.node = n1;
            }
        }
    }
    #endregion

    #region remove or replace text
    public void HandleTextChange(ChangeOffset change, EventQueue eventQueue)
    {
        // pure insertion, nodes at the same location can split depending on their movement type
        if (change.RemovalLength == 0)
        {
            InsertText(change.Offset, change.InsertionLength, change.DefaultAnchorMovementIsBeforeInsertion);

            return;
        }

        // otherwise split the set of deletion survivors only--not all nodes at an offset

        // a replacing text change must delete anchors within the deleted segment or
        // move them to the surviving side, and then adjust the segment size

        var offset = change.Offset;
        var remainingRemovalLength = change.RemovalLength;

        // nothing to do if the text change is happening after the last anchor
        if (root is null || offset >= root.totalLength)
        {
            return;
        }

        var node = FindNode(ref offset);
        AnchorNode firstDeletionSurvivor = null!;

        // delete all nodes in the removal segment
        while (node is not null && offset + remainingRemovalLength > node.length)
        {
            var anchor = node.Target as Anchor;

            if (anchor is not null && (anchor.SurvivesDeletion || change.RemovalNeverCausesAnchorDeletion))
            {
                firstDeletionSurvivor ??= node;

                // this node wants to survive; removing the deleted length segment will place
                // it in front of the removed segment
                remainingRemovalLength -= node.length - offset;
                node.length = offset;
                offset = 0;

                UpdateNodeData(node);

                node = node.Successor;
            }
            else
            {
                var s = node.Successor;
                remainingRemovalLength -= node.length;

                RemoveNode(node);

                // avoid deleting it twice
                nodesBin.Remove(node);

                anchor?.OnDeleted(eventQueue);

                node = s;
            }
        }

        // node is now the first anchor after the deleted segment or null if there aren't any

        // Because all non-surviving nodes up to node were deleted, the [firstDeletionSurvivor, node)
        // range now refers to the set of all survivors

        if (node is not null)
        {
            node.length -= remainingRemovalLength;

            Debug.Assert(node.length >= 0);
        }

        if (change.InsertionLength > 0)
        {
            if (firstDeletionSurvivor is not null)
            {
                // deletion survivors must be split into before and after insertion groups;
                // group only [firstDeletionSurvivor, node) at offset to ensure nodes immediately
                // before or after the replaced segment stay put independently of their movement type
                InsertText(firstDeletionSurvivor, node!, change.InsertionLength, change.DefaultAnchorMovementIsBeforeInsertion);
            }
            else if (node is not null)
            {
                // no survivors
                node.length += change.InsertionLength;
            }
        }
        if (node is not null)
        {
            UpdateNodeData(node);
        }

        EmptyBin();
    }
    #endregion

    #region empty the bin of GC'ed anchors
    private void EmptyBin()
    {
        CheckProperties();

        while (nodesBin.Count > 0)
        {
            var i = nodesBin.Count - 1;
            var node = nodesBin[i];
            var next = node.Successor;

            // combine section of n with the following section
            if (next is not null)
            {
                next.length += node.length;
            }

            RemoveNode(node);

            if (next is not null)
            {
                UpdateNodeData(next);
            }

            nodesBin.RemoveAt(i);

            CheckProperties();
        }

        CheckProperties();
    }
    #endregion

    #region find node
    /// <summary>
    /// Find the node at the specified offset.
    /// Offset is relative to the beginning of the returned node
    /// after each call!
    /// </summary>
    private AnchorNode? FindNode(ref int offset)
    {
        var node = root;

        while (true)
        {
            if (node!.left is not null)
            {
                if (offset < node.left.totalLength)
                {
                    node = node.left;

                    continue;
                }

                offset -= node.left.totalLength;
            }

            if (!node.IsAlive)
            {
                nodesBin.AddIfNotExists(node);
            }

            if (offset < node.length)
            {
                return node;
            }

            offset -= node.length;

            if (node.right is not null)
            {
                node = node.right;
            }
            else
            {
                return null;
            }
        }
    }
    #endregion

    #region update node augmented data
    private void UpdateNodeData(AnchorNode node)
    {
        if (!node.IsAlive)
        {
            nodesBin.AddIfNotExists(node);
        }

        var totalLength = node.length;

        if (node.left is not null)
        {
            totalLength += node.left.totalLength;
        }

        if (node.right is not null)
        {
            totalLength += node.right.totalLength;
        }

        if (node.totalLength != totalLength)
        {
            node.totalLength = totalLength;

            if (node.parent is not null)
            {
                UpdateNodeData(node.parent);
            }
        }
    }
    #endregion

    #region create an anchor
    public Anchor CreateAnchor(int offset)
    {
        Log("create anchor(" + offset + ")");

        var anchor = new Anchor(document);
        anchor.node = new AnchorNode(anchor);

        if (root is null)
        {
            // create the root anchor
            root = anchor.node;
            root.totalLength = root.length = offset;
        }
        else if (offset >= root.totalLength)
        {
            // append anchor at end of the tree
            anchor.node.totalLength = anchor.node.length = offset - root.totalLength;
            InsertAsRight(root.RightMost, anchor.node);
        }
        else
        {
            // insert anchor in the middle of tree
            var node = FindNode(ref offset);

            Debug.Assert(offset < node?.length);

            // split segment 'n' at offset
            anchor.node.totalLength = anchor.node.length = offset;
            node.length -= offset;

            InsertBefore(node, anchor.node);
        }

        EmptyBin();

        return anchor;
    }

    private void InsertBefore(AnchorNode node, AnchorNode newNode)
    {
        if (node.left is null)
        {
            InsertAsLeft(node, newNode);
        }
        else
        {
            InsertAsRight(node.left.RightMost, newNode);
        }
    }
    #endregion

    #region red/black tree
    internal const bool RED = true;
    internal const bool BLACK = false;

    private void InsertAsLeft(AnchorNode parent, AnchorNode node)
    {
        Debug.Assert(parent.left is null);

        parent.left = node;
        node.parent = parent;
        node.color = RED;

        UpdateNodeData(parent);

        Rebalance(node);
    }

    private void InsertAsRight(AnchorNode parent, AnchorNode node)
    {
        Debug.Assert(parent.right is null);

        parent.right = node;
        node.parent = parent;
        node.color = RED;

        UpdateNodeData(parent);

        Rebalance(node);
    }

    /// <summary>
    /// Rebalance the tree after inserting a node.
    /// </summary>
    /// <param name="node">The inserted node.</param>
    private void Rebalance(AnchorNode node)
    {
        Debug.Assert(node is not null);
        Debug.Assert(node.color == RED);
        Debug.Assert(node.left is null || node.left.color == BLACK);
        Debug.Assert(node.right is null || node.right.color == BLACK);

        var parent = node.parent;

        if (parent is null)
        {
            // inserting at root, the tree remains balanced
            node.color = BLACK;

            return;
        }

        if (parent.color == BLACK)
        {
            // RED node placed correctly, the tree remains balanced
            return;
        }

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
    private void RemoveNode(AnchorNode node)
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
    private void Rebalance(AnchorNode node, AnchorNode parent)
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
            GetColor(sibling.left) == BLACK
            && GetColor(sibling.right) == BLACK)
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

    private void ReplaceNode(AnchorNode node, AnchorNode newNode)
    {
        if (node.parent is null)
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

    private void RotateLeft(AnchorNode p)
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
        UpdateNodeData(q);
    }

    private void RotateRight(AnchorNode p)
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
        UpdateNodeData(q);
    }

    private static AnchorNode Sibling(AnchorNode node)
    {
        return node == node.parent.left
            ? node.parent.right
            : node.parent.left;
    }

    private static AnchorNode Sibling(AnchorNode node, AnchorNode parent)
    {
        Debug.Assert(node is null || node.parent == parent);

        return node == parent.left
            ? parent.right
            : parent.left;
    }

    private static bool GetColor(AnchorNode node)
    {
        return node is not null && node.color;
    }
    #endregion

    #region CheckProperties
    [Conditional("DATACONSISTENCYTEST")]
    internal void CheckProperties()
    {
#if DEBUG
        if (root != null)
        {
            int blackCount = -1;

            ValidateData(root);
            ValidateTree(root, null!, RED, 0, ref blackCount);
        }
#endif
    }

#if DEBUG
    private void ValidateData(AnchorNode node)
    {
        int totalLength = node.length;

        if (node.left is not null)
        {
            ValidateData(node.left);

            totalLength += node.left.totalLength;
        }

        if (node.right is not null)
        {
            ValidateData(node.right);

            totalLength += node.right.totalLength;
        }

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
    private void ValidateTree(AnchorNode node, AnchorNode parent, bool parentColor, int blackCount, ref int expectedBlackCount)
    {
        if (node is null)
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

        if (node.left is null && node.right is null)
        {
            // leaf node
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
#endif
    #endregion

    #region GetTreeAsString
#if DEBUG
    public string GetTreeAsString()
    {
        if (root == null)
        {
            return "<empty>";
        }

        StringBuilder b = new();
        AppendTreeToString(root, b, 0);

        return b.ToString();
    }

    private static void AppendTreeToString(AnchorNode node, StringBuilder b, int indent)
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
}
