using System;
using System.Collections.Generic;
using System.Text;

namespace Stellar.WPF.Utilities;

/// <summary>
/// An immutable stack.
/// foreach returns items from top to bottom (in the order they'd be popped)
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix")]
[Serializable]
public sealed class ImmutableStack<T> : IEnumerable<T>
{
    /// <summary>
    /// Gets the empty stack instance.
    /// </summary>
    public static readonly ImmutableStack<T> Empty = new();
    private readonly T? value;
    private readonly ImmutableStack<T>? next;

    private ImmutableStack()
    {
    }

    private ImmutableStack(T value, ImmutableStack<T> next)
    {
        this.value = value;
        this.next = next;
    }

    /// <summary>
    /// Push an item.
    /// Returns a new stack with the value pushed.
    /// </summary>
    public ImmutableStack<T> Push(T item)
    {
        return new ImmutableStack<T>(item, this);
    }

    /// <summary>
    /// Get the top item.
    /// </summary>
    /// <exception cref="InvalidOperationException">The stack is empty.</exception>
    public T Peek()
    {
        if (IsEmpty)
        {
            throw new InvalidOperationException("The stack is empty.");
        }

        return value!;
    }

    /// <summary>
    /// Get the top item or <c>default(T)</c> if the stack is empty.
    /// </summary>
    public T PeekOrDefault()
    {
        return value!;
    }

    /// <summary>
    /// Pop the top item from the stack.
    /// </summary>
    /// <exception cref="InvalidOperationException">The stack is empty.</exception>
    public ImmutableStack<T> Pop()
    {
        if (IsEmpty)
        {
            throw new InvalidOperationException("The stack is empty.");
        }

        return next!;
    }

    /// <summary>
    /// Gets if this stack is empty.
    /// </summary>
    public bool IsEmpty => next is null;

    /// <summary>
    /// Gets an enumerator that iterates through the stack top-to-bottom.
    /// </summary>
    public IEnumerator<T> GetEnumerator()
    {
        var stack = this;

        while (!stack!.IsEmpty)
        {
            yield return stack.value!;

            stack = stack.next;
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        StringBuilder b = new("[stack");

        foreach (T item in this)
        {
            b.Append(' ');
            b.Append(item);
        }

        b.Append(']');

        return b.ToString();
    }
}