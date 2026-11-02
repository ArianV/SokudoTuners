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

        // Grip multipliers per tire tier. Forward grip is what puts engine power down off the line.
        // Sideways grip rises more gently so the handbrake still kicks the back out for drifts.
        private static readonly float[] TireForwardGrip = { 1f, 1.25f, 1.5f, 1.8f };
        private static readonly float[] TireSideGrip = { 1f, 1.15f, 1.3f, 1.45f };

        // Stiffer springs with matching damping, so the car rolls less without turning bouncy.
        // Downforce climbs fastest because it only kicks in at speed, which is exactly where nitro puts you.
        private static readonly float[] SuspensionSpring = { 1f, 1.2f, 1.4f, 1.65f };
        private static readonly float[] SuspensionDamper = { 1f, 1.2f, 1.45f, 1.7f };
        private static readonly float[] SuspensionDownforce = { 1f, 1.5f, 2f, 3f };

        // Brake torque multiplier. Kept moderate because too much just locks the wheels and the car skids further.
        private static readonly float[] BrakeTorque = { 1f, 1.3f, 1.6f, 2f };

        // Nitro: a bigger tank, a faster refill and a harder push per tier. 18 m/s² at stock is almost 2g.
        // The speed cap is real physics km/h (about 200 / 209 / 217 / 226 mph on the HUD). I keep it from
        // climbing much because Unity's wheel physics gets jittery at silly speeds.
        private static readonly float[] NitroTank = { 5f, 6.5f, 8f, 10f };
        private static readonly float[] NitroRefill = { 12f, 10f, 8f, 6f };
        private static readonly float[] NitroPush = { 18f, 21f, 25f, 30f };
        private static readonly float[] NitroSpeedCap = { 230f, 240f, 250f, 260f };

        // Keyed by the car's save GUID rather than the Unity object, so per-car saving can plug straight in later.
        private static readonly Dictionary<Guid, int[]> _tiers = new Dictionary<Guid, int[]>();

        // For upgrades that are set once rather than patched every physics step (suspension).
        public static event Action<LandVehicle, UpgradeType> TierChanged;

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
            TierChanged?.Invoke(vehicle, type);
            return tiers[(int)type];
        }

        public static string TierName(int tier) => TierNames[tier];

        public static float EngineTorqueMultiplier(LandVehicle vehicle) => EngineTorque[GetTier(vehicle, UpgradeType.Engine)];
        public static float TireForwardGripMultiplier(LandVehicle vehicle) => TireForwardGrip[GetTier(vehicle, UpgradeType.Tires)];
        public static float TireSideGripMultiplier(LandVehicle vehicle) => TireSideGrip[GetTier(vehicle, UpgradeType.Tires)];
        public static float SuspensionSpringMultiplier(LandVehicle vehicle) => SuspensionSpring[GetTier(vehicle, UpgradeType.Suspension)];
        public static float SuspensionDamperMultiplier(LandVehicle vehicle) => SuspensionDamper[GetTier(vehicle, UpgradeType.Suspension)];
        public static float SuspensionDownforceMultiplier(LandVehicle vehicle) => SuspensionDownforce[GetTier(vehicle, UpgradeType.Suspension)];
        public static float BrakeMultiplier(LandVehicle vehicle) => BrakeTorque[GetTier(vehicle, UpgradeType.Brakes)];
        public static float NitroTankSeconds(LandVehicle vehicle) => NitroTank[GetTier(vehicle, UpgradeType.Nitro)];
        public static float NitroRefillSeconds(LandVehicle vehicle) => NitroRefill[GetTier(vehicle, UpgradeType.Nitro)];
        public static float NitroAcceleration(LandVehicle vehicle) => NitroPush[GetTier(vehicle, UpgradeType.Nitro)];
        public static float NitroMaxSpeedKmh(LandVehicle vehicle) => NitroSpeedCap[GetTier(vehicle, UpgradeType.Nitro)];

        // What the tier does, for the log line when it changes.
        public static string Describe(UpgradeType type, int tier)
        {
            switch (type)
            {
                case UpgradeType.Engine: return $"x{EngineTorque[tier]} torque";
                case UpgradeType.Tires: return $"x{TireForwardGrip[tier]} traction, x{TireSideGrip[tier]} cornering grip";
                case UpgradeType.Suspension: return $"x{SuspensionSpring[tier]} springs, x{SuspensionDamper[tier]} dampers, x{SuspensionDownforce[tier]} downforce";
                case UpgradeType.Brakes: return $"x{BrakeTorque[tier]} brake force";
                case UpgradeType.Nitro: return $"{NitroTank[tier]}s tank, {NitroRefill[tier]}s refill, {NitroPush[tier]} m/s² push";
                default: return "not built yet";
            }
        }
    }
}
