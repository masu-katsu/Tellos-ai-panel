using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;

namespace TelloViewer
{
    public sealed class StatusView : MonoBehaviour
    {
        [SerializeField] private NetworkHub networkHub;
        [SerializeField] private Text voxelStatusText;
        [SerializeField] private Text videoStatusText;

        private double _noSignalTimeoutTicks;
        private float _nextUpdateTime;
        private long _startedAtTicks;

        private void Awake()
        {
            _startedAtTicks = Stopwatch.GetTimestamp();
            if (networkHub == null)
            {
                UnityEngine.Debug.LogError("StatusView requires a NetworkHub reference in the scene.", this);
            }
        }

        private void Update()
        {
            if (networkHub == null || Time.unscaledTime < _nextUpdateTime)
            {
                return;
            }

            _nextUpdateTime = Time.unscaledTime + 0.25f;
            if (_noSignalTimeoutTicks == 0)
            {
                _noSignalTimeoutTicks = networkHub.NoSignalTimeoutMilliseconds * Stopwatch.Frequency / 1000.0;
            }

            long now = Stopwatch.GetTimestamp();
            if (voxelStatusText != null)
            {
                voxelStatusText.text = FormatStatus(
                    "Voxel",
                    networkHub.VoxelStatistics.GetSnapshot(),
                    networkHub.VoxelReceiverError,
                    now);
            }

            if (videoStatusText != null)
            {
                videoStatusText.text = FormatStatus(
                    "Video",
                    networkHub.VideoStatistics.GetSnapshot(),
                    networkHub.VideoReceiverError,
                    now);
            }
        }

        private string FormatStatus(
            string streamName,
            StreamStatisticsSnapshot statistics,
            string receiverError,
            long now)
        {
            if (!string.IsNullOrEmpty(receiverError))
            {
                return streamName + ": ERROR - " + receiverError;
            }

            bool hasSignal = statistics.LastValidPacketTicks != 0 &&
                             now - statistics.LastValidPacketTicks < _noSignalTimeoutTicks;
            long referenceTicks = statistics.LastValidPacketTicks == 0
                ? _startedAtTicks
                : statistics.LastValidPacketTicks;
            bool noSignal = now - referenceTicks >= _noSignalTimeoutTicks;
            string state = hasSignal ? "受信中" : (noSignal ? "信号なし" : "待受中");
            return string.Format(
                "{0}: {1}  {2:F0} FPS  欠落 {3:F1}%",
                streamName,
                state,
                statistics.FramesPerSecond,
                statistics.PacketLossPercent);
        }
    }
}
