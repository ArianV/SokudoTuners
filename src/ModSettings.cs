using MelonLoader;

namespace SokudoTuners
{
    // Saved to UserData/MelonPreferences.cfg.
    public static class ModSettings
    {
        private static MelonPreferences_Entry<bool> _useMph;
        private static MelonPreferences_Entry<bool> _debugLogging;

        public static void Init()
        {
            MelonPreferences_Category category = MelonPreferences.CreateCategory("SokudoTuners", "Sokudo Tuners");

            // Separate from the game's unit setting on purpose.
            _useMph = category.CreateEntry("UseMph", true, "Use mph", "Show speeds in mph instead of km/h.");
            _debugLogging = category.CreateEntry("DebugLogging", false, "Debug logging",
                "Log 0-60 / 60-0 times and per-car stats to the console.");
        }

        public static bool DebugLoggingEnabled => _debugLogging.Value;

        public static void LogDebug(string message)
        {
            if (_debugLogging.Value)
                Melon<SokudoTunersMod>.Logger.Msg(message);
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
