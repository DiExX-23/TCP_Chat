using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

/// <summary>
/// Minimal TCP client for local testing. Non-blocking receive using BeginRead.
/// Exposes event OnMessageReceived for UI to subscribe. Uses MainThreadInvoker to marshal UI updates.
/// </summary>
public class TCPClient : MonoBehaviour
{
    private TcpClient tcpClient;
    private NetworkStream networkStream;
    private byte[] receiveBuffer;

    public bool isServerConnected;

    // Event fired when a message is received from the server. UI subscribes to this.
    public event Action<string> OnMessageReceived;

    /// <summary>
    /// Connect to server IP and port.
    /// </summary>
    public void ConnectToServer(string ipAddress, int port)
    {
        try
        {
            if (tcpClient != null)
            {
                tcpClient.Close();
                tcpClient = null;
            }

            tcpClient = new TcpClient();
            tcpClient.Connect(IPAddress.Parse(ipAddress), port);
            networkStream = tcpClient.GetStream();
            receiveBuffer = new byte[tcpClient.ReceiveBufferSize];
            networkStream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, ReceiveData, null);
            isServerConnected = true;
            Debug.Log($"Connected to server {ipAddress}:{port}");
        }
        catch (Exception ex)
        {
            Debug.LogError("ConnectToServer error: " + ex.Message);
            isServerConnected = false;
        }
    }

    private void ReceiveData(IAsyncResult ar)
    {
        try
        {
            if (networkStream == null) return;
            int bytesRead = networkStream.EndRead(ar);
            if (bytesRead <= 0)
            {
                Debug.Log("Server closed connection.");
                isServerConnected = false;
                return;
            }

            byte[] receivedBytes = new byte[bytesRead];
            Array.Copy(receiveBuffer, receivedBytes, bytesRead);
            string receivedMessage = Encoding.UTF8.GetString(receivedBytes);
            Debug.Log("Received from server: " + receivedMessage);

            // Ensure UI runs on main thread
            MainThreadInvoker.Enqueue(() => OnMessageReceived?.Invoke(receivedMessage));

            // continue reading
            networkStream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, ReceiveData, null);
        }
        catch (ObjectDisposedException)
        {
            isServerConnected = false;
        }
        catch (Exception ex)
        {
            Debug.LogError("ReceiveData error: " + ex.Message);
            isServerConnected = false;
        }
    }

    public void SendData(string message)
    {
        if (networkStream == null || !isServerConnected)
        {
            Debug.LogWarning("There is no server connection to send the message: " + message);
            return;
        }

        try
        {
            byte[] sendBytes = Encoding.UTF8.GetBytes(message);
            networkStream.Write(sendBytes, 0, sendBytes.Length);
            networkStream.Flush();
            Debug.Log("Sent to server: " + message);
        }
        catch (Exception ex)
        {
            Debug.LogError("SendData error: " + ex.Message);
        }
    }

    private void OnDestroy()
    {
        try
        {
            if (networkStream != null) networkStream.Close();
            if (tcpClient != null) tcpClient.Close();
        }
        catch { }
    }
}