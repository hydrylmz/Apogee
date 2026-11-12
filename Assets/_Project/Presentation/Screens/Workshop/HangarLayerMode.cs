namespace Apogee.UI.Screens.Workshop
{
    /// <summary>
    /// Visualization layers for the 2D rocket hangar view (GDD §7.2, §14.3, §15.1).
    /// Continuous blending or tab toggling between:
    /// Exterior (Dış Görünüm), Cutaway (Kesit), Subsystems (İç Sistemler), and Schematic (Şematik).
    /// </summary>
    public enum HangarLayerMode
    {
        Exterior = 0,
        Cutaway = 1,
        Subsystems = 2,
        Schematic = 3
    }
}
