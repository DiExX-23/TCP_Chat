using System.Collections;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Capture from a WebCamTexture (or optional RenderTexture path) and send JPEG frames over UdpTransport.
/// Keep settings low (resolution / quality / fps) for CPU efficiency.
/// </summary>
[RequireComponent(typeof(UdpTransport))]
public class VideoSender : MonoBehaviour
{
    public UdpTransport transport;
    public int captureWidth = 480;
    public int captureHeight = 270;
    public int fps = 12;
    [Range(10, 90)] public int jpegQuality = 30;

    private WebCamTexture camTexture;
    private Texture2D captureTexture;
    private bool sending = false;
    private int frameCounter = 0;

    // Expose the webcam texture for local preview
    public WebCamTexture CameraTexture => camTexture;

    private void Awake()
    {
        transport = transport ?? GetComponent<UdpTransport>();
    }

    public void StartSending()
    {
        if (sending) return;
        sending = true;
        StartCoroutine(CaptureLoop());
    }

    public void StopSending()
    {
        sending = false;
        if (camTexture != null && camTexture.isPlaying)
        {
            camTexture.Stop();
        }
        camTexture = null; // reset para que StartSending vuelva a inicializarla
    }

    private IEnumerator CaptureLoop()
    {
        // start webcam
        if (camTexture == null)
        {
            camTexture = new WebCamTexture(captureWidth, captureHeight);
            camTexture.Play();
            yield return new WaitUntil(() => camTexture.width > 16);
            captureTexture = new Texture2D(camTexture.width, camTexture.height, TextureFormat.RGB24, false);
        }

        float interval = 1f / Mathf.Max(1, fps);
        while (sending)
        {
            // Sampling from webcam and encode to JPG
            captureTexture.SetPixels32(camTexture.GetPixels32());
            captureTexture.Apply(false);

            // Encode synchronously to JPG but keep resolution & quality low to reduce blocking.
            byte[] jpg = captureTexture.EncodeToJPG(jpegQuality);

            // Fragment and send
            var packets = Packetizer.Fragment(jpg, frameCounter++, 1);
            foreach (var p in packets)
            {
                transport.SendAsync(p);
            }

            // Wait so the main loop isn't blocked continuously
            yield return new WaitForSeconds(interval);
        }
    }

    private void OnDisable()
    {
        StopSending();
    }
}