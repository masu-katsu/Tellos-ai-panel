namespace TelloViewer
{
    public static class JpegHeaderValidator
    {
        public static bool TryGetDimensions(byte[] data, int length, int maxWidth, int maxHeight, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (data == null || length < 4 || data[0] != 0xff || data[1] != 0xd8 ||
                data[length - 2] != 0xff || data[length - 1] != 0xd9)
            {
                return false;
            }

            int position = 2;
            bool foundStartOfFrame = false;
            while (position < length - 2)
            {
                if (data[position++] != 0xff)
                {
                    return false;
                }

                while (position < length && data[position] == 0xff)
                {
                    position++;
                }

                if (position >= length)
                {
                    return false;
                }

                byte marker = data[position++];
                if (marker == 0xd9)
                {
                    break;
                }

                if (marker == 0xda)
                {
                    if (!foundStartOfFrame || position + 2 > length)
                    {
                        return false;
                    }

                    int scanLength = (data[position] << 8) | data[position + 1];
                    return scanLength >= 2 && position + scanLength <= length;
                }

                if (marker == 0xd8 || marker == 0x01 || (marker >= 0xd0 && marker <= 0xd7))
                {
                    continue;
                }

                if (position + 2 > length)
                {
                    return false;
                }

                int segmentLength = (data[position] << 8) | data[position + 1];
                if (segmentLength < 2 || position + segmentLength > length)
                {
                    return false;
                }

                if (IsStartOfFrame(marker))
                {
                    if (segmentLength < 8)
                    {
                        return false;
                    }

                    height = (data[position + 3] << 8) | data[position + 4];
                    width = (data[position + 5] << 8) | data[position + 6];
                    if (width < 1 || width > maxWidth || height < 1 || height > maxHeight)
                    {
                        return false;
                    }

                    float aspectRatio = (float)width / height;
                    if (System.Math.Abs(aspectRatio - 4f / 3f) > (4f / 3f) * 0.01f)
                    {
                        return false;
                    }

                    foundStartOfFrame = true;
                }

                position += segmentLength;
            }

            return false;
        }

        private static bool IsStartOfFrame(byte marker)
        {
            return marker == 0xc0 || marker == 0xc1 || marker == 0xc2 || marker == 0xc3 ||
                   marker == 0xc5 || marker == 0xc6 || marker == 0xc7 ||
                   marker == 0xc9 || marker == 0xca || marker == 0xcb ||
                   marker == 0xcd || marker == 0xce || marker == 0xcf;
        }
    }
}
