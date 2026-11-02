namespace SokudoTuners
{
    // The nitro fuel. 1 = full, 0 = empty. One shared tank for now, per-car tanks come with saving.
    public static class NitroTank
    {
        // How long a full tank lasts while holding the button.
        private const float FullTankSeconds = 5f;

        // How long an empty tank takes to fill back up.
        private const float RefillSeconds = 12f;

        // Wait a moment after boosting before refilling, so feathering Shift doesn't refill it for free.
        private const float RefillDelaySeconds = 1.5f;

        // Need at least this much to start a boost. Without it an empty tank stutters on and off
        // every frame as the tiny refill comes in.
        public const float MinFuelToStart = 0.15f;

        public static float Fuel { get; private set; } = 1f;
        public static bool IsBoosting { get; private set; }

        private static float _timeSinceBoost;

        // Called every physics step while I'm driving. Returns whether nitro should fire this step.
        public static bool Tick(bool wantsBoost, float deltaTime)
        {
            if (!wantsBoost || Fuel <= 0f)
                IsBoosting = false;
            else if (!IsBoosting && Fuel >= MinFuelToStart)
                IsBoosting = true;

            if (IsBoosting)
            {
                Fuel -= deltaTime / FullTankSeconds;
                if (Fuel < 0f) Fuel = 0f;
                _timeSinceBoost = 0f;
            }
            else
            {
                _timeSinceBoost += deltaTime;
                if (_timeSinceBoost >= RefillDelaySeconds)
                {
                    Fuel += deltaTime / RefillSeconds;
                    if (Fuel > 1f) Fuel = 1f;
                }
            }

            return IsBoosting;
        }
    }
}
