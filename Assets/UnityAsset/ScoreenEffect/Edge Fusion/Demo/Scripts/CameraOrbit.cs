using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EdgeFusion {

    public class CameraOrbit : MonoBehaviour {

        [Tooltip("The target object to orbit around")]
        public Transform target;

        [Tooltip("Rotation speed in degrees per second")]
        public float rotationSpeed = 20f;

        [Tooltip("Distance from the target")]
        public float distance = 5f;

        [Tooltip("Height offset from the target")]
        public float height = 2f;

        [Tooltip("Volume GameObject to toggle with Space key")]
        public GameObject volumeObject;

        [Tooltip("UI Text label to show current state")] public Text stateLabel;

        Vector3 targetPosition;
        float currentAngle;

        void Start() {
            if (target != null) {
                targetPosition = target.position;
                Vector3 offset = transform.position - targetPosition;
                distance = new Vector3(offset.x, 0, offset.z).magnitude;
                height = offset.y;
                currentAngle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            }
            UpdateStateLabel();
        }

        void LateUpdate() {
            if (target == null) return;

            targetPosition = target.position;
            currentAngle += rotationSpeed * Time.deltaTime;

            float radians = currentAngle * Mathf.Deg2Rad;
            float x = Mathf.Sin(radians) * distance;
            float z = Mathf.Cos(radians) * distance;

            transform.position = targetPosition + new Vector3(x, height, z);
            transform.LookAt(targetPosition);

            if (volumeObject != null) {
                bool toggled = false;
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) toggled = true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
                if (!toggled && Input.GetKeyDown(KeyCode.Space)) toggled = true;
#endif
                if (toggled) {
                    volumeObject.SetActive(!volumeObject.activeSelf);
                    UpdateStateLabel();
                }
            }
        }

        void UpdateStateLabel() {
            if (stateLabel == null) return;
            bool isOn = volumeObject != null && volumeObject.activeSelf;
            stateLabel.text = isOn ? "Press Space to Toggle Effect (Currently ON)" : "Press Space to Toggle Effect (Currently OFF)";
        }
    }
}

