using System;

namespace Apogee.Core
{
    /// <summary>
    /// Context in which a command is validated, executed, or undone.
    /// Provides actor metadata, current simulation tick, and the mutable target state reference.
    /// </summary>
    public sealed class CommandContext
    {
        public string PlayerId { get; }
        public PlayerRole ActorRole { get; }
        public long Tick { get; }
        public object State { get; set; }

        public CommandContext(string playerId, PlayerRole actorRole, long tick = 0, object state = null)
        {
            PlayerId = playerId ?? "local";
            ActorRole = actorRole;
            Tick = tick;
            State = state;
        }

        /// <summary>
        /// Convenience method to retrieve typed state (e.g. CampaignState).
        /// </summary>
        public T GetState<T>() where T : class
        {
            return State as T;
        }

        /// <summary>
        /// Creates a context with a different actor role while keeping the same player/tick/state.
        /// </summary>
        public CommandContext WithRole(PlayerRole role)
        {
            return new CommandContext(PlayerId, role, Tick, State);
        }

        /// <summary>
        /// Creates a default local admin context for single player or testing.
        /// </summary>
        public static CommandContext CreateLocalAdmin(object state = null, long tick = 0)
        {
            return new CommandContext("local-player", PlayerRole.All, tick, state);
        }
    }
}
