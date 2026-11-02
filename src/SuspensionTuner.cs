using System.Collections.Generic;
using HarmonyLib;
using ScheduleOne.Vehicles;
using UnityEngine;

namespace SokudoTuners
{
    // Suspension upgrade: stiffer springs and dampers, plus more downforce.
    // Unlike grip and torque, the game never touches these after the car spawns, so I set them once when the
    // tier changes instead of patching every physics step. I keep each car's stock values so that going back
    // to Stock puts them back exactly, and so tiers always scale from stock rather than stacking on each other.
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

                // JointSpring is a struct like WheelFrictionCurve: copy, change, assign back.
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
