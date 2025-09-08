using System;
using System.Net;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Client-side StreamingManager (connects to server at remoteAddress:remotePort).
/// </summary>
public class StreamingManager : MonoBehaviour
{
    // Remote endpoint values
    public string remoteAddress = "127.0.0.1";
    public int localPort = 5556;
    public int remotePort = 5555;

    // Module references
    public UdpTransport transport;
    public VideoSender videoSender;
    public VideoReceiver videoReceiver;
    public AudioSender audioSender;
    public AudioReceiver audioReceiver;
    public AudioPlayer audioPlayer;

    // UI previews
    public RawImage remoteVideoDisplay;
    public RawImage localPreviewDisplay;

    // Start streaming as client
    public void StartStreaming()
    {
        if (transport != null)
        {
            try
            {
                transport.StartTransport(localPort, remoteAddress, remotePort, bindLocal: false); // client mode
                Debug.Log($"[StreamingManager] Client started. Sending to {remoteAddress}:{remotePort} from local {localPort}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StreamingManager] Transport start failed: {ex.Message}");
            }
        }
        else Debug.LogWarning("[StreamingManager] No UdpTransport assigned.");

        // wire modules to transport and UI
        if (videoReceiver != null) videoReceiver.targetImage = remoteVideoDisplay;
        if (videoSender != null) videoSender.transport = transport;
        if (videoReceiver != null) videoReceiver.transport = transport;
        if (audioSender != null) audioSender.transport = transport;
        if (audioReceiver != null) audioReceiver.transport = transport;

        // init audio playback queue
        if (audioReceiver != null && audioPlayer != null) audioPlayer.Initialize(audioReceiver.audioQueue);

        // enable receivers and start senders
        if (videoReceiver != null) videoReceiver.enabled = true;
        if (videoSender != null)
        {
            videoSender.StartSending(); // start video capture
            if (localPreviewDisplay != null)
            {
                localPreviewDisplay.texture = videoSender.CameraTexture; // show local preview
                localPreviewDisplay.color = Color.white;
                localPreviewDisplay.enabled = true;
            }
        }
        if (audioReceiver != null) audioReceiver.enabled = true;
        audioSender?.StartSending(); // start audio capture
    }

    // Stop all streaming activity
    public void StopStreaming()
    {
        videoSender?.StopSending(); // stop video
        audioSender?.StopSending(); // stop audio
        try { transport?.StopTransport(); } catch (Exception ex) { Debug.LogWarning($"[StreamingManager] StopTransport error: {ex.Message}"); }
        if (localPreviewDisplay != null) localPreviewDisplay.texture = null; // clear preview
        Debug.Log("[StreamingManager] Client stopped.");
    }

    // Toggle video on/off
    public void ToggleVideo(bool on)
    {
        if (videoSender == null) return;
        if (on) videoSender.StartSending(); else videoSender.StopSending();
        if (localPreviewDisplay != null) localPreviewDisplay.texture = on ? videoSender.CameraTexture : null;
    }

    // Toggle audio on/off
    public void ToggleAudio(bool on)
    {
        if (audioSender == null) return;
        if (on) audioSender.StartSending(); else audioSender.StopSending();
    }

    // Set remote IP (called by UI)
    public void SetIP(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return; // ignore empty
        var trimmed = ip.Trim();
        // validate numeric IP but accept hostname too
        if (IPAddress.TryParse(trimmed, out _)) remoteAddress = trimmed;
        else remoteAddress = trimmed; // allow hostname
        Debug.Log($"[StreamingManager] Remote address set to {remoteAddress}");
    }

    // Set remote port (called by UI)
    public void SetPort(string portString)
    {
        if (int.TryParse(portString, out int p) && p >= 0 && p <= 65535)
        {
            remotePort = p;
            Debug.Log($"[StreamingManager] Remote port set to {remotePort}");
        }
        else Debug.LogWarning($"[StreamingManager] Invalid port '{portString}'");
    }
}