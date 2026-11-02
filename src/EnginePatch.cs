using HarmonyLib;
using ScheduleOne.Vehicles;
using UnityEngine;

namespace SokudoTuners
{
    // Scales the torque ApplyThrottle already put on the drive wheels, so each car keeps its own power curve.
    [HarmonyPatch(typeof(LandVehicle), "ApplyThrottle")]
    public static class EnginePatch
    {
        private static readonly AccessTools.FieldRef<LandVehicle, WheelCollider[]> DriveWheels =
            AccessTools.FieldRefAccess<LandVehicle, WheelCollider[]>("driveWheels");

        private static void Postfix(LandVehicle __instance)
        {
            float multiplier = Upgrades.EngineTorqueMultiplier(__instance);
            if (multiplier == 1f)
                return;

            foreach (WheelCollider wheel in DriveWheels(__instance))
                wheel.motorTorque *= multiplier;
        }
    }
}
