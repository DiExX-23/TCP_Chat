using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Minimal server-oriented StreamingManager.
/// Keeps behavior identical to the original manager but forces bind-local start
/// and adds lightweight diagnostics so you can see in console that the server started/stopped.
/// </summary>
public class StreamingManagerServer : MonoBehaviour
{
    public string remoteAddress = "127.0.0.1";
    public int localPort = 5555;
    public int remotePort = 5555;

    public UdpTransport transport;
    public VideoSender videoSender;
    public VideoReceiver videoReceiver;
    public AudioSender audioSender;
    public AudioReceiver audioReceiver;
    public AudioPlayer audioPlayer;

    public RawImage remoteVideoDisplay;
    public RawImage localPreviewDisplay;

    private void Awake()
    {
        // Ensure transport reference if the component sits on the same GameObject
        if (transport == null)
        {
            transport = GetComponent<UdpTransport>();
        }
    }

    /// <summary>
    /// Start transport bound to localPort (server mode) and wire modules and UI.
    /// </summary>
    public void StartStreaming()
    {
        // Start transport bound to local port so server listens
        if (transport != null)
        {
            try
            {
                transport.StartTransport(localPort, remoteAddress, remotePort, bindLocal: true);
                Debug.Log($"[StreamingManagerServer] Server started and bound to local port {localPort}. Waiting for clients...");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StreamingManagerServer] Failed to start transport on port {localPort}: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning("[StreamingManagerServer] No UdpTransport assigned; cannot start server transport.");
        }

        // Wire receivers/senders to UI and transport
        if (videoReceiver != null) videoReceiver.targetImage = remoteVideoDisplay;
        if (videoSender != null) videoSender.transport = transport;
        if (videoReceiver != null) videoReceiver.transport = transport;
        if (audioSender != null) audioSender.transport = transport;
        if (audioReceiver != null) audioReceiver.transport = transport;

        // initialize audio pipeline
        if (audioReceiver != null && audioPlayer != null)
            audioPlayer.Initialize(audioReceiver.audioQueue);

        // enable receivers and start senders as in the original manager
        if (videoReceiver != null) videoReceiver.enabled = true;

        if (videoSender != null)
        {
            videoSender.StartSending();
            if (localPreviewDisplay != null)
            {
                localPreviewDisplay.texture = videoSender.CameraTexture;
                localPreviewDisplay.color = Color.white;
                localPreviewDisplay.enabled = true;
            }
        }

        if (audioReceiver != null) audioReceiver.enabled = true;
        audioSender?.StartSending();
    }

    /// <summary>
    /// Stop senders and transport; clear preview and log stop event.
    /// </summary>
    public void StopStreaming()
    {
        videoSender?.StopSending();
        audioSender?.StopSending();

        // stop transport and log
        if (transport != null)
        {
            try
            {
                transport.StopTransport();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[StreamingManagerServer] Exception stopping transport: {ex.Message}");
            }
        }

        if (localPreviewDisplay != null)
        {
            localPreviewDisplay.texture = null;
        }

        Debug.Log("[StreamingManagerServer] Server stopped.");
    }

    /// <summary>
    /// Toggle video sending on/off (keeps same behavior as original).
    /// </summary>
    public void ToggleVideo(bool on)
    {
        if (videoSender == null) return;

        if (on)
        {
            videoSender.StartSending();
            if (localPreviewDisplay != null)
            {
                localPreviewDisplay.texture = videoSender.CameraTexture;
                localPreviewDisplay.color = Color.white;
                localPreviewDisplay.enabled = true;
            }
        }
        else
        {
            videoSender.StopSending();
            if (localPreviewDisplay != null)
            {
                localPreviewDisplay.texture = null;
            }
        }
    }

    /// <summary>
    /// Toggle audio sending on/off (unchanged).
    /// </summary>
    public void ToggleAudio(bool on)
    {
        if (audioSender == null) return;
        if (on) audioSender.StartSending();
        else audioSender.StopSending();
    }

    /// <summary>
    /// Set remote IP (kept for compatibility).
    /// </summary>
    public void SetIP(string ip)
    {
        if (!string.IsNullOrWhiteSpace(ip)) remoteAddress = ip.Trim();
    }

    /// <summary>
    /// Set remote port (kept for compatibility).
    /// </summary>
    public void SetPort(string portString)
    {
        if (int.TryParse(portString, out int p) && p >= 0 && p <= 65535)
            remotePort = p;
    }
}