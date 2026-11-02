using System;
using System.Collections.Generic;
using ScheduleOne.Vehicles;

namespace SokudoTuners
{
    public enum UpgradeType
    {
        Engine,
        Tires,
        Suspension,
        Brakes,
        Nitro
    }

    // Which tier each car has in each category. Tier 0 is stock.
    // Upgrades are multipliers on the car's own prefab values, so a tuned van stays slower than a
    // tuned Cheetah but both feel the same step up.
    public static class Upgrades
    {
        public const int MaxTier = 3;

        private static readonly string[] TierNames = { "Stock", "Tier 1", "Tier 2", "Tier 3" };

        // Torque multiplier per engine tier. More torque means harder acceleration and a higher top speed,
        // since nothing in the game caps speed apart from drag and friction.
        private static readonly float[] EngineTorque = { 1f, 1.3f, 1.65f, 2.1f };

        // Keyed by the car's save GUID rather than the Unity object, so per-car saving can plug straight in later.
        private static readonly Dictionary<Guid, int[]> _tiers = new Dictionary<Guid, int[]>();

        public static int GetTier(LandVehicle vehicle, UpgradeType type)
        {
            return _tiers.TryGetValue(vehicle.GUID, out int[] tiers) ? tiers[(int)type] : 0;
        }

        // Stock -> 1 -> 2 -> 3 -> back to stock. Only the debug keys use this, the tuning menu will set tiers directly.
        public static int CycleTier(LandVehicle vehicle, UpgradeType type)
        {
            if (!_tiers.TryGetValue(vehicle.GUID, out int[] tiers))
            {
                tiers = new int[Enum.GetValues(typeof(UpgradeType)).Length];
                _tiers[vehicle.GUID] = tiers;
            }

            tiers[(int)type] = (tiers[(int)type] + 1) % (MaxTier + 1);
            return tiers[(int)type];
        }

        public static string TierName(int tier) => TierNames[tier];

        public static float EngineTorqueMultiplier(LandVehicle vehicle) => EngineTorque[GetTier(vehicle, UpgradeType.Engine)];

        // What the tier does, for the log line when it changes.
        public static string Describe(UpgradeType type, int tier)
        {
            switch (type)
            {
                case UpgradeType.Engine: return $"x{EngineTorque[tier]} torque";
                default: return "not built yet";
            }
        }
    }
}
