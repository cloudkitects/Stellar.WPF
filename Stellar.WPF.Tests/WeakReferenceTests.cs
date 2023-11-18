using System.Runtime.CompilerServices;
using System.Windows.Threading;

using Stellar.WPF.Editing;
using Stellar.WPF.Rendering;

namespace Stellar.WPF.Tests;

public class WeakReferenceTests
{
    #region helpers
    private static void CollectGarbage()
    {
        for (var i = 0; i < 3; i++)
        {
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);

            // pump WPF messages so that WeakEventManager can unregister
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { }));
        }
    }

    // Use separate no-inline method so that the JIT can't keep a strong
    // reference to the text view alive past this method.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference TextViewWeakReference(WPF.Document.Document document = null!) => new(document is null
            ? new TextView()
            : new TextView { Document = document });

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference TextAreaWeakReference(WPF.Document.Document document = null!) => new(document is null
        ? new TextArea()
        : new TextArea { Document = document });

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference TextEditorWeakReference(WPF.Document.Document document = null!) => new(document is null
        ? new TextEditor()
        : new TextEditor { Document = document });

    // using a method to ensure the local variables can be garbage collected after the method returns
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference MarginWeakReference(WPF.Document.Document Document)
    {
        var textView = new TextView() { Document = Document };

        _ = new LineNumberMargin() { TextView = textView };

        return new WeakReference(textView);
    }
    #endregion

    [WpfFact]
    public void TextViewIsCollected()
    {
        var textView = TextViewWeakReference();

        CollectGarbage();

        Assert.False(textView.IsAlive);
    }

    [WpfFact]
    public void TextViewAndLineTrackersAreCollected()
    {
        var document = new WPF.Document.Document();

        Assert.Equal(0, document.LineTrackers.Count);

        var textView = TextViewWeakReference(document);

        Assert.Equal(1, document.LineTrackers.Count);

        CollectGarbage();

        Assert.False(textView.IsAlive);

        // document will not immediately clear line trackers...
        Assert.Equal(1, document.LineTrackers.Count);

        // ...but should clear them on the next change.
        document.Insert(0, "a");

        Assert.Equal(0, document.LineTrackers.Count);
    }

    [WpfFact]
    public void TextAreaIsCollected()
    {
        var document = new WPF.Document.Document();
        var textArea = TextAreaWeakReference(document);

        Assert.True(textArea.IsAlive);

        CollectGarbage();

        Assert.False(textArea.IsAlive);

        // the document should still be around
        GC.KeepAlive(document);
    }

    [WpfFact]
    public void TextEditorIsCollected()
    {
        var document = new WPF.Document.Document();
        var textEditor = TextEditorWeakReference(document);

        CollectGarbage();
        
        Assert.False(textEditor.IsAlive);
        
        GC.KeepAlive(document);
    }

    [WpfFact]
    public void MarginIsCollected()
    {
        var document = new WPF.Document.Document();

        var margin = MarginWeakReference(document);

        CollectGarbage();
        
        Assert.False(margin.IsAlive);

        GC.KeepAlive(document);
    }
}
