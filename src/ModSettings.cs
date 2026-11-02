using MelonLoader;

namespace SokudoTuners
{
    // Saved to UserData/MelonPreferences.cfg.
    public static class ModSettings
    {
        private static MelonPreferences_Entry<bool> _useMph;

        public static void Init()
        {
            MelonPreferences_Category category = MelonPreferences.CreateCategory("SokudoTuners", "Sokudo Tuners");

            // Separate from the game's unit setting on purpose.
            _useMph = category.CreateEntry("UseMph", true, "Use mph", "Show speeds in mph instead of km/h.");
        }

        public static bool UseMph
        {
            get => _useMph.Value;
            set
            {
                _useMph.Value = value;
                MelonPreferences.Save();
            }
        }

        // The game shows speed at 1.4x the physics speed (LandVehicle.SPEED_DISPLAY_MULTIPLIER), so I match it.
        public static float DisplaySpeed(float metersPerSecond)
        {
            return metersPerSecond * 1.4f * (UseMph ? 2.23694f : 3.6f);
        }

        public static string SpeedUnit => UseMph ? "mph" : "km/h";
    }
}
