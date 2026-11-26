/// <summary>ルーン発動時の表示を入力判定から切り離すための契約。</summary>
public interface IRuneCastPresentation
{

    bool IsCastReady
    {
        get;
    }

    void LaunchCast(MAGIC_CAST_DIRECTION direction);
}
