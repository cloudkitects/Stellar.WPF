using System;
using System.Diagnostics;
using System.Text;

namespace Stellar.WPF.Utilities;


/// <summary>
/// A branch/subtree in a tree with three types:
/// height >= 1, left != null, right != null, contents == null, a branch with no content
/// height == 0, left == null, right == null, contents != null, a leaf with content and no children
/// height == 0, left == null, right == null, contents == null, a leaf created by a function on-demand
/// </summary>
/// <typeparam name="T">The branch contents type.</typeparam>
[Serializable]
internal class Branch<T>
{
    #region fields and props
    internal const int Size = 256;

    internal static readonly Branch<T> Empty = new() { isShared = true, contents = new T[Branch<T>.Size] };

    // sub-node pointers
    internal Branch<T>? left, right;

    // whether shared by multiple trees
    internal volatile bool isShared;

    // the total length of all text in this branch
    internal int length;

    // the height of this branch: 0 for leaf nodes; 1 + max(left.height, right.height) for true branches
    internal byte height;

    // contents of leaf nodes only
    internal T[]? contents;

    // TODO: what's the value at construction? Does it throw?
    internal int Tilt => right!.height - left!.height;
    #endregion

    #region check consistency
    /// <summary>
    /// Check consistency by asserting every invariant.
    /// </summary>
    [Conditional("DATACONSISTENCYTEST")]
    internal void CheckConsistency()
    {
        // a leaf
        if (height == 0)
        {
            Debug.Assert(left is null && right is null);

            if (contents is null)
            {
                Debug.Assert(this is FunctionBranch<T>);
                Debug.Assert(length > 0);
                Debug.Assert(isShared);
            }
            else
            {
                Debug.Assert(contents is not null && contents.Length == Size);
                Debug.Assert(0 <= length && length <= Size);
            }
        }
        else
        {
            Debug.Assert(left is not null && right is not null);
            Debug.Assert(contents is null);
            Debug.Assert(length == left.length + right.length);
            Debug.Assert(height == 1 + Math.Max(left.height, right.height));

            // either balanced, slightly to the right or more to the left
            Debug.Assert(Math.Abs(Tilt) <= 1);

            // this additional invariant forces the branch to combine small leafs to
            // prevent excessive memory usage: all branches except for the empty tree's
            // single branch have length >= 1
            Debug.Assert(length > Size);

            if (isShared)
            {
                Debug.Assert(left.isShared && right.isShared);
            }

            left.CheckConsistency();
            right.CheckConsistency();
        }
    }
    #endregion

    #region clone, copy and share
    /// <summary>
    ///  super-fast clone
    /// </summary>
    /// <returns>A new branch with the same contents as this one.</returns>
    /// <remarks>A clone within the context of this branch (i.e., not shared) is this branch!</remarks>
    internal Branch<T> Clone()
    {
        if (!isShared)
        {
            return this;
        }

        // leaf
        if (height == 0)
        {
            // function--evaluate it
            if (contents is null)
            {
                return Create().Clone();
            }

            var copy = new T[Size];

            contents.CopyTo(copy, 0);

            return new Branch<T>
            {
                length = length,
                contents = copy
            };
        }
        else
        {
            return new Branch<T>
            {
                left = left,
                right = right,
                length = length,
                height = height
            };
        }
    }

    /// <summary>
    /// Copy a section of this branch to an array.
    /// </summary>
    internal void CopyTo(T[] array, int arrayIndex, int index, int count)
    {
        if (height == 0)
        {
            if (contents == null)
            {
                // function node
                Create().CopyTo(array, arrayIndex, index, count);
            }
            else
            {
                // leaf node
                Array.Copy(contents, index, array, arrayIndex, count);
            }
        }
        else
        {
            // concat node
            if (index + count <= left!.length)
            {
                left.CopyTo(array, arrayIndex, index, count);
            }
            else if (index >= left.length)
            {
                right!.CopyTo(array, arrayIndex, index - left.length, count);
            }
            else
            {
                var leftCount = left.length - index;

                left!.CopyTo(array, arrayIndex, index, leftCount);
                right!.CopyTo(array, arrayIndex + leftCount, 0, count - leftCount);
            }
        }
    }
    
    /// <summary>
    /// Mark this branch and all descendants as shareable.
    /// </summary>
    internal void Publish()
    {
        if (!isShared)
        {
            left?.Publish();
            right?.Publish();

            isShared = true;
        }
    }
    #endregion

