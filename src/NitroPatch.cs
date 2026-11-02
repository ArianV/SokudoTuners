using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using ScheduleOne;
using ScheduleOne.Vehicles;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SokudoTuners
{
    [HarmonyPatch(typeof(LandVehicle), "FixedUpdate")]
    public static class NitroPatch
    {
        private static readonly HashSet<int> _loggedVehicles = new HashSet<int>();

        private static void Postfix(LandVehicle __instance)
        {
            // Runs for every car, traffic included.
            if (!__instance.LocalPlayerIsDriver || !__instance.IsPhysicallySimulated)
                return;

            LogVehicleStatsOnce(__instance);

            // No push and no fuel burn at the cap.
            bool wantsBoost = IsNitroHeld() && __instance.Speed_Kmh < Upgrades.NitroMaxSpeedKmh(__instance);

            bool boosting = NitroTank.Tick(wantsBoost, Time.fixedDeltaTime,
                Upgrades.NitroTankSeconds(__instance), Upgrades.NitroRefillSeconds(__instance));

            if (boosting)
                __instance.Rb.AddForce(__instance.transform.forward * Upgrades.NitroAcceleration(__instance), ForceMode.Acceleration);
        }

        private static bool IsNitroHeld()
        {
            if (GameInput.IsTyping || TuningMenu.IsOpen)
                return false;

            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.leftShiftKey.isPressed;
        }

        // Prefabs override the code defaults, so I log the real values once per car.
        private static void LogVehicleStatsOnce(LandVehicle vehicle)
        {
            if (!_loggedVehicles.Add(vehicle.GetInstanceID()))
                return;

            Traverse t = Traverse.Create(vehicle);
            AnimationCurve torque = t.Field("motorTorque").GetValue<AnimationCurve>();

            string torqueKeys = "";
            if (torque != null)
                foreach (Keyframe k in torque.keys)
                    torqueKeys += $"({k.time:F0} km/h -> {k.value:F0}) ";

            Melon<SokudoTunersMod>.Logger.Msg(
                $"Driving '{vehicle.VehicleName}': mass {vehicle.Rb.mass:F0} kg, " +
                $"diffGearing {t.Field("diffGearing").GetValue<float>()}, " +
                $"handBrakeForce {t.Field("handBrakeForce").GetValue<float>()}, " +
                $"downforce {t.Field("downforce").GetValue<float>()}, " +
                $"maxSteer {t.Field("maxSteeringAngle").GetValue<float>()}");
            Melon<SokudoTunersMod>.Logger.Msg($"  torque curve: {torqueKeys}");
        }
    }
}
