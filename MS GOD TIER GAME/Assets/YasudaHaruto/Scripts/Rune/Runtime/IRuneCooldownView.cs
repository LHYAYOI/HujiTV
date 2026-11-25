// Optional presentation channel. The controller owns cooldown timing.
/// <summary>クールダウン進行を表示へ通知する契約。</summary>
public interface IRuneCooldownView
{

    void ShowCooldown(float remainingFraction);
}
