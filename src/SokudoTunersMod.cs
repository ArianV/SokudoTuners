using MelonLoader;

[assembly: MelonInfo(typeof(SokudoTuners.SokudoTunersMod), "Sokudo Tuners", "1.0.0", "Arian Vahdat")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace SokudoTuners
{
    public class SokudoTunersMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            ModSettings.Init();
            Upgrades.TierChanged += SuspensionTuner.OnTierChanged;
            LoggerInstance.Msg("Sokudo Tuners loaded.");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            UpgradeSaves.SubscribeToLoads();
        }

        public override void OnUpdate()
        {
            TuningMenu.Update();
            VehicleHud.Update();
            NitroFlames.Update();
            DebugTools.Update();
        }

        public override void OnLateUpdate()
        {
            GarageCamera.LateUpdate();
        }
    }
}
