using System;
using System.Collections.Generic;
using Apogee.Core.Simulation.Cruise;
using Apogee.Design;

namespace Apogee.Strategy.Mission
{
    /// <summary>
    /// Runtime instance of a campaign mission.
    /// Holds design reference, lifecycle status, accumulated cruise data, and pending decisions.
    /// All mutations occur through commands; this class is a pure data record.
    ///
    /// GDD §19.12 (Mission Instance schema), §10.1, §10.2, §6.2 (map progress marker).
    /// </summary>
    public sealed class MissionInstance
    {
        // ---------------------------------------------------------------- identity
        /// <summary>Unique mission ID within the campaign.</summary>
        public string MissionId { get; }

        /// <summary>Player-given mission name.</summary>
        public string MissionName { get; set; }

        /// <summary>Destination catalogue ID (e.g. "moon", "mars").</summary>
        public string DestinationId { get; }

        // ---------------------------------------------------------------- lifecycle
        /// <summary>Current lifecycle phase of this mission.</summary>
        public MissionStatus Status { get; set; }

        // ---------------------------------------------------------------- design
        /// <summary>
        /// The blueprint used for this mission.
        /// Snapshot is taken at launch; not the live active blueprint.
        /// </summary>
        public Blueprint Blueprint { get; }

        // ---------------------------------------------------------------- cruise progress (GDD §6.2 map marker)
        /// <summary>
        /// Cruise progress [0.0 – 1.0].
        /// 0 = just inserted into cruise orbit / transfer, 1 = destination reached.
        /// Used by the Solar System map to position the spacecraft marker.
        /// </summary>
        public double CruiseProgress { get; set; }

        /// <summary>Total planned cruise duration in game-days.</summary>
        public int CruiseDurationDays { get; }

        /// <summary>Accumulated reliability degradation from cruise events [0.0 – 1.0].</summary>
        public double AccumulatedReliabilityPenalty { get; set; }

        /// <summary>Accumulated correction-burn fuel fraction consumed during cruise.</summary>
        public double AccumulatedCorrectionFuel { get; set; }

        // ---------------------------------------------------------------- cruise events
        /// <summary>
        /// Segment events resolved so far (append-only log for cause-chain reporting).
        /// </summary>
        public List<CruiseSegmentEvent> CruiseEventLog { get; } = new List<CruiseSegmentEvent>();

        /// <summary>
        /// Decision cards that have been presented to the player and their resolved option IDs.
        /// Key = DecisionId, Value = chosen OptionId.
        /// </summary>
        public Dictionary<string, string> ResolvedDecisions { get; } =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Decision card currently awaiting player input (null if none pending).
        /// Set by AdvanceCruiseCommand when a DecisionRequired segment is reached.
        /// Cleared by ResolveCruiseDecisionCommand.
        /// </summary>
        public CruiseDecisionCard PendingDecisionCard { get; set; }

        // ---------------------------------------------------------------- dates
        /// <summary>Game tick at which the launch was executed.</summary>
        public int LaunchTick { get; set; }

        /// <summary>Game tick at which the cruise phase started.</summary>
        public int CruiseStartTick { get; set; }

        // ---------------------------------------------------------------- seed
        /// <summary>Deterministic seed for this mission's simulation stream.</summary>
        public ulong MissionSeed { get; }

        // ---------------------------------------------------------------- construction
        public MissionInstance(
            string missionId,
            string missionName,
            string destinationId,
            Blueprint blueprint,
            int cruiseDurationDays,
            ulong missionSeed)
        {
            MissionId          = missionId ?? throw new ArgumentNullException(nameof(missionId));
            MissionName        = missionName ?? string.Empty;
            DestinationId      = destinationId ?? string.Empty;
            Blueprint          = blueprint;
            CruiseDurationDays = cruiseDurationDays < 1 ? 1 : cruiseDurationDays;
            MissionSeed        = missionSeed;
            Status             = MissionStatus.Planning;
        }

        public override string ToString() =>
            $"[Mission:{MissionId}] \"{MissionName}\" → {DestinationId}, Status={Status}, Progress={CruiseProgress:P0}";
    }
}
