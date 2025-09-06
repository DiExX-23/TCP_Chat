using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Captures microphone audio, converts to PCM16 and sends small frames (e.g., 20ms).
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

    private void Awake()
    {
        if (transport == null) transport = GetComponent<UdpTransport>();
    }

    public void StartSending()
    {
        if (sending || Microphone.devices.Length == 0) return;

        sending = true;
        microphoneClip = Microphone.Start(null, true, 1, sampleRate);
        StartCoroutine(SendLoop());
    }

    public void StopSending()
    {
        sending = false;
        if (Microphone.IsRecording(null)) Microphone.End(null);
    }

    private IEnumerator SendLoop()
    {
        int samplesPerFrame = sampleRate * frameMilliseconds / 1000;
        float[] buffer = new float[samplesPerFrame * channels];

        while (sending)
        {
            int pos = Microphone.GetPosition(null);
            int available = pos - lastPosition;
            if (available < 0) available += microphoneClip.samples;

            while (available >= samplesPerFrame)
            {
                microphoneClip.GetData(buffer, lastPosition);

                // convert float [-1,1] to PCM16
                byte[] pcm = new byte[samplesPerFrame * channels * 2];
                int outIndex = 0;
                for (int i = 0; i < buffer.Length; i++)
                {
                    short s = (short)Mathf.Clamp(buffer[i] * 32767f, short.MinValue, short.MaxValue);
                    pcm[outIndex++] = (byte)(s & 0xff);
                    pcm[outIndex++] = (byte)((s >> 8) & 0xff);
                }

                foreach (var p in Packetizer.Fragment(pcm, frameId++, 2))
                {
                    transport.SendAsync(p);
                }

                lastPosition += samplesPerFrame;
                if (lastPosition >= microphoneClip.samples) lastPosition = 0;
                available -= samplesPerFrame;
            }
            yield return null;
        }
    }

    private void OnDisable() => StopSending();
}