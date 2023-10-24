using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Stellar.WPF.Utilities;

/// <summary>
/// A memory-saving IList&lt;T&gt; with O(logN) insertion and removal that allocates
/// a single node for repeated adjacent values.
/// </summary>
/// <remarks>
/// Storage is 5 * IntPtr.Size + 12 + sizeof(T) per node.
/// Use only when lots of adjacent values are identical.
/// </remarks>
public sealed class CompactTree<T> : IList<T>
{
    // Further memory optimization: this tree could work without parent pointers. But that
    // requires changing most of tree manipulating logic.
    // Also possible is to remove the count field and calculate it as totalCount-left.totalCount-right.totalCount
    // - but that would make tree manipulations more difficult to handle.

    #region node
    private sealed class Node
    {
        internal Node? left, right, parent;
        internal bool color;
        internal int count, totalCount;
        internal T value;

        public Node(T value, int count)
        {
            this.value = value;
            this.count = count;

            totalCount = count;
        }

        internal Node LeftMost
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

        internal Node RightMost
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

        internal Node Predecessor
        {
            get
            {
                if (left is not null)
                {
                    return left.RightMost;
                }
                
                var node = this;
                Node oldNode;
                
                do
                {
                    oldNode = node;
                    node = node.parent;
                    
                } while (node is not null && node.left == oldNode);
                
                return node!;
            }
        }

        /// <summary>
        /// Gets the inorder successor of the node.
        /// </summary>
        internal Node Successor
        {
            get
            {
                if (right is not null)
                {
                    return right.LeftMost;
                }
                
                var node = this;
                Node oldNode;
                
                do
                {
                    oldNode = node;
                    node = node.parent;
                } while (node is not null && node.right == oldNode);
                
                return node!;
            }
        }

        public override string ToString()
        {
            return $"[TotalCount={totalCount} Count={count} Value={value}]";
        }
    }
    #endregion

    #region fields
    private readonly Func<T, T, bool> equals;
    private Node? root;
    #endregion

    #region constructors
    /// <summary>
    /// Creates a new CompressingTreeList instance.
    /// </summary>
    /// <param name="equalityComparer">The equality comparer used for comparing consecutive values.
    /// A single node may be used to store the multiple values that are considered equal.</param>
    public CompactTree(IEqualityComparer<T> equalityComparer)
    {
        if (equalityComparer is null)
        {
            throw new ArgumentNullException(nameof(equalityComparer));
        }

        equals = equalityComparer.Equals;
    }

    /// <summary>
    /// Creates a new CompressingTreeList instance.
    /// </summary>
    /// <param name="equals">A function that checks two values for equality. If this
    /// function returns true, a single node may be used to store the two values.</param>
    public CompactTree(Func<T, T, bool> equals)
    {
        this.equals = equals ?? throw new ArgumentNullException(nameof(equals));
    }
    #endregion

