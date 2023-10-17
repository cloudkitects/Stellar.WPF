using System;
using System.Collections.Generic;

namespace Stellar.WPF.Utilities;

/// <summary>
/// Double-ended queue.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix")]
[Serializable]
public sealed class Deque<T> : ICollection<T>
{
    T[] arr = Array.Empty<T>();

    int size, head, tail;

    /// <inheritdoc/>
    public int Count => size;

    /// <inheritdoc/>
    public void Clear()
    {
        arr = Array.Empty<T>();

        size = 0;
        head = 0;
        tail = 0;
    }

    /// <summary>
    /// Gets/Sets an element inside the deque.
    /// </summary>
    public T this[int index]
    {
        get
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"{index} < 0");
            }

            return arr[(head + index) % arr.Length];
        }
        set
        {
            if (index < 0 || size - 1 < index)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"{index} < 0 or {size} - 1 < {index}");
            }

            arr[(head + index) % arr.Length] = value;
        }
    }

    /// <summary>
    /// Adds an element to the end of the deque.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", MessageId = "PushBack")]
    public void PushBack(T item)
    {
        if (size == arr.Length)
        {
            SetCapacity(Math.Max(4, arr.Length * 2));
        }

        arr[tail++] = item;

        if (tail == arr.Length)
        {
            tail = 0;
        }

        size++;
    }

    /// <summary>
    /// Pops an element from the end of the deque.
    /// </summary>
    public T PopBack()
    {
        if (size == 0)
        {
            throw new InvalidOperationException();
        }

        if (tail == 0)
        {
            tail = arr.Length - 1;
        }
        else
        {
            tail--;
        }

        T item = arr[tail];

        arr[tail] = default!; // allow GC to collect the item

        size--;

        return item;
    }

    /// <summary>
    /// Adds an element to the front of the deque.
    /// </summary>
    public void PushFront(T item)
    {
        if (size == arr.Length)
        {
            SetCapacity(Math.Max(4, arr.Length * 2));
        }

        if (head == 0)
        {
            head = arr.Length - 1;
        }
        else
        {
            head--;
        }

        arr[head] = item;

        size++;
    }

    /// <summary>
    /// Pops an element from the end of the deque.
    /// </summary>
    public T PopFront()
    {
        if (size == 0)
        {
            throw new InvalidOperationException();
        }

        T item = arr[head];
        
        arr[head] = default!; // allow GC to collect the item
        
        head++;
        
        if (head == arr.Length)
        {
            head = 0;
        }
        
        size--;
        
        return item;
    }

    void SetCapacity(int capacity)
    {
        var newArray = new T[capacity];

        CopyTo(newArray, 0);
        
        head = 0;
        tail = (size == capacity)
            ? 0
            : size;
        
        arr = newArray;
    }

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator()
    {
        if (head < tail)
        {
            for (var i = head; i < tail; i++)
            {
                yield return arr[i];
            }
        }
        else
        {
            for (var i = head; i < arr.Length; i++)
            {
                yield return arr[i];
            }

            for (var i = 0; i < tail; i++)
            {
                yield return arr[i];
            }
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    bool ICollection<T>.IsReadOnly => false;

    void ICollection<T>.Add(T item)
    {
        PushBack(item);
    }

    /// <inheritdoc/>
    public bool Contains(T item)
    {
        var comparer = EqualityComparer<T>.Default;


        foreach (T element in this)
        {
            if (comparer.Equals(item, element))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public void CopyTo(T[] array, int arrayIndex)
    {
        if (array is null)
        {
            throw new ArgumentNullException(nameof(array));
        }
        
        if (head < tail)
        {
            Array.Copy(arr, head, array, arrayIndex, tail - head);
        }
        else
        {
            var length = arr.Length - head;

            Array.Copy(arr, head, array, arrayIndex, length);
            Array.Copy(arr, 0, array, arrayIndex + length, tail);
        }
    }

    bool ICollection<T>.Remove(T item)
    {
        throw new NotSupportedException();
    }
}

