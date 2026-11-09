using UnityEngine;

namespace TelloViewer
{
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(1f)] private float distance = 5f;
        [SerializeField, Min(0.01f)] private float rotationSpeed = 3f;
        [SerializeField, Min(0.01f)] private float zoomSpeed = 1f;
        [SerializeField] private float minDistance = 1f;
        [SerializeField] private float maxDistance = 20f;
        [SerializeField] private float minVerticalAngle = -10f;
        [SerializeField] private float maxVerticalAngle = 80f;

        private float _yaw;
        private float _pitch = 20f;

        private void Awake()
        {
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
            UpdateCameraTransform();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            if (Input.GetMouseButton(1))
            {
                _yaw += Input.GetAxis("Mouse X") * rotationSpeed;
                _pitch -= Input.GetAxis("Mouse Y") * rotationSpeed;
                _pitch = Mathf.Clamp(_pitch, minVerticalAngle, maxVerticalAngle);
            }

            distance = Mathf.Clamp(
                distance - Input.mouseScrollDelta.y * zoomSpeed,
                minDistance,
                maxDistance);
            UpdateCameraTransform();
        }

        private void UpdateCameraTransform()
        {
            if (target == null)
            {
                return;
            }

            Quaternion orbit = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.position = target.position + orbit * (Vector3.back * distance);
            transform.rotation = Quaternion.LookRotation(target.position - transform.position, Vector3.up);
        }
    }
}
