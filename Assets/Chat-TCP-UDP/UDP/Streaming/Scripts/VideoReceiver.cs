using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Receives fragmented JPEG frames via UdpTransport, reassembles them and updates a RawImage.
/// </summary>
[RequireComponent(typeof(UdpTransport))]
public class VideoReceiver : MonoBehaviour
{
    public UdpTransport transport;
    public RawImage targetImage;

    private class FrameBuffer { public int frameId; public ushort total; public Dictionary<ushort, byte[]> chunks = new Dictionary<ushort, byte[]>(); public DateTime firstReceived = DateTime.UtcNow; }
    private readonly object lockObj = new object();
    private readonly Dictionary<int, FrameBuffer> frames = new Dictionary<int, FrameBuffer>();
    private readonly ConcurrentQueue<byte[]> readyFrames = new ConcurrentQueue<byte[]>();
    private bool subscribed = false;

    private void Awake() { transport = transport ?? GetComponent<UdpTransport>(); }
    private void OnEnable() { TrySubscribe(); }
    private void OnDisable()
    {
        if (subscribed && transport != null) transport.OnPacketReceived -= OnPacket;
        subscribed = false;
        ClearImage(); // clear frozen frame
    }

    private void TrySubscribe()
    {
        if (!subscribed && transport != null)
        {
            transport.OnPacketReceived += OnPacket;
            subscribed = true;
        }
    }

    private void OnPacket(byte[] data, System.Net.IPEndPoint src)
    {
        if (data == null || data.Length <= Packetizer.HeaderSize) return;
        Packetizer.ParseHeader(data, out int frameId, out ushort packetIndex, out ushort packetCount, out byte streamType);
        if (streamType != 1) return;

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

            var timeout = DateTime.UtcNow.AddSeconds(-2);
            var stale = new List<int>();
            foreach (var kv in frames) if (kv.Value.firstReceived < timeout) stale.Add(kv.Key);
            foreach (var id in stale) frames.Remove(id);
        }
    }

    private void Update()
    {
        TrySubscribe();
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
                    else Destroy(tex);
                }
                else Destroy(tex);
            }
            catch (Exception ex) { Debug.LogWarning($"[VideoReceiver] Frame error: {ex.Message}"); }
        }
    }

    public void ClearImage()
    {
        if (targetImage != null)
        {
            targetImage.texture = null;
            targetImage.color = Color.black;
        }
    }
}