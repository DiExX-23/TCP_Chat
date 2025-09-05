using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class TCPServer : MonoBehaviour
{
    private TcpListener tcpListener;
    private TcpClient connectedClient;
    private NetworkStream networkStream;
    private byte[] receiveBuffer;

    public bool isServerRunning;

    // Event so UI can subscribe
    public event Action<string> OnMessageReceived;

    public void StartServer(int port)
    {
        try
        {
            tcpListener = new TcpListener(IPAddress.Any, port);
            tcpListener.Start();
            Debug.Log("Server started, waiting for connections...");
            tcpListener.BeginAcceptTcpClient(HandleIncomingConnection, null);
            isServerRunning = true;
        }
        catch (Exception ex)
        {
            Debug.LogError("StartServer error: " + ex.Message);
            isServerRunning = false;
        }
    }

    private void HandleIncomingConnection(IAsyncResult result)
    {
        try
        {
            connectedClient = tcpListener.EndAcceptTcpClient(result);
            networkStream = connectedClient.GetStream();
            Debug.Log("Client connected: " + connectedClient.Client.RemoteEndPoint);

            receiveBuffer = new byte[connectedClient.ReceiveBufferSize];
            networkStream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, ReceiveData, null);

            tcpListener.BeginAcceptTcpClient(HandleIncomingConnection, null);
        }
        catch (ObjectDisposedException)
        {
            // Listener closed while waiting
        }
        catch (Exception ex)
        {
            Debug.LogError("HandleIncomingConnection error: " + ex.Message);
        }
    }

    private void ReceiveData(IAsyncResult result)
    {
        try
        {
            // Ensure stream and client are still valid
            if (networkStream == null || connectedClient == null || !connectedClient.Connected)
                return;

            int bytesRead = networkStream.EndRead(result);

            if (bytesRead <= 0)
            {
                Debug.Log("Client disconnected.");
                DisconnectClient();
                return;
            }

            byte[] receivedBytes = new byte[bytesRead];
            Array.Copy(receiveBuffer, receivedBytes, bytesRead);
            string receivedMessage = System.Text.Encoding.UTF8.GetString(receivedBytes);

            Debug.Log("Received from client: " + receivedMessage);

            // Safe UI invoke
            MainThreadInvoker.Enqueue(() => OnMessageReceived?.Invoke(receivedMessage));

            // Continue reading only if stream is still valid
            if (networkStream != null && connectedClient != null && connectedClient.Connected)
            {
                networkStream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, ReceiveData, null);
            }
        }
        catch (ObjectDisposedException)
        {
            DisconnectClient();
        }
        catch (Exception ex)
        {
            Debug.LogError("ReceiveData error: " + ex.Message);
            DisconnectClient();
        }
    }

    public void SendData(string message)
    {
        if (networkStream == null || connectedClient == null)
        {
            Debug.LogWarning("No client connected to send message: " + message);
            return;
        }

        try
        {
            byte[] sendBytes = System.Text.Encoding.UTF8.GetBytes(message);
            networkStream.Write(sendBytes, 0, sendBytes.Length);
            networkStream.Flush();
            Debug.Log("Sent to client: " + message);
        }
        catch (Exception ex)
        {
            Debug.LogError("SendData error: " + ex.Message);
            DisconnectClient();
        }
    }

    private void DisconnectClient()
    {
        try
        {
            if (networkStream != null) networkStream.Close();
            if (connectedClient != null) connectedClient.Close();
        }
        catch { }

        networkStream = null;
        connectedClient = null;
    }

    private void OnDestroy()
    {
        DisconnectClient();
        if (tcpListener != null) tcpListener.Stop();
    }
}