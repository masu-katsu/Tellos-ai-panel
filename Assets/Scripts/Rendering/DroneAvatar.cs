using UnityEngine;

namespace TelloViewer
{
    public sealed class DroneAvatar : MonoBehaviour
    {
        [SerializeField] private NetworkHub networkHub;

        private long _generation;

        private void Awake()
        {
            if (networkHub == null)
            {
                Debug.LogError("DroneAvatar requires a NetworkHub reference in the scene.", this);
            }
        }

        private void Update()
        {
            if (networkHub == null || networkHub.VoxelStore == null)
            {
                return;
            }

            long generation;
            VoxelFrameMetadata metadata;
            if (!networkHub.VoxelStore.TryGetNewerMetadata(_generation, out generation, out metadata))
            {
                return;
            }

            _generation = generation;
            transform.position = Vector3.zero;
            transform.rotation = CoordinateConverter.ToUnityRotation(metadata.Pitch, metadata.Roll, metadata.Yaw);
        }
    }
}
