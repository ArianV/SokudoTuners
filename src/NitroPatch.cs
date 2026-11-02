using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using ScheduleOne;
using ScheduleOne.Vehicles;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CarTuning
{
    // Runs right after the game's own physics step for every car. The game sets wheel torque in
    // FixedUpdate, so this is where I stack the nitro push on top of it.
    // "FixedUpdate" is a string because the method is protected and nameof() can't reach it.
    [HarmonyPatch(typeof(LandVehicle), "FixedUpdate")]
    public static class NitroPatch
    {
        // Hardcoded for now, these become upgrade tiers later.
        // ForceMode.Acceleration ignores mass, so every car gets the same kick. 18 m/s² is almost 2g.
        private const float NitroAcceleration = 18f;

        // Past this I stop pushing. Unity's wheel physics gets jittery at silly speeds.
        private const float NitroMaxSpeedKmh = 230f;

        private static bool _wasBoosting;
        private static readonly HashSet<int> _loggedVehicles = new HashSet<int>();

        private static void Postfix(LandVehicle __instance)
        {
            // Every car in the world runs FixedUpdate, NPC traffic included. Only touch the one I'm driving.
            if (!__instance.LocalPlayerIsDriver || !__instance.IsPhysicallySimulated)
                return;

            LogVehicleStatsOnce(__instance);

            bool boosting = IsNitroHeld() && __instance.Speed_Kmh < NitroMaxSpeedKmh;

            if (boosting)
                __instance.Rb.AddForce(__instance.transform.forward * NitroAcceleration, ForceMode.Acceleration);

            // Only log when the state flips, otherwise this would spam 50 lines a second.
            if (boosting != _wasBoosting)
            {
                Melon<CarTuningMod>.Logger.Msg(boosting
                    ? $"NITRO ON  at {__instance.Speed_Kmh:F0} km/h"
                    : $"NITRO OFF at {__instance.Speed_Kmh:F0} km/h");
                _wasBoosting = boosting;
            }
        }

        private static bool IsNitroHeld()
        {
            // Don't boost while typing in chat or a text box.
            if (GameInput.IsTyping)
                return false;

            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.leftShiftKey.isPressed;
        }

        // The prefab can override the code defaults, so I dump the real numbers the first time I
        // drive each car. I need these to balance the upgrade tiers later.
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

            Melon<CarTuningMod>.Logger.Msg(
                $"Driving '{vehicle.VehicleName}': mass {vehicle.Rb.mass:F0} kg, " +
                $"diffGearing {t.Field("diffGearing").GetValue<float>()}, " +
                $"handBrakeForce {t.Field("handBrakeForce").GetValue<float>()}, " +
                $"downforce {t.Field("downforce").GetValue<float>()}, " +
                $"maxSteer {t.Field("maxSteeringAngle").GetValue<float>()}");
            Melon<CarTuningMod>.Logger.Msg($"  torque curve: {torqueKeys}");
        }
    }
}
