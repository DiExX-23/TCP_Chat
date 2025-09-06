using UnityEngine;
using System.Collections.Concurrent;
using System;

[RequireComponent(typeof(AudioSource))]
public class AudioPlayer : MonoBehaviour
{
    public int sampleRate = 16000;
    public ConcurrentQueue<byte[]> inputQueue; // assigned from AudioReceiver.audioQueue

    private AudioSource audioSource;
    private float[] floatBuffer = new float[4096];
    private short[] pcmShortBuffer = new short[4096];

    public void Initialize(ConcurrentQueue<byte[]> queue)
    {
        inputQueue = queue;
    }

    private void Awake()
    {
        // Ensure AudioSource is available
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 0f; // 2D sound
        audioSource.volume = 1f;
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (inputQueue == null) return;
        int needed = data.Length;
        int writePos = 0;

        while (writePos < needed)
        {
            if (!inputQueue.TryDequeue(out var bytes))
            {
                // Fill silence if no data
                for (int i = writePos; i < needed; i++) data[i] = 0f;
                return;
            }

            int samples = bytes.Length / 2; // 2 bytes per sample
            for (int i = 0; i < samples && writePos < needed; i++)
            {
                short s = (short)(bytes[2 * i] | (bytes[2 * i + 1] << 8));
                data[writePos++] = s / 32768f;
            }

            // Handle leftover bytes
            if (samples > 0 && writePos >= needed && samples * 2 > (needed * 2))
            {
                int consumedBytes = (needed * 2);
                int remainBytes = bytes.Length - consumedBytes;
                if (remainBytes > 0)
                {
                    var rem = new byte[remainBytes];
                    Buffer.BlockCopy(bytes, consumedBytes, rem, 0, remainBytes);
                    inputQueue.Enqueue(rem);
                }
            }
        }
    }
}