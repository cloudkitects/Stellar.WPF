namespace Stellar.WPF.Utilities
{
    /// <summary>
    /// Immutable (thread-safe) contract.
    /// </summary>
    internal interface IFreezable
    {
        /// <summary>
        /// Whether this instance is frozen
        /// </summary>
        bool IsFrozen { get; }

        /// <summary>
        /// Freezes this instance.
        /// </summary>
        void Freeze();
    }
}
