using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Stellar.WPF.Document;

[DebuggerDisplay("Checkpoint {id}")]
internal sealed class Checkpoint : ICheckpoint
{
    // provider reference to determine if two checkpoints belong to the same document
    private readonly CheckpointProvider provider;

    // id to compare checkpoints
    private readonly int id;

    // the change from this checkpoint to the next
    internal TextChangeEventArgs? change;
    internal Checkpoint? next;

    internal Checkpoint(CheckpointProvider provider)
    {
        this.provider = provider;
    }

    internal Checkpoint(Checkpoint prev)
    {
        provider = prev.provider;

        id = unchecked(prev.id + 1);
    }

    public bool BelongsToSameDocumentAs(ICheckpoint checkpoint)
    {
        return checkpoint is Checkpoint other && provider == other.provider;
    }

    public int CompareAge(ICheckpoint checkpoint)
    {
        if (checkpoint is null)
        {
            throw new ArgumentNullException(nameof(checkpoint));
        }

        if (checkpoint is not Checkpoint other || provider != other.provider)
        {
            throw new ArgumentException("The checkpoint does not belong to the same document.");
        }

        // the max distance between checkpoints is 2^31-1, which is guaranteed on x86
        // as they wouldn't fit into memory anyways. That said, overflows allowed! 
        return Math.Sign(unchecked(id - other.id));
    }

    public IEnumerable<TextChangeEventArgs> GetChangesTo(ICheckpoint checkpoint)
    {
        var result = CompareAge(checkpoint);

        var other = (Checkpoint)checkpoint;

        if (result < 0)
        {
            return GetForwardChanges(other);
        }

        if (result > 0)
        {
            return other.GetForwardChanges(this).Reverse().Select(change => change.Invert());
        }

        return Array.Empty<TextChangeEventArgs>();
    }

    /// <summary>
    /// Get changes from [this, other).
    /// </summary>
    private IEnumerable<TextChangeEventArgs> GetForwardChanges(Checkpoint other)
    {
        for (var checkpoint = this; checkpoint != other; checkpoint = checkpoint?.next)
        {
            yield return checkpoint?.change!;
        }
    }

    public int MoveOffsetTo(ICheckpoint checkpoint, int oldOffset, AnchorMovementType movement)
    {
        var offset = oldOffset;

        foreach (var change in GetChangesTo(checkpoint))
        {
            offset = change.ComputeOffset(offset, movement);
        }

        return offset;
    }
}
