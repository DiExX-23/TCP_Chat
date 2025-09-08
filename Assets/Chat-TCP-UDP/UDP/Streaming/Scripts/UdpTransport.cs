// UdpTransport.cs
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
    public Action<byte[], IPEndPoint> OnPacketReceived;        // packet callback

    // Returns whether transport is running
    public bool IsRunning => started;

    /// <summary>
    /// StartTransport starts (or restarts) UDP transport; bindLocal true means server mode.
    /// </summary>
    public void StartTransport(int localPort, string remoteAddress, int remotePort, bool bindLocal)
    {
        // stop any existing transport first
        StopTransport();

        this.localPort = localPort;
        this.remoteAddress = remoteAddress;
        this.remotePort = remotePort;
        this.lastRemoteEndPoint = null;

        try
        {
            if (bindLocal)
            {
                // bind to local port (server/listen mode)
                udp = new UdpClient(localPort);
            }
            else
            {
                // client mode: create socket and optionally connect to configured remote
                udp = new UdpClient();
                if (!string.IsNullOrEmpty(remoteAddress))
                {
                    try { udp.Connect(remoteAddress, remotePort); } catch { /* ignore connect errors */ }
                }
            }

            cts = new CancellationTokenSource();
            started = true;
            _ = ReceiveLoopAsync(cts.Token); // start receive loop (fire-and-forget)
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

    /// <summary>
    /// StopTransport cancels receive loop and closes socket.
    /// </summary>
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

    /// <summary>
    /// SendAsync attempts to send non-blocking.
    /// It will prefer lastRemoteEndPoint, then configured remoteAddress, otherwise skip send.
    /// </summary>
    // SendAsync: non-blocking send that respects whether the underlying socket is connected.
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
            // If socket is connected (client mode), use SendAsync without endpoint.
            if (udp.Client != null && udp.Client.Connected)
            {
                _ = udp.SendAsync(data, data.Length); // send to connected remote
                return;
            }

            // If we know the last remote endpoint (server replying to client), use it.
            if (lastRemoteEndPoint != null)
            {
                _ = udp.SendAsync(data, data.Length, lastRemoteEndPoint.Address.ToString(), lastRemoteEndPoint.Port);
                return;
            }

            // If configured remote address is available, send to it (unconnected client case).
            if (!string.IsNullOrEmpty(remoteAddress) && remotePort > 0)
            {
                _ = udp.SendAsync(data, data.Length, remoteAddress, remotePort);
                return;
            }

            // No known remote; drop and warn.
            Debug.LogWarning("[UdpTransport] Send attempted but no remote endpoint known; packet dropped.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UdpTransport] Send exception: {ex.Message}");
        }
    }


    /// <summary>
    /// Async receive loop; updates lastRemoteEndPoint and invokes OnPacketReceived.
    /// </summary>
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
                catch (ObjectDisposedException)
                {
                    break; // socket closed
                }
                catch (SocketException se)
                {
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
                        // record last remote endpoint so server can reply
                        lastRemoteEndPoint = res.RemoteEndPoint;
                        // invoke handlers (they should be thread-safe or enqueue for main thread)
                        OnPacketReceived?.Invoke(res.Buffer, res.RemoteEndPoint);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[UdpTransport] OnPacketReceived exception: {ex.Message}");
                    }
                }
            }
        }
        finally
        {
            started = false;
        }
    }

    private void OnDestroy() => StopTransport();
}