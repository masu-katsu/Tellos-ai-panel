using System;
using System.Diagnostics;

namespace TelloViewer
{
    public struct StreamStatisticsSnapshot
    {
        public long LastValidPacketTicks;
        public long MalformedPackets;
        public long DuplicatePackets;
        public long StalePackets;
        public long TimedOutFrames;
        public long FailedFrames;
        public long DecodeFailures;
        public float FramesPerSecond;
        public float PacketLossPercent;
    }

    public sealed class StreamStatistics
    {
        private const int WindowCapacity = 128;
        private readonly object _sync = new object();
        private readonly long[] _ticks = new long[WindowCapacity];
        private readonly int[] _expected = new int[WindowCapacity];
        private readonly int[] _received = new int[WindowCapacity];
        private readonly bool[] _published = new bool[WindowCapacity];
        private int _next;
        private int _count;
        private long _lastValidPacketTicks;
        private long _malformedPackets;
        private long _duplicatePackets;
        private long _stalePackets;
        private long _timedOutFrames;
        private long _failedFrames;
        private long _decodeFailures;

        public void RecordValidPacket()
        {
            lock (_sync)
            {
                _lastValidPacketTicks = Stopwatch.GetTimestamp();
            }
        }

        public void RecordMalformedPacket()
        {
            lock (_sync)
            {
                _malformedPackets++;
            }
        }

        public void RecordDuplicatePacket()
        {
            lock (_sync)
            {
                _duplicatePackets++;
            }
        }

        public void RecordStalePacket()
        {
            lock (_sync)
            {
                _stalePackets++;
            }
        }

        public void RecordTimedOutFrame()
        {
            lock (_sync)
            {
                _timedOutFrames++;
            }
        }

        public void RecordDecodeFailure()
        {
            lock (_sync)
            {
                _decodeFailures++;
            }
        }

        public void RecordFrame(int expectedPackets, int receivedPackets, bool published, bool timedOut)
        {
            lock (_sync)
            {
                long now = Stopwatch.GetTimestamp();
                _ticks[_next] = now;
                _expected[_next] = expectedPackets;
                _received[_next] = receivedPackets;
                _published[_next] = published;
                _next = (_next + 1) % WindowCapacity;
                if (_count < WindowCapacity)
                {
                    _count++;
                }

                if (timedOut)
                {
                    _timedOutFrames++;
                }

                if (!published)
                {
                    _failedFrames++;
                }
            }
        }

        public StreamStatisticsSnapshot GetSnapshot()
        {
            lock (_sync)
            {
                long now = Stopwatch.GetTimestamp();
                long oneSecondAgo = now - Stopwatch.Frequency;
                int expected = 0;
                int missing = 0;
                int publishedFrames = 0;
                for (int i = 0; i < _count; i++)
                {
                    if (_ticks[i] < oneSecondAgo)
                    {
                        continue;
                    }

                    expected += _expected[i];
                    missing += Math.Max(0, _expected[i] - _received[i]);
                    if (_published[i])
                    {
                        publishedFrames++;
                    }
                }

                return new StreamStatisticsSnapshot
                {
                    LastValidPacketTicks = _lastValidPacketTicks,
                    MalformedPackets = _malformedPackets,
                    DuplicatePackets = _duplicatePackets,
                    StalePackets = _stalePackets,
                    TimedOutFrames = _timedOutFrames,
                    FailedFrames = _failedFrames,
                    DecodeFailures = _decodeFailures,
                    FramesPerSecond = publishedFrames,
                    PacketLossPercent = expected == 0 ? 0f : 100f * missing / expected
                };
            }
        }
    }
}
