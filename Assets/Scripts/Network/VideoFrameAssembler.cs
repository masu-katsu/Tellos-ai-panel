using System;
using System.Diagnostics;

namespace TelloViewer
{
    public sealed class VideoFrameAssembler
    {
        private readonly byte[] _frameBytes;
        private readonly byte[] _assembledBytes;
        private readonly bool[] _receivedPacketIds = new bool[VideoPacketParser.MaximumPacketsPerFrame];
        private readonly LatestVideoFrameStore _store;
        private readonly StreamStatistics _statistics;
        private readonly int _maxJpegBytes;
        private readonly int _maxWidth;
        private readonly int _maxHeight;
        private readonly long _timeoutTicks;

        private bool _hasCurrent;
        private bool _hasCommitted;
        private uint _currentFrameId;
        private uint _committedFrameId;
        private ushort _totalPackets;
        private int _receivedPackets;
        private int _finalPacketLength;
        private long _startedAtTicks;

        public VideoFrameAssembler(
            LatestVideoFrameStore store,
            StreamStatistics statistics,
            int maxJpegBytes,
            int maxWidth,
            int maxHeight,
            int timeoutMilliseconds)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
            if (maxJpegBytes < 1 || maxJpegBytes > VideoPacketParser.MaximumJpegSize)
            {
                throw new ArgumentOutOfRangeException(nameof(maxJpegBytes));
            }

            _frameBytes = new byte[maxJpegBytes];
            _assembledBytes = new byte[maxJpegBytes];
            _maxJpegBytes = maxJpegBytes;
            _maxWidth = maxWidth;
            _maxHeight = maxHeight;
            _timeoutTicks = Math.Max(1L, Stopwatch.Frequency * timeoutMilliseconds / 1000L);
        }

        public void ProcessDatagram(byte[] data, int length)
        {
            VideoPacketHeader packet;
            if (!VideoPacketParser.TryParse(data, length, out packet))
            {
                _statistics.RecordMalformedPacket();
                return;
            }

            _statistics.RecordValidPacket();
            if (_hasCurrent)
            {
                if (packet.FrameId == _currentFrameId)
                {
                    AddPacket(data, packet);
                    return;
                }

                if (!FrameIdOrder.IsNewer(packet.FrameId, _currentFrameId))
                {
                    _statistics.RecordStalePacket();
                    return;
                }

                DropCurrent(false);
            }

            if (_hasCommitted && packet.FrameId == _committedFrameId)
            {
                if (_receivedPacketIds[packet.PacketId])
                {
                    _statistics.RecordDuplicatePacket();
                }
                else
                {
                    _statistics.RecordStalePacket();
                }

                return;
            }

            if (_hasCommitted && !FrameIdOrder.IsNewer(packet.FrameId, _committedFrameId))
            {
                _statistics.RecordStalePacket();
                return;
            }

            StartFrame(packet);
            AddPacket(data, packet);
        }

        public void Tick()
        {
            if (_hasCurrent && Stopwatch.GetTimestamp() - _startedAtTicks >= _timeoutTicks)
            {
                DropCurrent(true);
            }
        }

        private void StartFrame(VideoPacketHeader packet)
        {
            _hasCurrent = true;
            _currentFrameId = packet.FrameId;
            _totalPackets = packet.TotalPackets;
            _receivedPackets = 0;
            _finalPacketLength = 0;
            _startedAtTicks = Stopwatch.GetTimestamp();
            Array.Clear(_receivedPacketIds, 0, _receivedPacketIds.Length);
        }

        private void AddPacket(byte[] data, VideoPacketHeader packet)
        {
            if (packet.TotalPackets != _totalPackets)
            {
                _statistics.RecordMalformedPacket();
                return;
            }

            if (_receivedPacketIds[packet.PacketId])
            {
                _statistics.RecordDuplicatePacket();
                return;
            }

            int offset = packet.PacketId * VideoPacketParser.MaximumPayloadSize;
            int end = offset + packet.PayloadLength;
            if (end > _maxJpegBytes)
            {
                _statistics.RecordMalformedPacket();
                DropCurrent(false);
                return;
            }

            Array.Copy(data, VideoPacketParser.HeaderSize, _frameBytes, offset, packet.PayloadLength);
            _receivedPacketIds[packet.PacketId] = true;
            _receivedPackets++;
            if (packet.PacketId == packet.TotalPackets - 1)
            {
                _finalPacketLength = packet.PayloadLength;
            }

            if (_receivedPackets == _totalPackets)
            {
                PublishIfValid();
            }
        }

        private void PublishIfValid()
        {
            int length = (_totalPackets - 1) * VideoPacketParser.MaximumPayloadSize + _finalPacketLength;
            if (length < 1 || length > _maxJpegBytes)
            {
                _statistics.RecordMalformedPacket();
                DropCurrent(false);
                return;
            }

            int destinationOffset = 0;
            for (int packetId = 0; packetId < _totalPackets; packetId++)
            {
                int packetLength = packetId == _totalPackets - 1
                    ? _finalPacketLength
                    : VideoPacketParser.MaximumPayloadSize;
                Array.Copy(
                    _frameBytes,
                    packetId * VideoPacketParser.MaximumPayloadSize,
                    _assembledBytes,
                    destinationOffset,
                    packetLength);
                destinationOffset += packetLength;
            }

            int width;
            int height;
            if (!JpegHeaderValidator.TryGetDimensions(_assembledBytes, length, _maxWidth, _maxHeight, out width, out height))
            {
                _statistics.RecordDecodeFailure();
                _statistics.RecordFrame(_totalPackets, _receivedPackets, false, false);
                CommitCurrent();
                return;
            }

            _store.Publish(_assembledBytes, length, _currentFrameId, width, height);
            _statistics.RecordFrame(_totalPackets, _receivedPackets, true, false);
            CommitCurrent();
        }

        private void DropCurrent(bool timedOut)
        {
            if (!_hasCurrent)
            {
                return;
            }

            _statistics.RecordFrame(_totalPackets, _receivedPackets, false, timedOut);
            CommitCurrent();
        }

        private void CommitCurrent()
        {
            _committedFrameId = _currentFrameId;
            _hasCommitted = true;
            _hasCurrent = false;
        }
    }
}
