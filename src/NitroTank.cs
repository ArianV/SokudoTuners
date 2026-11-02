namespace SokudoTuners
{
    // The nitro fuel. 1 = full, 0 = empty. One shared tank for now, per-car tanks come with saving.
    // How long a full tank lasts and how fast it refills come from the car's nitro tier, so a bigger tank
    // just drains the same 0-1 fuel more slowly and the HUD bar needs no changes.
    public static class NitroTank
    {
        // Wait a moment after boosting before refilling, so feathering Shift doesn't refill it for free.
        private const float RefillDelaySeconds = 1.5f;

        // Need at least this much to start a boost. Without it an empty tank stutters on and off
        // every frame as the tiny refill comes in.
        public const float MinFuelToStart = 0.15f;

        public static float Fuel { get; private set; } = 1f;
        public static bool IsBoosting { get; private set; }

        private static float _timeSinceBoost;

        // Called every physics step while I'm driving. Returns whether nitro should fire this step.
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
