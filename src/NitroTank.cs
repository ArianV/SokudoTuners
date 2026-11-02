namespace SokudoTuners
{
    // Fuel is 0-1, shared between cars for now.
    public static class NitroTank
    {
        // Stops feathering Shift from refilling it for free.
        private const float RefillDelaySeconds = 1.5f;

        // Without this an empty tank flickers on and off as it refills.
        public const float MinFuelToStart = 0.15f;

        public static float Fuel { get; private set; } = 1f;
        public static bool IsBoosting { get; private set; }

        private static float _timeSinceBoost;

        public static bool Tick(bool wantsBoost, float deltaTime, float fullTankSeconds, float refillSeconds)
        {
            if (!wantsBoost || Fuel <= 0f)
                IsBoosting = false;
            else if (!IsBoosting && Fuel >= MinFuelToStart)
                IsBoosting = true;

            if (IsBoosting)
            {
                Fuel -= deltaTime / fullTankSeconds;
                if (Fuel < 0f) Fuel = 0f;
                _timeSinceBoost = 0f;
            }
            else
            {
                _timeSinceBoost += deltaTime;
                if (_timeSinceBoost >= RefillDelaySeconds)
                {
                    Fuel += deltaTime / refillSeconds;
                    if (Fuel > 1f) Fuel = 1f;
                }
            }

            return IsBoosting;
        }
    }
}
