// Optional presentation channel. The controller owns cooldown timing.
public interface IRuneCooldownView
{
    void ShowCooldown(float remainingFraction);
}
