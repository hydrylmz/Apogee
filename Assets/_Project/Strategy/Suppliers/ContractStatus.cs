namespace Apogee.Strategy
{
    /// <summary>
    /// Lifecycle status for supplier procurement contracts (GDD §9.1, §19.12).
    /// </summary>
    public enum ContractStatus
    {
        /// <summary>Contract is signed and waiting for parts delivery according to scheduled date.</summary>
        PendingDelivery = 0,

        /// <summary>Hardware delivered and inspected; fulfilled to agency inventory.</summary>
        Fulfilled = 1,

        /// <summary>Contract terminated early with cancellation penalty or fee.</summary>
        Cancelled = 2
    }
}
