using UnityEngine;

namespace TelloViewer
{
    public sealed class VoxelRenderer : MonoBehaviour
    {
        private const int BatchSize = 1023;
        private VoxelPoint[] _points;
        private LatestVoxelFrameStore _store;
        private Mesh _mesh;
        private Material _material;
        private int _voxelCount;
        private long _generation;
        [SerializeField] private NetworkHub networkHub;
        [SerializeField] private Material voxelMaterial;
        [SerializeField, Min(0.001f)] private float voxelSizeMeters = 0.07f;
        [SerializeField] private Color voxelColor = new Color(0.22f, 0.82f, 0.95f, 1f);

        private void Awake()
        {
            if (networkHub == null)
            {
                Debug.LogError("VoxelRenderer requires a NetworkHub reference in the scene.", this);
            }

            _store = networkHub == null ? null : networkHub.VoxelStore;
            _points = new VoxelPoint[4000];

            _material = voxelMaterial;

            if (_material == null)
            {
                Debug.LogError("VoxelRenderer requires a voxel material asset in the scene.", this);
            }
            else
            {
                _material.enableInstancing = true;
                if (_material.HasProperty("_Color"))
                {
                    _material.color = voxelColor;
                }
            }

            _mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            if (_mesh == null)
            {
                Debug.LogError("VoxelRenderer could not load Unity's built-in cube mesh.", this);
            }
        }

        private void Update()
        {
            if (_store == null && networkHub != null)
            {
                _store = networkHub.VoxelStore;
            }

            if (_store == null || _points == null)
            {
                return;
            }

            long generation;
            VoxelFrameMetadata metadata;
            if (!_store.TryCopyNewer(_generation, _points, out generation, out metadata))
            {
                return;
            }

            _generation = generation;
            _voxelCount = metadata.VoxelCount;
            for (int i = 0; i < _voxelCount; i++)
            {
                Vector3 position = CoordinateConverter.ToUnityPosition(_points[i]);
                _batches[i / BatchSize][i % BatchSize] = Matrix4x4.TRS(
                    position,
                    Quaternion.identity,
                    Vector3.one * voxelSizeMeters);

                if (i % BatchSize == BatchSize - 1 || i == _voxelCount - 1)
                {
                    int batchStart = (i / BatchSize) * BatchSize;
                    int batchCount = i - batchStart + 1;
                    _batchCounts[i / BatchSize] = batchCount;
                }
            }

            _batchTotal = _voxelCount == 0 ? 0 : (_voxelCount + BatchSize - 1) / BatchSize;
        }

        private readonly Matrix4x4[][] _batches = CreateBatches();
        private readonly int[] _batchCounts = new int[4];
        private int _batchTotal;

        private void LateUpdate()
        {
            if (_mesh == null || _material == null)
            {
                return;
            }

            for (int i = 0; i < _batchTotal; i++)
            {
                Graphics.DrawMeshInstanced(_mesh, 0, _material, _batches[i], _batchCounts[i]);
            }
        }

        private static Matrix4x4[][] CreateBatches()
        {
            Matrix4x4[][] batches = new Matrix4x4[4][];
            for (int i = 0; i < batches.Length; i++)
            {
                batches[i] = new Matrix4x4[BatchSize];
            }

            return batches;
        }

    }
}
