using UnityEngine;

namespace TelloViewer
{
    public struct VoxelPoint
    {
        public float X;
        public float Y;
        public float Z;

        public VoxelPoint(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Vector3 ToUnityPosition()
        {
            return new Vector3(X, -Y, Z);
        }
    }

    public struct VoxelFrameMetadata
    {
        public uint FrameId;
        public int Pitch;
        public int Roll;
        public int Yaw;
        public int VoxelCount;
        public int ExpectedPackets;
        public int ReceivedPackets;

        public bool IsPartial => ReceivedPackets < ExpectedPackets;
    }
}
