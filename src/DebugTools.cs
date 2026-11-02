using MelonLoader;
using ScheduleOne.Vehicles;
using UnityEngine;

namespace SokudoTuners
{
    // Testing helpers while I balance the upgrades: automatic 0-60 and 60-0 timers in the console
    // (0-100 / 100-0 in km/h), so tiers can be compared with numbers, not feel.
    public static class DebugTools
    {
        // On the HUD's speed scale, in whichever unit the HUD is showing.
        private static float TimerTarget => ModSettings.UseMph ? 60f : 100f;

        private static float _accelStart = -1f;
        private static float _brakeStart = -1f;
        private static bool _brakeArmed;
        private static Vector3 _brakeStartPosition;

        // Called every frame from the mod.
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

        // Starts when I pull away from a standstill and stops at the target. Lifting off or stopping cancels it.
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
                Melon<SokudoTunersMod>.Logger.Msg(
                    $"0-{TimerTarget:0} {ModSettings.SpeedUnit} in {Time.time - _accelStart:F2}s ({TierSummary(vehicle)}{nitro})");
                _accelStart = -1f;
            }
        }

        // Starts the moment the speed falls through the target while braking (S while rolling forward), and stops
        // when the car does. Starting the clock as soon as I hit the brakes made runs from 110 and from 65 look
        // like the same test. Letting go of the brake cancels it. The distance is real distance in the world.
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
                Melon<SokudoTunersMod>.Logger.Msg(
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

        // "S" or "T2" keeps the log line short enough to read.
        private static string Short(LandVehicle vehicle, UpgradeType type)
        {
            int tier = Upgrades.GetTier(vehicle, type);
            return tier == 0 ? "S" : "T" + tier;
        }
    }
}
