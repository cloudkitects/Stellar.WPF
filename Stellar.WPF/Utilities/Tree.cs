using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Stellar.WPF.Utilities;

/// <summary>
/// An IList&lt;T&gt; implementation optimized for random insertions/removal, cheap cloning
/// and subsetting implemented by the <see cref="Branch{T}"/> class.
/// </summary>
/// <remarks>
/// Concurrent reads are thread-safe, and clones are safe to use on other threads despite
/// sharing data with the original. Concurrent writes and read/writes are not thread-safe
/// and have undefined behavior.
/// </remarks>
[Serializable]
public sealed class Tree<T> : IList<T>, ICloneable
{
    #region fields and props
    internal Branch<T> root;

    /// <summary>
    /// Gets the length of the tree. O(1).
    /// </summary>
    /// <remarks>
    /// Concurrent reads are thread-safe.
    /// </remarks>
    public int Length => root.length;

    /// <summary>
    /// Gets the length of the tree. O(1).
    /// </summary>
    /// <remarks>
    /// Concurrent reads are thread-safe.
    /// </remarks>
    public int Count => root.length;
    #endregion

    #region constructors
    /// <summary>
    /// Create a tree representing an imaginary static empty tree.
    /// </summary>
    public Tree()
    {
        root = Branch<T>.Empty;

        root.CheckConsistency();
    }

    /// <summary>
    /// Create a tree from the input. O(N).
    /// </summary>
    /// <exception cref="ArgumentNullException">input is null.</exception>
    public Tree(IEnumerable<T> input)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        if (input is Tree<T> tree)
        {
            // make the input tree shareable
            tree.root.Publish();

            root = tree.root;
        }
        else
        {
            if (input is string text)
            {
                // if a string is IEnumerable<T>, T must be char
                ((Tree<char>)(object)this).root = Branch<char>.FromString(text);
            }
            else
            {
                var arr = ToArray(input);

                root = Branch<T>.Create(arr, 0, arr.Length);
            }
        }

