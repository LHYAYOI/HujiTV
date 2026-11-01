public interface IEventMagicReceiver
{
    /// <summary>
    /// 対応するイベント魔法を受け付け、アクションを実行した場合にtrueを返す。
    /// </summary>
    bool TryReceiveEventMagic(EVENT_MAGIC_TYPE eventMagicType);
}
