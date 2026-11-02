using HarmonyLib;
using ScheduleOne.Vehicles;

namespace SokudoTuners
{
    // Brake upgrade. ApplyThrottle sets brake torque on every wheel each physics step, both for normal braking
    // (holding S while moving forward) and the handbrake. I scale whatever it set. The engine upgrade is also
    // a postfix on ApplyThrottle; Harmony runs both, they don't replace each other.
    [HarmonyPatch(typeof(LandVehicle), "ApplyThrottle")]
    public static class BrakePatch
    {
        private static void Postfix(LandVehicle __instance)
        {
            float multiplier = Upgrades.BrakeMultiplier(__instance);
            if (multiplier == 1f)
                return;

            foreach (Wheel wheel in __instance.wheels)
                wheel.wheelCollider.brakeTorque *= multiplier;
        }
    }
}
