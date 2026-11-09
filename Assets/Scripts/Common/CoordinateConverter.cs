using UnityEngine;

namespace TelloViewer
{
    public static class CoordinateConverter
    {
        public static Vector3 ToUnityPosition(VoxelPoint point)
        {
            return new Vector3(point.X, -point.Y, point.Z);
        }

        public static Quaternion ToUnityRotation(int pitchHundredths, int rollHundredths, int yawHundredths)
        {
            float pitch = pitchHundredths * 0.01f;
            float roll = rollHundredths * 0.01f;
            float yaw = yawHundredths * 0.01f;
            Quaternion backendRotation =
                Quaternion.AngleAxis(yaw, Vector3.up) *
                Quaternion.AngleAxis(pitch, Vector3.right) *
                Quaternion.AngleAxis(roll, Vector3.forward);

            return new Quaternion(
                -backendRotation.x,
                backendRotation.y,
                -backendRotation.z,
                backendRotation.w).normalized;
        }
    }
}
