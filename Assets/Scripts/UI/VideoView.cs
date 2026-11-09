using UnityEngine;
using UnityEngine.UI;

namespace TelloViewer
{
    public sealed class VideoView : MonoBehaviour
    {
        [SerializeField] private NetworkHub networkHub;
        [SerializeField] private RawImage rawImage;

        private LatestVideoFrameStore _store;
        private Texture2D _texture;
        private readonly byte[] _jpegBuffer = new byte[VideoPacketParser.MaximumJpegSize];
        private long _generation;

        private void Awake()
        {
            if (networkHub == null)
            {
                Debug.LogError("VideoView requires a NetworkHub reference in the scene.", this);
            }

            if (rawImage == null)
            {
                rawImage = GetComponent<RawImage>();
            }

            if (networkHub != null)
            {
                _store = networkHub.VideoStore;
            }

            _texture = new Texture2D(2, 2, TextureFormat.RGB24, false)
            {
                name = "Tello Live Video"
            };
        }

        private void Update()
        {
            if (_store == null && networkHub != null)
            {
                _store = networkHub.VideoStore;
            }

            if (_store == null || rawImage == null)
            {
                return;
            }

            long generation;
            int length;
            uint frameId;
            if (!_store.TryCopyNewer(_generation, _jpegBuffer, out generation, out length, out frameId, out _, out _))
            {
                return;
            }

            _generation = generation;
            if (!_texture.LoadImage(_jpegBuffer, false))
            {
                networkHub.VideoStatistics.RecordDecodeFailure();
                Debug.LogWarning("JPEGフレームの復号に失敗しました。Frame_ID=" + frameId, this);
                return;
            }

            rawImage.texture = _texture;
            rawImage.color = Color.white;
            rawImage.SetNativeSize();
        }

        private void OnDestroy()
        {
            if (_texture != null)
            {
                Destroy(_texture);
            }
        }
    }
}
