using Stellar.WPF.Utilities;

namespace Stellar.WPF.Document;

/// <summary>
/// Contains weak event managers for document events.
/// </summary>
public static class WeakDocumentEventManager
{
	/// <summary>
	/// Weak event manager for the <see cref="TextDocument.UpdateStarted"/> event.
	/// </summary>
	public sealed class UpdateStarted : WeakEventManagerBase<UpdateStarted, Document>
	{
		/// <inheritdoc/>
		protected override void StartListening(Document source)
		{
			source.UpdateStarted += DeliverEvent;
		}

		/// <inheritdoc/>
		protected override void StopListening(Document source)
		{
			source.UpdateStarted -= DeliverEvent;
		}
	}

	/// <summary>
	/// Weak event manager for the <see cref="TextDocument.UpdateFinished"/> event.
	/// </summary>
	public sealed class UpdateFinished : WeakEventManagerBase<UpdateFinished, Document>
	{
		/// <inheritdoc/>
		protected override void StartListening(Document source)
		{
			source.UpdateFinished += DeliverEvent;
		}

		/// <inheritdoc/>
		protected override void StopListening(Document source)
		{
			source.UpdateFinished -= DeliverEvent;
		}
	}

	/// <summary>
	/// Weak event manager for the <see cref="TextDocument.Changing"/> event.
	/// </summary>
	public sealed class Changing : WeakEventManagerBase<Changing, Document>
	{
		/// <inheritdoc/>
		protected override void StartListening(Document source)
		{
			source.Changing += DeliverEvent;
		}

		/// <inheritdoc/>
		protected override void StopListening(Document source)
		{
			source.Changing -= DeliverEvent;
		}
	}

	/// <summary>
	/// Weak event manager for the <see cref="TextDocument.Changed"/> event.
	/// </summary>
	public sealed class Changed : WeakEventManagerBase<Changed, Document>
	{
		/// <inheritdoc/>
		protected override void StartListening(Document source)
		{
			source.Changed += DeliverEvent;
		}

		/// <inheritdoc/>
		protected override void StopListening(Document source)
		{
			source.Changed -= DeliverEvent;
		}
	}

	/// <summary>
	/// Weak event manager for the <see cref="TextDocument.TextChanged"/> event.
	/// </summary>
	public sealed class TextChanged : WeakEventManagerBase<TextChanged, Document>
	{
		/// <inheritdoc/>
		protected override void StartListening(Document source)
		{
			source.TextChanged += DeliverEvent;
		}

		/// <inheritdoc/>
		protected override void StopListening(Document source)
		{
			source.TextChanged -= DeliverEvent;
		}
	}
}
