using VoxelTycoon;
using VoxelTycoon.Game.UI;
using VoxelTycoon.Localization;
using VoxelTycoon.Modding;

namespace AdvancedPathfinder.UI;

public class ModSettings : SettingsMod
{
    public static readonly Setting<bool> HighlightTrainPaths = new(true);
    public static readonly Setting<bool> HighlightAllTrainPaths = new(false);
    public static readonly Setting<bool> HighlightReservedPaths = new(true);
    public static readonly Setting<bool> HighlightReservedPathsExtended = new(false);
    public static readonly Setting<bool> DebugPathfinderStats = new(false);
    
    protected override void SetupSettingsControl(SettingsControl settingsControl, WorldSettings worldSettings)
    {
        var locale = LazyManager<LocaleManager>.Current.Locale;

        settingsControl.AddToggle(locale.GetString("advanced_pathfinder_mod/highlight_train_path"), null, HighlightTrainPaths);
        
        settingsControl.AddToggle(locale.GetString("advanced_pathfinder_mod/highlight_all_train_path"), 
            locale.GetString("advanced_pathfinder_mod/highlight_all_train_path_notice"), HighlightAllTrainPaths);
        
        settingsControl.AddToggle(locale.GetString("advanced_pathfinder_mod/highlight_reserved_path"), null, HighlightReservedPaths);
        
        settingsControl.AddToggle(locale.GetString("advanced_pathfinder_mod/highlight_reserved_path_extended"), null, HighlightReservedPathsExtended);
        
        // settingsControl.AddToggle("Debug pathfinder stats", null, DebugPathfinderStats);
    }
}