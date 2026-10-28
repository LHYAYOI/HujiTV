// Only a newly cast spell plays the release; resuming another page's cooldown does not.
public interface IRuneCooldownReleaseView
{
    void PlayCooldownRelease(float cooldownSeconds);
}
