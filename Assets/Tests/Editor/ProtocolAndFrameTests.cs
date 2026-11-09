using System;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace TelloViewer.Tests
{
    public sealed class ProtocolAndFrameTests
    {
        [Test]
        public void FrameIdOrderHandlesWrapAndRejectsHalfRange()
        {
            Assert.IsTrue(FrameIdOrder.IsNewer(0u, uint.MaxValue));
            Assert.IsFalse(FrameIdOrder.IsNewer(uint.MaxValue, 0u));
            Assert.IsFalse(FrameIdOrder.IsNewer(0x80000000u, 0u));
            Assert.IsFalse(FrameIdOrder.IsNewer(12u, 12u));
        }

        [Test]
        public void VoxelParserReadsLittleEndianCoordinatesAndRejectsLengthMismatch()
        {
            byte[] packet = BuildVoxelPacket(17u, 0, 1, 1, 100, -200, 300, 1.25f, -2.5f, 3.75f);
            VoxelPoint[] points = new VoxelPoint[100];
            VoxelPacketHeader header;

            Assert.IsTrue(VoxelPacketParser.TryParse(packet, packet.Length, 50f, points, out header));
            Assert.AreEqual(17u, header.FrameId);
            Assert.AreEqual(1, header.VoxelCount);
            Assert.AreEqual(1.25f, points[0].X);
            Assert.AreEqual(-2.5f, points[0].Y);
            Assert.AreEqual(3.75f, points[0].Z);
            Assert.IsFalse(VoxelPacketParser.TryParse(packet, packet.Length - 1, 50f, points, out header));
        }

        [Test]
        public void VoxelAssemblerPublishesOutOfOrderCompleteFrameOnce()
        {
            LatestVoxelFrameStore store = new LatestVoxelFrameStore(10);
            StreamStatistics statistics = new StreamStatistics();
            VoxelFrameAssembler assembler = new VoxelFrameAssembler(store, statistics, 10, 50f, 50);
            assembler.ProcessDatagram(BuildVoxelPacket(3u, 1, 2, 1, 0, 0, 0, 2f, 0f, 0f), 28);
            assembler.ProcessDatagram(BuildVoxelPacket(3u, 0, 2, 1, 0, 0, 0, 1f, 0f, 0f), 28);

            VoxelPoint[] points = new VoxelPoint[10];
            long generation;
            VoxelFrameMetadata metadata;
            Assert.IsTrue(store.TryCopyNewer(0, points, out generation, out metadata));
            Assert.AreEqual(1L, generation);
            Assert.AreEqual(2, metadata.VoxelCount);
            Assert.IsFalse(metadata.IsPartial);
            Assert.AreEqual(2f, points[0].X);
            Assert.AreEqual(1f, points[1].X);
            Assert.AreEqual(0, statistics.GetSnapshot().PacketLossPercent);
        }

        [Test]
        public void VoxelAssemblerPublishesPartialFrameAfterTimeout()
        {
            LatestVoxelFrameStore store = new LatestVoxelFrameStore(10);
            StreamStatistics statistics = new StreamStatistics();
            VoxelFrameAssembler assembler = new VoxelFrameAssembler(store, statistics, 10, 50f, 1);
            assembler.ProcessDatagram(BuildVoxelPacket(4u, 0, 2, 1, 0, 0, 0, 1f, 2f, 3f), 28);
            Thread.Sleep(5);
            assembler.Tick();

            VoxelPoint[] points = new VoxelPoint[10];
            long generation;
            VoxelFrameMetadata metadata;
            Assert.IsTrue(store.TryCopyNewer(0, points, out generation, out metadata));
            Assert.IsTrue(metadata.IsPartial);
            Assert.AreEqual(1, metadata.VoxelCount);
            Assert.AreEqual(50f, statistics.GetSnapshot().PacketLossPercent);
        }

        [Test]
        public void EmptyVoxelFrameReplacesPreviousFrame()
        {
            LatestVoxelFrameStore store = new LatestVoxelFrameStore(10);
            StreamStatistics statistics = new StreamStatistics();
            VoxelFrameAssembler assembler = new VoxelFrameAssembler(store, statistics, 10, 50f, 50);
            assembler.ProcessDatagram(BuildVoxelPacket(1u, 0, 1, 1, 0, 0, 0, 1f, 2f, 3f), 28);
            assembler.ProcessDatagram(BuildVoxelPacket(2u, 0, 1, 0, 0, 0, 0), 16);

            VoxelPoint[] points = new VoxelPoint[10];
            long generation;
            VoxelFrameMetadata metadata;
            Assert.IsTrue(store.TryCopyNewer(1, points, out generation, out metadata));
            Assert.AreEqual(0, metadata.VoxelCount);
            Assert.AreEqual(2u, metadata.FrameId);
        }

        [Test]
        public void VideoParserRequiresExactPayloadAndValidPacketOrder()
        {
            byte[] packet = BuildVideoPacket(8u, 0, 1, new byte[] { 1, 2, 3 });
            VideoPacketHeader header;
            Assert.IsTrue(VideoPacketParser.TryParse(packet, packet.Length, out header));
            Assert.AreEqual(3, header.PayloadLength);
            Assert.IsFalse(VideoPacketParser.TryParse(packet, packet.Length - 1, out header));
        }

        [Test]
        public void JpegValidatorRequiresDimensionsAndScanHeader()
        {
            byte[] jpeg = BuildMinimalJpeg();
            int width;
            int height;
            Assert.IsTrue(JpegHeaderValidator.TryGetDimensions(jpeg, jpeg.Length, 640, 480, out width, out height));
            Assert.AreEqual(4, width);
            Assert.AreEqual(3, height);
            Assert.IsFalse(JpegHeaderValidator.TryGetDimensions(jpeg, jpeg.Length, 3, 480, out width, out height));
        }

        [Test]
        public void VideoAssemblerPublishesOnlyValidatedCompleteJpeg()
        {
            byte[] jpeg = BuildMinimalJpeg();
            byte[] packet = BuildVideoPacket(23u, 0, 1, jpeg);
            LatestVideoFrameStore store = new LatestVideoFrameStore(262144);
            StreamStatistics statistics = new StreamStatistics();
            VideoFrameAssembler assembler = new VideoFrameAssembler(store, statistics, 262144, 640, 480, 100);
            assembler.ProcessDatagram(packet, packet.Length);

            byte[] destination = new byte[262144];
            long generation;
            int length;
            uint frameId;
            int width;
            int height;
            Assert.IsTrue(store.TryCopyNewer(0, destination, out generation, out length, out frameId, out width, out height));
            Assert.AreEqual(1L, generation);
            Assert.AreEqual(jpeg.Length, length);
            Assert.AreEqual(23u, frameId);
            Assert.AreEqual(4, width);
            Assert.AreEqual(3, height);
            CollectionAssert.AreEqual(jpeg, new ArraySegment<byte>(destination, 0, length));
        }

        [Test]
        public void UnityJpegDecoderAcceptsFixedCapacityBufferWithTrailingBytes()
        {
            Texture2D source = new Texture2D(4, 3, TextureFormat.RGB24, false);
            Texture2D destination = new Texture2D(2, 2, TextureFormat.RGB24, false);
            try
            {
                source.SetPixels(new[]
                {
                    Color.red, Color.green, Color.blue, Color.white,
                    Color.blue, Color.white, Color.red, Color.green,
                    Color.green, Color.blue, Color.white, Color.red
                });
                source.Apply();
                byte[] jpeg = source.EncodeToJPG();
                byte[] fixedCapacityBuffer = new byte[262144];
                Array.Copy(jpeg, fixedCapacityBuffer, jpeg.Length);

                Assert.IsTrue(destination.LoadImage(fixedCapacityBuffer, false));
                Assert.AreEqual(4, destination.width);
                Assert.AreEqual(3, destination.height);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(destination);
            }
        }

        [Test]
        public void CoordinateConverterFlipsImageDownAxis()
        {
            Vector3 position = CoordinateConverter.ToUnityPosition(new VoxelPoint(1f, 2f, 3f));
            Assert.AreEqual(new Vector3(1f, -2f, 3f), position);
            Assert.AreEqual(Quaternion.identity, CoordinateConverter.ToUnityRotation(0, 0, 0));
        }

        [Test]
        public void SampleSceneContainsAuthoredViewerObjectsAndUi()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            GameObject network = null;
            GameObject cameraObject = null;
            GameObject droneObject = null;
            GameObject voxelObject = null;
            bool hasVideoView = false;
            bool hasStatusView = false;
            bool hasNoSignalNotice = false;
            bool hasBootstrap = false;
            CameraRig cameraRig = null;
            VoxelRenderer voxelRenderer = null;
            DroneAvatar droneAvatar = null;
            VideoView videoView = null;
            StatusView statusView = null;
            NoSignalNotice noSignalNotice = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Network")
                {
                    network = root;
                }

                if (root.name == "Main Camera")
                {
                    cameraObject = root;
                    cameraRig = root.GetComponent<CameraRig>();
                }

                if (root.name == "DroneAvatar")
                {
                    droneObject = root;
                    droneAvatar = root.GetComponent<DroneAvatar>();
                }

                if (root.name == "VoxelWorld")
                {
                    voxelObject = root;
                    voxelRenderer = root.GetComponent<VoxelRenderer>();
                }

                foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component is VideoView)
                    {
                        hasVideoView = true;
                        videoView = (VideoView)component;
                    }

                    if (component is StatusView)
                    {
                        hasStatusView = true;
                        statusView = (StatusView)component;
                    }

                    if (component is NoSignalNotice)
                    {
                        hasNoSignalNotice = true;
                        noSignalNotice = (NoSignalNotice)component;
                    }

                    hasBootstrap |= component.GetType().Name == "TelloViewerBootstrap";
                }
            }

            Assert.IsNotNull(network);
            Assert.IsNotNull(network.GetComponent<AppSettings>());
            Assert.IsNotNull(network.GetComponent<NetworkHub>());
            Assert.IsNotNull(cameraObject);
            Assert.IsNotNull(droneObject);
            Assert.IsNotNull(voxelObject);
            Assert.IsNotNull(cameraRig);
            Assert.IsNotNull(voxelRenderer);
            Assert.IsNotNull(droneAvatar);
            Assert.IsTrue(hasVideoView);
            Assert.IsTrue(hasStatusView);
            Assert.IsTrue(hasNoSignalNotice);
            Assert.IsFalse(hasBootstrap);
            Assert.AreSame(droneObject.transform, new SerializedObject(cameraRig).FindProperty("target").objectReferenceValue);
            Assert.AreSame(network.GetComponent<NetworkHub>(), new SerializedObject(voxelRenderer).FindProperty("networkHub").objectReferenceValue);
            Assert.AreSame(network.GetComponent<NetworkHub>(), new SerializedObject(droneAvatar).FindProperty("networkHub").objectReferenceValue);
            Assert.AreSame(network.GetComponent<NetworkHub>(), new SerializedObject(videoView).FindProperty("networkHub").objectReferenceValue);
            Assert.IsNotNull(new SerializedObject(videoView).FindProperty("rawImage").objectReferenceValue);
            Assert.AreSame(network.GetComponent<NetworkHub>(), new SerializedObject(statusView).FindProperty("networkHub").objectReferenceValue);
            Assert.IsNotNull(new SerializedObject(statusView).FindProperty("voxelStatusText").objectReferenceValue);
            Assert.IsNotNull(new SerializedObject(statusView).FindProperty("videoStatusText").objectReferenceValue);
            Assert.AreSame(network.GetComponent<NetworkHub>(), new SerializedObject(noSignalNotice).FindProperty("networkHub").objectReferenceValue);
            Assert.IsNotNull(new SerializedObject(noSignalNotice).FindProperty("noticeText").objectReferenceValue);
        }

        private static byte[] BuildVoxelPacket(
            uint frameId,
            ushort packetId,
            ushort totalPackets,
            ushort voxelCount,
            short pitch,
            short roll,
            short yaw,
            params float[] coordinates)
        {
            byte[] data = new byte[16 + voxelCount * 12];
            WriteUInt32(data, 0, frameId);
            WriteUInt16(data, 4, packetId);
            WriteUInt16(data, 6, totalPackets);
            WriteUInt16(data, 8, voxelCount);
            WriteUInt16(data, 10, unchecked((ushort)pitch));
            WriteUInt16(data, 12, unchecked((ushort)roll));
            WriteUInt16(data, 14, unchecked((ushort)yaw));
            for (int i = 0; i < coordinates.Length; i++)
            {
                WriteSingle(data, 16 + i * 4, coordinates[i]);
            }

            return data;
        }

        private static byte[] BuildVideoPacket(uint frameId, ushort packetId, ushort totalPackets, byte[] payload)
        {
            byte[] data = new byte[10 + payload.Length];
            WriteUInt32(data, 0, frameId);
            WriteUInt16(data, 4, packetId);
            WriteUInt16(data, 6, totalPackets);
            WriteUInt16(data, 8, (ushort)payload.Length);
            Array.Copy(payload, 0, data, 10, payload.Length);
            return data;
        }

        private static byte[] BuildMinimalJpeg()
        {
            return new byte[]
            {
                0xff, 0xd8,
                0xff, 0xc0, 0x00, 0x0b, 0x08, 0x00, 0x03, 0x00, 0x04, 0x01,
                0x01, 0x11, 0x00,
                0xff, 0xda, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3f, 0x00,
                0x00, 0xff, 0xd9
            };
        }

        private static void WriteUInt16(byte[] data, int offset, ushort value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
        }

        private static void WriteUInt32(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteSingle(byte[] data, int offset, float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            Array.Copy(bytes, 0, data, offset, 4);
        }
    }
}
