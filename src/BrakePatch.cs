using HarmonyLib;
using ScheduleOne.Vehicles;

namespace SokudoTuners
{
    // ApplyThrottle sets brake torque every step, handbrake included, so I scale whatever it set.
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
