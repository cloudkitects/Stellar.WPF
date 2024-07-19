using System;

namespace Stellar.WPF.Document;


/// <summary>
/// An anchor node in a tree, a section of text with an anchor at the end.
/// Derives from weak reference to save memory.
/// </summary>
internal sealed class AnchorNode : WeakReference
{
    internal AnchorNode? left, right, parent;
    internal bool color;
    internal int length;
    internal int totalLength; // length + left.totalLength + right.totalLength

    public AnchorNode(Anchor anchor) : base(anchor)
    {
    }

    internal AnchorNode LeftMost
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

    internal AnchorNode RightMost
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
    internal AnchorNode Successor
    {
        get
        {
            if (right is not null)
            {
                return right.LeftMost;
            }
            var node = this;
            AnchorNode oldNode;

            do
            {
                oldNode = node;
                node = node.parent;
            }
            while (node != null && node.right == oldNode);

            return node;
        }
    }

    /// <summary>
    /// Gets the inorder predecessor of the node.
    /// </summary>
    internal AnchorNode Predecessor
    {
        get
        {
            if (left is not null)
            {
                return left.RightMost;
            }

            var node = this;
            AnchorNode oldNode;

            do
            {
                oldNode = node;
                node = node.parent;
            }
            while (node != null && node.left == oldNode);

            return node;
        }
    }

    public override string ToString()
    {
        return "[TextAnchorNode Length=" + length + " TotalLength=" + totalLength + " Target=" + Target + "]";
    }
}
