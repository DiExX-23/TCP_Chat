// AudioSender.cs
using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Capture microphone audio, convert to PCM16 and send small frames periodically.
/// Waits until microphone is ready and reuses buffers to reduce allocations.
/// </summary>
[RequireComponent(typeof(UdpTransport))]
public class AudioSender : MonoBehaviour
{
    public UdpTransport transport;
    public int sampleRate = 16000;
    public int channels = 1;
    public int frameMilliseconds = 20;

    private AudioClip microphoneClip;
    private int lastPosition = 0;
    private bool sending = false;
    private int frameId = 0;

    private void Awake() { transport = transport ?? GetComponent<UdpTransport>(); }

    // Start capturing and sending; wait until microphone actually provides samples
    public void StartSending()
    {
        if (sending) return;
        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            Debug.LogWarning("[AudioSender] No microphone found.");
            return;
        }
        StartCoroutine(StartMicrophoneAndSend());
    }

    // Coroutine to ensure microphone readiness
    private IEnumerator StartMicrophoneAndSend()
    {
        if (Microphone.IsRecording(null)) Microphone.End(null);
        microphoneClip = Microphone.Start(null, true, 1, sampleRate);
        float timeout = 1.0f;
        float start = Time.realtimeSinceStartup;
        int pos = 0;
        while (Time.realtimeSinceStartup - start < timeout)
        {
            try { pos = Microphone.GetPosition(null); if (pos > 0) break; } catch { pos = 0; break; }
            yield return null;
        }
        if (!Microphone.IsRecording(null) || pos <= 0)
        {
            Debug.LogWarning($"[AudioSender] Microphone failed to start (pos={pos}).");
            try { Microphone.End(null); } catch { }
            microphoneClip = null;
            yield break;
        }
        lastPosition = 0;
        sending = true;
        frameId = 0;
        StartCoroutine(SendLoop());
    }

    // Stop and cleanup
    public void StopSending()
    {
        sending = false;
        if (Microphone.IsRecording(null)) Microphone.End(null);
        microphoneClip = null;
        lastPosition = 0;
    }

    // Main loop: read audio frames, convert to PCM16 and send
    private IEnumerator SendLoop()
    {
        if (microphoneClip == null) yield break;

        int samplesPerFrame = Mathf.Max(1, sampleRate * frameMilliseconds / 1000);
        float[] buffer = new float[samplesPerFrame * channels];

        while (sending)
        {
            if (!Microphone.IsRecording(null) || microphoneClip == null) { sending = false; yield break; }

            int pos = Microphone.GetPosition(null);
            if (pos < 0) { yield return null; continue; }

            int available = pos - lastPosition;
            if (available < 0) available += microphoneClip.samples;

            while (available >= samplesPerFrame && sending)
            {
                try { microphoneClip.GetData(buffer, lastPosition); } catch (Exception ex) { Debug.LogWarning($"[AudioSender] GetData failed: {ex.Message}"); available = 0; break; }

                int outBytes = samplesPerFrame * channels * 2;
                byte[] pcm = new byte[outBytes];
                int outIndex = 0;
                for (int i = 0; i < samplesPerFrame * channels; i++)
                {
                    short s = (short)Mathf.Clamp(Mathf.RoundToInt(buffer[i] * 32767f), short.MinValue, short.MaxValue);
                    pcm[outIndex++] = (byte)(s & 0xff);
                    pcm[outIndex++] = (byte)((s >> 8) & 0xff);
                }

                var packets = Packetizer.Fragment(pcm, frameId++, 2);
                foreach (var p in packets) transport?.SendAsync(p);

                lastPosition += samplesPerFrame;
                if (lastPosition >= microphoneClip.samples) lastPosition = 0;
                available -= samplesPerFrame;
            }

            yield return null;
        }
    }

    private void OnDisable() => StopSending();
}