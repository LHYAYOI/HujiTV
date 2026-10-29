public interface IRuneCastPresentation
{
    bool IsCastReady { get; }
    void SetDirectionalCast(bool directional);
    void LaunchCast(MAGIC_CAST_DIRECTION direction);
}
