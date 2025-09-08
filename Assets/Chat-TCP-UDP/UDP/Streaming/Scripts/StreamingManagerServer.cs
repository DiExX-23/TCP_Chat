using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Server-side StreamingManager; binds local port and waits for clients.
/// </summary>
public class StreamingManagerServer : MonoBehaviour
{
    public string remoteAddress = "127.0.0.1";
    public int localPort = 5555;
    public int remotePort = 5556;

    public UdpTransport transport;
    public VideoSender videoSender;
    public VideoReceiver videoReceiver;
    public AudioSender audioSender;
    public AudioReceiver audioReceiver;
    public AudioPlayer audioPlayer;

    public RawImage remoteVideoDisplay;
    public RawImage localPreviewDisplay;

    // ensure transport reference if on same GameObject
    private void Awake() { if (transport == null) transport = GetComponent<UdpTransport>(); }

    // Start server and wire modules
    public void StartStreaming()
    {
        if (transport != null)
        {
            try
            {
                transport.StartTransport(localPort, remoteAddress, remotePort, bindLocal: true); // server binds local port
                Debug.Log($"[StreamingManagerServer] Server bound on {localPort}.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StreamingManagerServer] StartTransport failed: {ex.Message}");
            }
        }
        else Debug.LogWarning("[StreamingManagerServer] No transport assigned.");

        if (videoReceiver != null) videoReceiver.targetImage = remoteVideoDisplay;
        if (videoSender != null) videoSender.transport = transport;
        if (videoReceiver != null) videoReceiver.transport = transport;
        if (audioSender != null) audioSender.transport = transport;
        if (audioReceiver != null) audioReceiver.transport = transport;

        if (audioReceiver != null && audioPlayer != null) audioPlayer.Initialize(audioReceiver.audioQueue);

        if (videoReceiver != null) videoReceiver.enabled = true;
        if (videoSender != null)
        {
            videoSender.StartSending();
            if (localPreviewDisplay != null) { localPreviewDisplay.texture = videoSender.CameraTexture; localPreviewDisplay.color = Color.white; localPreviewDisplay.enabled = true; }
        }
        if (audioReceiver != null) audioReceiver.enabled = true;
        audioSender?.StartSending();
    }

    // Stop server streaming
    public void StopStreaming()
    {
        videoSender?.StopSending();
        audioSender?.StopSending();
        try { transport?.StopTransport(); } catch (Exception ex) { Debug.LogWarning($"[StreamingManagerServer] StopTransport error: {ex.Message}"); }
        if (localPreviewDisplay != null) localPreviewDisplay.texture = null;
        Debug.Log("[StreamingManagerServer] Server stopped.");
    }

    // Toggle video on/off
    public void ToggleVideo(bool on) { if (videoSender == null) return; if (on) videoSender.StartSending(); else videoSender.StopSending(); if (localPreviewDisplay != null) localPreviewDisplay.texture = on ? videoSender.CameraTexture : null; }
    // Toggle audio on/off
    public void ToggleAudio(bool on) { if (audioSender == null) return; if (on) audioSender.StartSending(); else audioSender.StopSending(); }

    // Set remote IP (kept for compatibility)
    public void SetIP(string ip) { if (!string.IsNullOrWhiteSpace(ip)) remoteAddress = ip.Trim(); }
    // Set remote port (kept for compatibility)
    public void SetPort(string portString) { if (int.TryParse(portString, out int p) && p >= 0 && p <= 65535) remotePort = p; }
}