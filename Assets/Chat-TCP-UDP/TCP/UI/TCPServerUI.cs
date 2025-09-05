using System;
using UnityEngine;
using TMPro;

/// <summary>
/// TCPServerUI: UI glue for the server side.
/// - Shows outgoing ("Me: ...") locally before sending to client.
/// - Subscribes to TCPServer.OnMessageReceived to display incoming client messages.
/// - Simple sanitization and length limiting included.
/// </summary>
public class TCPServerUI : MonoBehaviour
{
    [Header("Inspector References")]
    [SerializeField] private TCPServer server;                 // assign TCPServer component
    [SerializeField] private TMP_InputField messageInput;      // assign input field
    [SerializeField] private ChatUIManager chatUIManager;      // assign ChatUIManager

    [Header("Server Settings")]
    public int listenPort = 5555;

    [Header("Limits")]
    [SerializeField] private int maxMessageLength = 512;

    private void OnEnable()
    {
        if (server != null)
            server.OnMessageReceived += HandleClientMessage;
    }

    private void OnDisable()
    {
        if (server != null)
            server.OnMessageReceived -= HandleClientMessage;
    }

    /// <summary>
    /// Register a server instance at runtime (safe).
    /// </summary>
    public void RegisterServer(TCPServer newServer)
    {
        if (server != null)
            server.OnMessageReceived -= HandleClientMessage;

        server = newServer;

        if (server != null)
            server.OnMessageReceived += HandleClientMessage;
    }

    /// <summary>
    /// Start the TCP server listening on listenPort.
    /// </summary>
    public void StartServer()
    {
        if (server == null)
        {
            Debug.LogWarning("[TCPServerUI] TCPServer not assigned.");
            return;
        }

        server.StartServer(listenPort);

        // Informational message in UI (incoming style for system notice)
        if (chatUIManager != null)
            chatUIManager.AddIncoming($"Server: listening on port {listenPort}");
    }

    /// <summary>
    /// Called by UI to send a message to the connected client.
    /// Show locally first then send.
    /// </summary>
    public void SendServerMessage()
    {
        if (server == null)
        {
            Debug.LogWarning("[TCPServerUI] TCPServer not assigned.");
            return;
        }

        if (messageInput == null)
        {
            Debug.LogWarning("[TCPServerUI] messageInput not assigned.");
            return;
        }

        string raw = messageInput.text ?? string.Empty;
        string msg = SanitizeAndLimit(raw);
        if (string.IsNullOrWhiteSpace(msg)) return;

        // 1) Show locally
        try
        {
            if (chatUIManager != null)
                chatUIManager.AddOutgoing("Me: " + msg);
            else
                Debug.Log("[TCPServerUI] Sent (no UI): " + msg);
        }
        catch (Exception ex)
        {
            Debug.LogError("[TCPServerUI] Error showing local message: " + ex.Message);
        }

        // 2) Send to client
        try
        {
            server.SendData(msg);
        }
        catch (Exception ex)
        {
            Debug.LogError("[TCPServerUI] SendData exception: " + ex.Message);
        }

        // clear input
        messageInput.text = string.Empty;
        messageInput.ActivateInputField();
    }

    /// <summary>
    /// Handler for messages arriving from client (called on main thread by TCPServer).
    /// </summary>
    private void HandleClientMessage(string message)
    {
        try
        {
            if (chatUIManager != null)
                chatUIManager.AddIncoming("Client: " + message);
            else
                Debug.Log("[TCPServerUI] Incoming (no UI): " + message);
        }
        catch (Exception ex)
        {
            Debug.LogError("[TCPServerUI] Error handling client message: " + ex.Message);
        }
    }

    /// <summary>
    /// Trim, escape and limit message size to avoid TMP tags and extremely long messages.
    /// </summary>
    private string SanitizeAndLimit(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        string s = input.Trim();

        s = s.Replace("\r", " ").Replace("\n", " ");

        if (maxMessageLength > 0 && s.Length > maxMessageLength)
            s = s.Substring(0, maxMessageLength);

        s = s.Replace("<", "&lt;").Replace(">", "&gt;");
        return s;
    }
}