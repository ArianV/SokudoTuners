using MelonLoader;

namespace SokudoTuners
{
    // Player settings, saved by MelonLoader to UserData/MelonPreferences.cfg. The tuning menu flips them,
    // and they can also be edited in that file.
    public static class ModSettings
    {
        private static MelonPreferences_Entry<bool> _useMph;

        public static void Init()
        {
            MelonPreferences_Category category = MelonPreferences.CreateCategory("SokudoTuners", "Sokudo Tuners");

            // My own setting rather than the game's unit setting, so the speedometer can be mph while
            // the rest of the game stays however the player has it.
            _useMph = category.CreateEntry("UseMph", true, "Use mph", "Show speeds in mph instead of km/h.");
        }

        // Setting it saves straight away, so the choice survives a crash too. The HUD notices the change on its own.
        public static bool UseMph
        {
            get => _useMph.Value;
            set
            {
                _useMph.Value = value;
                MelonPreferences.Save();
            }
        }

        // The game shows speed at 1.4x the real physics speed (LandVehicle.SPEED_DISPLAY_MULTIPLIER).
        // Everything I show goes through here so the HUD and the timer always agree.
        public static float DisplaySpeed(float metersPerSecond)
        {
            return metersPerSecond * 1.4f * (UseMph ? 2.23694f : 3.6f);
        }

        public static string SpeedUnit => UseMph ? "mph" : "km/h";
    }
}
