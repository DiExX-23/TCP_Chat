using System;
using UnityEngine;
using TMPro;

/// <summary>
/// TCPClientUI: UI glue between the TCPClient network logic and the Chat UI.
/// - Shows outgoing ("Me: ...") locally before sending.
/// - Subscribes to TCPClient.OnMessageReceived and shows incoming messages.
/// - Performs simple sanitization and length limiting to avoid TMP issues.
/// </summary>
public class TCPClientUI : MonoBehaviour
{
    [Header("Network Settings (optional)")]
    public string serverAddress = "127.0.0.1";
    public int serverPort = 5555;

    [Header("Inspector References")]
    [SerializeField] private TCPClient client;                 // assign TCPClient component here
    [SerializeField] private TMP_InputField messageInput;      // assign input field
    [SerializeField] private ChatUIManager chatUIManager;      // assign ChatUIManager

    [Header("Limits")]
    [SerializeField] private int maxMessageLength = 512;

    private void OnEnable()
    {
        if (client != null)
            client.OnMessageReceived += HandleServerMessage;
    }

    private void OnDisable()
    {
        if (client != null)
            client.OnMessageReceived -= HandleServerMessage;
    }

    /// <summary>
    /// Register client at runtime (safe: unsubscribes previous before subscribing new).
    /// </summary>
    public void RegisterClient(TCPClient newClient)
    {
        if (client != null)
            client.OnMessageReceived -= HandleServerMessage;

        client = newClient;

        if (client != null)
            client.OnMessageReceived += HandleServerMessage;
    }

    /// <summary>
    /// Called by UI (send button). Shows local message then sends to server.
    /// </summary>
    public void SendClientMessage()
    {
        if (client == null)
        {
            Debug.LogWarning("[TCPClientUI] TCPClient not assigned.");
            return;
        }

        if (!client.isServerConnected)
        {
            Debug.Log("[TCPClientUI] Client not connected to server.");
            return;
        }

        if (messageInput == null)
        {
            Debug.LogWarning("[TCPClientUI] messageInput not assigned.");
            return;
        }

        string raw = messageInput.text ?? string.Empty;
        string msg = SanitizeAndLimit(raw);
        if (string.IsNullOrWhiteSpace(msg)) return;

        // 1) Show locally first so user sees it immediately
        try
        {
            if (chatUIManager != null)
                chatUIManager.AddOutgoing("Me: " + msg);
            else
                Debug.Log("[TCPClientUI] UI missing, message shown in console: " + msg);
        }
        catch (Exception ex)
        {
            Debug.LogError("[TCPClientUI] Error showing local message: " + ex.Message);
        }

        // 2) Send to server (if fails, local message already shown)
        try
        {
            client.SendData(msg);
        }
        catch (Exception ex)
        {
            Debug.LogError("[TCPClientUI] SendData exception: " + ex.Message);
        }

        // Clear and refocus input
        messageInput.text = string.Empty;
        messageInput.ActivateInputField();
    }

    /// <summary>
    /// Connect button (optional). Uses serverAddress and serverPort fields.
    /// </summary>
    public void ConnectClient()
    {
        if (client == null)
        {
            Debug.LogWarning("[TCPClientUI] TCPClient not assigned.");
            return;
        }

        client.ConnectToServer(serverAddress, serverPort);
    }

    /// <summary>
    /// Handler for messages arriving from the server (called on main thread because TCPClient enqueues).
    /// </summary>
    private void HandleServerMessage(string message)
    {
        try
        {
            if (chatUIManager != null)
                chatUIManager.AddIncoming("Server: " + message);
            else
                Debug.Log("[TCPClientUI] Incoming (no UI): " + message);
        }
        catch (Exception ex)
        {
            Debug.LogError("[TCPClientUI] Error handling server message: " + ex.Message);
        }
    }

    /// <summary>
    /// Trim, escape and limit message size to avoid TMP tags and extremely long messages.
    /// </summary>
    private string SanitizeAndLimit(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        string s = input.Trim();

        // Remove newlines to avoid unexpected layout issues
        s = s.Replace("\r", " ").Replace("\n", " ");

        if (maxMessageLength > 0 && s.Length > maxMessageLength)
            s = s.Substring(0, maxMessageLength);

        // Escape angle brackets to prevent TMP tag injection
        s = s.Replace("<", "&lt;").Replace(">", "&gt;");
        return s;
    }
}