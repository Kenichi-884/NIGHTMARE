using UnityEngine;

namespace EdgeFusion {

    public class RockBob : MonoBehaviour {

        [Tooltip("Movement amplitude in meters")] public float amplitude = 0.2f;
        [Tooltip("Oscillation period in seconds")] public float period = 2.5f;
        [Tooltip("Ease-in/out factor (0=sinusoidal)")] [Range(0f,1f)] public float smooth = 0.25f;

        Vector3 initialPosition;
        float angularSpeed;
        float phase;

        void Awake() {
            initialPosition = transform.position;
            angularSpeed = period > 0f ? (Mathf.PI * 2f) / period : 0f;
            phase = Random.value * Mathf.PI * 2f;
        }

        void OnEnable() {
            angularSpeed = period > 0f ? (Mathf.PI * 2f) / period : 0f;
        }

        void Update() {
            if (angularSpeed <= 0f || amplitude == 0f) return;

            phase += angularSpeed * Time.deltaTime;
            if (phase > Mathf.PI * 2f) phase -= Mathf.PI * 2f;

            float s = Mathf.Sin(phase);
            if (smooth > 0f) {
                float t = (s + 1f) * 0.5f;          // [0..1]
                float eased = t * t * (3f - 2f * t);
                s = Mathf.LerpUnclamped(-1f, 1f, eased);
            }

            Vector3 p = initialPosition;
            p.y += s * amplitude;
            transform.position = p;
        }
    }
}

