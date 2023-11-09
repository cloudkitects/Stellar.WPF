using System;
using System.Threading;

namespace Stellar.WPF.Utilities
{
    /// <summary>
    /// A first-come, first-served disposable, ensuring the passed-in
    /// action is invoked at most once despite Dispose being called by
    /// multiple threads,
    /// </summary>
    internal sealed class OnDisposeCall : IDisposable
    {
        private Action action;

        public OnDisposeCall(Action action)
        {
            this.action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public void Dispose()
        {
            var a = Interlocked.Exchange(ref action!, null);

            if (a is not null)
            {
                a();
            }
        }
    }
}
