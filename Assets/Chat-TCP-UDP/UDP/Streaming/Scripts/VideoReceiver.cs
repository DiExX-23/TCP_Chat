// VideoReceiver.cs
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Receives fragmented JPEG frames via UdpTransport, reassembles and updates RawImage on main thread.
/// Uses ConcurrentQueue to hand over assembled frames to Unity thread.
/// </summary>
[RequireComponent(typeof(UdpTransport))]
public class VideoReceiver : MonoBehaviour
{
    public UdpTransport transport;
    public RawImage targetImage;

    private readonly object lockObj = new object();
    private class FrameBuffer { public int frameId; public ushort total; public Dictionary<ushort, byte[]> chunks = new Dictionary<ushort, byte[]>(); public DateTime firstReceived = DateTime.UtcNow; }
    private Dictionary<int, FrameBuffer> frames = new Dictionary<int, FrameBuffer>();

    private ConcurrentQueue<byte[]> readyFrames = new ConcurrentQueue<byte[]>(); // complete JPGs to process on main thread

    private void Awake() { transport = transport ?? GetComponent<UdpTransport>(); }

    private void OnEnable() { if (transport != null) transport.OnPacketReceived += OnPacket; }
    private void OnDisable() { if (transport != null) transport.OnPacketReceived -= OnPacket; }

    // Called on transport receive thread: reassemble fragments and enqueue full images
    private void OnPacket(byte[] data, System.Net.IPEndPoint src)
    {
        if (data == null || data.Length <= Packetizer.HeaderSize) return;

        Packetizer.ParseHeader(data, out int frameId, out ushort packetIndex, out ushort packetCount, out byte streamType);
        if (streamType != 1) return; // only video

        int payloadOffset = Packetizer.HeaderSize;
        int payloadLen = data.Length - payloadOffset;
        var payload = new byte[payloadLen];
        Buffer.BlockCopy(data, payloadOffset, payload, 0, payloadLen);

        lock (lockObj)
        {
            if (!frames.TryGetValue(frameId, out FrameBuffer fb))
            {
                fb = new FrameBuffer { frameId = frameId, total = packetCount, firstReceived = DateTime.UtcNow };
                frames[frameId] = fb;
            }
            if (!fb.chunks.ContainsKey(packetIndex)) fb.chunks[packetIndex] = payload;

            if (fb.chunks.Count == fb.total)
            {
                int totalLen = 0;
                for (ushort i = 0; i < fb.total; i++) totalLen += fb.chunks[i].Length;
                var all = new byte[totalLen];
                int pos = 0;
                for (ushort i = 0; i < fb.total; i++)
                {
                    var c = fb.chunks[i];
                    Buffer.BlockCopy(c, 0, all, pos, c.Length);
                    pos += c.Length;
                }
                readyFrames.Enqueue(all);
                frames.Remove(frameId);
            }

            // cleanup stale
            var timeout = DateTime.UtcNow.AddSeconds(-2);
            var stale = new List<int>();
            foreach (var kv in frames) if (kv.Value.firstReceived < timeout) stale.Add(kv.Key);
            foreach (var id in stale) frames.Remove(id);
        }
    }

    // On main thread, process ready frames and update UI
    private void Update()
    {
        while (readyFrames.TryDequeue(out var jpg))
        {
            try
            {
                var tex = new Texture2D(2, 2);
                if (ImageConversion.LoadImage(tex, jpg))
                {
                    if (targetImage != null)
                    {
                        targetImage.texture = tex;
                        targetImage.color = Color.white;
                        targetImage.enabled = true;
                    }
                }
                else
                {
                    UnityEngine.Object.Destroy(tex);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VideoReceiver] Failed to load image: {ex.Message}");
            }
        }
    }
}