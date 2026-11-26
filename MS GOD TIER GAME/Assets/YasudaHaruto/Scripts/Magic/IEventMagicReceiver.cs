public interface IMagicReceiver
{

    /// <summary>
    /// 対応するイベント魔法を受け付け、アクションを実行した場合にtrueを返す。
    /// </summary>
    bool TryReceiveEventMagic(MAGIC_TYPE eventMagicType);
}
