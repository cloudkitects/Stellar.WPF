using System;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows;

namespace Stellar.WPF.Rendering;

/// <summary>
/// Encapsulates and adds MouseHover support to UIElements.
/// </summary>
/// <remarks>
/// Handlers do not set e.Handled to allow others to also handle mouse movement.
/// </remarks>
public class MouseHover : IDisposable
{
    private readonly UIElement target;
    private DispatcherTimer? timer;
    private Point startPoint;
    private MouseEventArgs? lastEventArgs;
    private bool isHovering;
    private bool isDisposed;
    
    /// <summary>
    /// Occurs when the mouse starts hovering over a certain location.
    /// </summary>
    public event EventHandler<MouseEventArgs>? Hover;

    /// <summary>
    /// Occurs when the mouse stops hovering over a certain location.
    /// </summary>
    public event EventHandler<MouseEventArgs>? Stopped;

    /// <summary>
    /// Creates a new instance and attaches itself to the <paramref name="target" /> UIElement.
    /// </summary>
    public MouseHover(UIElement target)
    {
        this.target = target ?? throw new ArgumentNullException(nameof(target));
        this.target.MouseLeave += MouseLeave;
        this.target.MouseMove += MouseMove;
        this.target.MouseEnter += MouseEnter;
    }

    private void MouseMove(object sender, MouseEventArgs e)
    {
        var mouseMovement = startPoint - e.GetPosition(target);

        if (Math.Abs(mouseMovement.X) > SystemParameters.MouseHoverWidth ||
            Math.Abs(mouseMovement.Y) > SystemParameters.MouseHoverHeight)
        {
            Start(e);
        }
    }

    private void MouseEnter(object sender, MouseEventArgs e)
    {
        Start(e);
    }

    private void Start(MouseEventArgs e)
    {
        Stop();
        startPoint = e.GetPosition(target);
        lastEventArgs = e;
        timer = new DispatcherTimer(SystemParameters.MouseHoverTime, DispatcherPriority.Background, OnTimerElapsed, target.Dispatcher);
        timer.Start();
    }

    private void MouseLeave(object sender, MouseEventArgs e)
    {
        Stop();
    }

    private void Stop()
    {
        if (timer is not null)
        {
            timer.Stop();
            
            timer = null!;
        }
        
        if (isHovering)
        {
            isHovering = false;
            
            OnMouseHoverStopped(lastEventArgs!);
        }
    }

    private void OnTimerElapsed(object sender, EventArgs e)
    {
        timer?.Stop();
        timer = null;

        isHovering = true;
        
        OnMouseHover(lastEventArgs!);
    }

    /// <summary>
    /// Raises the <see cref="MouseHover"/> event.
    /// </summary>
    protected virtual void OnMouseHover(MouseEventArgs e)
    {
        Hover?.Invoke(this, e);
    }

    /// <summary>
    /// Raises the <see cref="Stopped"/> event.
    /// </summary>
    protected virtual void OnMouseHoverStopped(MouseEventArgs e)
    {
        Stopped?.Invoke(this, e);
    }

    /// <summary>
    /// Removes the MouseHover support from the target UIElement.
    /// </summary>
    public void Dispose()
    {
        if (!isDisposed)
        {
            target.MouseLeave -= MouseLeave;
            target.MouseMove -= MouseMove;
            target.MouseEnter -= MouseEnter;
        }

        isDisposed = true;

        GC.SuppressFinalize(this);
    }
}