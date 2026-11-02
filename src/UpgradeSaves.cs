using System;
using System.IO;
using HarmonyLib;
using MelonLoader;
using ScheduleOne.DevUtilities;
using ScheduleOne.Persistence;
using ScheduleOne.Vehicles;

namespace SokudoTuners
{
    // Upgrades live in their own file in the save folder, so removing the mod can't break a save.
    // The game deletes unknown files at the end of every save, so I write mine right before that and add it to the approved list.
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

        public static void SubscribeToLoads()
        {
            if (_subscribed || !Singleton<LoadManager>.InstanceExists)
                return;

            Singleton<LoadManager>.Instance.onLoadComplete.AddListener(OnLoadComplete);
            _subscribed = true;
        }

        private static void OnLoadComplete()
        {
            // Don't carry upgrades over from another save.
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

            // Suspension is the only upgrade that's set once instead of every step.
            if (!NetworkSingleton<VehicleManager>.InstanceExists)
                return;
            foreach (LandVehicle vehicle in NetworkSingleton<VehicleManager>.Instance.AllVehicles)
                if (vehicle != null && Upgrades.GetTier(vehicle, UpgradeType.Suspension) > 0)
                    SuspensionTuner.Apply(vehicle);
        }
    }
}
