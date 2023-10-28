using System;
using System.Diagnostics;
using System.Windows;

namespace Stellar.WPF.Utilities;

/// <summary>
/// WeakEventManager DRY wrapper.
/// </summary>
public abstract class WeakEventManagerBase<TManager, TEventSource> : WeakEventManager
	where TManager : WeakEventManagerBase<TManager, TEventSource>, new()
	where TEventSource : class
{
	/// <summary>
	/// Creates a new WeakEventManagerBase instance.
	/// </summary>
	protected WeakEventManagerBase()
	{
		Debug.Assert(GetType() == typeof(TManager));
	}

    /// <summary>
    /// Adds a weak event listener.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1000:DoNotDeclareStaticMembersOnGenericTypes")]
    public static void AddListener(TEventSource source, IWeakEventListener listener) => CurrentManager.ProtectedAddListener(source, listener);

    /// <summary>
    /// Removes a weak event listener.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1000:DoNotDeclareStaticMembersOnGenericTypes")]
    public static void RemoveListener(TEventSource source, IWeakEventListener listener) => CurrentManager.ProtectedRemoveListener(source, listener);

    /// <inheritdoc/>
    protected sealed override void StartListening(object source) => StartListening((TEventSource)(source ?? throw new ArgumentNullException(nameof(source))));

    /// <inheritdoc/>
    protected sealed override void StopListening(object source) => StopListening((TEventSource)(source ?? throw new ArgumentNullException(nameof(source))));

    /// <summary>
    /// Attaches the event handler.
    /// </summary>
    protected abstract void StartListening(TEventSource source);

	/// <summary>
	/// Detaches the event handler.
	/// </summary>
	protected abstract void StopListening(TEventSource source);

	/// <summary>
	/// Gets/Sets the current manager.
	/// </summary>
	protected static TManager CurrentManager
	{
		get {
			var managerType = typeof(TManager);
			
			var manager = (TManager)GetCurrentManager(managerType);
			
			if (manager is null)
			{
				manager = new TManager();
				
				SetCurrentManager(managerType, manager);
			}

			return manager;
		}
	}
}
