using MelonLoader;
using ScheduleOne.Vehicles;
using UnityEngine;

namespace SokudoTuners
{
    // 0-60 and 60-0 timers in the console so I can compare tiers with numbers.
    public static class DebugTools
    {
        private static float TimerTarget => ModSettings.UseMph ? 60f : 100f;

        private static float _accelStart = -1f;
        private static float _brakeStart = -1f;
        private static bool _brakeArmed;
        private static Vector3 _brakeStartPosition;

        public static void Update()
        {
            LandVehicle vehicle = LocalDriver.GetVehicle();
            if (vehicle == null)
            {
                _accelStart = -1f;
                _brakeStart = -1f;
                _brakeArmed = false;
                return;
            }

            float speed = ModSettings.DisplaySpeed(vehicle.Rb.velocity.magnitude);
            UpdateAccelerationTimer(vehicle, speed);
            UpdateBrakingTimer(vehicle, speed);
        }

        private static void UpdateAccelerationTimer(LandVehicle vehicle, float speed)
        {
            if (speed < 1f)
            {
                _accelStart = vehicle.currentThrottle > 0f ? Time.time : -1f;
                return;
            }

            if (_accelStart < 0f)
                return;

            if (vehicle.currentThrottle <= 0f)
            {
                _accelStart = -1f;
                return;
            }

            if (speed >= TimerTarget)
            {
                string nitro = NitroTank.IsBoosting ? ", with nitro" : "";
                ModSettings.LogDebug(
                    $"0-{TimerTarget:0} {ModSettings.SpeedUnit} in {Time.time - _accelStart:F2}s ({TierSummary(vehicle)}{nitro})");
                _accelStart = -1f;
            }
        }

        // The clock starts when the speed drops through the target, so every run is a true 60-0 however fast I was going.
        private static void UpdateBrakingTimer(LandVehicle vehicle, float speed)
        {
            bool braking = vehicle.currentThrottle < 0f && vehicle.Speed_Kmh > 0f;

            if (_brakeStart < 0f)
            {
                if (!braking)
                    _brakeArmed = false;
                else if (speed > TimerTarget)
                    _brakeArmed = true;
                else if (_brakeArmed)
                {
                    _brakeArmed = false;
                    _brakeStart = Time.time;
                    _brakeStartPosition = vehicle.transform.position;
                }
                return;
            }

            if (speed < 1f)
            {
                float meters = Vector3.Distance(_brakeStartPosition, vehicle.transform.position);
                string distance = ModSettings.UseMph ? $"{meters * 3.28084f:F0} ft" : $"{meters:F1} m";
                ModSettings.LogDebug(
                    $"{TimerTarget:0}-0 {ModSettings.SpeedUnit} in {Time.time - _brakeStart:F2}s over {distance} ({TierSummary(vehicle)})");
                _brakeStart = -1f;
                return;
            }

            if (!braking)
                _brakeStart = -1f;
        }

        private static string TierSummary(LandVehicle vehicle)
        {
            return $"engine {Short(vehicle, UpgradeType.Engine)}, tires {Short(vehicle, UpgradeType.Tires)}, " +
                   $"suspension {Short(vehicle, UpgradeType.Suspension)}, brakes {Short(vehicle, UpgradeType.Brakes)}, " +
                   $"nitro {Short(vehicle, UpgradeType.Nitro)}";
        }

        private static string Short(LandVehicle vehicle, UpgradeType type)
        {
            int tier = Upgrades.GetTier(vehicle, type);
            return tier == 0 ? "S" : "T" + tier;
        }
    }
}
