using System;

namespace TelloViewer
{
    public sealed class LatestVideoFrameStore
    {
        private readonly object _sync = new object();
        private readonly byte[] _jpeg;
        private int _length;
        private int _width;
        private int _height;
        private uint _frameId;
        private long _generation;

        public LatestVideoFrameStore(int capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _jpeg = new byte[capacity];
        }

        public void Publish(byte[] source, int length, uint frameId, int width, int height)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (length < 1 || length > source.Length || length > _jpeg.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            lock (_sync)
            {
                Array.Copy(source, _jpeg, length);
                _length = length;
                _width = width;
                _height = height;
                _frameId = frameId;
                _generation++;
            }
        }

        public bool TryCopyNewer(long lastGeneration, byte[] destination, out long generation, out int length, out uint frameId, out int width, out int height)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            lock (_sync)
            {
                generation = _generation;
                length = _length;
                frameId = _frameId;
                width = _width;
                height = _height;
                if (_generation == 0 || _generation == lastGeneration)
                {
                    return false;
                }

                if (destination.Length < _length)
                {
                    return false;
                }

                Array.Copy(_jpeg, destination, _length);
                return true;
            }
        }
    }
}
