using System;

namespace Apogee.Core
{
    /// <summary>
    /// Department roles with authorization masks for commands and proposals.
    /// In single-player, the user acts with All roles. In multiplayer, each seat holds specific role(s).
    /// </summary>
    [Flags]
    public enum PlayerRole
    {
        None = 0,
        AgencyDirector = 1 << 0,       // Agency Director: Budget allocation, launch approvals, macro campaign
        LeadEngineer = 1 << 1,         // Lead Engineer: Rocket and spacecraft blueprint design
        ProcurementDirector = 1 << 2,  // Procurement & Production Director: Supplier contracts, manufacturing queue
        ScienceDirector = 1 << 3,      // Science & Operations Director: Objectives, scientific instruments, station links

        All = AgencyDirector | LeadEngineer | ProcurementDirector | ScienceDirector
    }
}
