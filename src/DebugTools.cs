using MelonLoader;
using ScheduleOne;
using ScheduleOne.Vehicles;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SokudoTuners
{
    // Testing helpers while I balance the upgrades. The tuning menu replaces the hotkeys later.
    //   F5  cycle the engine tier of the car I'm driving
    // Plus an automatic 0-100 km/h timer in the console, so tiers can be compared with numbers, not feel.
    public static class DebugTools
    {
        // On the game's speedometer scale, so it matches what the HUD shows.
        private const float TimerTargetKmh = 100f;

        private static float _timerStart = -1f;

        // Called every frame from the mod.
        public static void Update()
        {
            LandVehicle vehicle = LocalDriver.GetVehicle();
            if (vehicle == null)
            {
                _timerStart = -1f;
                return;
            }

            HandleHotkeys(vehicle);
            UpdateAccelerationTimer(vehicle);
        }

        private static void HandleHotkeys(LandVehicle vehicle)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || GameInput.IsTyping)
                return;

            if (keyboard.f5Key.wasPressedThisFrame)
                Cycle(vehicle, UpgradeType.Engine);
        }

        private static void Cycle(LandVehicle vehicle, UpgradeType type)
        {
            int tier = Upgrades.CycleTier(vehicle, type);
            Melon<SokudoTunersMod>.Logger.Msg(
                $"{type} on '{vehicle.VehicleName}' is now {Upgrades.TierName(tier)} ({Upgrades.Describe(type, tier)})");
        }

        // Starts when I pull away from a standstill and stops at 100. Lifting off or stopping cancels it.
        private static void UpdateAccelerationTimer(LandVehicle vehicle)
        {
            float speed = vehicle.Rb.velocity.magnitude * 1.4f * 3.6f;

            if (speed < 1f)
            {
                _timerStart = vehicle.currentThrottle > 0f ? Time.time : -1f;
                return;
            }

            if (_timerStart < 0f)
                return;

            if (vehicle.currentThrottle <= 0f)
            {
                _timerStart = -1f;
                return;
            }

            if (speed >= TimerTargetKmh)
            {
                string nitro = NitroTank.IsBoosting ? ", with nitro" : "";
                Melon<SokudoTunersMod>.Logger.Msg(
                    $"0-{TimerTargetKmh:0} km/h in {Time.time - _timerStart:F2}s " +
                    $"(engine {Upgrades.TierName(Upgrades.GetTier(vehicle, UpgradeType.Engine))}{nitro})");
                _timerStart = -1f;
            }
        }
    }
}
