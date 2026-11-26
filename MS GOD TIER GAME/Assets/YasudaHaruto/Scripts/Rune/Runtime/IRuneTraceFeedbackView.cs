// Optional feedback channel; tracing rules remain in the evaluator.
/// <summary>ルーン入力の成功・失敗フィードバックを表示へ渡す契約。</summary>
public interface IRuneTraceFeedbackView
{

    void SetTraceDeviation(float normalizedDeviation);

    void PlayCompletion();
}
