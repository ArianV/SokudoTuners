using MelonLoader;

[assembly: MelonInfo(typeof(SokudoTuners.SokudoTunersMod), "Sokudo Tuners", "0.1.0", "Arian Vahdat")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace SokudoTuners
{
    public class SokudoTunersMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Sokudo Tuners loaded.");
        }

        // Logging scene changes for now so I can see when the actual game world (not the menu) is up.
        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            LoggerInstance.Msg($"Scene loaded: {sceneName} ({buildIndex})");
        }

        public override void OnUpdate()
        {
            VehicleHud.Update();
            NitroFlames.Update();
        }
    }
}
