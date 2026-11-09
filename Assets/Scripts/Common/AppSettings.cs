using UnityEngine;

namespace TelloViewer
{
    public sealed class AppSettings : MonoBehaviour
    {
        [SerializeField, Min(1)] private int voxelPort = 9999;
        [SerializeField, Min(1)] private int videoPort = 9998;
        [SerializeField, Min(0.001f)] private float voxelSizeMeters = 0.07f;
        [SerializeField, Min(1)] private int maxVoxelsPerFrame = 4000;
        [SerializeField, Min(1)] private int maxJpegBytes = 262144;
        [SerializeField, Min(1)] private int voxelAssemblyTimeoutMs = 50;
        [SerializeField, Min(1)] private int videoAssemblyTimeoutMs = 100;
        [SerializeField, Min(1)] private int receiveWakeIntervalMs = 25;
        [SerializeField, Min(1)] private int noSignalTimeoutMs = 1000;
        [SerializeField, Min(0.1f)] private float maxCoordinateMeters = 50f;
        [SerializeField, Min(1)] private int maxVideoWidth = 640;
        [SerializeField, Min(1)] private int maxVideoHeight = 480;

        public int VoxelPort => voxelPort;
        public int VideoPort => videoPort;
        public float VoxelSizeMeters => voxelSizeMeters;
        public int MaxVoxelsPerFrame => maxVoxelsPerFrame;
        public int MaxJpegBytes => maxJpegBytes;
        public int VoxelAssemblyTimeoutMs => voxelAssemblyTimeoutMs;
        public int VideoAssemblyTimeoutMs => videoAssemblyTimeoutMs;
        public int ReceiveWakeIntervalMs => receiveWakeIntervalMs;
        public int NoSignalTimeoutMs => noSignalTimeoutMs;
        public float MaxCoordinateMeters => maxCoordinateMeters;
        public int MaxVideoWidth => maxVideoWidth;
        public int MaxVideoHeight => maxVideoHeight;

        private void OnValidate()
        {
            voxelPort = Mathf.Clamp(voxelPort, 1, 65535);
            videoPort = Mathf.Clamp(videoPort, 1, 65535);
            maxVoxelsPerFrame = Mathf.Clamp(maxVoxelsPerFrame, 1, 4000);
            maxJpegBytes = Mathf.Clamp(maxJpegBytes, 1, 262144);
            voxelAssemblyTimeoutMs = Mathf.Max(1, voxelAssemblyTimeoutMs);
            videoAssemblyTimeoutMs = Mathf.Max(1, videoAssemblyTimeoutMs);
            receiveWakeIntervalMs = Mathf.Max(1, receiveWakeIntervalMs);
            noSignalTimeoutMs = Mathf.Max(1, noSignalTimeoutMs);
            maxVideoWidth = Mathf.Clamp(maxVideoWidth, 1, 640);
            maxVideoHeight = Mathf.Clamp(maxVideoHeight, 1, 480);
        }
    }
}
