using MelonLoader;

[assembly: MelonInfo(typeof(CarTuning.CarTuningMod), "Car Tuning", "0.1.0", "Arian Vahdat")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace CarTuning
{
    public class CarTuningMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Car Tuning loaded.");
        }

        // Logging scene changes for now so I can see when the actual game world (not the menu) is up.
        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            LoggerInstance.Msg($"Scene loaded: {sceneName} ({buildIndex})");
        }
    }
}
