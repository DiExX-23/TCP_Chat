using System;
using System.Collections.Generic;

/// <summary>
/// Small packet header and fragmentation helpers.
/// Header (12 bytes): [int frameId (4)] [ushort packetIndex (2)] [ushort packetCount (2)] [byte streamType (1)] [3 bytes reserved]
/// </summary>
public static class Packetizer
{
    public const int HeaderSize = 12;
    public const int DefaultMtu = 1200; // safe payload target

    public static byte[] CreateHeader(int frameId, ushort packetIndex, ushort packetCount, byte streamType)
    {
        var header = new byte[HeaderSize];
        Buffer.BlockCopy(BitConverter.GetBytes(frameId), 0, header, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(packetIndex), 0, header, 4, 2);
        Buffer.BlockCopy(BitConverter.GetBytes(packetCount), 0, header, 6, 2);
        header[8] = streamType;
        // header[9..11] reserved as zeros
        return header;
    }

    public static void ParseHeader(byte[] buffer, out int frameId, out ushort packetIndex, out ushort packetCount, out byte streamType)
    {
        frameId = BitConverter.ToInt32(buffer, 0);
        packetIndex = BitConverter.ToUInt16(buffer, 4);
        packetCount = BitConverter.ToUInt16(buffer, 6);
        streamType = buffer[8];
    }

    public static List<byte[]> Fragment(byte[] raw, int frameId, byte streamType, int mtu = DefaultMtu)
    {
        int chunkPayload = mtu - HeaderSize;
        if (chunkPayload <= 0) throw new ArgumentException("MTU too small");

        int totalPackets = (raw.Length + chunkPayload - 1) / chunkPayload;
        var list = new List<byte[]>(totalPackets);
        for (ushort i = 0; i < totalPackets; i++)
        {
            int offset = i * chunkPayload;
            int len = Math.Min(chunkPayload, raw.Length - offset);
            var header = CreateHeader(frameId, i, (ushort)totalPackets, streamType);
            var pkt = new byte[header.Length + len];
            Buffer.BlockCopy(header, 0, pkt, 0, header.Length);
            Buffer.BlockCopy(raw, offset, pkt, header.Length, len);
            list.Add(pkt);
        }
        return list;
    }
}