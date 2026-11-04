using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

// Nonblocking, bounded polling on Unity's main thread. No background callbacks.
public sealed class ServerDiscoveryClient : MonoBehaviour
{
    private UdpClient m_socket;
    private string m_nonce;
    private float m_deadline;
    private float m_nextQuery;
    public bool IsSearching => m_socket != null;
    public event Action<string> ServerFound;
    public event Action<string> SearchFinished;

    public void BeginSearch()
    {
        StopSearch();
        try
        {
            m_socket = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
            m_socket.EnableBroadcast = true;
            m_socket.Client.Blocking = false;
            m_nonce = Guid.NewGuid().ToString("N");
            m_deadline = Time.unscaledTime + 4f;
            m_nextQuery = 0f;
        }
        catch (SocketException e) { Finish("Discovery unavailable: " + e.SocketErrorCode); }
    }

    private void Update()
    {
        if (m_socket == null) return;
        try
        {
            for (int i = 0; i < 16 && m_socket.Available > 0; i++)
            {
                IPEndPoint source = new IPEndPoint(IPAddress.Any, 0);
                byte[] bytes = m_socket.Receive(ref source);
                if (source.Port != ServerDiscoveryProtocol.DiscoveryPort || bytes.Length > 128) continue;
                if (Encoding.ASCII.GetString(bytes) != ServerDiscoveryProtocol.ReplyPrefix + m_nonce) continue;
                string address = source.Address.ToString();
                StopSearch();
                ServerFound?.Invoke(address);
                return;
            }
            if (Time.unscaledTime >= m_deadline) { Finish("Server not found. Enter IP manually."); return; }
            if (Time.unscaledTime < m_nextQuery) return;
            m_nextQuery = Time.unscaledTime + 0.75f;
            byte[] query = ServerDiscoveryProtocol.Encode(ServerDiscoveryProtocol.QueryPrefix + m_nonce);
            m_socket.Send(query, query.Length, new IPEndPoint(IPAddress.Broadcast, ServerDiscoveryProtocol.DiscoveryPort));
            // Also supports a client and server running on the same PC.
            m_socket.Send(query, query.Length, new IPEndPoint(IPAddress.Loopback, ServerDiscoveryProtocol.DiscoveryPort));
        }
        catch (SocketException e) { Finish("Discovery unavailable: " + e.SocketErrorCode); }
    }

    private void Finish(string message) { StopSearch(); SearchFinished?.Invoke(message); }
    public void StopSearch() { m_socket?.Dispose(); m_socket = null; }
    private void OnDisable() => StopSearch();
}
