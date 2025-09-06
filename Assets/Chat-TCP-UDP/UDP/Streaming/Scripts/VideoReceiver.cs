using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reassemble fragments for video frames (streamType==1) and decode JPEG to Texture2D on main thread.
/// Provide a RawImage target for display.
/// </summary>
[RequireComponent(typeof(UdpTransport))]
public class VideoReceiver : MonoBehaviour
{
    public UdpTransport transport;
    public RawImage targetImage;

    private class FrameBuffer
    {
        public int frameId;
        public ushort total;
        public Dictionary<ushort, byte[]> chunks = new Dictionary<ushort, byte[]>();
        public DateTime firstReceived = DateTime.UtcNow;
    }

    private readonly object lockObj = new object();
    private Dictionary<int, FrameBuffer> frames = new Dictionary<int, FrameBuffer>();
    private Queue<byte[]> readyQueue = new Queue<byte[]>();

    private void Awake()
    {
        transport = transport ?? GetComponent<UdpTransport>();
    }

    private void OnEnable()
    {
        transport.OnPacketReceived += OnPacket;
    }

    private void OnDisable()
    {
        transport.OnPacketReceived -= OnPacket;
    }

    private void OnPacket(byte[] data, System.Net.IPEndPoint src)
    {
        // parse header
        if (data.Length <= Packetizer.HeaderSize) return;
        Packetizer.ParseHeader(data, out int frameId, out ushort packetIndex, out ushort packetCount, out byte streamType);
        if (streamType != 1) return; // not video

        int payloadOffset = Packetizer.HeaderSize;
        int payloadLen = data.Length - payloadOffset;
        var payload = new byte[payloadLen];
        Buffer.BlockCopy(data, payloadOffset, payload, 0, payloadLen);

        lock (lockObj)
        {
            if (!frames.TryGetValue(frameId, out FrameBuffer buf))
            {
                buf = new FrameBuffer { frameId = frameId, total = packetCount };
                frames[frameId] = buf;
            }
            if (!buf.chunks.ContainsKey(packetIndex))
            {
                buf.chunks[packetIndex] = payload;
            }

            // if full, assemble
            if (buf.chunks.Count == buf.total)
            {
                // assemble in order
                int totalLen = 0;
                for (ushort i = 0; i < buf.total; i++) totalLen += buf.chunks[i].Length;
                var all = new byte[totalLen];
                int pos = 0;
                for (ushort i = 0; i < buf.total; i++)
                {
                    var c = buf.chunks[i];
                    Buffer.BlockCopy(c, 0, all, pos, c.Length);
                    pos += c.Length;
                }
                readyQueue.Enqueue(all);
                frames.Remove(frameId);
            }
            // cleanup old partial frames
            var timeout = DateTime.UtcNow.AddSeconds(-2);
            var stale = new List<int>();
            foreach (var kv in frames)
            {
                if (kv.Value.firstReceived < timeout) stale.Add(kv.Key);
            }
            foreach (var id in stale) frames.Remove(id);
        }
    }

    private void Update()
    {
        // decode one ready frame per update to avoid stalling
        if (readyQueue.Count > 0)
        {
            byte[] jpg;
            lock (lockObj) { jpg = readyQueue.Dequeue(); }
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (ImageConversion.LoadImage(tex, jpg))
            {
                if (targetImage != null) targetImage.texture = tex;
            }
            else
            {
                Debug.LogWarning("Failed to LoadImage on received JPG");
                Destroy(tex);
            }
        }
    }
}