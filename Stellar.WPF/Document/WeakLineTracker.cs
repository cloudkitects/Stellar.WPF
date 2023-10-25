using System;

namespace Stellar.WPF.Document;

/// <summary>
/// Registers a line tracker on a document using a weak reference.
/// </summary>
public sealed class WeakLineTracker : ILineTracker
{
    #region fields and props
    private Document? document;
    private readonly WeakReference tracker;
    #endregion

    #region constructors
    private WeakLineTracker(Document document, ILineTracker tracker)
    {
        this.document = document;
        this.tracker = new WeakReference(tracker);
    }
    #endregion

    #region methods
    /// <summary>
    /// Registers the <paramref name="tracker"/> as line tracker for the <paramref name="document"/>.
    /// A weak reference to the target tracker will be used, and the WeakLineTracker will unregister itself
    /// when the target tracker is garbage collected.
    /// </summary>
    public static WeakLineTracker Register(Document document, ILineTracker tracker)
    {
        var _tracker = new WeakLineTracker(
            document ?? throw new ArgumentNullException(nameof(document)),
            tracker ?? throw new ArgumentNullException(nameof(tracker)));
        
        document.LineTrackers.Add(_tracker);
        
        return _tracker;
    }

    /// <summary>
    /// Unregister this weak line tracker from the document.
    /// </summary>
    public void Unregister()
    {
        if (document is not null)
        {
            document.LineTrackers.Remove(this);
            document = null;
        }
    }
    #endregion

    #region ILineTracker
    void ILineTracker.BeforeRemoving(Line line)
    {
        if (tracker.Target is ILineTracker _tracker)
        {
            _tracker.BeforeRemoving(line);
        }
        else
        {
            Unregister();
        }
    }

    void ILineTracker.ResetLength(Line line, int newLength)
    {
        if (tracker.Target is ILineTracker _tracker)
        {
            _tracker.ResetLength(line, newLength);
        }
        else
        {
            Unregister();
        }
    }

    void ILineTracker.AfterInserting(Line line, Line newLine)
    {
        if (tracker.Target is ILineTracker _tracker)
        {
            _tracker.AfterInserting(line, newLine);
        }
        else
        {
            Unregister();
        }
    }

    void ILineTracker.Rebuild()
    {
        if (tracker.Target is ILineTracker _tracker)
        {
            _tracker.Rebuild();
        }
        else
        {
            Unregister();
        }
    }

    void ILineTracker.AfterChange(DocumentChangeEventArgs e)
    {
        if (tracker.Target is ILineTracker _tracker)
        {
            _tracker.AfterChange(e);
        }
        else
        {
            Unregister();
        }
    }
    #endregion
}