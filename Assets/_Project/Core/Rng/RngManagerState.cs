using System;
using System.Collections.Generic;

namespace Apogee.Core
{
    /// <summary>
    /// Snapshot of the complete RNG state across all managed streams.
    /// Can be serialized into CampaignState for game saves.
    /// </summary>
    [Serializable]
    public class RngManagerState
    {
        public ulong MasterSeed;
        public List<StreamStateEntry> StreamStates = new List<StreamStateEntry>();

        public RngManagerState()
        {
        }

        public RngManagerState(ulong masterSeed)
        {
            MasterSeed = masterSeed;
        }

        [Serializable]
        public struct StreamStateEntry
        {
            public RngStreamType StreamType;
            public RngState State;

            public StreamStateEntry(RngStreamType streamType, RngState state)
            {
                StreamType = streamType;
                State = state;
            }
        }
    }
}
