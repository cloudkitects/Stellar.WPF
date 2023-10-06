using System;
using System.Collections.Generic;

namespace Stellar.WPF.Utilities;

/// <summary>
/// A simple event queue.
/// </summary>
internal sealed class EventQueue
{
    private readonly struct Event
    {
        private readonly EventHandler handler;
        private readonly object sender;
        private readonly EventArgs e;

        public Event(EventHandler handler, object sender, EventArgs e)
        {
            this.handler = handler;
            this.sender = sender;
            this.e = e;
        }

        public void Raise()
        {
            handler(sender, e);
        }
    }

    private readonly Queue<Event> eventQueue = new();

    public void Enqueue(EventHandler handler, object sender, EventArgs e)
    {
        if (handler != null)
        {
            eventQueue.Enqueue(new Event(handler, sender, e));
        }
    }

    public void Flush()
    {
        while (eventQueue.Count > 0)
        {
            eventQueue.Dequeue().Raise();
        }
    }
}
