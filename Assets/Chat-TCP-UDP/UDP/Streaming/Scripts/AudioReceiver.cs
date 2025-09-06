using System.Collections.Generic;
using System;
using UnityEngine;
using System.Collections.Concurrent;

[RequireComponent(typeof(UdpTransport))]
public class AudioReceiver : MonoBehaviour
{
    public UdpTransport transport;
    public ConcurrentQueue<byte[]> audioQueue = new ConcurrentQueue<byte[]>();

    private class FrameBuffer
    {
        public int frameId;
        public ushort total;
        public Dictionary<ushort, byte[]> chunks = new Dictionary<ushort, byte[]>();
        public DateTime firstReceived = DateTime.UtcNow;
    }

    private readonly object lockObj = new object();
    private readonly Dictionary<int, FrameBuffer> frames = new Dictionary<int, FrameBuffer>();

    private void Awake()
    {
        if (transport == null) transport = GetComponent<UdpTransport>();
    }

    private void OnEnable() => transport.OnPacketReceived += OnPacket;
    private void OnDisable() => transport.OnPacketReceived -= OnPacket;

    private void OnPacket(byte[] data, System.Net.IPEndPoint src)
    {
        if (data.Length <= Packetizer.HeaderSize) return;

        Packetizer.ParseHeader(data, out int frameId, out ushort packetIndex, out ushort packetCount, out byte streamType);
        if (streamType != 2) return;

        int payloadOffset = Packetizer.HeaderSize;
        var payload = new byte[data.Length - payloadOffset];
        Buffer.BlockCopy(data, payloadOffset, payload, 0, payload.Length);

        lock (lockObj)
        {
            if (!frames.TryGetValue(frameId, out FrameBuffer buf))
            {
                buf = new FrameBuffer { frameId = frameId, total = packetCount };
                frames[frameId] = buf;
            }
            buf.chunks[packetIndex] = payload;

            if (buf.chunks.Count == buf.total)
            {
                int totalLen = 0;
                foreach (var c in buf.chunks.Values) totalLen += c.Length;

                var all = new byte[totalLen];
                int pos = 0;
                for (ushort i = 0; i < buf.total; i++)
                {
                    var c = buf.chunks[i];
                    Buffer.BlockCopy(c, 0, all, pos, c.Length);
                    pos += c.Length;
                }
                audioQueue.Enqueue(all);
                frames.Remove(frameId);
            }

            // cleanup old frames
            var timeout = DateTime.UtcNow.AddSeconds(-2);
            var stale = new List<int>();
            foreach (var kv in frames) if (kv.Value.firstReceived < timeout) stale.Add(kv.Key);
            foreach (var id in stale) frames.Remove(id);
        }
    }
}