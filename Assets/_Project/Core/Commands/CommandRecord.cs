using System;

namespace Apogee.Core
{
    /// <summary>
    /// Audit and replication log entry for an executed command.
    /// Complies with GDD §19.14 (id, type, roles, sequence, actor, tick, parameters).
    /// </summary>
    [Serializable]
    public sealed class CommandRecord
    {
        public ulong SequenceNumber { get; set; }
        public string CommandId { get; set; }
        public string CommandType { get; set; }
        public PlayerRole RequiredRoles { get; set; }
        public PlayerRole ActorRole { get; set; }
        public string PlayerId { get; set; }
        public long Tick { get; set; }
        public string PayloadJson { get; set; }

        public CommandRecord()
        {
        }

        public CommandRecord(
            ulong sequenceNumber,
            string commandId,
            string commandType,
            PlayerRole requiredRoles,
            PlayerRole actorRole,
            string playerId,
            long tick,
            string payloadJson = null)
        {
            SequenceNumber = sequenceNumber;
            CommandId = commandId;
            CommandType = commandType;
            RequiredRoles = requiredRoles;
            ActorRole = actorRole;
            PlayerId = playerId;
            Tick = tick;
            PayloadJson = payloadJson;
        }

        public override string ToString()
        {
            return $"#{SequenceNumber} [{CommandType}] id={CommandId} actor={PlayerId} role={ActorRole} tick={Tick}";
        }
    }
}