    #region methods
    /// <summary>
    /// Inserts <paramref name="item"/> <paramref name="count"/> times at position
    /// <paramref name="index"/>.
    /// </summary>
    public void InsertRange(int index, int count, T item)
    {
        if (index < 0 || Count < index)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"{index} < 0 or {Count} < {index}");
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), $"{count} < 0");
        }

        if (count == 0)
        {
            return;
        }

        unchecked
        {
            if (Count + count < 0)
            {
                throw new OverflowException($"{Count} + {count} < 0");
            }
        }

        if (root is null)
        {
            root = new Node(item, count);
        }
        else
        {
            var node = GetNode(ref index);

            if (equals(node.value, item))
            {
                node.count += count;
                UpdateNodeData(node);
            }
            else if (index == node.count)
            {
                Debug.Assert(node == root.RightMost);

                InsertAsRight(node, new Node(item, count));
            }
            else if (index == 0)
            {
                var predecessor = node.Predecessor;

                if (predecessor is not null && equals(predecessor.value, item))
                {
                    predecessor.count += count;
                    UpdateNodeData(predecessor);
                }
                else
                {
                    InsertBefore(node, new Node(item, count));
                }
            }
            else
            {
                Debug.Assert(index > 0 && index < node.count);

                node.count -= index;

                InsertBefore(node, new Node(node.value, index));
                InsertBefore(node, new Node(item, count));

                UpdateNodeData(node);
            }
        }

        CheckProperties();
    }

    private void InsertBefore(Node node, Node newNode)
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

    /// <summary>
    /// Removes <paramref name="count"/> items starting at position
    /// <paramref name="index"/>.
    /// </summary>
    public void RemoveRange(int index, int count)
    {
        if (index < 0 || Count < index)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"{index} < 0 or {Count} < {index}");
        }

        if (count < 0 || Count < index + count)
        {
            throw new ArgumentOutOfRangeException(nameof(count), $"{count} < 0 or {Count} < {index} + {count}");
        }

        if (count == 0)
        {
            return;
        }

        var node = GetNode(ref index);
        
        if (index + count < node.count)
        {
            node.count -= count;
            
            UpdateNodeData(node);
        }
        else
        {
            Node lastSurvivor;
            
            if (index > 0)
            {
                count -= (node.count - index);
                node.count = index;
                
                UpdateNodeData(node);
                
                lastSurvivor = node;
                node = node.Successor;
            }
            else
            {
                Debug.Assert(index == 0);
                
                lastSurvivor = node.Predecessor;
            }

            while (node is not null && count >= node.count)
            {
                count -= node.count;
                var succesor = node.Successor;
                
                RemoveNode(node);
                
                node = succesor;
            }

            if (count > 0)
            {
                Debug.Assert(node is not null && count < node.count);
                
                node.count -= count;
                
                UpdateNodeData(node);
            }

            if (node is not null)
            {
                Debug.Assert(node.Predecessor == lastSurvivor);
                
                if (lastSurvivor is not null && equals(lastSurvivor.value, node.value))
                {
                    lastSurvivor.count += node.count;
                    
                    RemoveNode(node);
                    UpdateNodeData(lastSurvivor);
                }
            }
        }

        CheckProperties();
    }

    /// <summary>
    /// Sets <paramref name="count"/> indices starting at <paramref name="index"/> to
    /// <paramref name="item"/>
    /// </summary>
    public void UpdateRange(int index, int count, T item)
    {
        RemoveRange(index, count);
        InsertRange(index, count, item);
    }

    private Node GetNode(ref int index)
    {
        var node = root;

        while (true)
        {
            if (node!.left is not null && index < node.left.totalCount)
            {
                node = node.left;
            }
            else
            {
                if (node.left is not null)
                {
                    index -= node.left.totalCount;
                }

                if (index < node.count || node.right == null)
                {
                    return node;
                }

                index -= node.count;
                node = node.right;
            }
        }
    }

    private void UpdateNodeData(Node node)
    {
        var totalCount = node.count;

        if (node.left is not null)
        {
            totalCount += node.left.totalCount;
        }

        if (node.right is not null)
        {
            totalCount += node.right.totalCount;
        }

        if (node.totalCount != totalCount)
        {
            node.totalCount = totalCount;
            if (node.parent is not null)
            {
                UpdateNodeData(node.parent);
            }
        }
    }
    #endregion

    #region IList<T>
    /// <summary>
    /// Gets or sets an item by index.
    /// </summary>
    public T this[int index]
    {
        get
        {
            if (index < 0 || Count <= index)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"{index} < 0 or {Count} <= {index}");
            }

            return GetNode(ref index).value;
        }

        set
        {
            RemoveAt(index);
            Insert(index, value);
        }
    }

    /// <summary>
    /// Gets the number of items in the list.
    /// </summary>
    public int Count
    {
        get
        {
            if (root is not null)
            {
                return root.totalCount;
            }
            else
            {
                return 0;
            }
        }
    }

    bool ICollection<T>.IsReadOnly
    {
        get
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the index of the specified <paramref name="item"/>.
    /// </summary>
    public int IndexOf(T item)
    {
        int index = 0;
        if (root is not null)
        {
            var n = root.LeftMost;
            while (n is not null)
            {
                if (equals(n.value, item))
                {
                    return index;
                }

                index += n.count;
                n = n.Successor;
            }
        }
        Debug.Assert(index == Count);
        return -1;
    }

    /// <summary>
    /// Gets the first index so that all values from the result index to <paramref name="index"/>
    /// are equal.
    /// </summary>
    public int GetStartOfRun(int index)
    {
        if (index < 0 || index >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Value must be between 0 and " + (Count - 1));
        }

        int indexInRun = index;
        GetNode(ref indexInRun);
        return index - indexInRun;
    }

    /// <summary>
    /// Gets the first index after <paramref name="index"/> so that the value at the result index is not
    /// equal to the value at <paramref name="index"/>.
    /// That is, this method returns the exclusive end index of the run of equal values.
    /// </summary>
    public int GetEndOfRun(int index)
    {
        if (index < 0 || index >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Value must be between 0 and " + (Count - 1));
        }

        int indexInRun = index;
        int runLength = GetNode(ref indexInRun).count;
        return index - indexInRun + runLength;
    }

    /// <summary>
    /// Applies the conversion function to all elements in this CompressingTreeList.
    /// </summary>
    public void Transform(Func<T, T> converter)
    {
        if (root is null)
        {
            return;
        }

        Node prevNode = null!;
        for (var n = root.LeftMost; n is not null; n = n.Successor)
        {
            n.value = converter(n.value);
            if (prevNode is not null && equals(prevNode.value, n.value))
            {
                n.count += prevNode.count;
                UpdateNodeData(n);
                RemoveNode(prevNode);
            }
            prevNode = n;
        }
        CheckProperties();
    }

    /// <summary>
    /// Applies the conversion function to the elements in the specified range.
    /// </summary>
    public void TransformRange(int index, int length, Func<T, T> converter)
    {
        if (root is null)
        {
            return;
        }

        int endIndex = index + length;
        int pos = index;
        while (pos < endIndex)
        {
            int endPos = Math.Min(endIndex, GetEndOfRun(pos));
            T oldValue = this[pos];
            T newValue = converter(oldValue);
            UpdateRange(pos, endPos - pos, newValue);
            pos = endPos;
        }
    }

    /// <summary>
    /// Inserts the specified <paramref name="item"/> at <paramref name="index"/>
    /// </summary>
    public void Insert(int index, T item)
    {
        InsertRange(index, 1, item);
    }

    /// <summary>
    /// Removes one item at <paramref name="index"/>
    /// </summary>
    public void RemoveAt(int index)
    {
        RemoveRange(index, 1);
    }

    /// <summary>
    /// Adds the specified <paramref name="item"/> to the end of the list.
    /// </summary>
    public void Add(T item)
    {
        InsertRange(Count, 1, item);
    }

    /// <summary>
    /// Removes all items from this list.
    /// </summary>
    public void Clear()
    {
        root = null;
    }

    /// <summary>
    /// Gets whether this list contains the specified item.
    /// </summary>
    public bool Contains(T item)
    {
        return IndexOf(item) >= 0;
    }

    /// <summary>
    /// Copies all items in this list to the specified array.
    /// </summary>
    public void CopyTo(T[] array, int arrayIndex)
    {
        if (array is null)
        {
            throw new ArgumentNullException(nameof(array));
        }

        if (array.Length < Count)
        {
            throw new ArgumentException("The array is too small", nameof(array));
        }

        if (arrayIndex < 0 || arrayIndex + Count > array.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex, "Value must be between 0 and " + (array.Length - Count));
        }

        foreach (T v in this)
        {
            array[arrayIndex++] = v;
        }
    }

    /// <summary>
    /// Removes the specified item from this list.
    /// </summary>
    public bool Remove(T item)
    {
        int index = IndexOf(item);
        if (index >= 0)
        {
            RemoveAt(index);
            return true;
        }
        else
        {
            return false;
        }
    }
    #endregion

    #region IEnumerable<T>
    /// <summary>
    /// Gets an enumerator for this list.
    /// </summary>
    public IEnumerator<T> GetEnumerator()
    {
        if (root is not null)
        {
            var n = root.LeftMost;
            while (n is not null)
            {
                for (int i = 0; i < n.count; i++)
                {
                    yield return n.value;
                }
                n = n.Successor;
            }
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
    #endregion

    #region red/black tree
    internal const bool RED = true;
    internal const bool BLACK = false;

    private void InsertAsLeft(Node parent, Node node)
    {
        Debug.Assert(parent.left is null);
        
        parent.left = node;
        node.parent = parent;
        node.color = RED;
        
        UpdateNodeData(parent);
        Rebalance(node);
    }

    private void InsertAsRight(Node parent, Node node)
    {
        Debug.Assert(parent.right is null);

        parent.right = node;
        node.parent = parent;
        node.color = RED;
        
        UpdateNodeData(parent);
        Rebalance(node);
    }

    private void Rebalance(Node node)
    {
        Debug.Assert(node is not null);
        Debug.Assert(node.color == RED);
        Debug.Assert(node.left is null || node.left.color == BLACK);
        Debug.Assert(node.right is null || node.right.color == BLACK);

        var parent = node.parent;

        if (parent is null)
        {
            // inserted at root
            node.color = BLACK;
            
            return;
        }

        if (parent.color == BLACK)
        {
            return;
        }

        var grandpa = parent.parent!;
        var uncle = Sibling(parent);

        if (uncle is not null && uncle.color == RED)
        {
            parent.color = BLACK;
            uncle.color = BLACK;
            grandpa.color = RED;

            Rebalance(grandpa);
            
            return;
        }

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
        
        parent = node.parent;
        grandpa = parent!.parent!;

        parent.color = BLACK;
        grandpa.color = RED;

        if (node == parent.left && parent == grandpa.left)
        {
            RotateRight(grandpa);
        }
        else
        {
            Debug.Assert(node == parent.right && parent == grandpa.right);
            
            RotateLeft(grandpa);
        }
    }

    private void RemoveNode(Node node)
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

        // get the remaining child
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
    /// Rebalance after delete
    /// </summary>
    private void Rebalance(Node node, Node parent)
    {
        Debug.Assert(node is null || node.parent == parent);
        
        if (parent is null)
        {
            return;
        }

        var sibling = Sibling(node!, parent);

        if (sibling!.color == RED)
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

        if (parent.color == BLACK
            && sibling!.color == BLACK
            && GetColor(sibling.left!) == BLACK
            && GetColor(sibling.right!) == BLACK)
        {
            sibling.color = RED;

            Rebalance(parent, parent.parent!);
            
            return;
        }

        if (parent.color == RED
            && sibling!.color == BLACK
            && GetColor(sibling.left!) == BLACK
            && GetColor(sibling.right!) == BLACK)
        {
            sibling.color = RED;
            parent.color = BLACK;

            return;
        }

        if (node == parent.left &&
            sibling!.color == BLACK &&
            GetColor(sibling.left!) == RED &&
            GetColor(sibling.right!) == BLACK)
        {
            sibling.color = RED;
            sibling.left!.color = BLACK;

            RotateRight(sibling);
        }
        else if (node == parent.right &&
                   sibling!.color == BLACK &&
                   GetColor(sibling.right!) == RED &&
                   GetColor(sibling.left!) == BLACK)
        {
            sibling.color = RED;
            sibling.right!.color = BLACK;

            RotateLeft(sibling);
        }

        sibling = Sibling(node!, parent);

        sibling!.color = parent.color;
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

    private void ReplaceNode(Node node, Node newNode)
    {
        if (node.parent == null)
        {
            Debug.Assert(node == root);

            root = newNode;
        }
        else
        {
            if (node.parent.left == node)
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
            newNode.parent = node.parent;
        }

        node.parent = null;
    }

    private void RotateLeft(Node p)
    {
        var q = p.right;

        Debug.Assert(q is not null);
        Debug.Assert(q.parent == p);
        
        // make q the new root
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

    private void RotateRight(Node p)
    {
        var q = p.left;

        Debug.Assert(q is not null);
        Debug.Assert(q.parent == p);

        // make q the new root
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

    private static Node? Sibling(Node node)
    {
        return node == node.parent!.left
            ? node.parent!.right
            : node.parent!.left;
    }

    private static Node? Sibling(Node node, Node parent)
    {
        Debug.Assert(node == null || node.parent == parent);

        return node == parent.left
            ? parent.right
            : parent.left;
    }

    private static bool GetColor(Node node)
    {
        return node is not null && node.color;
    }
    #endregion

    #region check properties
    [Conditional("DATACONSISTENCYTEST")]
    internal void CheckProperties()
    {
#if DEBUG
        if (root is not null)
        {
            CheckProperties(root);

            // check red-black property:
            int blackCount = -1;
            CheckNodeProperties(root, null!, RED, 0, ref blackCount);

            // ensure that the tree is compressed:
            var leftMost = root.LeftMost;
            var node = leftMost.Successor;

            while (node is not null)
            {
                Debug.Assert(!equals(leftMost.value, node.value));

                leftMost = node;
                node = leftMost.Successor;
            }
        }
#endif
    }

#if DEBUG
    private void CheckProperties(Node node)
    {
        Debug.Assert(node.count > 0);

        var totalCount = node.count;

        if (node.left is not null)
        {
            CheckProperties(node.left);
            
            totalCount += node.left.totalCount;
        }
        
        if (node.right is not null)
        {
            CheckProperties(node.right);

            totalCount += node.right.totalCount;
        }

        Debug.Assert(node.totalCount == totalCount);
    }

    /*
	1. A node is either red or black.
	2. The root is black.
	3. All leaves are black. (The leaves are the NIL children.)
	4. Both children of every red node are black. (So every red node must have a black parent.)
	5. Every simple path from a node to a descendant leaf contains the same number of black nodes. (Not counting the leaf node.)
	 */
    private void CheckNodeProperties(Node node, Node parent, bool parentColor, int blackCount, ref int expectedBlackCount)
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
            // leaf
            if (expectedBlackCount == -1)
            {
                expectedBlackCount = blackCount;
            }
            else
            {
                Debug.Assert(expectedBlackCount == blackCount);
            }
        }
        
        CheckNodeProperties(node.left!, node, node.color, blackCount, ref expectedBlackCount);
        CheckNodeProperties(node.right!, node, node.color, blackCount, ref expectedBlackCount);
    }
#endif
    #endregion

    #region as string
    internal string GetAsString()
    {
#if DEBUG
        if (root is null)
        {
            return "<empty>";
        }

        var b = new StringBuilder();

        AppendTreeToString(root, b, 0);
        
        return b.ToString();
#else
		return "Not available in release builds.";
#endif
    }

#if DEBUG
    private static void AppendTreeToString(Node node, StringBuilder b, int indent)
    {
        b.Append(node.color == RED ? "RED   " : "BLACK ");
        b.AppendLine(node.ToString());

        indent += 2;
        
        if (node.left is not null)
        {
            b.Append(' ', indent);
            b.Append("L: ");
            
            AppendTreeToString(node.left, b, indent);
        }

        if (node.right is not null)
        {
            b.Append(' ', indent);
            b.Append("R: ");
            
            AppendTreeToString(node.right, b, indent);
        }
    }
#endif
    #endregion
}
