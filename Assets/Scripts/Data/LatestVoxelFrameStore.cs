using System;

namespace TelloViewer
{
    public sealed class LatestVoxelFrameStore
    {
        private readonly object _sync = new object();
        private readonly VoxelPoint[] _points;
        private VoxelFrameMetadata _metadata;
        private long _generation;

        public LatestVoxelFrameStore(int capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _points = new VoxelPoint[capacity];
        }

        public long Generation
        {
            get
            {
                lock (_sync)
                {
                    return _generation;
                }
            }
        }

        public void Publish(
            VoxelPoint[] source,
            int count,
            uint frameId,
            int pitch,
            int roll,
            int yaw,
            int expectedPackets,
            int receivedPackets)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (count < 0 || count > source.Length || count > _points.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            lock (_sync)
            {
                Array.Copy(source, _points, count);
                _metadata = new VoxelFrameMetadata
                {
                    FrameId = frameId,
                    Pitch = pitch,
                    Roll = roll,
                    Yaw = yaw,
                    VoxelCount = count,
                    ExpectedPackets = expectedPackets,
                    ReceivedPackets = receivedPackets
                };
                _generation++;
            }
        }

        public bool TryCopyNewer(long lastGeneration, VoxelPoint[] destination, out long generation, out VoxelFrameMetadata metadata)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            lock (_sync)
            {
                generation = _generation;
                metadata = _metadata;
                if (_generation == 0 || _generation == lastGeneration)
                {
                    return false;
                }

                if (destination.Length < _metadata.VoxelCount)
                {
                    throw new ArgumentException("Destination cannot hold the published voxel frame.", nameof(destination));
                }

                Array.Copy(_points, destination, _metadata.VoxelCount);
                return true;
            }
        }

        public bool TryGetNewerMetadata(long lastGeneration, out long generation, out VoxelFrameMetadata metadata)
        {
            lock (_sync)
            {
                generation = _generation;
                metadata = _metadata;
                return _generation != 0 && _generation != lastGeneration;
            }
        }
    }
}
