using ScheduleOne.PlayerScripts;
using ScheduleOne.Vehicles;

namespace SokudoTuners
{
    public static class LocalDriver
    {
        public static LandVehicle GetVehicle()
        {
            if (Player.Local == null || Player.Local.CurrentVehicle == null)
                return null;

            LandVehicle vehicle = Player.Local.CurrentVehicle.GetComponent<LandVehicle>();
            return vehicle != null && vehicle.LocalPlayerIsDriver ? vehicle : null;
        }

        // The tank keeps its state if I hop out mid-boost.
        public static bool IsBoosting => NitroTank.IsBoosting && GetVehicle() != null;
    }
}