    #region create
    /// <summary>
    /// Create a branch from an element array.
    /// </summary>
    /// <param name="arr">The element array.</param>
    /// <param name="index"></param>
    /// <param name="length">The desired branch length.</param>
    /// <returns>a new branch from an element array.</returns>
    internal static Branch<T> Create(T[] arr, int index, int length)
    {
        if (length == 0)
        {
            return Empty;
        }

        var branch = Create(length);

        return branch.Populate(arr, index, 0, length);
    }

    /// <summary>
    /// Create a new branch with the given length.
    /// </summary>
    /// <param name="length">The desired length.</param>
    /// <returns>A new branch with the given length.</returns>
    internal static Branch<T> Create(int length)
    {
        var count = (length + Size - 1) / Size;

        return Create(count, length);
    }

    /// <summary>
    /// Create a new branch with the given leaf count and length.
    /// </summary>
    /// <param name="count">The desired leaf count.</param>
    /// <param name="length">The desired length.</param>
    /// <returns>A new branch with the given length.</returns>
    private static Branch<T> Create(int count, int length)
    {
        Debug.Assert(count > 0);
        Debug.Assert(length > 0);

        var result = new Branch<T>() { length = length };

        if (count == 1)
        {
            result.contents = new T[Size];
        }
        else
        {
            var rightCount = count / 2;
            var leftCount = count - rightCount;
            var leftLength = leftCount * Size;

            result.left = Create(leftCount, leftLength);
            result.right = Create(rightCount, length - leftLength);

            result.height = (byte)(1 + Math.Max(result.left.height, result.right.height));
        }

        return result;
    }

    /// <summary>
    /// Populate this branch from an array.
    /// </summary>
    internal Branch<T> Populate(T[] array, int arrayIndex, int index, int count)
    {
        var result = Clone();

        // leaf
        if (result.height == 0)
        {
            Array.Copy(array, arrayIndex, result.contents!, index, count);
        }
        else
        {
            if (index + count <= result.left!.length)
            {
                result.left = result.left.Populate(array, arrayIndex, index, count);
            }
            else if (index >= left!.length)
            {
                result.right = result.right!.Populate(array, arrayIndex, index - result.left.length, count);
            }
            else
            {
                var leftCount = result.left.length - index;

                result.left = result.left.Populate(array, arrayIndex, index, leftCount);
                result.right = result.right!.Populate(array, arrayIndex + leftCount, 0, count - leftCount);
            }

            // the layout can change when function branches are replaced with leaves
            result.Rebalance();
        }

        return result;
    }

    /// <summary>
    /// Create a leaf from a lazily-evaluated function--implemented
    /// only by function branches.
    /// </summary>
    internal virtual Branch<T> Create()
    {
        throw new InvalidOperationException("Create() is only implemented by function branches by design.");
    }
    #endregion

    #region balance
    /// <summary>
    /// Rebalance this branch and recompute height.
    /// Assumes children are already balanced and have up-to-date height.
    /// </summary>
    /// <remarks>
    /// It shouldn't be called on shared nodes and should only be called after
    /// modifications.
    /// left is null is enough to identify a leaf branch (which is always balanced)
    /// Adelson-Velsky and Landis (AVL) balancing: we don't care about true branches'
    /// identity, we only rearrange children--this branch remains on top.
    /// </remarks>
    internal void Rebalance()
    {
        Debug.Assert(!isShared);
        
        if (left is null)
        {
            return;
        }

        // ensure a merge wasn't missed
        Debug.Assert(length > Size);

        // loop until balanced; rotations can cause two small leaves to combine
        // into a larger one, changing the height
        while (Math.Abs(Tilt) > 1)
        {
            if (Tilt > 1)
            {
                if (right!.Tilt < 0)
                {
                    right = right.Clone();
                    right.RotateRight();
                }
                
                RotateLeft();
                
                // we shifted some of the inbalance to the left
                left.Rebalance();
            }
            else if (Tilt < -1)
            {
                if (left.Tilt > 0)
                {
                    left = left.Clone();
                    left.RotateLeft();
                }
                
                RotateRight();
                
                // we shifted some of the inbalance to the right
                right!.Rebalance();
            }
        }

        Debug.Assert(Math.Abs(Tilt) <= 1);
        
        height = (byte)(1 + Math.Max(left.height, right!.height));
    }

