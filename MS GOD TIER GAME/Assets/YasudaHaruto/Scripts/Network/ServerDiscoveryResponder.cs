using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public sealed class ServerDiscoveryResponder : MonoBehaviour
{
    private UdpClient m_socket;
    private BookNetworkServer m_server;

    // Called only after the Transport server successfully binds and listens.
    public void BeginResponding()
    {
        if (m_socket != null) return;
        m_server = GetComponent<BookNetworkServer>();
        try
        {
            m_socket = new UdpClient(new IPEndPoint(IPAddress.Any, ServerDiscoveryProtocol.DiscoveryPort));
            m_socket.Client.Blocking = false;
        }
        catch (SocketException e)
        {
            StopResponding();
            Debug.LogWarning("Discovery unavailable; manual IP still works: " + e.SocketErrorCode, this);
        }
    }

    private void Update()
    {
        if (m_socket == null) return;
        if (m_server == null || !m_server.isActiveAndEnabled) return;
        try
        {
            for (int i = 0; i < 16 && m_socket.Available > 0; i++)
            {
                IPEndPoint source = new IPEndPoint(IPAddress.Any, 0);
                byte[] query = m_socket.Receive(ref source);
                if (query.Length != ServerDiscoveryProtocol.QueryPrefix.Length + 32) continue;
                string message = Encoding.ASCII.GetString(query);
                if (!message.StartsWith(ServerDiscoveryProtocol.QueryPrefix, StringComparison.Ordinal)) continue;
                string nonce = message.Substring(ServerDiscoveryProtocol.QueryPrefix.Length);
                if (!Guid.TryParseExact(nonce, "N", out _)) continue;
                byte[] reply = ServerDiscoveryProtocol.Encode(ServerDiscoveryProtocol.ReplyPrefix + nonce);
                m_socket.Send(reply, reply.Length, source);
            }
        }
        catch (SocketException e)
        {
            StopResponding();
            Debug.LogWarning("Discovery stopped; manual IP still works: " + e.SocketErrorCode, this);
        }
    }

    public void StopResponding() { m_socket?.Dispose(); m_socket = null; }
    private void OnDisable() => StopResponding();
}
