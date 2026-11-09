namespace TelloViewer
{
    public static class FrameIdOrder
    {
        public static bool IsNewer(uint candidate, uint current)
        {
            uint difference = unchecked(candidate - current);
            return difference != 0x80000000u && unchecked((int)difference) > 0;
        }
    }
}
