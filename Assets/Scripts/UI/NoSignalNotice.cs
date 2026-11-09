using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;

namespace TelloViewer
{
    public sealed class NoSignalNotice : MonoBehaviour
    {
        [SerializeField] private NetworkHub networkHub;
        [SerializeField] private Text noticeText;
        private long _startedAtTicks;

        private void Awake()
        {
            _startedAtTicks = Stopwatch.GetTimestamp();
            if (networkHub == null)
            {
                UnityEngine.Debug.LogError("NoSignalNotice requires a NetworkHub reference in the scene.", this);
            }

            if (noticeText == null)
            {
                noticeText = GetComponent<Text>();
            }
        }

        private void Update()
        {
            if (networkHub == null || noticeText == null)
            {
                return;
            }

            long now = Stopwatch.GetTimestamp();
            double timeoutTicks = networkHub.NoSignalTimeoutMilliseconds * Stopwatch.Frequency / 1000.0;
            StreamStatisticsSnapshot voxel = networkHub.VoxelStatistics.GetSnapshot();
            StreamStatisticsSnapshot video = networkHub.VideoStatistics.GetSnapshot();
            bool voxelMissing = IsMissing(voxel.LastValidPacketTicks, now, timeoutTicks);
            bool videoMissing = IsMissing(video.LastValidPacketTicks, now, timeoutTicks);

            if (voxelMissing && videoMissing)
            {
                noticeText.text = "ボクセル / 映像: 信号なし";
            }
            else if (voxelMissing)
            {
                noticeText.text = "ボクセル: 信号なし";
            }
            else if (videoMissing)
            {
                noticeText.text = "映像: 信号なし";
            }
            else
            {
                noticeText.text = string.Empty;
            }
        }

        private bool IsMissing(long lastPacketTicks, long now, double timeoutTicks)
        {
            long referenceTicks = lastPacketTicks == 0 ? _startedAtTicks : lastPacketTicks;
            return now - referenceTicks >= timeoutTicks;
        }
    }
}
