// Optional feedback channel; tracing rules remain in the evaluator.
public interface IRuneTraceFeedbackView
{
    void SetTraceDeviation(float normalizedDeviation);
    void PlayCompletion();
}
