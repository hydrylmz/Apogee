namespace Apogee.Core.Audio
{
    /// <summary>
    /// Identifiers for audio sound effects and musical cues triggered across APOGEE (GDD §15.4, M8-02).
    /// Pure C# enum — no UnityEngine references.
    /// </summary>
    public enum AudioEvent
    {
        // ── UI Events ────────────────────────────────────────────────────────
        None = 0,
        ButtonClick = 1,
        ButtonHover = 2,
        TabSwitch = 3,
        ModalOpen = 4,
        ModalClose = 5,
        ErrorBuzz = 6,

        // ── Mission & Launch Events ──────────────────────────────────────────
        CountdownBeep = 10,
        RocketIgnition = 11,
        StageSeparation = 12,
        OrbitInsertion = 13,
        AbortAlarm = 14,

        // ── Operations & EDL Events ──────────────────────────────────────────
        TelemetryPing = 20,
        AtmosphericEntryRumble = 21,
        ParachuteDeployment = 22,
        RetroThrust = 23,
        LandingSuccess = 24,
        LandingCrash = 25,

        // ── Strategy & Report Events ─────────────────────────────────────────
        EventNotification = 30,
        ContractSigned = 31,
        ResearchCompleted = 32,
        MilestoneClaimed = 33,
        MissionSuccessJingle = 34,
        MissionFailureJingle = 35,
        BankruptcySiren = 36,

        // ── Science / Flight Outcomes ─────────────────────────────────────────
        ScienceSuccess = 40,
        ScienceFail = 41,
        CruiseNominalPing = 42,
        TransferInjectionBurn = 43,
        ArrivalCaptureRing = 44,

        // ── Ambient Music Cues ───────────────────────────────────────────────
        MusicMainMenu = 50,
        MusicStrategyAmbient = 51,
        MusicWorkshopCad = 52,
        MusicLaunchTension = 53,
        MusicCruiseAmbient = 54,
        MusicEdlTension = 55,
        MusicVictoryTheme = 56,
        MusicFailureStinger = 57
    }

    /// <summary>
    /// Audio channel categorization for independent volume mixing (GDD §14.1 #19, §15.4).
    /// </summary>
    public enum AudioChannel
    {
        Master = 0,
        Music = 1,
        Sfx = 2,
        Ambience = 3,
        Ui = 4
    }
}