        root!.CheckConsistency();
    }

    /// <summary>
    /// Create a tree from an array range. O(N).
    /// </summary>
    /// <exception cref="ArgumentNullException">input is null.</exception>
    public Tree(T[] array, int index, int count)
    {
        VerifyRange(array, index, count);

        root = Branch<T>.Create(array, index, count);

        root.CheckConsistency();
    }

    /// <summary>
    /// Create a new tree that lazily initalizes its content.
    /// </summary>
    /// <param name="length">The length of the tree.</param>
    /// <param name="initializer">The callback that will initialize (populate) the tree.</param>
    /// <remarks>
    /// The initializer will be called exactly once when the content of this tree is first
    /// requested, and must return a tree with the pecified length.
    /// Cloning a tree does **not** call the initializer, making it possible for clones 
    /// to be used in other threads, as well as calling the initializer from any thread.
    /// Insertions at the beginning or the end do **not** call the initializer either.
    /// Concatenation into larger trees using <see cref="Concat(Tree{T},Tree{T})"/> may call
    /// the initializer, but without further modification it may yield a tree with parts 
    /// yet to be lazily loaded.
    /// </remarks>
    public Tree(int length, Func<Tree<T>> initializer)
    {
        if (initializer is null)
        {
            throw new ArgumentNullException(nameof(initializer));
        }

        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), $"Length = {length} < 0");
        }

        if (length == 0)
        {
            root = Branch<T>.Empty;
        }
        else
        {
            root = new FunctionBranch<T>(length, initializer);
        }

        root.CheckConsistency();
    }

    /// <summary>
    /// Create a tree from a branch.
    /// </summary>
    /// <param name="root">A branch from which to build the tree.</param>
    internal Tree(Branch<T> root)
    {
        this.root = root;

        root.CheckConsistency();
    }
    #endregion

    #region clone and clear
    /// <summary>
    /// Clones the tree. O(N) to the number of tree nodes touched since the last clone
    /// was created, O(1) for the remainder (the per-node cost of the alterations has no impact).
    /// </summary>
    /// <remarks>
    /// Concurrent reads are thread-safe.
    /// Publish() alters this instance but remains thread-safe as long as as the structure doesn't
    /// change.
    /// </remarks>
    public Tree<T> Clone()
    {
        root.Publish();

        return new Tree<T>(root);
    }

    object ICloneable.Clone()
    {
        return Clone();
    }

    /// <summary>
    /// Resets the tree to an empty list. O(1).
    /// </summary>
    public void Clear()
    {
        root = Branch<T>.Empty;

        OnChanged();
    }
    #endregion

    #region alterations
    /// <summary>
    /// Insert another tree into this tree at index. O(logN + logM) plus the per-node
    /// cost as if Clone() was called.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">index is outside the valid range.</exception>
    /// <exception cref="ArgumentNullException">tree is null.</exception>
    public void InsertAt(int index, Tree<T> tree)
    {
        VerifyRange(index);

        (tree ?? throw new ArgumentNullException(nameof(tree))).root.Publish();

        root = root.InsertAt(index, tree.root);

        OnChanged();
    }

    /// <summary>
    /// Insert elements from an enumerable into this tree at index. O(logN + M).
    /// </summary>
    /// <exception cref="ArgumentNullException">input is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">index or length is outside the valid range.</exception>
    public void InsertAt(int index, IEnumerable<T> input)
    {
        if (input is Tree<T> tree)
        {
            InsertAt(index, tree);
        }
        else
        {
            var arr = ToArray(input ?? throw new ArgumentNullException(nameof(input)));

            InsertAt(index, arr, 0, arr.Length);
        }
    }

    /// <summary>
    /// Inserts elements from an array into this tree at index. O(logN + M).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">index or count is outside the valid range.</exception>
    public void InsertAt(int index, T[] array, int arrayIndex, int count)
    {
        VerifyRange(index);

        VerifyRange(array, arrayIndex, count);

        if (count > 0)
        {
            root = root.InsertAt(index, array, arrayIndex, count);

            OnChanged();
        }
    }

    /// <summary>
    /// Append the input to the end of this tree. O(logN + M).
    /// </summary>
    public void Append(IEnumerable<T> input)
    {
        InsertAt(Length, input);
    }

    /// <summary>
    /// Append another tree to the end of this tree. O(logN + logM), plus a per-node cost as
    /// if <c>tree.Clone()</c> was called.
    /// </summary>
    public void Append(Tree<T> tree)
    {
        InsertAt(Length, tree);
    }

    /// <summary>
    /// Append an array range to the end of this tree. O(logN + M).
    /// </summary>
    public void Append(T[] array, int arrayIndex, int count)
    {
        InsertAt(Length, array, arrayIndex, count);
    }

    /// <summary>
    /// Removes a range of elements from the tree. O(logN).
    /// </summary>
    public void RemoveAt(int index, int count)
    {
        VerifyRange(index, count);

        if (count > 0)
        {
            root = root.RemoveAt(index, count);

            OnChanged();
        }
    }

    /// <summary>
    /// Removes a single item from the tree.
    /// </summary>
    public void RemoveAt(int index)
    {
        RemoveAt(index, 1);
    }

    /// <summary>
    /// Overwrites a range of the specified array into the tree with a range of
    /// array elements. O(logN + M).
    /// </summary>
    public void UpdateAt(int index, T[] array, int arrayIndex, int count)
    {
        VerifyRange(index, count);

        VerifyRange(array, arrayIndex, count);

        if (count > 0)
        {
            root = root.Populate(array, arrayIndex, index, count);

            OnChanged();
        }
    }

    /// <summary>
    /// Create a new tree and initializes it with a slice of this tree. O(logN) plus a a
    /// per-node cost as if <c>this.Clone()</c> was called.
    /// </summary>
    /// <remarks>
    /// Concurrent reads are thread-safe.
    /// </remarks>
    public Tree<T> Slice(int index, int count)
    {
        VerifyRange(index, count);

        Tree<T> tree = Clone();

        var end = index + count;

        tree.RemoveAt(end, tree.Length - end);
        tree.RemoveAt(0, index);

        return tree;
    }
    #endregion

    #region concatenate
    /// <summary>
    /// Concatenate two trees without modifying them. O(logN + logM).
    /// </summary>
    /// <remarks>
    /// Concurrent reads are thread-safe.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1000:DoNotDeclareStaticMembersOnGenericTypes")]
    public static Tree<T> Concat(Tree<T> left, Tree<T> right)
    {
        (left  ?? throw new ArgumentNullException(nameof(left))).root.Publish();
        (right ?? throw new ArgumentNullException(nameof(right))).root.Publish();
        
        return new Tree<T>(Branch<T>.Concat(left.root, right.root));
    }

    /// <summary>
    /// Concatenates multiple trees. The input trees are not modified.
    /// </summary>
    /// <remarks>
    /// Concurrent reads are thread-safe.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1000:DoNotDeclareStaticMembersOnGenericTypes")]
    public static Tree<T> Concat(params Tree<T>[] trees)
    {
        Tree<T> result = new();

        foreach (Tree<T> tree in trees ?? throw new ArgumentNullException(nameof(trees)))
        {
            result.Append(tree);
        }

        return result;
    }
    #endregion

    #region indexing
    /// <summary>
    /// A branch + index in tree composite, used to determine
    /// whether other branches are close and speed up find ops.
    /// </summary>
    internal readonly struct BranchCacheEntry
    {
        internal readonly Branch<T> branch;
        internal readonly int index;

        internal BranchCacheEntry(Branch<T> branch, int offset)
        {
            this.branch = branch;
            index = offset;
        }

        internal bool IsInside(int offset)
        {
            return index <= offset && offset < index + branch.length;
        }
    }

    /// <summary>
    /// A cache of visited branches with the last one visited on top,
    /// used to speed up the tree indexer and find ops.
    /// </summary>
    [NonSerialized]
    private volatile ImmutableStack<BranchCacheEntry>? branchCache;

    /// <summary>
    /// Find a branch in the cache by index, updating the cache
    /// as the tree is traversed.
    /// </summary>
    internal ImmutableStack<BranchCacheEntry> FindCachedBranch(int index)
    {
        Debug.Assert(0 <= index && index < Length);

        // fetch stack into a local variable for thread safety
        var cache = branchCache;
        var oldCache = cache;

        cache ??= ImmutableStack<BranchCacheEntry>.Empty.Push(new BranchCacheEntry(root, 0));

        while (!cache.PeekOrDefault().IsInside(index))
        {
            cache = cache.Pop();
        }

        while (true)
        {
            var entry = cache.PeekOrDefault();

            // check if we've reached a leaf or function node
            if (entry.branch.height == 0)
            {
                // function branch, go down into its subtree
                if (entry.branch.contents is null)
                {
                    entry = new BranchCacheEntry(entry.branch.Create(), entry.index);
                }

                // a leaf, we're done
                if (entry.branch.contents is not null)
                {
                    break;
                }
            }

            // keep traversing
            if (index - entry.index >= entry.branch.left!.length)
            {
                cache = cache.Push(new BranchCacheEntry(entry.branch.right!, entry.index + entry.branch.left.length));
            }
            else
            {
                cache = cache.Push(new BranchCacheEntry(entry.branch.left, entry.index));
            }
        }

        // update the cache if it changed
        if (oldCache != cache)
        {
            branchCache = cache;
        }

        // guarantee that a leaf node was found
        Debug.Assert(cache.Peek().branch.contents is not null);

        return cache;
    }


    /// <summary>
    /// Get/Set the content elemen at indext. O(logN) for random access,
    /// amortized O(1) for sequential read-only access.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">index is outside the valid range (0 to Length-1).</exception>
    /// <remarks>
    /// Concurrent getter calls are thread-safe.
    /// Casting integers as unsigned integers make negative values overflow and satisfy the condition--nice trick.
    /// </remarks>
    public T this[int index]
    {
        get
        {
            if (unchecked((uint)index >= (uint)Length))
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"{index} >= {Length}");
            }

            var entry = FindCachedBranch(index).PeekOrDefault();

            return entry.branch.contents![index - entry.index];
        }
        set
        {
            if (index < 0 || Length <= index)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"{index} < 0 or {Length} <= {index}");
            }

            root = root.SetElement(index, value);

            OnChanged();
        }
    }

    /// <summary>
    /// The index of the first occurrence of item in a given range of the tree or -1 if not found. O(N).
    /// </summary>
    /// <param name="item">Item to search for.</param>
    /// <param name="index">Start index of the search.</param>
    /// <param name="count">Length of the area to search.</param>
    /// <returns>The first index where the item was found; or -1 if no occurrence was found.</returns>
    /// <remarks>
    /// Concurrent reads are thread-safe.
    /// </remarks>
    public int IndexOf(T item, int index, int count)
    {
        VerifyRange(index, count);

        while (count > 0)
        {
            var entry = FindCachedBranch(index).PeekOrDefault();

            var contents = entry.branch.contents;

            var startIndex = index - entry.index;
            
            var length = Math.Min(entry.branch.length, startIndex + count);
            
            var i = Array.IndexOf(contents!, item, startIndex, length - startIndex);
 
            if (i >= 0)
            {
                return entry.index + i;
            }

            count -= length - startIndex;
            index = entry.index + length;
        }

        return -1;
    }

    /// <summary>
    /// The index of the last occurrence of item in the tree or -1 if not found. O(N).
    /// </summary>
    public int LastIndexOf(T item)
    {
        return LastIndexOf(item, 0, Length);
    }

    /// <summary>
    /// The index of the last occurrence of item in a given range of the tree or -1 if not found. O(N).
    /// </summary>
    /// <remarks>Proceeds backwards from index + count. This is different than the meaning of
    /// Array.LastIndexOf() parameters.</remarks>
    public int LastIndexOf(T item, int index, int count)
    {
        VerifyRange(index, count);

        var comparer = EqualityComparer<T>.Default;

        for (int i = index + count - 1; i >= index; i--)
        {
            if (comparer.Equals(this[i], item))
            {
                return i;
            }
        }

        return -1;
    }
    #endregion

    #region events
    internal void OnChanged()
    {
        branchCache = null!;

        root.CheckConsistency();
    }
    #endregion

    #region to array
    /// <summary>
    /// Convert the input to array. O(N).
    /// </summary>
    private static T[] ToArray(IEnumerable<T> input)
    {
        return input as T[] ?? input.ToArray();
    }

    /// <summary>
    /// Return the entire tree as an array. O(N).
    /// </summary>
    /// <remarks>
    /// Counts as a read; concurrent reads are thread-safe.
    /// </remarks>
    public T[] ToArray()
    {
        var array = new T[Length];

        root.CopyTo(array, 0, 0, array.Length);

        return array;
    }

    /// <summary>
    /// Return a section of the tree as an array. O(N).
    /// Runs in O(N).
    /// </summary>
    /// <remarks>
    /// Counts as a read; concurrent reads are thread-safe.
    /// </remarks>
    public T[] ToArray(int startIndex, int count)
    {
        VerifyRange(startIndex, count);

        var arr = new T[count];

        CopyTo(startIndex, arr, 0, count);

        return arr;
    }
    #endregion

    #region IList<T>
    /// <summary>
    /// Inserts the item at the specified index in the tree.
    /// </summary>
    public void Insert(int index, T item)
    {
        InsertAt(index, new[] { item }, 0, 1);
    }

    /// <summary>
    /// Append one item at the end of the tree.
    /// </summary>
    public void Add(T item)
    {
        Append(new[] { item }, 0, 1);
    }

    /// <summary>
    /// Removes the first occurrence of an item from the tree. O(N).
    /// </summary>
    public bool Remove(T item)
    {
        int index = IndexOf(item);

        if (index >= 0)
        {
            RemoveAt(index);

            return true;
        }

        return false;
    }

    /// <summary>
    /// The index of the first occurrence of item in the tree or -1 if not found. O(N).
    /// </summary>
    /// <remarks>
    /// Concurrent reads are thread-safe.
    /// </remarks>
    public int IndexOf(T item)
    {
        return IndexOf(item, 0, Length);
    }
    #endregion

    #region ICollection<T>
    bool ICollection<T>.IsReadOnly => false;

    /// <summary>
    /// Whether the tree contains item. O(N).
    /// </summary>
    /// <remarks>
    /// Concurrent reads are thread-safe.
    /// </remarks>
    public bool Contains(T item)
    {
        return IndexOf(item) >= 0;
    }

    /// <summary>
    /// Copies the whole content of the tree into the specified array. O(N).
    /// </summary>
    /// <remarks>
    /// Concurrent reads are thread-safe.
    /// </remarks>
    public void CopyTo(T[] array, int arrayIndex)
    {
        CopyTo(0, array, arrayIndex, Length);
    }

    /// <summary>
    /// Copies a range of the tree into the specified array. O(logN + M).
    /// </summary>
    /// <remarks>
    /// Concurrent reads are thread-safe.
    /// </remarks>
    public void CopyTo(int index, T[] array, int arrayIndex, int count)
    {
        VerifyRange(index, count);
        VerifyRange(array, arrayIndex, count);

        root.CopyTo(array, arrayIndex, index, count);
    }
    #endregion

    #region IEnumerator
    /// <summary>
    /// Retrieves an enumerator to iterate through the tree.
    /// The enumerator will reflect the state of the tree from the GetEnumerator() call, further modifications
    /// to the tree will not be visible to the enumerator.
    /// </summary>
    /// <remarks>
    /// This method counts as a read access and may be called concurrently to other read accesses.
    /// </remarks>
    public IEnumerator<T> GetEnumerator()
    {
        root.Publish();
        return Enumerate(root);
    }

    private static IEnumerator<T> Enumerate(Branch<T> node)
    {
        Stack<Branch<T>> stack = new();
        while (node != null)
        {
            // go to leftmost node, pushing the right parts that we'll have to visit later
            while (node.contents == null)
            {
                if (node.height == 0)
                {
                    // go down into function nodes
                    node = node.Create();
                    continue;
                }
                Debug.Assert(node.right != null);
                stack.Push(node.right);
                node = node.left!;
            }

            // yield contents of leaf node
            for (int i = 0; i < node.length; i++)
            {
                yield return node.contents[i];
            }
            // go up to the next node not visited yet
            if (stack.Count > 0)
            {
                node = stack.Pop();
            }
            else
            {
                node = null!;
            }
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
    #endregion

    #region verify range
    /// <summary>
    /// Verify index is within this tree's range.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">index is out of range.</exception>
    internal void VerifyRange(int index)
    {
        if (index < 0 || Length < index)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"{index} < 0 or {Length} < {index}");
        }
    }

    /// <summary>
    /// Verify index and length are within this tree's range.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">At least one argument is not in range.</exception>
    internal void VerifyRange(int index, int length)
    {
        VerifyRange(index);

        if (length < 0 || Length < index + length)
        {
            throw new ArgumentOutOfRangeException(nameof(length), $"{length} < 0 or {Length} < {index} + {length}");
        }
    }

    /// <summary>
    /// Verify that index and count are within an array's range.
    /// </summary>
    /// <exception cref="ArgumentNullException">The array is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">At least one argument is not in range.</exception>
    internal static void VerifyRange(T[] array, int index, int count)
    {
        if (array is null)
        {
            throw new ArgumentNullException(nameof(array));
        }

        if (index < 0 || array.Length < index)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"{index} < 0 or {array.Length} < {index}");
        }

        if (count < 0 || array.Length < index + count)
        {
            throw new ArgumentOutOfRangeException(nameof(count), $"{count} < 0 or {array.Length} < {index} + {count}");
        }
    }
    #endregion

    #region to string
    /// <summary>
    /// Creates a string from the tree. Runs in O(N).
    /// </summary>
    /// <returns>A string consisting of all elements in the tree as comma-separated list in {}.
    /// As a special case, Rope&lt;char&gt; will return its contents as string without any additional separators or braces,
    /// so it can be used like StringBuilder.ToString().</returns>
    /// <remarks>
    /// This method counts as a read access and may be called concurrently to other read accesses.
    /// </remarks>
    public override string ToString()
    {
        if (this is Tree<char> tree)
        {
            return tree.ToString(0, Length);
        }
        else
        {
            StringBuilder b = new();

            foreach (T element in this)
            {
                b.Append(b.Length == 0 ? "{ " : ", ");
                b.Append(element!.ToString());
            }

            b.Append(" }");

            return b.ToString();
        }
    }

    internal string GetTreeAsString()
    {
#if DEBUG
        return root.ToStringRecursive();
#else
		return "Available only in DEBUG builds.";
#endif
    }
    #endregion
}
