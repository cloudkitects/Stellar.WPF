using Stellar.WPF.Utilities;
using System;

namespace Stellar.WPF.Document;

public sealed class Anchor : IAnchor
{
    private readonly Document document;
    internal AnchorNode? node;

    internal Anchor(Document document)
    {
        this.document = document;
    }

    /// <summary>
    /// Gets the document owning the anchor.
    /// </summary>
    public Document Document => document;

    /// <inheritdoc/>
    public AnchorMovementType MovementType { get; set; }

    /// <inheritdoc/>
    public bool SurviveDeletion { get; set; }

    /// <inheritdoc/>
    public bool IsDeleted
    {
        get
        {
            document.DebugVerifyAccess();

            return node is null;
        }
    }

    /// <inheritdoc/>
    public event EventHandler? Deleted;

    internal void OnDeleted(EventQueue eventQueue)
    {
        node = null;

        eventQueue.Enqueue(Deleted!, this, EventArgs.Empty);
    }

    /// <summary>
    /// Gets the offset of the text anchor.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when trying to get the Offset from a deleted anchor.</exception>
    public int Offset
    {
        get
        {
            document.DebugVerifyAccess();

            var n = node ?? throw new InvalidOperationException();
            var offset = n.length;

            if (n.left is not null)
            {
                offset += n.left.totalLength;
            }

            while (n.parent is not null)
            {
                if (n == n.parent.right)
                {
                    if (n.parent.left is not null)
                    {
                        offset += n.parent.left.totalLength;
                    }

                    offset += n.parent.length;
                }

                n = n.parent;
            }

            return offset;
        }
    }

    /// <summary>
    /// Gets the line number of the anchor.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when trying to get the Offset from a deleted anchor.</exception>
    public int Line => document.GetLineByOffset(Offset).Number;

    /// <summary>
    /// Gets the column number of this anchor.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when trying to get the Offset from a deleted anchor.</exception>
    public int Column
    {
        get
        {
            int offset = Offset;
            return offset - document.GetLineByOffset(offset).Offset + 1;
        }
    }

    /// <summary>
    /// Gets the text location of this anchor.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when trying to get the Offset from a deleted anchor.</exception>
    public Location Location => document.GetLocation(Offset);

    /// <inheritdoc/>
    public override string ToString()
    {
        return "[Anchor Offset=" + Offset + "]";
    }
}

