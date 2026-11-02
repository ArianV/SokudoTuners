using System;
using System.IO;
using HarmonyLib;
using MelonLoader;
using ScheduleOne.DevUtilities;
using ScheduleOne.Persistence;
using ScheduleOne.Vehicles;

namespace SokudoTuners
{
    // Keeps each car's upgrades in the save, in a small text file of my own next to the game's files.
    //
    // At the end of every save the game deletes any file in the save folder it doesn't recognise
    // (ClearBaseLevelOutdatedSaves). So right before that sweep I write my file and add its name to the game's
    // list of approved files. That also means my file is written during the game's own save, so upgrades are
    // saved exactly when the game is, and quitting without saving loses them the same way it loses everything else.
    //
    // A separate file instead of squeezing into the game's vehicle data means uninstalling the mod can't break a save.
    [HarmonyPatch(typeof(SaveManager), "ClearBaseLevelOutdatedSaves")]
    public static class UpgradeSaves
    {
        private const string FileName = "SokudoTuners.txt";

        private static bool _subscribed;

        private static void Prefix(SaveManager __instance, string saveFolderPath)
        {
            try
            {
                File.WriteAllText(Path.Combine(saveFolderPath, FileName), Upgrades.Serialize());
                __instance.ApprovedBaseLevelPaths.Add(FileName);
            }
            catch (Exception ex)
            {
                Melon<SokudoTunersMod>.Logger.Error($"Couldn't save upgrades: {ex}");
            }
        }

        // LoadManager lives for the whole session, so I only need to hook its load event once.
        // Called on every scene load until it finds the manager.
        public static void SubscribeToLoads()
        {
            if (_subscribed || !Singleton<LoadManager>.InstanceExists)
                return;

            Singleton<LoadManager>.Instance.onLoadComplete.AddListener(OnLoadComplete);
            _subscribed = true;
        }

        private static void OnLoadComplete()
        {
            // Start clean every time, otherwise loading a second save would carry over the first one's upgrades.
            Upgrades.Clear();

            string path = Path.Combine(Singleton<LoadManager>.Instance.LoadedGameFolderPath, FileName);
            if (File.Exists(path))
            {
                try
                {
                    int cars = Upgrades.Deserialize(File.ReadAllText(path));
                    Melon<SokudoTunersMod>.Logger.Msg($"Loaded upgrades for {cars} car(s).");
                }
                catch (Exception ex)
                {
                    Melon<SokudoTunersMod>.Logger.Error($"Couldn't load upgrades from {path}: {ex}");
                }
            }

            // Engine, tires, brakes and nitro read their tier every physics step, so they just work.
            // Suspension is set once, so the cars need it applied now.
            if (!NetworkSingleton<VehicleManager>.InstanceExists)
                return;
            foreach (LandVehicle vehicle in NetworkSingleton<VehicleManager>.Instance.AllVehicles)
                if (vehicle != null && Upgrades.GetTier(vehicle, UpgradeType.Suspension) > 0)
                    SuspensionTuner.Apply(vehicle);
        }
    }
}