    private void RotateLeft()
    {
        Debug.Assert(!isShared);

        /* Rotate tree to the left
		 * 
		 *       this               this
		 *       /  \               /  \
		 *      A   right   ===>  left  C
		 *           / \          / \
		 *          B   C        A   B
		 */
        var a = left;
        var b = right!.left;
        var c = right.right;

        // reuse right if possible
        left = right.isShared
            ? new Branch<T>()
            : right;

        left.left = a;
        left.right = b;
        left.length = a!.length + b!.length;
        left.height = (byte)(1 + Math.Max(a.height, b.height));
        
        right = c;

        left.TryMerge();
    }

    private void RotateRight()
    {
        Debug.Assert(!isShared);

        /* Rotate tree to the right
		 * 
		 *       this             this
		 *       /  \             /  \
		 *     left  C   ===>    A  right
		 *     / \                   /  \
		 *    A   B                 B    C
		 */
        var a = left!.left;
        var b = left.right;
        var c = right;

        // reuse left if possible
        right = left.isShared
            ? new Branch<T>()
            : left;

        right.left = b;
        right.right = c;
        right.length = b!.length + c!.length;
        right.height = (byte)(1 + Math.Max(b.height, c.height));
        
        left = a;

        right.TryMerge();
    }

    /// <summary>
    /// Try merging branches, possible when this branch is not shared
    /// and length is less than or equeal to Size.
    /// </summary>
    /// <remarks>
    /// A branch is turned into a leaf if length <= Size.
    /// As opposed to leaves, function nodes are always marked shared.
    /// </remarks>
    private void TryMerge()
    {
        Debug.Assert(!isShared);

        if (length <= Size)
        {
            // convert this branch into a leaf
            height = 0;
            var leftLength = left!.length;

            if (left.isShared)
            {
                // must be a function node
                contents = new T[Size];
                left!.CopyTo(contents, 0, 0, leftLength);
            }
            else
            {
                // must be a leaf
                Debug.Assert(left.contents is not null);
                
                // reference it's contents to save memory
                contents = left.contents;
#if DEBUG
                // invalidate it in DEBUG builds--no one should be using it
                left.contents = Array.Empty<T>();
#endif
            }
            
            left = null;
            right!.CopyTo(contents, leftLength, 0, right.length);
            right = null;
        }
    }
    #endregion

    #region manipulate
    /// <summary>
    /// Set the contents element at the specified offset to the given value.
    /// </summary>
    /// <returns>A rebalanced clone of this branch with the updated contents.</returns>
    internal Branch<T> SetElement(int offset, T value)
    {
        var result = Clone();

        if (result.height == 0)
        {
            result.contents![offset] = value;
        }
        else
        {
            if (offset < result.left!.length)
            {
                result.left = result.left.SetElement(offset, value);
            }
            else
            {
                result.right = result.right!.SetElement(offset - result.left.length, value);
            }

            // layout can change after realizing function branches
            result.Rebalance();
        }

        return result;
    }

    /// <summary>
    /// Concatenate two branches.
    /// </summary>
    /// <remarks>
    /// left is guaranteed to be a leaf and not a function node (due to Clone())
    /// or a branch (because it's too short).
    /// </remarks>
    internal static Branch<T> Concat(Branch<T> left, Branch<T> right)
    {
        if (left.length == 0)
        {
            return right;
        }

        if (right.length == 0)
        {
            return left;
        }

        if (left.length + right.length <= Size)
        {
            left = left.Clone();

            right!.CopyTo(left.contents!, left.length, 0, right.length);

            left.length += right.length;

            return left;
        }
        else
        {
            var branch = new Branch<T>
            {
                left = left,
                right = right,
                length = left.length + right.length
            };

            branch.Rebalance();
            
            return branch;
        }
    }

    /// <summary>
    /// Split this leaf at offset and return a new branch with the text after offset.
    /// </summary>
    private Branch<T> SplitAt(int offset)
    {
        Debug.Assert(!isShared && height == 0 && contents is not null);

        var branch = new Branch<T>
        {
            contents = new T[Size],
            length = length - offset
        };

        Array.Copy(contents, offset, branch.contents, 0, branch.length);
        
        length = offset;
        
        return branch;
    }

