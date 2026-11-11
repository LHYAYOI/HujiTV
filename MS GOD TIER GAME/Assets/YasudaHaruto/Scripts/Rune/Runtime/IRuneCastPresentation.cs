public interface IRuneCastPresentation
{
    bool IsCastReady { get; }
    void LaunchCast(MAGIC_CAST_DIRECTION direction);
}
