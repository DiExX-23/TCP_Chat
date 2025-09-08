// VideoSender.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Capture webcam frames, encode to JPG (low quality) and send via UdpTransport.
/// Designed kept simple: reuse buffers, limit fps and quality for LAN use.
/// </summary>
[RequireComponent(typeof(UdpTransport))]
public class VideoSender : MonoBehaviour
{
    public UdpTransport transport;                // transport to send through
    public int captureWidth = 480;
    public int captureHeight = 270;
    public int fps = 12;
    [Range(10, 90)] public int jpegQuality = 30;

    private WebCamTexture camTexture;
    private Texture2D captureTexture;
    private Color32[] capturePixels;              // reused pixel buffer
    private bool sending = false;
    private int frameCounter = 0;
    private volatile bool encodingInProgress = false;

    // Expose camera texture for preview
    public WebCamTexture CameraTexture => camTexture;

    private void Awake()
    {
        transport = transport ?? GetComponent<UdpTransport>();
    }

    // Start sending coroutine
    public void StartSending()
    {
        if (sending) return;
        sending = true;
        StartCoroutine(CaptureLoop());
    }

    // Stop sending and cleanup
    public void StopSending()
    {
        sending = false;
        if (camTexture != null && camTexture.isPlaying) camTexture.Stop();
        camTexture = null;
    }

    private IEnumerator CaptureLoop()
    {
        if (camTexture == null)
        {
            camTexture = new WebCamTexture(captureWidth, captureHeight);
            camTexture.Play();
            yield return new WaitUntil(() => camTexture.width > 16);
            captureTexture = new Texture2D(camTexture.width, camTexture.height, TextureFormat.RGB24, false);
            capturePixels = new Color32[camTexture.width * camTexture.height];
        }

        float interval = 1f / Mathf.Max(1, fps);

        while (sending)
        {
            camTexture.GetPixels32(capturePixels);
            captureTexture.SetPixels32(capturePixels);
            captureTexture.Apply(false);

            if (!encodingInProgress)
            {
                encodingInProgress = true;
                byte[] jpg = captureTexture.EncodeToJPG(jpegQuality);
                encodingInProgress = false;

                // fragment and send
                var packets = Packetizer.Fragment(jpg, frameCounter++, 1);
                foreach (var p in packets)
                {
                    transport?.SendAsync(p);
                }
            }

            yield return new WaitForSecondsRealtime(interval);
        }
    }

    private void OnDisable() => StopSending();
}