using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Coordinates client/server streaming with minimal UI wiring.
/// </summary>
public class StreamingManager : MonoBehaviour
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

    public void StartStreaming()
    {
        transport?.StartTransport(localPort, remoteAddress, remotePort, bindLocal: true);

        if (videoReceiver != null) videoReceiver.targetImage = remoteVideoDisplay;
        if (videoSender != null) videoSender.transport = transport;
        if (videoReceiver != null) videoReceiver.transport = transport;
        if (audioSender != null) audioSender.transport = transport;
        if (audioReceiver != null) audioReceiver.transport = transport;

        if (audioReceiver != null && audioPlayer != null)
            audioPlayer.Initialize(audioReceiver.audioQueue);

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

    public void StopStreaming()
    {
        videoSender?.StopSending();
        audioSender?.StopSending();
        transport?.StopTransport();

        if (localPreviewDisplay != null)
        {
            localPreviewDisplay.texture = null;
        }
    }

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

    public void ToggleAudio(bool on)
    {
        if (audioSender == null) return;
        if (on) audioSender.StartSending();
        else audioSender.StopSending();
    }

    public void SetIP(string ip)
    {
        if (!string.IsNullOrWhiteSpace(ip)) remoteAddress = ip.Trim();
    }

    public void SetPort(string portString)
    {
        if (int.TryParse(portString, out int p) && p >= 0 && p <= 65535)
            remotePort = p;
    }
}