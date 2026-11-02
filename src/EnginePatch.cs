using HarmonyLib;
using ScheduleOne.Vehicles;
using UnityEngine;

namespace SokudoTuners
{
    // Engine upgrade. ApplyThrottle works out the torque from the car's torque curve and puts it on the
    // drive wheels. I scale whatever it put there, so the car's own power curve keeps its shape and just
    // gets stronger. Nothing to undo when the tier changes either, the game recalculates every step.
    [HarmonyPatch(typeof(LandVehicle), "ApplyThrottle")]
    public static class EnginePatch
    {
        // driveWheels is protected. FieldRefAccess builds a fast getter once, which matters at 50 calls a second.
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
