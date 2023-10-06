using System;
using System.Diagnostics;
using System.Text;

namespace Stellar.WPF.Utilities;

/// <summary>
/// A Branch specialization that is always shared and cannot be cloned,
/// and that ensures the initializer is only evaluated once.
/// </summary>
/// <typeparam name="T">The branch contents type.</typeparam>
internal sealed class FunctionBranch<T> : Branch<T>
{
    private Func<Tree<T>> initializer;
    private Branch<T>? cachedResult;

    public FunctionBranch(int length, Func<Tree<T>> initializer)
    {
        Debug.Assert(length > 0);
        Debug.Assert(initializer is not null);

        this.length = length;
        this.initializer = initializer;
        
        isShared = true;
    }

    /// <summary>
    /// Get a branch using the initializer function.
    /// </summary>
    /// <returns>A branch or a leaf instance.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    internal override Branch<T> Create()
    {
        lock (this)
        {
            if (cachedResult is null)
            {
                if (initializer is null)
                {
                    throw new InvalidOperationException("The tree initializer is null.");
                }

                var initialize = initializer;

                initializer = null!;
                
                var result = (initialize() ?? throw new InvalidOperationException("The tree initializer returned null.")).root;

                // share it with the tree containing the function branch
                result.Publish();
                
                if (result.length != length)
                {
                    throw new InvalidOperationException("The tree initializer returned a tree with the wrong length.");
                }

                // keep going down if result is another function branch
                if (result.height == 0 && result.contents is null)
                {
                    cachedResult = result.Create();
                }
                else
                {
                    cachedResult = result;
                }
            }

            return cachedResult;
        }
    }

#if DEBUG
    internal override void AppendToString(StringBuilder b, int indent)
    {
        Branch<T> result;

        lock (this)
        {
            b.AppendLine(ToString());

            result = cachedResult!;
        }

        indent += 2;
        
        if (result != null)
        {
            b.Append(' ', indent);
            b.Append("C: ");
            
            result.AppendToString(b, indent);
        }
    }

    public override string ToString()
    {
        return $"[function length={length} initialized={initializer is null}]";
    }
#endif
}
