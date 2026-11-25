using System.Text;

// Discovery only locates the existing game endpoint; it never registers players.
/// <summary>サーバー探索の要求・応答で共通使用する通信識別子。</summary>
public static class ServerDiscoveryProtocol
{
    public const int DiscoveryPort = 7776;
    public const ushort GamePort = 7777;
    public const string QueryPrefix = "MS_BOOK_DISCOVERY/1/";
    public const string ReplyPrefix = "MS_BOOK_SERVER/1/7777/";


    public static byte[] Encode(string message) => Encoding.ASCII.GetBytes(message);
}
