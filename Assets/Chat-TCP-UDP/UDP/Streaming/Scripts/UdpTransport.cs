using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Minimal UDP transport using UdpClient and async receive/send loops.
/// Uses lastRemoteEndPoint or configured remoteAddress to send when socket is not connected.
/// </summary>
public class UdpTransport : MonoBehaviour
{
    private UdpClient udp;                                      // internal UDP client
    private CancellationTokenSource cts;                        // cancellation for receive loop
    private int localPort;                                      // configured local port
    private string remoteAddress;                               // configured remote address (may be hostname)
    private int remotePort;                                     // configured remote port
    private volatile bool started = false;                      // running flag
    private IPEndPoint lastRemoteEndPoint = null;               // last endpoint that sent us a packet

    // event raised when a packet arrives, provides payload and remote endpoint
    public Action<byte[], IPEndPoint> OnPacketReceived;

    public bool IsRunning => started;

    public void StartTransport(int localPort, string remoteAddress, int remotePort, bool bindLocal)
    {
        StopTransport(); // ensure cleanup

        this.localPort = localPort;
        this.remoteAddress = remoteAddress;
        this.remotePort = remotePort;
        this.lastRemoteEndPoint = null;

        try
        {
            if (bindLocal)
            {
                udp = new UdpClient(localPort);
            }
            else
            {
                udp = new UdpClient();
                if (!string.IsNullOrEmpty(remoteAddress))
                {
                    try { udp.Connect(remoteAddress, remotePort); } catch { }
                }
            }

            cts = new CancellationTokenSource();
            started = true;
            _ = ReceiveLoopAsync(cts.Token);
            Debug.Log($"[UdpTransport] Started. LocalPort={localPort}, Remote={remoteAddress}:{remotePort}, Bound={bindLocal}");
        }
        catch (Exception ex)
        {
            started = false;
            Debug.LogError($"[UdpTransport] Failed to start transport: {ex.Message}");
            try { udp?.Close(); } catch { }
            udp = null;
            cts = null;
        }
    }

    public void StopTransport()
    {
        if (!started && udp == null) return;
        try
        {
            cts?.Cancel();
            udp?.Close();
            Debug.Log("[UdpTransport] Stopped.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UdpTransport] Exception stopping: {ex.Message}");
        }
        finally
        {
            udp = null;
            cts = null;
            started = false;
            lastRemoteEndPoint = null;
        }
    }

    public void SendAsync(byte[] data)
    {
        if (data == null || data.Length == 0) return;
        if (udp == null)
        {
            Debug.LogWarning($"[UdpTransport] Send attempted but socket is not open. LocalPort={localPort}");
            return;
        }

        try
        {
            if (udp.Client != null && udp.Client.Connected)
            {
                _ = udp.SendAsync(data, data.Length);
                return;
            }

            if (lastRemoteEndPoint != null)
            {
                _ = udp.SendAsync(data, data.Length, lastRemoteEndPoint);
                return;
            }

            if (!string.IsNullOrEmpty(remoteAddress) && remotePort > 0)
            {
                _ = udp.SendAsync(data, data.Length, remoteAddress, remotePort);
                return;
            }

            Debug.LogWarning("[UdpTransport] Send attempted but no remote endpoint known; packet dropped.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UdpTransport] Send exception: {ex.Message}");
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                UdpReceiveResult res;
                try
                {
                    res = await udp.ReceiveAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException se)
                {
                    if (se.SocketErrorCode == SocketError.ConnectionReset)
                    {
                        Debug.LogWarning("[UdpTransport] Receive socket reset (ignored).");
                        continue;
                    }
                    Debug.LogWarning($"[UdpTransport] Receive socket exception: {se.SocketErrorCode}");
                    await Task.Delay(10);
                    continue;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[UdpTransport] Receive exception: {ex.Message}");
                    await Task.Delay(10);
                    continue;
                }

                if (res.Buffer != null && res.Buffer.Length > 0)
                {
                    try
                    {
                        lastRemoteEndPoint = res.RemoteEndPoint;
                        OnPacketReceived?.Invoke(res.Buffer, res.RemoteEndPoint);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[UdpTransport] OnPacketReceived exception: {ex.Message}");
                    }
                }
            }
        }
        finally { started = false; }
    }

    private void OnDestroy() => StopTransport();
}