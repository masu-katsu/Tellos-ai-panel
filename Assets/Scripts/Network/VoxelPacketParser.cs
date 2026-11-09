using System;

namespace TelloViewer
{
    public struct VoxelPacketHeader
    {
        public uint FrameId;
        public ushort PacketId;
        public ushort TotalPackets;
        public ushort VoxelCount;
        public short Pitch;
        public short Roll;
        public short Yaw;
    }

    public static class VoxelPacketParser
    {
        public const int HeaderSize = 16;
        public const int MaximumVoxelsPerPacket = 100;
        public const int MaximumPacketsPerFrame = 40;
        public const int MaximumDatagramSize = HeaderSize + MaximumVoxelsPerPacket * 12;

        public static bool TryParse(
            byte[] data,
            int length,
            float maxCoordinateMeters,
            VoxelPoint[] pointBuffer,
            out VoxelPacketHeader header)
        {
            header = default(VoxelPacketHeader);
            if (data == null || pointBuffer == null || length < HeaderSize || length > MaximumDatagramSize)
            {
                return false;
            }

            header.FrameId = ReadUInt32(data, 0);
            header.PacketId = ReadUInt16(data, 4);
            header.TotalPackets = ReadUInt16(data, 6);
            header.VoxelCount = ReadUInt16(data, 8);
            header.Pitch = ReadInt16(data, 10);
            header.Roll = ReadInt16(data, 12);
            header.Yaw = ReadInt16(data, 14);

            if (header.TotalPackets < 1 ||
                header.TotalPackets > MaximumPacketsPerFrame ||
                header.PacketId >= header.TotalPackets ||
                header.VoxelCount > MaximumVoxelsPerPacket ||
                header.VoxelCount > pointBuffer.Length ||
                length != HeaderSize + header.VoxelCount * 12)
            {
                return false;
            }

            bool emptyFrame = header.TotalPackets == 1 && header.PacketId == 0 && header.VoxelCount == 0;
            if (header.VoxelCount == 0 && !emptyFrame)
            {
                return false;
            }

            for (int i = 0; i < header.VoxelCount; i++)
            {
                int offset = HeaderSize + i * 12;
                float x = ReadSingle(data, offset);
                float y = ReadSingle(data, offset + 4);
                float z = ReadSingle(data, offset + 8);
                if (!IsValidCoordinate(x, maxCoordinateMeters) ||
                    !IsValidCoordinate(y, maxCoordinateMeters) ||
                    !IsValidCoordinate(z, maxCoordinateMeters))
                {
                    return false;
                }

                pointBuffer[i] = new VoxelPoint(x, y, z);
            }

            return true;
        }

        internal static ushort ReadUInt16(byte[] data, int offset)
        {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        internal static short ReadInt16(byte[] data, int offset)
        {
            return unchecked((short)ReadUInt16(data, offset));
        }

        internal static uint ReadUInt32(byte[] data, int offset)
        {
            return (uint)data[offset] |
                   ((uint)data[offset + 1] << 8) |
                   ((uint)data[offset + 2] << 16) |
                   ((uint)data[offset + 3] << 24);
        }

        private static float ReadSingle(byte[] data, int offset)
        {
            uint bits = ReadUInt32(data, offset);
            return BitConverter.Int32BitsToSingle(unchecked((int)bits));
        }

        private static bool IsValidCoordinate(float value, float maximum)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && Math.Abs(value) <= maximum;
        }
    }
}
