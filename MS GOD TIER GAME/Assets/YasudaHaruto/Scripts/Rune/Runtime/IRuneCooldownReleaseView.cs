// Only a newly cast spell plays the release; resuming another page's cooldown does not.
/// <summary>クールダウン解除時の表示通知を受け取る契約。</summary>
public interface IRuneCooldownReleaseView
{

    void PlayCooldownRelease(float cooldownSeconds);
}
