using ScheduleOne.PlayerScripts;
using ScheduleOne.Vehicles;

namespace SokudoTuners
{
    public static class LocalDriver
    {
        // The car I'm driving, or null when I'm on foot or riding as a passenger.
        public static LandVehicle GetVehicle()
        {
            if (Player.Local == null || Player.Local.CurrentVehicle == null)
                return null;

            LandVehicle vehicle = Player.Local.CurrentVehicle.GetComponent<LandVehicle>();
            return vehicle != null && vehicle.LocalPlayerIsDriver ? vehicle : null;
        }

        // The tank keeps its last state when I hop out mid-boost, so effects check I'm still driving too.
        public static bool IsBoosting => NitroTank.IsBoosting && GetVehicle() != null;
    }
}
