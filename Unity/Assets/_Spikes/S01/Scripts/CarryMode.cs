namespace ParkRangers.Spikes.S01
{
    public enum CarryMode
    {
        /// <summary>Host simulates; carrier sends input.</summary>
        A_HostSim,
        /// <summary>Ownership moves to the carrier; returns to host at rest.</summary>
        B_Ownership,
        /// <summary>Kinematic, parented to the carrier; host simulates after release.</summary>
        C_Kinematic,
    }
}
