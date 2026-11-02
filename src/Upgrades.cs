using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

    // Value is 0-1 against the Race tier.
    public struct UpgradeStat
    {
        public string Label;
        public float Value;
    }

    // Multipliers on each car's own values. Tier 0 is stock.
    public static class Upgrades
    {
        public const int MaxTier = 3;

        private static readonly string[] TierNames = { "Stock", "Street", "Sport", "Race" };

        private static readonly float[] EngineTorque = { 1f, 1.3f, 1.65f, 2.1f };

        // Side grip rises slower so handbrake drifts still work.
        private static readonly float[] TireForwardGrip = { 1f, 1.25f, 1.5f, 1.8f };
        private static readonly float[] TireSideGrip = { 1f, 1.15f, 1.3f, 1.45f };

        private static readonly float[] SuspensionSpring = { 1f, 1.2f, 1.4f, 1.65f };
        private static readonly float[] SuspensionDamper = { 1f, 1.2f, 1.45f, 1.7f };
        private static readonly float[] SuspensionDownforce = { 1f, 1.5f, 2f, 3f };

        // Too much and the wheels just lock.
        private static readonly float[] BrakeTorque = { 1f, 1.3f, 1.6f, 2f };

        // The speed cap is physics km/h. Wheel colliders get jittery much past this.
        private static readonly float[] NitroTank = { 5f, 6.5f, 8f, 10f };
        private static readonly float[] NitroRefill = { 12f, 10f, 8f, 6f };
        private static readonly float[] NitroPush = { 18f, 21f, 25f, 30f };
        private static readonly float[] NitroSpeedCap = { 230f, 240f, 250f, 260f };

        private static readonly Dictionary<Guid, int[]> _tiers = new Dictionary<Guid, int[]>();

        public static event Action<LandVehicle, UpgradeType> TierChanged;

        public static int GetTier(LandVehicle vehicle, UpgradeType type)
        {
            return _tiers.TryGetValue(vehicle.GUID, out int[] tiers) ? tiers[(int)type] : 0;
        }

        public static void Clear()
        {
            _tiers.Clear();
        }

        // One car per line: GUID=engine,tires,suspension,brakes,nitro
        public static string Serialize()
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("# Sokudo Tuners upgrades. One car per line: GUID=engine,tires,suspension,brakes,nitro (0 stock - 3 race)");
            foreach (KeyValuePair<Guid, int[]> car in _tiers)
            {
                if (car.Value.All(tier => tier == 0))
                    continue;
                text.AppendLine($"{car.Key}={string.Join(",", car.Value.Select(tier => tier.ToString()).ToArray())}");
            }
            return text.ToString();
        }

        // Skips bad lines instead of failing the whole load.
        public static int Deserialize(string text)
        {
            int count = 0;
            int categories = Enum.GetValues(typeof(UpgradeType)).Length;

            foreach (string rawLine in text.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;

                string[] parts = line.Split('=');
                if (parts.Length != 2)
                    continue;

                Guid guid;
                try { guid = new Guid(parts[0].Trim()); }
                catch (FormatException) { continue; }

                string[] values = parts[1].Split(',');
                int[] tiers = new int[categories];
                for (int i = 0; i < categories && i < values.Length; i++)
                    if (int.TryParse(values[i].Trim(), out int tier))
                        tiers[i] = Math.Max(0, Math.Min(MaxTier, tier));

                _tiers[guid] = tiers;
                count++;
            }
            return count;
        }

        public static void SetTier(LandVehicle vehicle, UpgradeType type, int tier)
        {
            if (!_tiers.TryGetValue(vehicle.GUID, out int[] tiers))
            {
                tiers = new int[Enum.GetValues(typeof(UpgradeType)).Length];
                _tiers[vehicle.GUID] = tiers;
            }

            tiers[(int)type] = Math.Max(0, Math.Min(MaxTier, tier));
            TierChanged?.Invoke(vehicle, type);
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

        public static UpgradeStat[] GetStats(UpgradeType type, int tier)
        {
            switch (type)
            {
                case UpgradeType.Engine:
                    return new[] { Stat("POWER", EngineTorque, tier) };
                case UpgradeType.Tires:
                    return new[] { Stat("TRACTION", TireForwardGrip, tier), Stat("CORNERING", TireSideGrip, tier) };
                case UpgradeType.Suspension:
                    return new[] { Stat("STIFFNESS", SuspensionSpring, tier), Stat("DOWNFORCE", SuspensionDownforce, tier) };
                case UpgradeType.Brakes:
                    return new[] { Stat("STOPPING", BrakeTorque, tier) };
                case UpgradeType.Nitro:
                    // Lower refill time is better, so flip it.
                    return new[]
                    {
                        Stat("CAPACITY", NitroTank, tier),
                        new UpgradeStat { Label = "REFILL", Value = NitroRefill[MaxTier] / NitroRefill[tier] },
                        Stat("BOOST", NitroPush, tier)
                    };
                default:
                    return new UpgradeStat[0];
            }
        }

        private static UpgradeStat Stat(string label, float[] values, int tier)
        {
            return new UpgradeStat { Label = label, Value = values[tier] / values[MaxTier] };
        }

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
