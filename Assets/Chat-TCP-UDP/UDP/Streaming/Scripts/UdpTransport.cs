using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Async UDP transport wrapper. Raises OnPacketReceived when a UDP datagram arrives.
/// </summary>
public class UdpTransport : MonoBehaviour
{
    public int LocalPort = 0; // 0 = random
    public string RemoteAddress = "127.0.0.1";
    public int RemotePort = 5555;

    private UdpClient udp;
    private IPEndPoint remoteEndPoint;

    public event Action<byte[], IPEndPoint> OnPacketReceived;

    private bool running = false;

    public void StartTransport(int localPort, string remoteAddress, int remotePort, bool bindLocal = true)
    {
        StopTransport();

        LocalPort = localPort;
        RemoteAddress = remoteAddress;
        RemotePort = remotePort;

        try
        {
            if (bindLocal)
                udp = new UdpClient(localPort);
            else
                udp = new UdpClient();

            udp.Client.ReceiveBufferSize = 1 << 20;
            udp.Client.SendBufferSize = 1 << 20;

            remoteEndPoint = new IPEndPoint(IPAddress.Parse(remoteAddress), remotePort);
            running = true;
            BeginReceive();
            Debug.Log($"UdpTransport started. LocalPort={localPort}, Remote={remoteAddress}:{remotePort}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"UdpTransport Start error: {ex}");
            StopTransport();
        }
    }

    private void BeginReceive()
    {
        try
        {
            udp.BeginReceive(ReceiveCallback, null);
        }
        catch (Exception ex)
        {
            Debug.LogError($"UdpTransport BeginReceive error: {ex}");
        }
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        if (!running) return;
        try
        {
            IPEndPoint source = null;
            byte[] data = udp.EndReceive(ar, ref source);
            OnPacketReceived?.Invoke(data, source);
        }
        catch (ObjectDisposedException) { /* shutting down */ }
        catch (Exception ex)
        {
            Debug.LogError($"UdpTransport ReceiveCallback exception: {ex}");
        }
        finally
        {
            try { if (running) udp.BeginReceive(ReceiveCallback, null); } catch { }
        }
    }

    public async void SendAsync(byte[] data)
    {
        if (udp == null) return;
        try
        {
            // Use Task-based send; non-blocking for caller.
            await udp.SendAsync(data, data.Length, remoteEndPoint);
        }
        catch (Exception ex)
        {
            Debug.LogError($"UdpTransport SendAsync error: {ex.Message}");
        }
    }

    public void StopTransport()
    {
        running = false;
        try
        {
            udp?.Close();
        }
        catch { }
        udp = null;
    }
}