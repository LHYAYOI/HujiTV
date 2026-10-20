/// <summary>
/// ノイズページを消す演出（BookController.RemoveNoisePage から呼ぶ）。
/// BookController はこのインターフェース越しに呼ぶので、演出のコンポーネント（BookMagicPresentation など）が
/// プロジェクトに無くてもコンパイルできる。同じ GameObject に実装があれば演出付きで消す。
/// </summary>
public interface IBookNoiseRemoval
{
    /// <summary>今、演出できるか（無効・準備前なら false）</summary>
    bool CanDissolve { get; }
    /// <summary>演出の長さ（秒）</summary>
    float NoiseRemovalDuration { get; }
    void BeginNoiseRemoval(int index, BookPageView view);
    void SampleNoiseRemoval(float progress, float deltaTime);
    void EndNoiseRemoval(bool cancelled);
}
