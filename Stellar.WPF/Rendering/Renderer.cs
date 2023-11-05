using System;
using System.Collections.Generic;

namespace Stellar.WPF.Rendering;

/// <summary>
/// Base class for splitting visual elements so that styles can be easily assigned
/// to individual words/characters.
/// </summary>
public abstract class Renderer : IRenderer, ITextViewConnector
{
    /// <summary>
    /// Gets the list of elements currently being transformed.
    /// </summary>
    protected IList<VisualLineElement>? CurrentElements { get; private set; }

    /// <inheritdoc/>
    public void Initialize(ITextRunContext context, IList<VisualLineElement> elements)
    {
        if (CurrentElements is not null)
        {
            throw new InvalidOperationException("Recursive Render() call");
        }

        CurrentElements = elements ?? throw new ArgumentNullException(nameof(elements));
        
        try
        {
            Render(context);
        }
        finally
        {
            CurrentElements = null;
        }
    }

    /// <summary>
    /// Context-based rendering, e.g., not necessarily the current elements.
    /// </summary>
    protected abstract void Render(ITextRunContext context);

    /// <summary>
    /// Render current elements in a visual column range.
    /// </summary>
    /// <remarks>
    /// This method should only be called during a <see cref="Render"/> call since it manipulates <see cref="CurrentElements"/>.
    /// It will try splitting elements as needed to render a subrange by  setting <see cref="VisualLineElement.TextRunProperties"/>
    /// on whole elements and calling the <paramref name="action"/> on all elements in the range.
    /// Note the visual column range is a 2D region when current elements span across multiple lines.
    /// </remarks>
    /// <param name="visualStartColumn">Start visual column of the range to change.</param>
    /// <param name="visualEndColumn">End visual column of the range to change.</param>
    /// <param name="action">Action that renders (styles) an individual <see cref="VisualLineElement"/>.</param>
    protected void Render(int visualStartColumn, int visualEndColumn, Action<VisualLineElement> action)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        for (var i = 0; i < CurrentElements!.Count; i++)
        {
            var element = CurrentElements[i];

            if (element.VisualColumn > visualEndColumn)
            {
                break;
            }

            if (element.VisualColumn < visualStartColumn &&
                element.VisualColumn + element.VisualLength > visualStartColumn)
            {
                if (element.CanSplit)
                {
                    element.Split(visualStartColumn, CurrentElements, i--);
                    continue;
                }
            }
            if (element.VisualColumn >= visualStartColumn && element.VisualColumn < visualEndColumn)
            {
                if (element.VisualColumn + element.VisualLength > visualEndColumn)
                {
                    if (element.CanSplit)
                    {
                        element.Split(visualEndColumn, CurrentElements, i--);
                        continue;
                    }
                }
                else
                {
                    action(element);
                }
            }
        }
    }

    /// <summary>
    /// Called when added to a text view.
    /// </summary>
    protected virtual void OnAttachTo(TextView textView)
    {
    }

    /// <summary>
    /// Called when removed from a text view.
    /// </summary>
    protected virtual void OnDetachFrom(TextView textView)
    {
    }

    void ITextViewConnector.AttachTo(TextView textView)
    {
        OnAttachTo(textView);
    }

    void ITextViewConnector.DetachFrom(TextView textView)
    {
        OnDetachFrom(textView);
    }
}