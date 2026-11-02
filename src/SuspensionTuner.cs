using System.Collections.Generic;
using HarmonyLib;
using ScheduleOne.Vehicles;
using UnityEngine;

namespace SokudoTuners
{
    // The game never touches these after the car spawns, so I set them once per tier change and scale from the stock values.
    public static class SuspensionTuner
    {
        private static readonly AccessTools.FieldRef<LandVehicle, float> Downforce =
            AccessTools.FieldRefAccess<LandVehicle, float>("downforce");

        private static readonly Dictionary<WheelCollider, JointSpring> _stockSprings = new Dictionary<WheelCollider, JointSpring>();
        private static readonly Dictionary<LandVehicle, float> _stockDownforce = new Dictionary<LandVehicle, float>();

        public static void OnTierChanged(LandVehicle vehicle, UpgradeType type)
        {
            if (type == UpgradeType.Suspension)
                Apply(vehicle);
        }

        public static void Apply(LandVehicle vehicle)
        {
            float springMultiplier = Upgrades.SuspensionSpringMultiplier(vehicle);
            float damperMultiplier = Upgrades.SuspensionDamperMultiplier(vehicle);

            foreach (Wheel wheel in vehicle.wheels)
            {
                WheelCollider collider = wheel.wheelCollider;
                if (!_stockSprings.TryGetValue(collider, out JointSpring stock))
                {
                    stock = collider.suspensionSpring;
                    _stockSprings[collider] = stock;
                }

                JointSpring spring = stock;
                spring.spring *= springMultiplier;
                spring.damper *= damperMultiplier;
                collider.suspensionSpring = spring;
            }

            if (!_stockDownforce.TryGetValue(vehicle, out float stockDownforce))
            {
                stockDownforce = Downforce(vehicle);
                _stockDownforce[vehicle] = stockDownforce;
            }
            Downforce(vehicle) = stockDownforce * Upgrades.SuspensionDownforceMultiplier(vehicle);
        }
    }
}
