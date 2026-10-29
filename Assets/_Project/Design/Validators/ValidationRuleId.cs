namespace Apogee.Design
{
    /// <summary>
    /// Well-known identifier constants for design validation rules.
    /// Corresponds to the 9 core checks specified in GDD §7.4, §8.1, and §21 (M1-07, M1-08).
    /// </summary>
    public static class ValidationRuleId
    {
        /// <summary>Menzil / Delta-V requirement check.</summary>
        public const string DeltaV = "VAL_DELTAV";

        /// <summary>Kütle / Kapasite (wet mass vs pad capacity) check.</summary>
        public const string MassCapacity = "VAL_MASS_CAPACITY";

        /// <summary>Rampa Uyumu (stage count &amp; launcher class vs pad level) check.</summary>
        public const string LaunchPad = "VAL_LAUNCH_PAD";

        /// <summary>Araştırma Kilidi (unresearched components or materials) check.</summary>
        public const string ResearchLock = "VAL_RESEARCH_LOCK";

        /// <summary>Güç (power generation vs consumption and margin) check.</summary>
        public const string Power = "VAL_POWER";

        /// <summary>Isı (thermal dissipation vs load and margin) check.</summary>
        public const string Thermal = "VAL_THERMAL";

        /// <summary>Zorunlu Modül (bus, engine, EDL, life support, mission categories) check.</summary>
        public const string MandatoryModules = "VAL_MANDATORY_MODULES";

        /// <summary>Güvenilirlik Eşiği (vehicle aggregate reliability vs mission threshold) check.</summary>
        public const string Reliability = "VAL_RELIABILITY";

        /// <summary>
        /// Fırlatıcı Kapasite (payload mass vs computed max deliverable payload) check.
        /// GDD §8.1 M1-08.
        /// </summary>
        public const string PayloadCapacity = "VAL_PAYLOAD_CAPACITY";
    }
}