    /// <summary>
    /// Insert a branch at the specified offset.
    /// </summary>
    internal Branch<T> InsertAt(int offset, Branch<T> branch)
    {
        if (offset == 0)
        {
            return Concat(branch, this);
        }
        else if (offset == length)
        {
            return Concat(this, branch);
        }

        // clone realizes function branches
        var result = Clone();

        if (result.height == 0)
        {
            // leaf
            var left = result;
            var right = left.SplitAt(offset);
            
            return Concat(Concat(left, branch), right);
        }
        else
        {
            if (offset < result.left!.length)
            {
                result.left = result.left.InsertAt(offset, branch);
            }
            else
            {
                result.right = result.right!.InsertAt(offset - result.left.length, branch);
            }

            result.length += branch.length;

            result.Rebalance();
            
            return result;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="offset"></param>
    /// <param name="array"></param>
    /// <param name="arrayIndex"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    internal Branch<T> InsertAt(int offset, T[] array, int arrayIndex, int count)
    {
        Debug.Assert(count > 0);

        if (length + count < Size)
        {
            var result = Clone();

            var resultContents = result.contents;

            // shift elements
            for (var i = result.length - offset; i >= 0; i--)
            {
                resultContents![i + offset + count] = resultContents[i + offset];
            }

            // fill the gap
            Array.Copy(array, arrayIndex, resultContents!, offset, count);
            
            result.length += count;
            
            return result;
        }
        else if (height == 0)
        {
            return InsertAt(offset, Create(array, arrayIndex, count));
        }
        else
        {
            var result = Clone();

            if (offset < result.left!.length)
            {
                result.left = result.left.InsertAt(offset, array, arrayIndex, count);
            }
            else
            {
                result.right = result.right!.InsertAt(offset - result.left.length, array, arrayIndex, count);
            }

            result.length += count;
            
            result.Rebalance();
            
            return result;
        }
    }

    internal Branch<T> RemoveAt(int index, int count)
    {
        Debug.Assert(count > 0);

        // removing the entire range
        if (index == 0 && count == length)
        {
            return Empty;
        }

        var end = index + count;
        
        var result = Clone();
        
        if (result.height == 0)
        {
            var remainder = result.length - end;

            for (var i = 0; i < remainder; i++)
            {
                result.contents![index + i] = result.contents[end + i];
            }

            result.length -= count;
        }
        else
        {
            if (end <= result.left!.length)
            {
                // left part only
                result.left = result.left.RemoveAt(index, count);
            }
            else if (index >= result.left.length)
            {
                // right part only
                result.right = result.right!.RemoveAt(index - result.left.length, count);
            }
            else
            {
                var leftCount = result.left.length - index;

                result.left = result.left.RemoveAt(index, leftCount);
                result.right = result.right!.RemoveAt(0, count - leftCount);
            }

            // remove empty branches
            if (result.left.length == 0)
            {
                return result.right!;
            }

            if (result.right!.length == 0)
            {
                return result.left;
            }

            result.length -= count;
            
            result.TryMerge();
            
            result.Rebalance();
        }

        return result;
    }
    #endregion

    #region specializations
    /// <summary>
    /// Create a T = char branch from a string.
    /// </summary>
    internal static Branch<char> FromString(string text)
    {
        if (text.Length == 0)
        {
            return Branch<char>.Empty;
        }

        var branch = Branch<char>.Create(text.Length);

        branch.Populate(text, 0);
        
        return branch;
    }
    #endregion

    #region debug output
#if DEBUG
    internal string ToStringRecursive()
    {
        var b = new StringBuilder();

        AppendToString(b, 0);

        return b.ToString();
    }
    
    internal virtual void AppendToString(StringBuilder b, int indent)
    {
        b.AppendLine(ToString());

        indent += 2;
        
        if (left is not null)
        {
            b.Append(' ', indent);
            b.Append("L: ");
            
            left.AppendToString(b, indent);
        }
        
        if (right is not null)
        {
            b.Append(' ', indent);
            b.Append("R: ");
            
            right.AppendToString(b, indent);
        }
    }

    public override string ToString()
    {
        var commonProps = $"length={length}, shared={isShared}";

        if (contents is not null)
        {
            return contents is char[] charContents
                ? $"[leaf {commonProps}, text=\"{new string(charContents, 0, length)}\"]"
                : $"[leaf {commonProps}]";
        }
        else
        {
            return $"[branch {commonProps}, height={height}, Tilt={Tilt}]";
        }
    }
#endif
    #endregion
}

