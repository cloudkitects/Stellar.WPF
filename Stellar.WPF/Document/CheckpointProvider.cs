using System;

namespace Stellar.WPF.Document;

/// <summary>
/// Maintains a linked list of ICheckpoint instances.
/// </summary>
public class CheckpointProvider
{
    private Checkpoint current;

    public CheckpointProvider()
    {
        current = new Checkpoint(this);
    }

    public ICheckpoint Current => current;

    /// <summary>
    /// Replaces the current checkpoint with a new one.
    /// </summary>
    /// <param name="change">Change from the current checkpoint to the new one</param>
    public void Append(ChangeEventArgs change)
    {
        current.change = change ?? throw new ArgumentNullException(nameof(change));

        current.next = new Checkpoint(current);

        current = current.next;
    }
}
