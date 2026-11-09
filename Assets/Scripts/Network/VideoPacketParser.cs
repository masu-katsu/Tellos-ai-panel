namespace TelloViewer
{
    public struct VideoPacketHeader
    {
        public uint FrameId;
        public ushort PacketId;
        public ushort TotalPackets;
        public ushort PayloadLength;
    }

    public static class VideoPacketParser
    {
        public const int HeaderSize = 10;
        public const int MaximumPayloadSize = 1200;
        public const int MaximumPacketsPerFrame = 219;
        public const int MaximumJpegSize = 262144;
        public const int MaximumDatagramSize = HeaderSize + MaximumPayloadSize;

        public static bool TryParse(byte[] data, int length, out VideoPacketHeader header)
        {
            header = default(VideoPacketHeader);
            if (data == null || length < HeaderSize || length > MaximumDatagramSize)
            {
                return false;
            }

            header.FrameId = VoxelPacketParser.ReadUInt32(data, 0);
            header.PacketId = VoxelPacketParser.ReadUInt16(data, 4);
            header.TotalPackets = VoxelPacketParser.ReadUInt16(data, 6);
            header.PayloadLength = VoxelPacketParser.ReadUInt16(data, 8);

            return header.PayloadLength >= 1 &&
                   header.PayloadLength <= MaximumPayloadSize &&
                   header.TotalPackets >= 1 &&
                   header.TotalPackets <= MaximumPacketsPerFrame &&
                   header.PacketId < header.TotalPackets &&
                   length == HeaderSize + header.PayloadLength &&
                   (header.PacketId == header.TotalPackets - 1 || header.PayloadLength == MaximumPayloadSize);
        }
    }
}
