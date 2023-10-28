using System.ComponentModel;

namespace Stellar.WPF.Utilities;

/// <summary>
/// WeakEventManager for INotifyPropertyChanged.PropertyChanged.
/// </summary>
public sealed class PropertyChangedWeakEventManager : WeakEventManagerBase<PropertyChangedWeakEventManager, INotifyPropertyChanged>
{
    /// <inheritdoc/>
    protected override void StartListening(INotifyPropertyChanged source)
    {
        source.PropertyChanged += DeliverEvent;
    }

    /// <inheritdoc/>
    protected override void StopListening(INotifyPropertyChanged source)
    {
        source.PropertyChanged -= DeliverEvent;
    }
}
