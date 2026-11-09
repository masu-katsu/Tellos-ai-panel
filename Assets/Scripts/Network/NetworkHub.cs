using System;
using UnityEngine;

namespace TelloViewer
{
    public sealed class NetworkHub : MonoBehaviour
    {
        [SerializeField] private AppSettings settings;

        private LatestVoxelFrameStore _voxelStore;
        private LatestVideoFrameStore _videoStore;
        private StreamStatistics _voxelStatistics;
        private StreamStatistics _videoStatistics;
        private VoxelFrameAssembler _voxelAssembler;
        private VideoFrameAssembler _videoAssembler;
        private UdpReceiver _voxelReceiver;
        private UdpReceiver _videoReceiver;
        private bool _initialized;

        public LatestVoxelFrameStore VoxelStore => _voxelStore;
        public LatestVideoFrameStore VideoStore => _videoStore;
        public StreamStatistics VoxelStatistics => _voxelStatistics;
        public StreamStatistics VideoStatistics => _videoStatistics;
        public string VoxelReceiverError => _voxelReceiver == null ? null : _voxelReceiver.LastError;
        public string VideoReceiverError => _videoReceiver == null ? null : _videoReceiver.LastError;
        public bool IsVoxelBound => _voxelReceiver != null && _voxelReceiver.IsBound;
        public bool IsVideoBound => _videoReceiver != null && _videoReceiver.IsBound;
        public int NoSignalTimeoutMilliseconds => settings == null ? 1000 : settings.NoSignalTimeoutMs;
        public float VoxelSizeMeters => settings == null ? 0.07f : settings.VoxelSizeMeters;

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            if (settings == null)
            {
                settings = GetComponent<AppSettings>();
            }

            if (settings == null)
            {
                throw new InvalidOperationException("NetworkHub requires an AppSettings component on the same GameObject.");
            }

            _voxelStore = new LatestVoxelFrameStore(settings.MaxVoxelsPerFrame);
            _videoStore = new LatestVideoFrameStore(settings.MaxJpegBytes);
            _voxelStatistics = new StreamStatistics();
            _videoStatistics = new StreamStatistics();
            _voxelAssembler = new VoxelFrameAssembler(
                _voxelStore,
                _voxelStatistics,
                settings.MaxVoxelsPerFrame,
                settings.MaxCoordinateMeters,
                settings.VoxelAssemblyTimeoutMs);
            _videoAssembler = new VideoFrameAssembler(
                _videoStore,
                _videoStatistics,
                settings.MaxJpegBytes,
                settings.MaxVideoWidth,
                settings.MaxVideoHeight,
                settings.VideoAssemblyTimeoutMs);
            _voxelReceiver = new UdpReceiver(
                settings.VoxelPort,
                settings.ReceiveWakeIntervalMs,
                _voxelAssembler.ProcessDatagram,
                _voxelAssembler.Tick);
            _videoReceiver = new UdpReceiver(
                settings.VideoPort,
                settings.ReceiveWakeIntervalMs,
                _videoAssembler.ProcessDatagram,
                _videoAssembler.Tick);
            _initialized = true;
        }

        private void OnEnable()
        {
            if (_voxelReceiver != null)
            {
                _voxelAssembler.Tick();
                _voxelReceiver.Start();
            }

            if (_videoReceiver != null)
            {
                _videoAssembler.Tick();
                _videoReceiver.Start();
            }
        }

        private void OnDisable()
        {
            StopReceivers();
        }

        private void OnDestroy()
        {
            StopReceivers();
            if (_voxelReceiver != null)
            {
                _voxelReceiver.Dispose();
            }

            if (_videoReceiver != null)
            {
                _videoReceiver.Dispose();
            }
        }

        private void StopReceivers()
        {
            if (_voxelReceiver != null)
            {
                _voxelReceiver.Stop();
            }

            if (_videoReceiver != null)
            {
                _videoReceiver.Stop();
            }
        }
    }
}
