// AudioPlayer.cs
using System;
using System.Collections.Concurrent;
using UnityEngine;

/// <summary>
/// Pulls PCM16 frames from a ConcurrentQueue and feeds Unity audio system via OnAudioFilterRead.
/// Resamples inputSampleRate -> AudioSettings.outputSampleRate via linear interpolation.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioPlayer : MonoBehaviour
{
    public int inputSampleRate = 16000;              // incoming PCM sample rate
    public int inputChannels = 1;                    // expected incoming channels

    public ConcurrentQueue<byte[]> inputQueue;       // assigned by StreamingManager

    private float[] ring;                            // mono float ring buffer
    private int ringCapacity;
    private int ringHead = 0;
    private int ringCount = 0;

    private float outputSampleRate;
    private float resampleIncrement;                 // input samples per output sample
    private float resamplePos = 0f;

    public int playedFramesCount = 0;                // debug counter
    public float outputRms = 0f;                     // RMS of last buffer

    private const int MIN_RING_SECONDS = 1;

    private AudioSource audioSource;

    public void Initialize(ConcurrentQueue<byte[]> queue) { inputQueue = queue; }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource != null) { audioSource.playOnAwake = false; audioSource.loop = true; audioSource.spatialBlend = 0f; }

        outputSampleRate = AudioSettings.outputSampleRate;
        resampleIncrement = (float)inputSampleRate / outputSampleRate;

        int maxRate = Mathf.Max(Mathf.CeilToInt(outputSampleRate), inputSampleRate);
        ringCapacity = maxRate * Mathf.Max(2, MIN_RING_SECONDS);
        ring = new float[ringCapacity];

        ringHead = ringCount = 0;
        resamplePos = 0f;
    }

    // append float to ring
    private void RingAppend(float value)
    {
        if (ringCount >= ringCapacity)
        {
            ringHead = (ringHead + 1) % ringCapacity;
            ringCount--;
            if (resamplePos >= 1f) resamplePos -= 1f; else resamplePos = 0f;
        }
        int writeIndex = ringHead + ringCount;
        if (writeIndex >= ringCapacity) writeIndex -= ringCapacity;
        ring[writeIndex] = value;
        ringCount++;
    }

    private float RingGet(int index)
    {
        if (index < 0 || index >= ringCount) return 0f;
        int idx = ringHead + index;
        if (idx >= ringCapacity) idx -= ringCapacity;
        return ring[idx];
    }

    // Audio thread callback
    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (inputQueue != null)
        {
            while (inputQueue.TryDequeue(out var bytes))
            {
                if (bytes == null || bytes.Length == 0) continue;
                int samples = bytes.Length / 2;
                if (samples > (ringCapacity - ringCount))
                {
                    int overflow = samples - (ringCapacity - ringCount);
                    if (overflow >= ringCount) { ringHead = 0; ringCount = 0; resamplePos = 0f; }
                    else { ringHead = (ringHead + overflow) % ringCapacity; ringCount -= overflow; if (resamplePos >= overflow) resamplePos -= overflow; else resamplePos = 0f; }
                }
                for (int i = 0; i < samples; i++)
                {
                    int b0 = bytes[2 * i];
                    int b1 = bytes[2 * i + 1];
                    short s = (short)(b0 | (b1 << 8));
                    float f = s / 32768f;
                    RingAppend(f);
                }
            }
        }

        int outChannels = channels;
        int outFrames = data.Length / outChannels;
        float inToOut = resampleIncrement;
        float sumSq = 0f;

        for (int frame = 0; frame < outFrames; frame++)
        {
            float sample = 0f;
            if (ringCount == 0) sample = 0f;
            else
            {
                int idxFloor = (int)Mathf.Floor(resamplePos);
                float frac = resamplePos - idxFloor;
                if (idxFloor >= ringCount) sample = 0f;
                else
                {
                    float s0 = RingGet(idxFloor);
                    float s1 = (idxFloor + 1 < ringCount) ? RingGet(idxFloor + 1) : s0;
                    sample = s0 + (s1 - s0) * frac;
                }
            }

            int baseIdx = frame * outChannels;
            for (int ch = 0; ch < outChannels; ch++) data[baseIdx + ch] = sample;

            sumSq += sample * sample;
            resamplePos += inToOut;
        }

        int consumed = (int)Mathf.Floor(resamplePos);
        if (consumed > 0)
        {
            if (consumed >= ringCount) { ringHead = 0; ringCount = 0; resamplePos = 0f; }
            else { ringHead = (ringHead + consumed) % ringCapacity; ringCount -= consumed; resamplePos -= consumed; }
        }

        playedFramesCount++;
        outputRms = (outFrames > 0) ? Mathf.Sqrt(sumSq / outFrames) : 0f;
    }
}