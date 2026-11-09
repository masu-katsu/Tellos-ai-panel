using System;
using System.Diagnostics;

namespace TelloViewer
{
    public sealed class VoxelFrameAssembler
    {
        private readonly VoxelPoint[] _packetPoints = new VoxelPoint[VoxelPacketParser.MaximumVoxelsPerPacket];
        private readonly VoxelPoint[] _framePoints;
        private readonly bool[] _receivedPacketIds = new bool[VoxelPacketParser.MaximumPacketsPerFrame];
        private readonly LatestVoxelFrameStore _store;
        private readonly StreamStatistics _statistics;
        private readonly float _maxCoordinateMeters;
        private readonly long _timeoutTicks;

        private bool _hasCurrent;
        private bool _hasCommitted;
        private uint _currentFrameId;
        private uint _committedFrameId;
        private ushort _totalPackets;
        private int _receivedPackets;
        private int _voxelCount;
        private short _pitch;
        private short _roll;
        private short _yaw;
        private long _startedAtTicks;

        public VoxelFrameAssembler(
            LatestVoxelFrameStore store,
            StreamStatistics statistics,
            int capacity,
            float maxCoordinateMeters,
            int timeoutMilliseconds)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
            if (capacity < 1 || capacity > 4000)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _framePoints = new VoxelPoint[capacity];
            _maxCoordinateMeters = maxCoordinateMeters;
            _timeoutTicks = Math.Max(1L, Stopwatch.Frequency * timeoutMilliseconds / 1000L);
        }

        public void ProcessDatagram(byte[] data, int length)
        {
            VoxelPacketHeader packet;
            if (!VoxelPacketParser.TryParse(data, length, _maxCoordinateMeters, _packetPoints, out packet))
            {
                _statistics.RecordMalformedPacket();
                return;
            }

            _statistics.RecordValidPacket();
            if (_hasCurrent)
            {
                if (packet.FrameId == _currentFrameId)
                {
                    AddPacket(packet);
                    return;
                }

                if (!FrameIdOrder.IsNewer(packet.FrameId, _currentFrameId))
                {
                    _statistics.RecordStalePacket();
                    return;
                }

                FinalizeCurrent();
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
            AddPacket(packet);
        }

        public void Tick()
        {
            if (_hasCurrent && Stopwatch.GetTimestamp() - _startedAtTicks >= _timeoutTicks)
            {
                FinalizeCurrent();
            }
        }

        private void StartFrame(VoxelPacketHeader packet)
        {
            _hasCurrent = true;
            _currentFrameId = packet.FrameId;
            _totalPackets = packet.TotalPackets;
            _pitch = packet.Pitch;
            _roll = packet.Roll;
            _yaw = packet.Yaw;
            _receivedPackets = 0;
            _voxelCount = 0;
            _startedAtTicks = Stopwatch.GetTimestamp();
            Array.Clear(_receivedPacketIds, 0, _receivedPacketIds.Length);
        }

        private void AddPacket(VoxelPacketHeader packet)
        {
            if (packet.TotalPackets != _totalPackets ||
                packet.Pitch != _pitch ||
                packet.Roll != _roll ||
                packet.Yaw != _yaw)
            {
                _statistics.RecordMalformedPacket();
                return;
            }

            if (_receivedPacketIds[packet.PacketId])
            {
                _statistics.RecordDuplicatePacket();
                return;
            }

            if (_voxelCount + packet.VoxelCount > _framePoints.Length)
            {
                _statistics.RecordMalformedPacket();
                _statistics.RecordFrame(_totalPackets, _receivedPackets, false, false);
                _hasCurrent = false;
                _hasCommitted = true;
                _committedFrameId = _currentFrameId;
                return;
            }

            Array.Copy(_packetPoints, 0, _framePoints, _voxelCount, packet.VoxelCount);
            _voxelCount += packet.VoxelCount;
            _receivedPacketIds[packet.PacketId] = true;
            _receivedPackets++;

            if (_receivedPackets == _totalPackets)
            {
                PublishCurrent();
            }
        }

        private void FinalizeCurrent()
        {
            if (!_hasCurrent)
            {
                return;
            }

            if (_receivedPackets > 0)
            {
                PublishCurrent();
            }
            else
            {
                _statistics.RecordFrame(_totalPackets, 0, false, true);
                CommitCurrent();
            }
        }

        private void PublishCurrent()
        {
            _store.Publish(
                _framePoints,
                _voxelCount,
                _currentFrameId,
                _pitch,
                _roll,
                _yaw,
                _totalPackets,
                _receivedPackets);
            _statistics.RecordFrame(_totalPackets, _receivedPackets, true, _receivedPackets < _totalPackets);
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
