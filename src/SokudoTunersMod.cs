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
            AutoShop.Update();
            ShopCutscene.Update();
            TuningMenu.Update();
            VehicleHud.Update();
            HudPrompts.Update();
            NitroFlames.Update();
            DebugTools.Update();
            ShopInspector.Update();
            ShopMakeover.Update();
        }

        public override void OnLateUpdate()
        {
            GarageCamera.LateUpdate();
            ShopCutscene.LateUpdate();
        }
    }
}
