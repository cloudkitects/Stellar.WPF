using System;
using System.Diagnostics;

namespace Stellar.WPF.Document;

/// <summary>
/// A text segment living in a <see cref="SegmentCollection{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// A text <see cref="Segment"/> can be stand-alone or part of a collection.
/// Colections mainain the Offset and Length of member segments when the document changes.
/// </para>
/// <para>
/// Start offsets move like <see cref="AnchorMovementType">AnchorMovementType.AfterInsertion</see>,
/// end offsets move like <see cref="AnchorMovementType">AnchorMovementType.BeforeInsertion</see>
/// (i.e., the segment will always stay as small as possible).</para>
/// <para>
/// If a document change causes a segment to be deleted completely, it will be reduced to length 0, but segments are
/// never automatically removed from the collection.
/// Segments with length 0 will never expand due to document changes, and they move as <c>AfterInsertion</c>.
/// </para>
/// <para>
/// Thread-safety: a collection connected to a <see cref="Document"/> may only be used on that document's owner thread.
/// A disconnected collection is safe for concurrent reads, but concurrent access is not safe when there are writes.
/// Keep in mind that reading the Offset properties of a segment inside the collection is a read access on the
/// collection, and setting an Offset property of a segment is a write access on the collection.
/// </para>
/// </remarks>
/// <seealso cref="ISegment"/>
/// <seealso cref="AnchorSegment"/>
/// <seealso cref="SegmentCollection{T}"/>
public class Segment : ISegment
{
    internal ISegmentTree? ownerTree;
    internal Segment? left, right, parent;

    /// <summary>
    /// The color of the segment in the red/black tree.
    /// </summary>
    internal bool color;

    /// <summary>
    /// The "length" of the node (distance to previous node)
    /// </summary>
    internal int nodeLength;

    /// <summary>
    /// The total "length" of this subtree.
    /// </summary>
    internal int totalNodeLength; // totalNodeLength = nodeLength + left.totalNodeLength + right.totalNodeLength

    /// <summary>
    /// The length of the segment (do not confuse with nodeLength).
    /// </summary>
    internal int segmentLength;

    /// <summary>
    /// distanceToMaxEnd = Max(segmentLength,
    ///                        left.distanceToMaxEnd + left.Offset - Offset,
    ///                        left.distanceToMaxEnd + right.Offset - Offset)
    /// </summary>
    internal int distanceToMaxEnd;

    int ISegment.Offset {
        get { return StartOffset; }
    }

    /// <summary>
    /// Gets whether this segment is connected to a TextSegmentCollection and will automatically
    /// update its offsets.
    /// </summary>
    protected bool IsConnectedToCollection {
        get
        {
            return ownerTree is not null;
        }
    }

    /// <summary>
    /// Gets/Sets the start offset of the segment.
    /// </summary>
    /// <remarks>
    /// When setting the start offset, the end offset will change, too: the Length of the segment will stay constant.
    /// </remarks>
    public int StartOffset {
        get
        {
            // If the segment is not connected to a tree, we store the offset in "nodeLength".
            // Otherwise, "nodeLength" contains the distance to the start offset of the previous node
            Debug.Assert(!(ownerTree is null && parent is not null));
            Debug.Assert(!(ownerTree is null && left is not null));

            var n = this;
            var offset = n.nodeLength;

            if (n.left is not null)
            {
                offset += n.left.totalNodeLength;
            }

            while (n.parent is not null)
            {
                if (n == n.parent.right)
                {
                    if (n.parent.left is not null)
                    {
                        offset += n.parent.left.totalNodeLength;
                    }

                    offset += n.parent.nodeLength;
                }
                n = n.parent;
            }

            return offset;
        }
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Offset must not be negative");
            }

            if (StartOffset != value)
            {
                // make a copy, ownerTree.Remove() can set ownerTree to null
                ISegmentTree? tree = ownerTree;

                if (tree is not null)
                {
                    tree.Remove(this);

                    nodeLength = value;

                    tree.Add(this);
                }
                else
                {
                    nodeLength = value;
                }

                OnSegmentChanged();
            }
        }
    }

    /// <summary>
    /// Gets/Sets the end offset of the segment.
    /// </summary>
    /// <remarks>
    /// Setting the end offset will change the length, the start offset will stay constant.
    /// </remarks>
    public int EndOffset {
        get
        {
            return StartOffset + Length;
        }
        set
        {
            int newLength = value - StartOffset;

            if (newLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "EndOffset must be greater or equal to StartOffset");
            }

            Length = newLength;
        }
    }

    /// <summary>
    /// Gets/Sets the length of the segment.
    /// </summary>
    /// <remarks>
    /// Setting the length will change the end offset, the start offset will stay constant.
    /// </remarks>
    public int Length {
        get
        {
            return segmentLength;
        }
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Length must not be negative");
            }

            if (segmentLength != value)
            {
                segmentLength = value;

                ownerTree?.UpdateAugmentedData(this);

                OnSegmentChanged();
            }
        }
    }

    /// <summary>
    /// This method gets called when the StartOffset/Length/EndOffset properties are set.
    /// It is not called when StartOffset/Length/EndOffset change due to document changes
    /// </summary>
    protected virtual void OnSegmentChanged()
    {
    }

    internal Segment? LeftMost {
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

    internal Segment? RightMost {
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
    internal Segment? Successor {
        get
        {
            if (right is not null)
            {
                return right.LeftMost;
            }
            else
            {
                var node = this;
                Segment? oldNode;
                
                do
                {
                    oldNode = node;
                    node = node.parent;
                    // go up until we are coming out of a left subtree
                } while (node is not null && node.right == oldNode);

                return node;
            }
        }
    }

    /// <summary>
    /// Gets the inorder predecessor of the node.
    /// </summary>
    internal Segment? Predecessor {
        get
        {
            if (left is not null)
            {
                return left.RightMost;
            }
            else
            {
                var node = this;
                Segment oldNode;

                do
                {
                    oldNode = node;
                    node = node.parent;
                    // go up until we are coming out of a right subtree
                } while (node is not null && node.left == oldNode);

                return node;
            }
        }
    }

#if DEBUG
    internal string ToDebugString()
    {
        return $"[nodeLength={nodeLength} totalNodeLength={totalNodeLength} distanceToMaxEnd={distanceToMaxEnd} MaxEndOffset={StartOffset + distanceToMaxEnd}]";
    }
#endif

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"[{GetType().Name} Offset={StartOffset} Length={Length} EndOffset={EndOffset}]";
    }
}


