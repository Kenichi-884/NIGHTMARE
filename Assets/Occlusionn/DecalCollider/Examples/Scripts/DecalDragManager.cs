using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace DecalCollider.Examples.Scripts
{
    /// <summary>
    /// Manages dragging and rotating a target object, as well as dragging decals on a surface.
    /// Handles input, inertia, and smooth transitions for decal properties.
    /// </summary>
    public class DecalDragManager : MonoBehaviour
    {
        #region Public Fields

        [Header("Target Object To Rotate")]
        public Transform target;

        [Header("Surface To Drag On")]
        public Transform dragSurfaceTarget;

        [Header("Rotation Settings")]
        [Range(0.01f, 1f)]
        public float sensitivity = 1f;
        [Range(0.8f, 0.99f)]
        public float inertiaDamping = 0.95f;

        [Header("Decal Drag Settings")]
        [Tooltip("The distance from the decal object's pivot point to the surface during dragging.")]
        public float decalPivotOffset = 0.1f;
        [Tooltip("Damping after release. The closer to 1, the longer it will slide.")]
        [Range(0.8f, 0.99f)]
        public float decalInertiaDamping = 0.94f;
        
        [Tooltip("While dragging, applies this additional offset to the decal's mesh. Useful for lifting it slightly on the Z-axis.")]
        public Vector3 decalMeshOffsetOnDrag = new(0, 0, 0.025f);

        [Header("Decal Rotation Settings")]
        [Tooltip("The speed (in degrees) for rotating the decal on its own axis with the mouse scroll wheel.")]
        public float scrollSensitivity = 100f;

        [Header("Transition Settings")]
        [Tooltip("The duration for the smooth transition of the offset value when a decal is grabbed or released.")]
        public float offsetTransitionDuration = 0.4f;
        
        [Header("Cursor Settings")]
        public RectTransform customCursor;
        public Vector2 cursorOffset = Vector2.zero;
        [Tooltip("Smoothly transitions to this scale when the mouse button is held down.")]
        public float cursorHoldScale = 1.25f;
        
        [Header("Audio Source")]
        public List<AudioSource> audioSource;
        
        [Header("Layer Settings")]
        public LayerMask decalLayerMask;

        #endregion

        #region Private Fields

        private Camera m_Camera;
        private Vector3 m_LastMousePos;
        private bool m_IsDragging;
        private bool m_IsDecalDrag;
        private Transform m_CurrentDecalCollider;
        private Vector2 m_AngularVelocity;
        private Vector3 m_DecalVelocity;
        private Collider m_SurfaceCollider;
        private float m_DecalZRotation; 

        private const float KCursorScaleSpeed = 10f;
        private Vector3 m_CursorOriginalScale;
        
        // Fields for managing decal property transitions
        private Runtime.DecalCollider m_ActiveDecalComponent;
        private float m_OriginalSurfaceOffset;
        private Vector3 m_OriginalMeshOffset;
        
        private readonly Dictionary<Runtime.DecalCollider, Coroutine> m_OffsetCoroutines = new();
        private readonly Dictionary<Runtime.DecalCollider, Coroutine> m_MeshOffsetCoroutines = new();
        
        #endregion

        #region Unity Lifecycle Methods

        private void Start()
        {
            m_Camera = Camera.main;
            if (dragSurfaceTarget != null)
                m_SurfaceCollider = dragSurfaceTarget.GetComponent<Collider>();

            if (customCursor != null)
            {
                customCursor.gameObject.SetActive(true);
                m_CursorOriginalScale = customCursor.localScale;
            }
        }

        private void Update()
        {
            HandleCustomCursor();
            HandleInput();
            HandleInertia();
        }

        #endregion

        #region Input & State Handling

        /// <summary>
        /// Manages custom cursor visibility, position, and scale animations.
        /// </summary>
        private void HandleCustomCursor()
        {
            if (customCursor == null) return;
            
            Cursor.visible = false;
            customCursor.position = Input.mousePosition + (Vector3)cursorOffset;

            bool isHeld = Input.GetMouseButton(0);
            Vector3 targetScale = isHeld ? Vector3.one * cursorHoldScale : m_CursorOriginalScale;
            customCursor.localScale = Vector3.Lerp(
                customCursor.localScale,
                targetScale,
                Time.deltaTime * KCursorScaleSpeed
            );
        }

        /// <summary>
        /// Handles mouse button down/up events and active dragging logic.
        /// </summary>
        private void HandleInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                OnDragStart();
            }
            
            if (Input.GetMouseButtonUp(0))
            {
                OnDragEnd();
            }
            
            if (m_IsDragging)
            {
                ProcessActiveDrag();
            }
        }

        /// <summary>
        /// Sets initial states when a drag operation begins.
        /// </summary>
        private void OnDragStart()
        {
            m_LastMousePos = Input.mousePosition;
            m_IsDragging = true;
            m_AngularVelocity = Vector2.zero;
            m_DecalVelocity = Vector3.zero;
            m_IsDecalDrag = false;
            m_CurrentDecalCollider = null;
            m_ActiveDecalComponent = null;

            if (m_Camera == null) return;
            
            Ray ray = m_Camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit decalHit, Mathf.Infinity, decalLayerMask))
            {
                if (Vector3.Dot(decalHit.normal, m_Camera.transform.forward) > 0f) return; // Ignore backfaces
                if (IsOccludedBySurface(ray, decalHit.distance)) return;
                
                var decal = decalHit.collider.GetComponent<Runtime.DecalCollider>();
                if (decal != null)
                {
                    if (Mathf.Abs(decal.surfaceOffset - m_OriginalSurfaceOffset) > 0.01f ||
                        Vector3.Distance(decal.meshOffset, m_OriginalMeshOffset) > 0.01f)
                        return;
                    
                    m_CurrentDecalCollider = decal.transform;
                    m_IsDecalDrag = true;
                    m_ActiveDecalComponent = decal;
                    
                    // --- Start Offset Transitions ---

                    // Track coroutine per decal instance.
                    if (m_OffsetCoroutines.ContainsKey(decal) && m_OffsetCoroutines[decal] != null)
                        StopCoroutine(m_OffsetCoroutines[decal]);

                    m_OffsetCoroutines[decal] = StartCoroutine(
                        SmoothSetOffset(decal, decalPivotOffset, offsetTransitionDuration, EaseOutBack)
                    );

                    if (m_MeshOffsetCoroutines.ContainsKey(decal) && m_MeshOffsetCoroutines[decal] != null)
                        StopCoroutine(m_MeshOffsetCoroutines[decal]);

                    m_MeshOffsetCoroutines[decal] = StartCoroutine(
                        SmoothSetMeshOffset(decal, decalMeshOffsetOnDrag, offsetTransitionDuration, EaseOutBack)
                    );
                    
                    // --- End Offset Transitions ---
                    
                    StartCoroutine(PlayAudioDelayed(audioSource[0], 0));
                }
            }
        }
        
        /// <summary>
        /// Cleans up states when a drag operation ends.
        /// </summary>
        private void OnDragEnd()
        {
            m_IsDragging = false;
            if (m_ActiveDecalComponent != null)
            {
                var decal = m_ActiveDecalComponent;

                // Surface offset revert
                if (m_OffsetCoroutines.ContainsKey(decal) && m_OffsetCoroutines[decal] != null)
                    StopCoroutine(m_OffsetCoroutines[decal]);
                m_OffsetCoroutines[decal] = StartCoroutine(
                    SmoothSetOffset(decal, m_OriginalSurfaceOffset, offsetTransitionDuration, EaseInBack)
                );

                // Mesh offset revert
                if (m_MeshOffsetCoroutines.ContainsKey(decal) && m_MeshOffsetCoroutines[decal] != null)
                    StopCoroutine(m_MeshOffsetCoroutines[decal]);
                m_MeshOffsetCoroutines[decal] = StartCoroutine(
                    SmoothSetMeshOffset(decal, m_OriginalMeshOffset, offsetTransitionDuration, EaseInBack)
                );

                StartCoroutine(PlayAudioDelayed(audioSource[1], offsetTransitionDuration));
                m_ActiveDecalComponent = null;
            }
        }

        /// <summary>
        /// Contains the logic for what happens frame-by-frame during an active drag.
        /// </summary>
        private void ProcessActiveDrag()
        {
            if (!m_IsDecalDrag && target != null)
            {
                // Rotate the main target object
                Vector3 delta = Input.mousePosition - m_LastMousePos;
                float yaw = -delta.x * sensitivity;
                float pitch = delta.y * sensitivity;

                target.Rotate(Vector3.up, yaw, Space.World);
                target.Rotate(Vector3.right, pitch, Space.World);

                m_AngularVelocity = new Vector2(yaw, pitch);
                m_LastMousePos = Input.mousePosition;
            }
            else if (m_IsDecalDrag && m_CurrentDecalCollider != null)
            {
                // Drag the decal and handle its rotation via scroll wheel
                DragDecalOnSurface();

                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0f && m_CurrentDecalCollider != null)
                {
                    // Each decal rotates around its own Z rotation.
                    Vector3 euler = m_CurrentDecalCollider.localEulerAngles;
                    euler.z += scroll * scrollSensitivity;
                    m_CurrentDecalCollider.localEulerAngles = euler;
                }
            }
        }
        
        #endregion

        #region Drag & Inertia Logic

        /// <summary>
        /// Handles object movement when not actively dragging (inertia).
        /// </summary>
        private void HandleInertia()
        {
            if (m_IsDragging) return;

            // When the user is not dragging, continuously update the target closest to the mouse.
            UpdateTargetAndSurfaceBasedOnProximity();

            // Apply inertia to the main target
            if (target != null && m_AngularVelocity.sqrMagnitude > 0.0001f)
            {
                target.Rotate(Vector3.up, m_AngularVelocity.x, Space.World);
                target.Rotate(Vector3.right, m_AngularVelocity.y, Space.World);
                m_AngularVelocity *= inertiaDamping;
            }

            // Apply inertia to the decal
            if (m_CurrentDecalCollider != null)
            {
                HandleDecalInertia();
            }
        }
        
        /// <summary>
        /// When the mouse is idle, updates the target object (with the "Target" tag) and its surface
        /// that are closest to the cursor. Improves performance by only assigning when the target changes.
        /// </summary>
        private void UpdateTargetAndSurfaceBasedOnProximity()
        {
            GameObject[] potentialTargets = GameObject.FindGameObjectsWithTag("Target");
            if (potentialTargets.Length == 0) return;

            Transform closestTarget = null;
            float minDistance = float.MaxValue;
            Vector2 mousePos = Input.mousePosition;

            foreach (var potentialTarget in potentialTargets)
            {
                Vector3 screenPos3D = m_Camera.WorldToScreenPoint(potentialTarget.transform.position);
                // Only consider objects that are in front of the camera
                if (screenPos3D.z > 0) 
                {
                    float distance = Vector2.Distance(mousePos, new Vector2(screenPos3D.x, screenPos3D.y));
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestTarget = potentialTarget.transform;
                    }
                }
            }

            // Only update if the closest target is different from the current target
            if (closestTarget != null && closestTarget != target)
            {
                target = closestTarget;

                // Assumes a specific hierarchy: Target -> child 0 -> child 0 is the surface
                if (target.childCount > 0 && target.GetChild(0).childCount > 0)
                {
                    dragSurfaceTarget = target.GetChild(0).GetChild(0);
                    m_SurfaceCollider = dragSurfaceTarget.GetComponent<Collider>();
                }
                else
                {
                    Debug.LogError($"The required hierarchy (0th child -> 0th child) was not found on object '{target.name}'! Drag surface could not be assigned.");
                    dragSurfaceTarget = null;
                    m_SurfaceCollider = null;
                }
            }
        }
        
        /// <summary>
        /// Moves the active decal along the surface based on mouse position.
        /// </summary>
        private void DragDecalOnSurface()
        {
            if (m_Camera == null || m_SurfaceCollider == null) return;

            Ray ray = m_Camera.ScreenPointToRay(Input.mousePosition);
            Vector3 targetPoint = default;
            Vector3 targetNormal = default;
            bool foundSurfacePoint = false;

            if (Physics.Raycast(ray, out RaycastHit surfaceHit, Mathf.Infinity, 1 << dragSurfaceTarget.gameObject.layer))
            {
                targetPoint = surfaceHit.point;
                targetNormal = surfaceHit.normal;
                foundSurfacePoint = true;
            }
            else
            {
                // Fallback for sphere colliders if raycast misses the edge
                if (m_SurfaceCollider is SphereCollider sphereCollider)
                {
                    Vector3 sphereCenter = sphereCollider.bounds.center;
                    float t = Vector3.Dot(sphereCenter - ray.origin, ray.direction);
                    Vector3 pointOnRay = ray.origin + ray.direction * t;
                    targetPoint = sphereCenter + (pointOnRay - sphereCenter).normalized * (sphereCollider.radius * dragSurfaceTarget.lossyScale.x);
                    targetNormal = (targetPoint - sphereCenter).normalized;
                    foundSurfacePoint = true;
                }
            }

            if (foundSurfacePoint)
            {
                Transform decalT = m_CurrentDecalCollider.transform;
                Vector3 previousPosition = decalT.position;
                
                Vector3 finalTargetPosition = targetPoint + targetNormal * decalPivotOffset;
                decalT.position = finalTargetPosition;
                
                if (Time.deltaTime > 0)
                {
                    m_DecalVelocity = (decalT.position - previousPosition) / Time.deltaTime;
                }

                Quaternion baseRot = Quaternion.LookRotation(-targetNormal, dragSurfaceTarget.up);

                float zRot = 0f;
                if (m_CurrentDecalCollider != null)
                    zRot = m_CurrentDecalCollider.localEulerAngles.z;

                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0f)
                    zRot += scroll * scrollSensitivity;

                Quaternion finalRot = baseRot * Quaternion.Euler(0f, 0f, zRot);
                decalT.rotation = Quaternion.Slerp(decalT.rotation, finalRot, Time.deltaTime * 20f);

            }
        }

        /// <summary>
        /// Simulates the decal sliding on the surface after being released.
        /// </summary>
        private void HandleDecalInertia()
        {
            if (m_CurrentDecalCollider == null || m_DecalVelocity.sqrMagnitude < 0.0001f || dragSurfaceTarget == null)
            {
                StopDecalInertia();
                return;
            }

            int surfaceMaskOnly = 1 << dragSurfaceTarget.gameObject.layer;
            Ray currentPosRay = new Ray(m_CurrentDecalCollider.position - m_CurrentDecalCollider.forward * 0.2f, m_CurrentDecalCollider.forward);

            if (!Physics.Raycast(currentPosRay, out RaycastHit currentHit, 0.4f, surfaceMaskOnly))
            {
                StopDecalInertia();
                return;
            }

            Vector3 currentNormal = currentHit.normal;
            Vector3 projectedVelocity = Vector3.ProjectOnPlane(m_DecalVelocity, currentNormal);
            float stepDistance = projectedVelocity.magnitude * Time.deltaTime;

            if (stepDistance <= 0.001f) { StopDecalInertia(); return; }

            Vector3 slideDirection = projectedVelocity.normalized;
            Ray lookAheadRay = new Ray(m_CurrentDecalCollider.position + currentNormal * 0.1f, slideDirection);

            if (Physics.Raycast(lookAheadRay, out RaycastHit nextHit, stepDistance * 1.5f, surfaceMaskOnly))
            {
                m_CurrentDecalCollider.position = nextHit.point; 
                Quaternion targetRotation = Quaternion.LookRotation(-nextHit.normal, dragSurfaceTarget.up);
                m_CurrentDecalCollider.rotation = Quaternion.Slerp(m_CurrentDecalCollider.rotation, targetRotation, Time.deltaTime * 15f);
                m_DecalVelocity = Quaternion.FromToRotation(currentNormal, nextHit.normal) * projectedVelocity;
            }
            else
            {
                m_CurrentDecalCollider.position = currentHit.point;
                m_CurrentDecalCollider.rotation = Quaternion.LookRotation(-currentHit.normal, dragSurfaceTarget.up);
                StopDecalInertia();
                return;
            }

            m_DecalVelocity *= decalInertiaDamping;
        }

        private void StopDecalInertia()
        {
            m_DecalVelocity = Vector3.zero;
            m_CurrentDecalCollider = null;
            m_IsDecalDrag = false;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Checks if another object (like the main drag surface) is between the camera and the decal.
        /// </summary>
        private bool IsOccludedBySurface(Ray ray, float decalHitDistance)
        {
            if (dragSurfaceTarget == null) return false;
            int surfaceMask = 1 << dragSurfaceTarget.gameObject.layer;
            return Physics.Raycast(ray, out _, decalHitDistance - 0.001f, surfaceMask);
        }

        /// <summary>
        /// Coroutine to smoothly transition the decal's surface offset over time using an easing function.
        /// </summary>
        private IEnumerator SmoothSetOffset(Runtime.DecalCollider decal, float targetValue, float duration, Func<float, float> easingFunction)
        {
            if (decal == null) yield break;

            float time = 0;
            float startValue = decal.surfaceOffset;

            while (time < duration)
            {
                float normalizedTime = time / duration;
                float easedProgress = easingFunction(normalizedTime);
                
                decal.surfaceOffset = Mathf.LerpUnclamped(startValue, targetValue, easedProgress);
                decal.RebuildSafe();

                time += Time.deltaTime;
                yield return null;
            }

            decal.surfaceOffset = targetValue;
            decal.RebuildSafe();
        }

        /// <summary>
        /// Coroutine to smoothly transition the decal's MESH offset over time using an easing function.
        /// </summary>
        private IEnumerator SmoothSetMeshOffset(Runtime.DecalCollider decal, Vector3 targetValue, float duration, Func<float, float> easingFunction)
        {
            if (decal == null) yield break;

            float time = 0;
            Vector3 startValue = decal.meshOffset;

            while (time < duration)
            {
                float normalizedTime = time / duration;
                float easedProgress = easingFunction(normalizedTime);
                
                decal.meshOffset = Vector3.LerpUnclamped(startValue, targetValue, easedProgress);
                decal.RebuildSafe();

                time += Time.deltaTime;
                yield return null;
            }

            decal.meshOffset = targetValue;
            decal.RebuildSafe();
        }
        
        private IEnumerator PlayAudioDelayed(AudioSource source, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (audioSource is { Count: > 0 } && source != null)
            {
                source.Play();
            }
        }

        #endregion

        #region Easing Functions

        private static float EaseInBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return c3 * x * x * x - c1 * x * x;
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1 + c3 * Mathf.Pow(x - 1, 3) + c1 * Mathf.Pow(x - 1, 2);
        }

        #endregion
    }
}
