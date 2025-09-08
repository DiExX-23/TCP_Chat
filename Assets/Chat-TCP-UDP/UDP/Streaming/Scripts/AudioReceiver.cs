using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;

/// <summary>
/// Receives fragmented PCM frames, reassembles them and enqueues byte[] for AudioPlayer.
/// Robust subscription handling to ensure client subscribes even if transport assigned later.
/// </summary>
[RequireComponent(typeof(UdpTransport))]
public class AudioReceiver : MonoBehaviour
{
    // transport used to receive packets
    public UdpTransport transport;

    // public queue exposed for the AudioPlayer to consume
    public ConcurrentQueue<byte[]> audioQueue = new ConcurrentQueue<byte[]>();

    // internal frame reassembly structure
    private class FrameBuffer { public int frameId; public ushort total; public Dictionary<ushort, byte[]> chunks = new Dictionary<ushort, byte[]>(); public DateTime firstReceived = DateTime.UtcNow; }

    private readonly object lockObj = new object();                      // protect frames dict
    private readonly Dictionary<int, FrameBuffer> frames = new Dictionary<int, FrameBuffer>(); // partial frames map

    private bool subscribed = false;                                     // subscription state

    // Try to resolve transport automatically if not assigned in inspector
    private void Awake()
    {
        transport = transport ?? GetComponent<UdpTransport>(); // fallback to local transport
    }

    // Subscribe when enabled (if transport already present)
    private void OnEnable()
    {
        TrySubscribe();
    }

    // Unsubscribe and clear buffers when disabled
    private void OnDisable()
    {
        TryUnsubscribe();
        ClearQueues(); // avoid stale audio frames when stopped
    }

    // Ensure subscription if transport is assigned later; called from Update or externally
    private void TrySubscribe()
    {
        if (!subscribed && transport != null)
        {
            transport.OnPacketReceived += OnPacket; // subscribe to transport packets
            subscribed = true;
            Debug.Log("[AudioReceiver] Subscribed to transport.");
        }
    }

    // Unsubscribe safely
    private void TryUnsubscribe()
    {
        if (subscribed && transport != null)
        {
            try { transport.OnPacketReceived -= OnPacket; } catch { }
        }
        subscribed = false;
    }

    // Drain audioQueue and partial frames to avoid leftover audio after stop
    private void ClearQueues()
    {
        // clear audioQueue
        while (audioQueue.TryDequeue(out _)) { }
        // clear reassembly buffers
        lock (lockObj) { frames.Clear(); }
    }

    // Regularly ensure subscription in case transport is assigned after enable
    private void Update()
    {
        TrySubscribe();
    }

    // Packet handler — can be called on receive thread; keep thread-safe and non-Unity
    private void OnPacket(byte[] data, System.Net.IPEndPoint src)
    {
        if (data == null || data.Length <= Packetizer.HeaderSize) return; // ignore invalid packets

        // parse header (frameId, packetIndex, packetCount, streamType)
        Packetizer.ParseHeader(data, out int frameId, out ushort packetIndex, out ushort packetCount, out byte streamType);
        if (streamType != 2) return; // only handle audio streamType == 2

        int payloadOffset = Packetizer.HeaderSize;
        int payloadLen = data.Length - payloadOffset;
        var payload = new byte[payloadLen];
        Buffer.BlockCopy(data, payloadOffset, payload, 0, payloadLen); // copy payload

        lock (lockObj)
        {
            // get or create frame buffer
            if (!frames.TryGetValue(frameId, out FrameBuffer buf))
            {
                buf = new FrameBuffer { frameId = frameId, total = packetCount, firstReceived = DateTime.UtcNow };
                frames[frameId] = buf;
            }

            // store chunk if not already present
            if (!buf.chunks.ContainsKey(packetIndex)) buf.chunks[packetIndex] = payload;

            // if all chunks received, reassemble and enqueue for playback
            if (buf.chunks.Count == buf.total)
            {
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

                audioQueue.Enqueue(all); // queue assembled PCM frame for AudioPlayer
                frames.Remove(frameId);  // free memory for this frame
            }

            // cleanup stale partial frames older than 2 seconds to avoid memory growth
            var timeout = DateTime.UtcNow.AddSeconds(-2);
            var stale = new List<int>();
            foreach (var kv in frames) if (kv.Value.firstReceived < timeout) stale.Add(kv.Key);
            foreach (var id in stale) frames.Remove(id);
        }
    }
}