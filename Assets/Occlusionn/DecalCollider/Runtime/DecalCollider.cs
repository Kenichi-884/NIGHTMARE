#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

namespace DecalCollider.Runtime
{
    /// <summary>
    /// Defines the primary axis for the decal projection.
    /// </summary>
    public enum ProjectionDirection
    {
        Up, Down, Forward, Back, Right, Left,
        ForwardUp, ForwardDown, BackUp, BackDown,
        RightUp, RightDown, LeftUp, LeftDown,
        ForwardRight, ForwardLeft, BackRight, BackLeft
    }

    /// <summary>
    /// Defines the coordinate space for the projection direction.
    /// </summary>
    public enum ProjectionSpace
    {
        [Tooltip("The projection direction is determined relative to the object's own local axes (right, up, forward).")]
        Local,
        [Tooltip("The projection direction is determined by the scene's absolute world axes, ignoring the object's rotation.")]
        World
    }

    public enum DecalMode
    {
        GridProjection, // Standard grid-based projection (Plane)
        MeshProjection  // Projects a custom Mesh or Sprite
    }

    /// <summary>
    /// Projects a grid or a custom mesh onto surrounding geometry to generate a decal visual and a corresponding physics collider.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Physics/Decal Collider")]
    [HelpURL("https://occlusionn.gitbook.io/docs")]
    public partial class DecalCollider : MonoBehaviour
    {
        #region Component Configuration

        [Tooltip("The local X and Y size of the projection box.")]
        public Vector2 size = new(1f, 1f);
        
        [Tooltip("X and Y scale multiplier for the Sprite or Mesh when in Mesh Projection mode.")]
        public Vector2 meshScale = new(1f, 1f);
        
        [Tooltip("The maximum distance the decal will project along its projection direction.")]
        public float maxDistance = 1f;
        
        [Tooltip("Determines whether the projection direction is relative to the object's local axes or the world's axes.")]
        public ProjectionSpace projectionSpace = ProjectionSpace.Local; 

        [Tooltip("The local axis along which the decal is projected.")]
        public ProjectionDirection projectionDirection = ProjectionDirection.Forward;

        [Tooltip("The layer mask defining which layers this decal can project onto.")]
        public LayerMask wrapMask = ~0;

        [Tooltip("The local offset of the projection box's center.")]
        public Vector3 center = Vector3.zero;
        
        [Tooltip("A local offset applied to the generated mesh vertices after projection.")]
        public Vector3 meshOffset = Vector3.zero;
        
        [Tooltip("A small world-space offset applied to hit points to lift the generated mesh off the surface it's projected on, preventing Z-fighting.")]
        public float surfaceOffset = 0.005f;

        [Tooltip("The local X and Y size of the raycasting grid. Can be smaller than 'Size' to improve performance by reducing the raycast area.")]
        public Vector2 raycastGridExtent = new(1f, 1f);

        [Tooltip("The resolution of the generated visual mesh. Grid Mode: N x N grid. Mesh Mode: Subdivision level (0-4).")]
        public int meshSubdivisions = 32;

        [Tooltip("The resolution of the generated collider mesh. Can be lower than mesh subdivisions for better physics performance.")]
        public int colliderSubdivisions = 16;
        
        [Tooltip("Grid: Creates a plane. Mesh: Projects an input mesh or Sprite.")]
        public DecalMode decalMode = DecalMode.GridProjection;

        [Tooltip("The mesh to project onto the surface (e.g., TMP mesh or custom model).")]
        public Mesh inputMesh; 
        
        [Tooltip("A scale factor applied to the collider mesh relative to the visual mesh.")]
        public float colliderScale = 1f;

        [Tooltip("If the material's main texture has alpha, triangles will be removed from the collider if all three of their vertices correspond to texture areas with alpha below this threshold.")]
        [Range(0f, 1f)]
        public float alphaThreshold = 0.5f;

        [Tooltip("The color of the debug rays shown in the Scene view when 'Show Debug Rays' is enabled.")]
        public Color rayColor = Color.cyan;

        [Tooltip("If true, the decal will automatically rebuild whenever its transform changes.")]
        public bool alwaysRebuild = true;
        
        [Tooltip("If true, rays pass through this object's own collider (preventing self-intersection).")]
        public bool ignoreSelf = true; 
        
        [Tooltip("If true, rebuilds are skipped when the object is not visible to the main camera.")]
        public bool cullIfInvisible = true;
        
        [Tooltip("Update interval in seconds for Live Link (TMP/Sprite changes).")]
        public float liveUpdateInterval = 0.1f;

        [Tooltip("If enabled, the Collider is also rebuilt during live updates. Keeping this OFF significantly improves performance.")]
        public bool updateColliderOnLive;
        
        [Tooltip("If enabled, automatically reduces mesh density based on distance from the camera.")]
        public bool useDynamicLOD;

        [Tooltip("Distance at which the decal switches to lowest quality (Quad).")]
        public float lodDistance = 40f;

        [Tooltip("Update frequency for LOD checks (seconds).")]
        public float lodCheckInterval = 0.5f;
        
        #endregion
        
        #region Parameter Cache
        
        private Vector2 m_CachedSize;
        private float m_CachedMaxDistance;
        private Vector3 m_CachedMeshOffset;
        private Vector2 m_CachedRaycastGridExtent;
        private int m_CachedMeshSubdivisions;
        private int m_CachedColliderSubdivisions;
        private float m_CachedColliderScale;
        private Vector3 m_CachedCenter;
        private float m_CachedAlphaThreshold;
        private string m_LastTMPText;
        private Color m_LastTMPColor;
        private float m_LastTMPSize;
        private bool m_CachedIgnoreSelf;
        private Mesh m_VirtualMesh;
        private MaterialPropertyBlock m_PropBlock;
        private Vector2 m_CachedMeshScale;
        private float m_LastUpdateTime;
        private float m_LastLODCheckTime;
        private int m_OriginalMeshSubdivisions = -1;
        private int m_OriginalColliderSubdivisions = -1;

        // Reusable lists to prevent garbage collection spikes when accessing mesh data
        private readonly List<Vector3> m_CacheVertices = new();
        private readonly List<Vector2> m_CacheUVs = new();
        private readonly List<Color32> m_CacheColors = new();
        private readonly List<int> m_FilteredTrisCache = new();
        
        #endregion

        #region Editor-Only Fields & Methods

        // These fields are controlled by the custom editor script.
        [HideInInspector] public bool editorToolEditColliderActive;
        [HideInInspector] public bool editorToolEditProjectionActive;
        [HideInInspector] public bool showBoundsGizmos;
        [HideInInspector] public bool showDebugRays;
        [HideInInspector] public TextMeshPro sourceTMP; 
        [HideInInspector] public SpriteRenderer sourceSpriteRenderer;

        private Sprite m_LastSprite;
        private bool m_LastFlipX;
        private bool m_LastFlipY;
        private class TextureCacheData
        {
            public Color32[] Colors;
            public int Width;
            public int Height;
        }
        private readonly Dictionary<Texture, TextureCacheData> m_TextureColorCache = new();
        
#if UNITY_EDITOR
        /// <summary>
        /// Draws gizmos in the scene view for visualization and debugging.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!enabled || !gameObject.activeInHierarchy) return;
            
            // Component checks
            if (m_MeshCollider == null) CacheComponents();
            bool hasStandard = m_MeshFilter != null;
            bool hasVirtual = sourceSpriteRenderer != null;
            
            if (!hasStandard && !hasVirtual) return;

            Transform currentTransform = transform;
            Vector3 projectionVec = GetProjectionVector(currentTransform).normalized;
            if (projectionVec == Vector3.zero) projectionVec = -currentTransform.up;

            float scaleX = Mathf.Abs(currentTransform.localScale.x);
            float scaleY = Mathf.Abs(currentTransform.localScale.y);
            float scaleZ = Mathf.Abs(currentTransform.localScale.z);

            // --- GIZMO BOX LOGIC ---
            // The yellow Gizmo box represents the 'Projection Size'.
            // The content (Sprite/Mesh) scales independently using 'meshScale',
            // but the box visualization strictly follows the 'size' parameter.
            Vector3 gizmoBoxSize = new Vector3(
                size.x * scaleX,
                size.y * scaleY,
                maxDistance * scaleZ
            );
            // -----------------------

            Matrix4x4 originalMatrix = Gizmos.matrix;
            Vector3 decalVolumeWorldCenter = currentTransform.TransformPoint(center);
            Vector3 gizmoUp = GetOrthogonalUpVector(currentTransform, projectionVec);
            Quaternion gizmoRotation = Quaternion.LookRotation(projectionVec, gizmoUp);
            Matrix4x4 handleMatrix = Matrix4x4.TRS(decalVolumeWorldCenter, gizmoRotation, Vector3.one);

            // --- CASE A: COLLIDER EDIT MODE (Detailed View) ---
            if (editorToolEditColliderActive)
            {
                Gizmos.color = Color.yellow;
                Gizmos.matrix = handleMatrix;

                Vector3 boxCenterInGizmoSpace = Vector3.forward * (gizmoBoxSize.z * 0.5f);
                Gizmos.DrawWireCube(boxCenterInGizmoSpace, gizmoBoxSize);
                
                Gizmos.matrix = originalMatrix;

                // Draw semi-transparent projection face
                Vector3[] faceWorldVerts = {
                    handleMatrix.MultiplyPoint3x4(new Vector3(-0.5f * gizmoBoxSize.x, -0.5f * gizmoBoxSize.y, gizmoBoxSize.z)),
                    handleMatrix.MultiplyPoint3x4(new Vector3( 0.5f * gizmoBoxSize.x, -0.5f * gizmoBoxSize.y, gizmoBoxSize.z)),
                    handleMatrix.MultiplyPoint3x4(new Vector3( 0.5f * gizmoBoxSize.x,  0.5f * gizmoBoxSize.y, gizmoBoxSize.z)),
                    handleMatrix.MultiplyPoint3x4(new Vector3(-0.5f * gizmoBoxSize.x,  0.5f * gizmoBoxSize.y, gizmoBoxSize.z))
                };
                
                Color projectionFaceColor = new Color(1f, 1f, 0f, 0.15f);
                Handles.color = projectionFaceColor;
                Handles.DrawAAConvexPolygon(faceWorldVerts);
            }
            // --- CASE B: STANDARD VIEW (Simple Box) ---
            else
            {
                // Draw only when selected and collider is not convex OR virtual mesh exists
                if ((m_MeshCollider != null && !m_MeshCollider.convex) || hasVirtual)
                {
                    Gizmos.color = new Color(0.5f, 0.95f, 0.5f, 1); // Light Green
                    Gizmos.matrix = handleMatrix;
                    
                    Vector3 boxCenterInGizmoSpace = Vector3.forward * (gizmoBoxSize.z * 0.5f);
                    Gizmos.DrawWireCube(boxCenterInGizmoSpace, gizmoBoxSize);
                    
                    Gizmos.matrix = originalMatrix;
                }
            }

            // --- DEBUG RAY DRAWING ---
            if (showDebugRays && m_RayStarts.Count > 0)
            {
                int step = Mathf.Max(1, meshSubdivisions > 0 ? meshSubdivisions / 4 : 1);
                
                Texture2D tex = null;
                if (m_MeshRenderer != null && m_MeshRenderer.sharedMaterial != null)
                    tex = m_MeshRenderer.sharedMaterial.mainTexture as Texture2D;
                else if (sourceSpriteRenderer != null && sourceSpriteRenderer.sprite != null)
                    tex = sourceSpriteRenderer.sprite.texture;

                // Optimization: Limit the number of debug rays drawn to prevent Editor lag.
                const int maxGizmoRays = 1000; 
                int drawnCount = 0;

                for (int i = 0; i < m_RayStarts.Count; i += step)
                {
                    // Safety Break: Stop drawing if we exceed the limit
                    if (drawnCount > maxGizmoRays) break;

                    if (i >= m_RayHitSuccess.Count || !m_RayHitSuccess[i]) continue;

                    // Optimization: Skip expensive texture checks inside Gizmos if simpler checks fail
                    bool shouldDraw = true;

                    // Optional: Only do the texture lookup if absolutely necessary
                    // (Keeping existing logic but wrapping it for safety)
                    if (tex != null && tex.isReadable && i < m_RayUVs.Count)
                    {
                        // Simple bounds check before calling GetPixelBilinear
                        Vector2 uv = m_RayUVs[i];
                        if (uv.x >= 0 && uv is { x: <= 1, y: >= 0 and <= 1 })
                        {
                            try {
                                // Note: GetPixelBilinear is slow in Gizmos, but acceptable with the limit above.
                                if (tex.GetPixelBilinear(uv.x, uv.y).a < alphaThreshold) shouldDraw = false;
                            }
                            catch { /* ignored */ }
                        }
                    }
    
                    if (shouldDraw && i < m_RayStarts.Count && i < m_RayHits.Count)
                    {
                        Gizmos.color = rayColor;
                        Gizmos.DrawLine(m_RayStarts[i], m_RayHits[i]);
                        // Sphere drawing is very heavy, reducing radius or removing it helps
                        Gizmos.DrawSphere(m_RayHits[i], 0.005f); 
                        drawnCount++;
                    }
                }
            }
        }
#endif
        #endregion

        #region Public API
        
        public void RebuildSafe()
        {
            ClampParameters();
            if (!this || !enabled || !gameObject.activeInHierarchy) { ClearMeshes(); return; }
    
            m_HitObjectsCache.Clear();
            m_LastRayHitCount = 0; 
            var sw = Stopwatch.StartNew();

            CacheComponents();
            HandleSkinnedMeshBaking();
            Rebuild();

            sw.Stop();
    
            long visualMem = 0; int visualTris = 0;
            if (m_MeshFilter != null && m_MeshFilter.sharedMesh != null)
            {
                visualMem = Profiler.GetRuntimeMemorySizeLong(m_MeshFilter.sharedMesh);
                visualTris = m_MeshFilter.sharedMesh.triangles.Length / 3;
            }
            else if (m_VirtualMesh != null) 
            {
                visualMem = Profiler.GetRuntimeMemorySizeLong(m_VirtualMesh);
                visualTris = m_VirtualMesh.triangles.Length / 3;
            }
            long colliderMem = m_MeshCollider && m_MeshCollider.sharedMesh ? Profiler.GetRuntimeMemorySizeLong(m_MeshCollider.sharedMesh) : 0;
            
            m_LastStats = new RebuildStats
            {
                TrianglesVisual   = visualTris,
                TrianglesCollider = m_MeshCollider && m_MeshCollider.sharedMesh ? m_MeshCollider.sharedMesh.triangles.Length / 3 : 0,
                RaysHit           = m_LastRayHitCount,
                BuildTimeMS       = sw.Elapsed.TotalMilliseconds,
                MemoryKb          = (visualMem + colliderMem) / 1024f
            };
        }

        /// <summary>
        /// Gets a list of the unique GameObjects that were hit by rays during the last rebuild.
        /// </summary>
        /// <returns>A new list containing the GameObjects that were hit.</returns>
        public List<GameObject> GetHitObjects()
        {
            return new List<GameObject>(m_HitObjectsCache);
        }
        
        /// <summary>
        /// Projects a custom mesh using cached lists to minimize memory allocation.
        /// </summary>
        private void ProjectCustomMesh(
            Mesh sourceMesh,
            bool recordDebug,
            out Vector3[] outVerts,
            out Vector3[] outNormals,
            out Vector2[] outUVs,
            out Color32[] outColors, 
            out bool[] outValidVerts,
            out int hitCount)
        {
            // Temporarily disable collider if ignoring self
            bool wasColliderEnabled = false;
            if (ignoreSelf && m_MeshCollider != null) {
                wasColliderEnabled = m_MeshCollider.enabled;
                if (wasColliderEnabled && m_MeshCollider != null && m_MeshCollider.sharedMesh != null) 
                {
                    m_MeshCollider.enabled = false;
                }
            }

            // --- MEMORY OPTIMIZATION START ---
            // Instead of "sourceMesh.vertices" (which creates a new array copy), use GetVertices with cached lists.
            sourceMesh.GetVertices(m_CacheVertices);
            sourceMesh.GetUVs(0, m_CacheUVs);
            sourceMesh.GetColors(m_CacheColors); 
    
            int vertexCount = m_CacheVertices.Count;

            // Handle missing colors
            if (m_CacheColors.Count != vertexCount)
            {
                m_CacheColors.Clear();
                // Fill with white if missing
                Color32 white = new Color32(255, 255, 255, 255);
                for (int k = 0; k < vertexCount; k++) m_CacheColors.Add(white);
            }
    
            // Convert cached lists to arrays for output (still needed for final mesh, but we saved the input allocation)
            // Note: We access the List elements directly in the loop below.
            outUVs = m_CacheUVs.ToArray();
            outColors = m_CacheColors.ToArray();
    
            outVerts = new Vector3[vertexCount];
            outNormals = new Vector3[vertexCount];
            outValidVerts = new bool[vertexCount]; 
            // --- MEMORY OPTIMIZATION END ---

            Transform tf = transform;
            Vector3 projVec = GetProjectionVector(tf).normalized;
            if (projVec == Vector3.zero) projVec = -tf.up;

            float worldDepthScale = GetAxisScale(tf, projVec);
            float scaledMaxDistance = maxDistance * worldDepthScale;
            Matrix4x4 worldToLocal = tf.worldToLocalMatrix;

            if (recordDebug) {
                m_RayStarts.Clear(); m_RayHits.Clear(); m_RayHitSuccess.Clear(); m_RayUVs.Clear();
            }

            hitCount = 0;
            const float rayBacktrack = 0.01f; 

            // Calculate Basis once
            GetOrthogonalBasis(projVec, tf.up, tf.right, out var worldRight, out var worldUp);
            Vector3 localRight = tf.InverseTransformDirection(worldRight);
            Vector3 localUp = tf.InverseTransformDirection(worldUp);
            Vector3 localFwd = tf.InverseTransformDirection(projVec);

            // Cache parameters to avoid struct access in loop
            Vector3 centerCache = center;
            Vector3 meshOffsetCache = meshOffset;
            Vector2 meshScaleCache = meshScale;
            Vector2 sizeCache = size;
            int layerMask = wrapMask;

            for (int i = 0; i < vertexCount; i++)
            {
                // Access data from Cached Lists
                Vector3 rawVert = m_CacheVertices[i];

                float sx = rawVert.x * meshScaleCache.x;
                float sy = rawVert.y * meshScaleCache.y;
                float sz = rawVert.z; 

                // Rotation and Offsets
                Vector3 modifiedLocalPos = 
                    localRight * sx + 
                    localUp * sy + 
                    localFwd * sz + 
                    centerCache +      
                    meshOffsetCache;   

                // --- BOUNDS CHECK (CLIPPING) ---
                Vector3 posRelativeToBox = modifiedLocalPos - centerCache;
                float distX = Vector3.Dot(posRelativeToBox, localRight.normalized); // optimization: pre-normalize outside loop if possible
                float distY = Vector3.Dot(posRelativeToBox, localUp.normalized);
        
                bool insideBox = Mathf.Abs(distX) <= sizeCache.x * 0.5f + 0.001f && 
                                 Mathf.Abs(distY) <= sizeCache.y * 0.5f + 0.001f;
                
                if (!insideBox)
                {
                    outVerts[i] = modifiedLocalPos; 
                    outNormals[i] = -projVec;
                    continue; 
                }

                Vector3 vertexWorldPos = tf.TransformPoint(modifiedLocalPos);
                Vector3 rayOrigin = vertexWorldPos - projVec * rayBacktrack;
        
                Vector3 hitPos; 
                Vector3 hitNorm;
                bool isHit;

                if (Physics.Raycast(rayOrigin, projVec, out var hit, scaledMaxDistance + rayBacktrack, layerMask))
                {
                    hitPos = hit.point + hit.normal * surfaceOffset;
                    hitNorm = hit.normal;
                    isHit = true;
                    hitCount++;

                    if (hit.collider != null) {
                        bool isSelf = hit.collider.transform == tf; 
                        if (!ignoreSelf || !isSelf) m_HitObjectsCache.Add(hit.collider.gameObject);
                    }
                }
                else
                {
                    hitPos = vertexWorldPos;
                    hitNorm = -projVec;
                    isHit = false; 
                }

                outVerts[i] = worldToLocal.MultiplyPoint3x4(hitPos);
                outNormals[i] = worldToLocal.MultiplyVector(hitNorm).normalized;
                outValidVerts[i] = isHit;

                if (recordDebug) {
                    m_RayStarts.Add(rayOrigin); 
                    m_RayHits.Add(hitPos);
                    m_RayHitSuccess.Add(isHit);
                    m_RayUVs.Add(outUVs.Length > i ? outUVs[i] : Vector2.zero);
                }
            }
            
            if (hitCount == 0)
            {
                for (int i = 0; i < vertexCount; i++)
                {
                    outValidVerts[i] = true; 
                }
            }
            if (ignoreSelf && wasColliderEnabled && m_MeshCollider != null) {
                m_MeshCollider.enabled = true;
            }
        }

        /// <summary>
        /// Cleans up triangles that have invalid (clipped) vertices using a cached list to avoid GC.
        /// </summary>
        private int[] FilterTriangles(int[] originalTris, bool[] validVerts)
        {
            if (originalTris == null || validVerts == null) return Array.Empty<int>();

            // Optimization: Reuse the class-level list instead of allocating a new one every time
            m_FilteredTrisCache.Clear();
    
            // Ensure capacity to avoid internal resizing
            if (m_FilteredTrisCache.Capacity < originalTris.Length)
                m_FilteredTrisCache.Capacity = originalTris.Length;

            for (int i = 0; i < originalTris.Length; i += 3)
            {
                int a = originalTris[i];
                int b = originalTris[i + 1];
                int c = originalTris[i + 2];

                // Bounds Check & Validity Check
                if (a < validVerts.Length && b < validVerts.Length && c < validVerts.Length)
                {
                    if (validVerts[a] && validVerts[b] && validVerts[c])
                    {
                        m_FilteredTrisCache.Add(a);
                        m_FilteredTrisCache.Add(b);
                        m_FilteredTrisCache.Add(c);
                    }
                }
            }
            return m_FilteredTrisCache.ToArray();
        }

        /// <summary>
        /// Subdivides mesh data purely using Lists to avoid creating intermediate Mesh objects.
        /// Significantly reduces Garbage Collection pressure.
        /// </summary>
        private static Mesh SubdivideMesh(Mesh sourceMesh, int subdivisionLevel)
        {
            // Optimization: If level is 0, just return a copy.
            if (subdivisionLevel <= 0) return Instantiate(sourceMesh);

            // 1. Extract Initial Data
            // Using Lists immediately to handle dynamic growth
            List<Vector3> vertices = new List<Vector3>(sourceMesh.vertices);
            List<Vector3> normals = new List<Vector3>(sourceMesh.normals);
            List<Vector2> uvs = new List<Vector2>(sourceMesh.uv);
            List<Color32> colors = new List<Color32>(sourceMesh.colors32);
            List<int> triangles = new List<int>(sourceMesh.triangles);

            // Ensure color list matches vertex count (handle cases where source has no colors)
            if (colors.Count < vertices.Count)
            {
                colors.Clear(); // Mismatch fix
                for (int k = 0; k < vertices.Count; k++) colors.Add(new Color32(255, 255, 255, 255));
            }

            // 2. Iterative Subdivision (Data Only)
            // We modify the lists directly in each pass, never touching the Mesh API until the end.
            for (int i = 0; i < subdivisionLevel; i++)
            {
                // Safety Break: Stop if vertex count gets too high for Unity
                if (vertices.Count > 60000)
                {
                    Debug.LogWarning("[DecalCollider] Max vertex limit reached during subdivision.");
                    break;
                }

                List<int> newTriangles = new List<int>(triangles.Count * 4);
                Dictionary<long, int> midpointCache = new Dictionary<long, int>();

                for (int t = 0; t < triangles.Count; t += 3)
                {
                    int v0 = triangles[t];
                    int v1 = triangles[t + 1];
                    int v2 = triangles[t + 2];

                    int a = GetMidpoint(v0, v1);
                    int b = GetMidpoint(v1, v2);
                    int c = GetMidpoint(v2, v0);

                    // Add 4 new triangles
                    newTriangles.Add(v0); newTriangles.Add(a); newTriangles.Add(c);
                    newTriangles.Add(v1); newTriangles.Add(b); newTriangles.Add(a);
                    newTriangles.Add(v2); newTriangles.Add(c); newTriangles.Add(b);
                    newTriangles.Add(a);  newTriangles.Add(b); newTriangles.Add(c);
                }

                // Swap triangle lists for the next iteration
                triangles = newTriangles;

                // Local function to handle midpoint caching and interpolation
                int GetMidpoint(int p1, int p2)
                {
                    // Create a unique key for the edge (smaller index first)
                    long key = p1 < p2 ? ((long)p1 << 32) | (uint)p2 : ((long)p2 << 32) | (uint)p1;

                    if (midpointCache.TryGetValue(key, out int index)) return index;

                    // Interpolate Data
                    Vector3 pos = (vertices[p1] + vertices[p2]) * 0.5f;
                    Vector3 nrm = (normals[p1] + normals[p2]).normalized;
                    Vector2 uv  = (uvs[p1] + uvs[p2]) * 0.5f;
                    Color32 col = Color32.Lerp(colors[p1], colors[p2], 0.5f);

                    index = vertices.Count;
                    vertices.Add(pos);
                    normals.Add(nrm);
                    uvs.Add(uv);
                    colors.Add(col);

                    midpointCache.Add(key, index);
                    return index;
                }
            }

            // 3. Construct Final Mesh
            // Only created ONCE at the end of the process.
            Mesh finalMesh = new Mesh();
            // Optimization: IndexFormat.UInt32 allows for more than 65k vertices if needed (Unity 2017.3+)
            if (vertices.Count > 65000) finalMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
    
            finalMesh.SetVertices(vertices);
            finalMesh.SetNormals(normals);
            finalMesh.SetUVs(0, uvs);
            finalMesh.SetColors(colors);
            finalMesh.SetTriangles(triangles, 0);
    
            finalMesh.RecalculateBounds();

            return finalMesh;
        }
        
        private void GenerateGrid(
            int           subdivisions,
            Vector2       gridExtent,
            bool          shouldRecordDebugRays,
            out Vector3[] outVertices,
            out Vector3[] outNormals,
            out Vector2[] outUVs,
            out bool[]    outHitFlags)
        {
            Transform tf = transform;
            Vector3 projVec = GetProjectionVector(tf).normalized;
            if (projVec == Vector3.zero) projVec = -tf.up;

            float worldDepthScale   = GetAxisScale(tf, projVec);
            float scaledMaxDistance = maxDistance * worldDepthScale;
            Matrix4x4 worldToLocalMatrix = tf.worldToLocalMatrix;

            GetOrthogonalBasis(projVec, tf.up, tf.right, out var basisRight, out var basisUp);
            Vector3 localBasisRight = tf.InverseTransformDirection(basisRight).normalized;
            Vector3 localBasisUp    = tf.InverseTransformDirection(basisUp).normalized;
            Vector3 localProjDir    = tf.InverseTransformDirection(projVec).normalized;

            Vector3 offsetFromOrigin = meshOffset; 
            float planarComponentX   = Vector3.Dot(offsetFromOrigin, localBasisRight);
            float planarComponentY   = Vector3.Dot(offsetFromOrigin, localBasisUp);
            float depthInfluence     = Vector3.Dot(offsetFromOrigin, localProjDir);
            Vector3 planarOffsetLocal = localBasisRight * planarComponentX + localBasisUp * planarComponentY;
            Vector3 planarCenterLocal = center + planarOffsetLocal;

            // Disable Collider temporarily
            bool wasColliderEnabled = false;
            if (ignoreSelf && m_MeshCollider != null)
            {
                wasColliderEnabled = m_MeshCollider.enabled;
                if (wasColliderEnabled && m_MeshCollider.sharedMesh != null) {
                    m_MeshCollider.enabled = false;
                }
            }

            int verticesPerRow = subdivisions + 1;
            int vertexCount    = verticesPerRow * verticesPerRow;
            outVertices = new Vector3[vertexCount];
            outNormals  = new Vector3[vertexCount];
            outUVs      = new Vector2[vertexCount];
            outHitFlags = new bool[vertexCount];

            if (shouldRecordDebugRays)
            {
                m_RayStarts.Clear(); m_RayHits.Clear(); m_RayHitSuccess.Clear(); m_RayUVs.Clear();
            }

            bool anyVertexIsValid = false;
            float step = Mathf.Max(1, subdivisions);

            for (int r = 0; r <= subdivisions; r++)
            for (int c = 0; c <= subdivisions; c++)
            {
                int idx = r * verticesPerRow + c;
                float u = c / step;
                float v = r / step;
                outUVs[idx] = new Vector2(u, v);

                float px = (u - 0.5f) * gridExtent.x;
                float py = (v - 0.5f) * gridExtent.y;
                Vector3 localGridPoint = localBasisRight * px + localBasisUp * py;
                Vector3 rayLocalPoint  = localGridPoint + planarCenterLocal;
                Vector3 posRelativeToBoxCenter = localGridPoint + planarOffsetLocal;

                float finalPlanarX = Vector3.Dot(posRelativeToBoxCenter, localBasisRight);
                float finalPlanarY = Vector3.Dot(posRelativeToBoxCenter, localBasisUp);

                bool isInVisualBounds =
                    Mathf.Abs(finalPlanarX) <= size.x * 0.5f + KEpsilon &&
                    Mathf.Abs(finalPlanarY) <= size.y * 0.5f + KEpsilon;

                if (!isInVisualBounds)
                {
                    outHitFlags[idx] = false;
                    outVertices[idx] = Vector3.zero;
                    outNormals[idx]  = Vector3.zero;
                    continue;
                }

                Vector3 rayOriginPoint = tf.TransformPoint(rayLocalPoint);
                Vector3 hitPosition = Vector3.zero; 
                Vector3 hitNormal = -projVec;
                bool wasHitSuccessful = false;

                if (Physics.Raycast(rayOriginPoint - projVec * KRaycastStartOffset,
                        projVec,
                        out var hit3D,
                        scaledMaxDistance + KRaycastStartOffset,
                        wrapMask,
                        QueryTriggerInteraction.Ignore))
                {
                    // --- LIST FILTERING ---
                    if (hit3D.collider != null)
                    {
                        bool isSelf = hit3D.collider.gameObject == gameObject;
                        if (!ignoreSelf || !isSelf)
                        {
                            m_HitObjectsCache.Add(hit3D.collider.gameObject);
                        }
                    }
                    // ----------------------

                    hitPosition = hit3D.point;
                    hitNormal = hit3D.normal;
                    wasHitSuccessful  = true;
                }
                else
                {
                    // 2D Raycast logic
                    RaycastHit2D hit2D = Physics2D.GetRayIntersection(
                        new Ray(rayOriginPoint - projVec * KRaycastStartOffset, projVec),
                        scaledMaxDistance + KRaycastStartOffset,
                        wrapMask);
            
                    if (hit2D.collider != null)
                    {
                        // 2D Filter
                        bool isSelf = hit2D.collider.gameObject == gameObject;
                        if (!ignoreSelf || !isSelf)
                        {
                            m_HitObjectsCache.Add(hit2D.collider.gameObject);
                        }
                
                        hitPosition = new Vector3(hit2D.point.x, hit2D.point.y, hit2D.transform.position.z);
                        hitNormal = new Vector3(hit2D.normal.x, hit2D.normal.y, 0).normalized;
                        if (hitNormal.sqrMagnitude < KEpsilon) hitNormal = -projVec;
                
                        wasHitSuccessful = true;
                    }
                }

                if(wasHitSuccessful)
                {
                    Vector3 surfacePoint    = hitPosition + hitNormal * surfaceOffset;
                    Vector3 clampingSegment = rayOriginPoint - surfacePoint;
                    float   maxPullDistance = clampingSegment.magnitude;
                    float   clampedPull     = Mathf.Clamp(depthInfluence, 0, maxPullDistance);

                    hitPosition       = surfacePoint + (maxPullDistance > KEpsilon ? clampingSegment.normalized * clampedPull : Vector3.zero);
                    hitNormal         = hitNormal.sqrMagnitude < KEpsilon ? -projVec : hitNormal;
                }
                else
                {
                    hitPosition = rayOriginPoint + projVec * scaledMaxDistance;
                }
        
                bool isVertexValid = wasHitSuccessful;
                outHitFlags[idx]   = isVertexValid;
                anyVertexIsValid  |= isVertexValid;
        
                outVertices[idx] = worldToLocalMatrix.MultiplyPoint3x4(hitPosition);
                outNormals[idx]  = worldToLocalMatrix.MultiplyVector(hitNormal).normalized;

                if (shouldRecordDebugRays)
                {
                    m_RayStarts.Add(rayOriginPoint - projVec * KRaycastStartOffset);
                    m_RayHits.Add(hitPosition);
                    m_RayHitSuccess.Add(isVertexValid);
                    m_RayUVs.Add(outUVs[idx]);
                }
            }

            if (!anyVertexIsValid)
            {
                for (int i = 0; i < outVertices.Length; i++)
                {
                    float u = outUVs[i].x, v = outUVs[i].y;
                    float lx = (u - 0.5f) * gridExtent.x;
                    float ly = (v - 0.5f) * gridExtent.y;
                    Vector3 finalLocalPos = localBasisRight * lx + localBasisUp * ly;
                    Vector3 posRelativeToBoxCenter = finalLocalPos + planarOffsetLocal;
                    float finalPlanarX = Vector3.Dot(posRelativeToBoxCenter, localBasisRight);
                    float finalPlanarY = Vector3.Dot(posRelativeToBoxCenter, localBasisUp);

                    if (Mathf.Abs(finalPlanarX) > size.x * 0.5f + KEpsilon ||
                        Mathf.Abs(finalPlanarY) > size.y * 0.5f + KEpsilon)
                    {
                        outHitFlags[i] = false;
                        outVertices[i] = Vector3.zero;
                        outNormals[i]  = Vector3.zero;
                        continue;
                    }

                    Vector3 localPos =
                        localBasisRight * lx +
                        localBasisUp    * ly +
                        planarCenterLocal +
                        localProjDir    * Mathf.Clamp(depthInfluence, 0f, maxDistance);

                    outVertices[i] = localPos; 
                    outNormals[i]  = worldToLocalMatrix.MultiplyVector(-projVec).normalized;
                    outHitFlags[i] = true;
                }
            }

            // Restore Collider
            if (ignoreSelf && wasColliderEnabled && m_MeshCollider != null) 
            {
                m_MeshCollider.enabled = true;
            }
        }

        #endregion
        
        #region Internal State & Cache

        private MeshFilter m_MeshFilter;
        private MeshRenderer m_MeshRenderer;
        private MeshCollider m_MeshCollider;
        private SkinnedMeshRenderer m_SkinnedMeshRenderer;
        private bool m_IsPendingRebuild;

        // A flag to track if a rebuild has already been scheduled.
        private bool m_RebuildScheduled;

        // A HashSet to store the unique objects hit by rays, preventing duplicates.
        private readonly HashSet<GameObject> m_HitObjectsCache = new();
        
        // A counter for the number of successful ray hits.
        private int m_LastRayHitCount;
        
        // Caches for debug ray visualization
        private readonly List<Vector3> m_RayStarts = new();
        private readonly List<Vector3> m_RayHits = new();
        private readonly List<bool> m_RayHitSuccess = new();
        private readonly List<Vector2> m_RayUVs = new();
        
        private static bool _sIsExitingPlayMode; 

        // --- CONSTANTS ---
        private const float KRaycastStartOffset = 0.005f;
        public const float KEpsilon = 0.0001f; 
        
        #endregion

        #region Unity Lifecycle
        
        private void Awake()
        {
            CacheComponents();
            ClampParameters();
            if (Application.isPlaying) Rebuild();
        }

        private void OnEnable()
        {
            CacheComponents();
            if (m_MeshCollider != null) m_MeshCollider.enabled = true;
            
            ClampParameters();
            CacheParametersState();
            
            // Schedule a rebuild to ensure the decal is correctly generated on enable.
            if (isActiveAndEnabled && alwaysRebuild)
            {
#if UNITY_EDITOR
                EditorApplication.delayCall += () =>
                {
                    if (this && enabled && gameObject.activeInHierarchy)
                        RebuildSafe();
                };
#else
                if (Application.isPlaying) RebuildSafe();
#endif
            }
        }

        private void OnDisable()
        {
            if (m_MeshCollider != null) m_MeshCollider.enabled = false;
            
            // Re-enable SpriteRenderer if we disabled it
            if (sourceSpriteRenderer != null) sourceSpriteRenderer.enabled = true;
        }

        private void Update()
        {
            if (decalMode == DecalMode.MeshProjection)
            {
                bool needRebuild = false;

                // A) TMP CHECK
                if (sourceTMP != null)
                {
                    bool isChanged = sourceTMP.text != m_LastTMPText || 
                                     sourceTMP.color != m_LastTMPColor || 
                                     !Mathf.Approximately(sourceTMP.fontSize, m_LastTMPSize) ||
                                     sourceTMP.havePropertiesChanged;

                    if (isChanged && Time.time - m_LastUpdateTime > liveUpdateInterval)
                    {
                        m_LastTMPText = sourceTMP.text;
                        m_LastTMPColor = sourceTMP.color;
                        m_LastTMPSize = sourceTMP.fontSize;

                        sourceTMP.ForceMeshUpdate();
                        if (inputMesh != null && inputMesh.name.StartsWith("Auto")) DestroyImmediate(inputMesh);
                        inputMesh = Instantiate(sourceTMP.mesh);
                        inputMesh.name = "Auto_TMP_Snapshot";
                        needRebuild = true;
                    }
                }
                // B) SPRITE CHECK
                else if (sourceSpriteRenderer != null)
                {
                    bool isChanged = sourceSpriteRenderer.sprite != m_LastSprite ||
                                     sourceSpriteRenderer.flipX != m_LastFlipX ||
                                     sourceSpriteRenderer.flipY != m_LastFlipY;

                    if (isChanged && Time.time - m_LastUpdateTime > liveUpdateInterval)
                    {
                        m_LastSprite = sourceSpriteRenderer.sprite;
                        m_LastFlipX = sourceSpriteRenderer.flipX;
                        m_LastFlipY = sourceSpriteRenderer.flipY;

                        if (inputMesh != null && inputMesh.name.StartsWith("Auto")) DestroyImmediate(inputMesh);
                        
                        if (m_LastSprite != null)
                        {
                            inputMesh = BuildMeshFromSprite(m_LastSprite, m_LastFlipX, m_LastFlipY);
                            inputMesh.name = "Auto_Sprite_Snapshot";
                        }
                        else
                        {
                            inputMesh = null;
                        }
                        
                        needRebuild = true;
                    }
                }

                if (needRebuild)
                {
                    m_LastUpdateTime = Time.time;
                    if (updateColliderOnLive)
                    {
                        RebuildSafe(); 
                    }
                    else
                    {
                        ClampParameters();
                        GenerateAndApplyVisualMesh(); 
                    }
                }
            }

            if (HasTransformChanged())
            {
                m_IsPendingRebuild = true;
            }
        
            // 2. Check for Parameter Changes
            bool parametersChanged = size != m_CachedSize ||
                                     meshScale != m_CachedMeshScale ||
                                     !Mathf.Approximately(maxDistance, m_CachedMaxDistance) ||
                                     meshOffset != m_CachedMeshOffset ||
                                     raycastGridExtent != m_CachedRaycastGridExtent ||
                                     meshSubdivisions != m_CachedMeshSubdivisions ||
                                     colliderSubdivisions != m_CachedColliderSubdivisions ||
                                     !Mathf.Approximately(colliderScale, m_CachedColliderScale) ||
                                     center != m_CachedCenter || 
                                     !Mathf.Approximately(alphaThreshold, m_CachedAlphaThreshold) ||
                                     ignoreSelf != m_CachedIgnoreSelf;

            if (parametersChanged)
            {
                ClampParameters();
                CacheParametersState();
                m_IsPendingRebuild = true;
            }

            // 3. SMART REBUILD LOGIC (Optimization 1: Visibility Check)
            if (alwaysRebuild && m_IsPendingRebuild)
            {
                // If optimization is enabled and we are playing (not editor scene view)
                if (cullIfInvisible && Application.isPlaying)
                {
                    if (IsVisibleToMainCamera())
                    {
                        RebuildSafe();
                        m_IsPendingRebuild = false; // Task done
                    }
                    // Skipped! We keep m_IsPendingRebuild = true.
                    // It will try again next frame, but won't do heavy math until visible.
                }
                else
                {
                    // Default behavior (Editor or Optimization OFF)
                    RebuildSafe();
                    m_IsPendingRebuild = false;
                }
            }
            
            // --- FIX: Rendering Logic ---
            // If there is no MeshRenderer (Sprite Mode), we render the virtual mesh manually.
            if (m_MeshRenderer != null || sourceSpriteRenderer == null || m_VirtualMesh == null) return;
            Material mat = sourceSpriteRenderer.sharedMaterial;
            if (mat == null) return;
            // The SpriteRenderer must stay 'enabled' but invisible (Alpha=0).
            // When drawing the virtual mesh, we force Alpha back to 1.
                    
            m_PropBlock ??= new MaterialPropertyBlock();
            sourceSpriteRenderer.GetPropertyBlock(m_PropBlock);
                    
            Color drawColor = sourceSpriteRenderer.color;
            if (drawColor.a <= 0.01f) drawColor.a = 1f;
                    
            m_PropBlock.SetColor("_Color", drawColor);

            Graphics.DrawMesh(m_VirtualMesh, transform.localToWorldMatrix, mat, gameObject.layer, null, 0, m_PropBlock);
            
            // 4. DYNAMIC LOD SYSTEM
            if (useDynamicLOD && Application.isPlaying)
            {
                if (Time.time - m_LastLODCheckTime > lodCheckInterval)
                {
                    m_LastLODCheckTime = Time.time;
                    HandleLOD();
                }
            }
        }

        private IEnumerator Start()
        {
            if (!Application.isPlaying || !isActiveAndEnabled) yield break;
            yield return null; 
            
            CacheComponents();
            if (alwaysRebuild)
            {
                bool isVisualMeshMissing = m_MeshFilter == null || m_MeshFilter.sharedMesh == null || m_MeshFilter.sharedMesh.vertexCount < 3;
                bool isColliderMeshMissing = m_MeshCollider == null || m_MeshCollider.sharedMesh == null || m_MeshCollider.sharedMesh.vertexCount < 3;
            
                if (isVisualMeshMissing || isColliderMeshMissing)
                {
                    Debug.LogWarning($"[DecalCollider] Mesh was missing on Start for '{name}'. Forcing rebuild.", this);
                    RebuildSafe();
                }
            }
        }
        private void OnDestroy()
        {
            if (Application.isPlaying || _sIsExitingPlayMode) return;
            ClearMeshes();
        }

        private void Reset()
        {
            // Check for SpriteRenderer first
            var sr = GetComponent<SpriteRenderer>();
            
            // If NO SpriteRenderer, add standard Mesh components
            if (sr == null)
            {
                if (!GetComponent<MeshFilter>()) gameObject.AddComponent<MeshFilter>();
                if (!GetComponent<MeshRenderer>()) gameObject.AddComponent<MeshRenderer>();
            }
            
            if (!GetComponent<MeshCollider>()) gameObject.AddComponent<MeshCollider>();

            CacheComponents();
            ClampParameters();
            
            // Auto-configure if Sprite detected
            if (sr != null)
            {
                sourceSpriteRenderer = sr;
                decalMode = DecalMode.MeshProjection;
                TryAutoCaptureInputMesh();
            }
        }
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;

            ClampParameters();

            // --- AUTO CAPTURE ---
            if (decalMode == DecalMode.MeshProjection && inputMesh == null)
            {
                TryAutoCaptureInputMesh();
            }

            if (m_RebuildScheduled) return;

            if (this && enabled && isActiveAndEnabled && gameObject.activeInHierarchy)
            {
                EditorApplication.delayCall += () =>
                {
                    if (this != null && gameObject != null && !Equals(null))
                    {
                        if (enabled && isActiveAndEnabled)
                            RebuildSafe();
                    }
                    m_RebuildScheduled = false;
                };
                m_RebuildScheduled = true;
            }
        }
#endif
        
        #endregion
        
        #region Core Generation Logic
        
        private void HandleLOD()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            // Cache original values on first run.
            if (m_OriginalMeshSubdivisions == -1)
            {
                m_OriginalMeshSubdivisions = meshSubdivisions;
                m_OriginalColliderSubdivisions = colliderSubdivisions;
            }

            float distance = Vector3.Distance(transform.position, cam.transform.position);
            
            // Determine target resolutions.
            int targetMeshSub, targetColSub;

            if (distance > lodDistance)
            {
                // Very far: lowest quality (only 1-2 subdivisions or quad).
                // Grid mode uses 1, mesh mode also uses minimum (1).
                targetMeshSub = 1; 
                targetColSub = 1; 
            }
            else if (distance > lodDistance * 0.5f)
            {
                // Mid distance: half quality.
                targetMeshSub = Mathf.Max(2, m_OriginalMeshSubdivisions / 2);
                targetColSub = Mathf.Max(2, m_OriginalColliderSubdivisions / 2);
            }
            else
            {
                // Near: original quality.
                targetMeshSub = m_OriginalMeshSubdivisions;
                targetColSub = m_OriginalColliderSubdivisions;
            }

            // Apply only when a change is required.
            if (meshSubdivisions != targetMeshSub || colliderSubdivisions != targetColSub)
            {
                meshSubdivisions = targetMeshSub;
                colliderSubdivisions = targetColSub;
                
                // Update cached state to prevent endless rebuild loops in Update.
                m_CachedMeshSubdivisions = meshSubdivisions;
                m_CachedColliderSubdivisions = colliderSubdivisions;

                // Request rebuild (visibility check already runs in Update).
                m_IsPendingRebuild = true;
            }
        }

        /// <summary>
        /// Internal rebuild method that generates and applies both the visual and collider meshes.
        /// </summary>
        private void Rebuild()
        {
            if (!enabled || !gameObject.activeInHierarchy)
            {
                ClearMeshes();
                return;
            }

            CacheComponents();

            // FIX: Re-find SpriteRenderer
            if (sourceSpriteRenderer == null) sourceSpriteRenderer = GetComponent<SpriteRenderer>();

            bool isSpriteMode = sourceSpriteRenderer != null;
            bool isMeshFilterMissing = m_MeshFilter == null;

            // Only error if NOT in Sprite mode and MeshFilter is missing
            if (!isSpriteMode && isMeshFilterMissing)
            {
                return; 
            }

            GenerateAndApplyVisualMesh();
            GenerateAndApplyColliderMesh();
            
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (m_MeshFilter != null) EditorUtility.SetDirty(m_MeshFilter);
                if (m_MeshCollider != null) EditorUtility.SetDirty(m_MeshCollider);
                EditorUtility.SetDirty(this);
            }
#endif
        }
        
        private void TryAutoCaptureInputMesh()
        {
            // 1. TMP CHECK
            var tmp = GetComponent<TextMeshPro>();
            if (tmp != null)
            {
                sourceTMP = tmp;
                decalMode = DecalMode.MeshProjection;
                sourceSpriteRenderer = null; 

                if (inputMesh == null)
                {
                    tmp.ForceMeshUpdate();
                    if (tmp.mesh != null)
                    {
                        inputMesh = Instantiate(tmp.mesh);
                        inputMesh.name = "Auto_TMP_Snapshot";
                    }
                }
                return;
            }

            // 2. SPRITE RENDERER CHECK
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = GetComponentInParent<SpriteRenderer>();

            if (sr != null && sr.sprite != null)
            {
                if (sourceSpriteRenderer != sr) 
                {
                    sourceSpriteRenderer = sr;
                    m_LastSprite = sr.sprite;
                    m_LastFlipX = sr.flipX;
                    m_LastFlipY = sr.flipY;
                }

                decalMode = DecalMode.MeshProjection;
                sourceTMP = null;

                // FIX: SYNC BOX AND CENTER TO SPRITE
                size = sr.sprite.bounds.size;
                center = sr.sprite.bounds.center; 

                if (inputMesh == null || inputMesh.name != "Generated_Sprite_Quad")
                {
                    inputMesh = BuildMeshFromSprite(sr.sprite, sr.flipX, sr.flipY);
                    inputMesh.name = "Generated_Sprite_Quad";
                }
            }
        }

        /// <summary>
        /// Generates a clean Quad mesh based on Sprite bounds.
        /// Avoids 'Tight Mesh' randomness, ensuring perfect subdivision.
        /// </summary>
        private static Mesh BuildMeshFromSprite(Sprite sprite, bool flipX, bool flipY)
        {
            var newMesh = new Mesh
            {
                name = "Generated_Sprite_Quad"
            };

            // 1. Get Sprite Bounds
            Bounds b = sprite.bounds;
            Vector3 min = b.min;
            Vector3 max = b.max;

            // 2. Create Clean Corners
            Vector3[] vertices = new Vector3[4];
            vertices[0] = new Vector3(min.x, min.y, 0); // Bottom Left
            vertices[1] = new Vector3(min.x, max.y, 0); // Top Left
            vertices[2] = new Vector3(max.x, max.y, 0); // Top Right
            vertices[3] = new Vector3(max.x, min.y, 0); // Bottom Right

            // 3. Flip Logic
            if (flipX)
            {
                float temp = vertices[0].x; vertices[0].x = vertices[3].x; vertices[3].x = temp;
                temp = vertices[1].x; vertices[1].x = vertices[2].x; vertices[2].x = temp;
            }
            if (flipY)
            {
                float temp = vertices[0].y; vertices[0].y = vertices[1].y; vertices[1].y = temp;
                temp = vertices[3].y; vertices[3].y = vertices[2].y; vertices[2].y = temp;
            }

            // 4. Calculate UVs
            Vector2[] originalUVs = sprite.uv;
            Vector2 minUV = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 maxUV = new Vector2(float.MinValue, float.MinValue);

            foreach (var uv in originalUVs)
            {
                if (uv.x < minUV.x) minUV.x = uv.x;
                if (uv.y < minUV.y) minUV.y = uv.y;
                if (uv.x > maxUV.x) maxUV.x = uv.x;
                if (uv.y > maxUV.y) maxUV.y = uv.y;
            }

            Vector2[] uvs = new Vector2[4];
            uvs[0] = new Vector2(minUV.x, minUV.y); 
            uvs[1] = new Vector2(minUV.x, maxUV.y); 
            uvs[2] = new Vector2(maxUV.x, maxUV.y); 
            uvs[3] = new Vector2(maxUV.x, minUV.y); 

            // 5. Create Standard Triangles
            int[] triangles = {
                0, 1, 2,
                0, 2, 3
            };

            newMesh.vertices = vertices;
            newMesh.uv = uvs;
            newMesh.triangles = triangles;
            newMesh.RecalculateNormals();
            newMesh.RecalculateBounds();

            return newMesh;
        }

        /// <summary>
        /// Gets the effective world scale along a given direction vector.
        /// </summary>
        private static float GetAxisScale(Transform tf, Vector3 dir)
        {
            dir = dir.normalized;
            float dotX = Mathf.Abs(Vector3.Dot(dir, tf.right));
            float dotY = Mathf.Abs(Vector3.Dot(dir, tf.up));
            float dotZ = Mathf.Abs(Vector3.Dot(dir, tf.forward));

            if (dotX >= dotY && dotX >= dotZ) return Mathf.Abs(tf.localScale.x);
            if (dotY >= dotX && dotY >= dotZ) return Mathf.Abs(tf.localScale.y);
            return Mathf.Abs(tf.localScale.z);
        }

        #endregion
        
        #region Utility & Management
        
        /// <summary>
        /// Fetches texture data from GPU to CPU once and caches it as a Color32 array.
        /// avoids repeated calls to ReadPixels or GetPixelBilinear.
        /// </summary>
        private TextureCacheData GetReadableTextureData(Texture source)
        {
            if (source == null) return null;

            // 1. Check Cache
            if (m_TextureColorCache.TryGetValue(source, out var data))
            {
                if (data != null) return data;
            }

            Color32[] retrievedColors = null;
            int width = source.width;
            int height = source.height;

            // 2. Optimization: If texture is already readable, get data directly
            if (source is Texture2D { isReadable: true } t2d)
            {
                try 
                {
                    retrievedColors = t2d.GetPixels32();
                }
                catch 
                { 
                    // Fallback to RenderTexture method if direct access fails
                }
            }

            // 3. GPU Readback (RenderTexture method)
            // Only executed if not cached and not readable directly
            if (retrievedColors == null)
            {
                RenderTexture tmp = RenderTexture.GetTemporary(
                    width, 
                    height, 
                    0, 
                    RenderTextureFormat.Default, 
                    RenderTextureReadWrite.Linear);

                // Blit to temporary RT
                Graphics.Blit(source, tmp);
        
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = tmp;

                // Create a temporary Texture2D to read pixels
                Texture2D tempTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tempTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tempTex.Apply();

                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(tmp);

                // Extract raw data
                retrievedColors = tempTex.GetPixels32();

                // Cleanup the temporary texture immediately
                if (Application.isPlaying) Destroy(tempTex); else DestroyImmediate(tempTex);
            }

            // 4. Cache and Return
            var cacheData = new TextureCacheData
            {
                Colors = retrievedColors,
                Width = width,
                Height = height
            };

            m_TextureColorCache[source] = cacheData;
            return cacheData;
        }
       
       

        /// <summary>
        /// Caches the state of the current parameters to check for changes in the next frame.
        /// </summary>
        private void CacheParametersState()
        {
            m_CachedSize = size;
            m_CachedMeshScale = meshScale;
            m_CachedMaxDistance = maxDistance;
            m_CachedMeshOffset = meshOffset;
            m_CachedRaycastGridExtent = raycastGridExtent;
            m_CachedMeshSubdivisions = meshSubdivisions;
            m_CachedColliderSubdivisions = colliderSubdivisions;
            m_CachedColliderScale = colliderScale;
            m_CachedCenter = center;
            m_CachedAlphaThreshold = alphaThreshold;
            m_CachedIgnoreSelf = ignoreSelf;
        }

        /// <summary>
        /// Clamps public parameters to valid ranges to prevent errors.
        /// </summary>
        private void ClampParameters()
        {
            size.x = Mathf.Max(0.001f, size.x);
            size.y = Mathf.Max(0.001f, size.y);
            raycastGridExtent.x = Mathf.Max(0.001f, raycastGridExtent.x);
            raycastGridExtent.y = Mathf.Max(0.001f, raycastGridExtent.y);

            meshSubdivisions = Mathf.Clamp(meshSubdivisions, 1, 128); 
            colliderSubdivisions = Mathf.Clamp(colliderSubdivisions, 1, 64);

            colliderScale = Mathf.Max(0.001f, colliderScale);
            maxDistance = Mathf.Max(0.001f, maxDistance);
            surfaceOffset = Mathf.Clamp(surfaceOffset, 0f, maxDistance * 0.5f);

            // Offset Clamping logic
            Transform tf = transform;
            Vector3 projDirWorld = GetProjectionVector(tf).normalized;
            GetOrthogonalBasis(projDirWorld, tf.up, tf.right, out var rightWorld, out var upWorld);
            Vector3 offsetAsWorldVector = tf.TransformDirection(meshOffset);
            Vector3 depthComponent = Vector3.Project(offsetAsWorldVector, projDirWorld);
            Vector3 planarComponent = offsetAsWorldVector - depthComponent;
            float planarDistX = Vector3.Dot(planarComponent, rightWorld);
            float planarDistY = Vector3.Dot(planarComponent, upWorld);
            planarDistX = Mathf.Clamp(planarDistX, -size.x * 0.5f, size.x * 0.5f);
            planarDistY = Mathf.Clamp(planarDistY, -size.y * 0.5f, size.y * 0.5f);
            float depthDist = depthComponent.magnitude;
            if (Vector3.Dot(depthComponent, projDirWorld) < 0) depthDist *= -1;
            depthDist = Mathf.Clamp(depthDist, 0, maxDistance);
            Vector3 newOffsetWorld = rightWorld * planarDistX + upWorld * planarDistY + projDirWorld * depthDist;
            meshOffset = tf.InverseTransformDirection(newOffsetWorld);
            float epsilon = 1e-6f;
            if (Mathf.Abs(meshOffset.x) < epsilon) meshOffset.x = 0f;
            if (Mathf.Abs(meshOffset.y) < epsilon) meshOffset.y = 0f;
            if (Mathf.Abs(meshOffset.z) < epsilon) meshOffset.z = 0f;
        }

        private void GenerateAndApplyVisualMesh()
        {
            CacheComponents();
            Mesh visualMesh;

            if (decalMode == DecalMode.GridProjection)
            {
                GenerateGrid(meshSubdivisions, raycastGridExtent, true, out var vv, out var vn, out var vu, out var vh);
                m_LastRayHitCount = vh.Count(hit => hit);
                visualMesh = BuildPlaneMeshFromGrid(vv, vn, vu, vh, meshSubdivisions);
            }
            else 
            {
                if (inputMesh == null) return;
                
                Mesh detailedMesh = null;

                // --- LINEAR VS RECURSIVE CHECK ---
                if (inputMesh.vertexCount == 4)
                {
                    detailedMesh = SubdivideQuadLinearly(inputMesh, meshSubdivisions);
                }
                
                if (detailedMesh == null)
                {
                    int iterations = CalculateAutoSubdivisionLevel(inputMesh, meshSubdivisions);
                    detailedMesh = SubdivideMesh(inputMesh, iterations);
                }
                // ---------------------------------

                ProjectCustomMesh(detailedMesh, true, out var pv, out var pn, out var pu, out var pc, out var validVerts, out var hitCount);
                m_LastRayHitCount = hitCount;

                int[] filteredTriangles = FilterTriangles(detailedMesh.triangles, validVerts);

                visualMesh = new Mesh
                {
                    name = "Decal_Projected_" + inputMesh.name,
                    vertices = pv, normals = pn, uv = pu, colors32 = pc, triangles = filteredTriangles
                };
                visualMesh.RecalculateBounds();
                visualMesh.MarkDynamic();

                if (detailedMesh != inputMesh) DestroyImmediate(detailedMesh);
            }

            if (m_MeshFilter != null)
            {
                CleanupOldMesh(m_MeshFilter.sharedMesh, "Decal_Visual");
                m_MeshFilter.sharedMesh = visualMesh;

                if (decalMode == DecalMode.MeshProjection && sourceSpriteRenderer != null)
                {
                    if (m_MeshRenderer != null)
                    {
                        if (m_MeshRenderer.sharedMaterial == null || m_MeshRenderer.sharedMaterial.shader.name.Contains("InternalErrorShader")) {
                            m_MeshRenderer.sharedMaterial = sourceSpriteRenderer.sharedMaterial;
                        }

                        m_PropBlock ??= new MaterialPropertyBlock();
                        m_MeshRenderer.GetPropertyBlock(m_PropBlock);
                        m_PropBlock.SetColor("_Color", sourceSpriteRenderer.color);
                        if (sourceSpriteRenderer.sprite != null)
                            m_PropBlock.SetTexture("_MainTex", sourceSpriteRenderer.sprite.texture);
                        m_MeshRenderer.SetPropertyBlock(m_PropBlock);
                    }
                    sourceSpriteRenderer.enabled = false;
                }
            }
            else
            {
                if (m_VirtualMesh != null && m_VirtualMesh != visualMesh) DestroyImmediate(m_VirtualMesh);
                m_VirtualMesh = visualMesh;

                if (sourceSpriteRenderer != null)
                {
                    Color originalColor = sourceSpriteRenderer.color; originalColor.a = 1f;
                    m_PropBlock ??= new MaterialPropertyBlock();
                    sourceSpriteRenderer.GetPropertyBlock(m_PropBlock);
                    m_PropBlock.SetColor("_Color", originalColor);
                    Color invisibleColor = sourceSpriteRenderer.color; invisibleColor.a = 0f;
                    sourceSpriteRenderer.color = invisibleColor;
                }
            }
        }
        /// <summary>
        /// Converts the user-specified linear resolution (e.g., 64, 128) into a 
        /// "Subdivision Level" (0-5) by comparing it against the mesh's current vertex count.
        /// </summary>
        private static int CalculateAutoSubdivisionLevel(Mesh mesh, int targetResolution)
        {
            if (mesh == null) return 0;

            // 1. Determine Target Vertex Count
            // Grid logic: If input is 128, we target 128x128 = ~16,384 vertices.
            long targetVertexCount = (long)targetResolution * targetResolution;

            // 2. Get Current Vertex Count
            int currentVertexCount = mesh.vertexCount;

            // 3. Calculate required subdivisions
            // Each subdivision level quadruples (x4) the vertex count.
            int requiredLevel = 0;
    
            // Safety limit: Do not exceed Level 6 (4^6 = 4096x growth), otherwise Unity might crash due to memory.
            while (currentVertexCount < targetVertexCount && requiredLevel < 6)
            {
                currentVertexCount *= 4;
                requiredLevel++;
            }

            return requiredLevel;
        }
        private void GenerateAndApplyColliderMesh()
        {
            // 1. Check for MeshCollider component
            if (m_MeshCollider == null) 
            { 
                CacheComponents(); 
                if (m_MeshCollider == null) return; 
            }
            
            Mesh colliderMesh;

            // ---------------------------------------------------------
            // MODE A: MESH PROJECTION (Sprite / Text / Custom Model)
            // ---------------------------------------------------------
            if (decalMode == DecalMode.MeshProjection)
            {
                if (inputMesh == null) return;
                
                // Create a temporary copy of the source mesh to modify
                Mesh sourceForCollider = Instantiate(inputMesh);

                // --- Apply Collider Scale ---
                // We scale the vertices relative to their center before projection
                if (Mathf.Abs(colliderScale - 1f) > 0.001f && colliderScale > 0.01f)
                {
                    sourceForCollider.RecalculateBounds();
                    Vector3 boundsCenter = sourceForCollider.bounds.center;
                    Vector3[] sVerts = sourceForCollider.vertices;
                    for(int i=0; i<sVerts.Length; i++) {
                        Vector3 dir = sVerts[i] - boundsCenter;
                        sVerts[i] = boundsCenter + dir * colliderScale;
                    }
                    sourceForCollider.vertices = sVerts;
                }

                // --- NEW: SUBDIVISION STRATEGY ---
                Mesh detailedMesh;

                // 1. Linear Grid Strategy (For Quads/Sprites)
                // If the input is a simple Quad, we apply exact linear subdivision.
                // e.g., Density 16 -> 16x16 grid.
                if (sourceForCollider.vertexCount == 4)
                {
                    detailedMesh = SubdivideQuadLinearly(sourceForCollider, colliderSubdivisions);
                }
                // 2. Recursive Strategy (For Complex 3D Models)
                // If the input is complex, we fall back to the recursive "Level" system.
                else
                {
                    int iterations = CalculateAutoSubdivisionLevel(sourceForCollider, colliderSubdivisions);
                    detailedMesh = SubdivideMesh(sourceForCollider, iterations);
                }

                // Cleanup the temporary source copy
                DestroyImmediate(sourceForCollider); 

                // --- PROJECTION ---
                ProjectCustomMesh(detailedMesh, false, out var pv, out var pn, out var pu, out _, out var validVerts, out _);
                
                // Filter invalid triangles (clipped by the box)
                int[] filteredTriangles = FilterTriangles(detailedMesh.triangles, validVerts);

                colliderMesh = new Mesh
                {
                    name = "Decal_Collider_Projected",
                    vertices = pv,
                    normals = pn,
                    uv = pu,
                    triangles = filteredTriangles
                };

                DestroyImmediate(detailedMesh);

                // --- ALPHA MASKING ---
                Texture mainTex = null;
                if (m_MeshRenderer != null && m_MeshRenderer.sharedMaterial != null)
                {
                    mainTex = m_MeshRenderer.sharedMaterial.mainTexture;
                }
                else if (sourceSpriteRenderer != null && sourceSpriteRenderer.sprite != null)
                {
                    mainTex = sourceSpriteRenderer.sprite.texture;
                }

                if (mainTex != null)
                {
                    colliderMesh = MaskMeshByAlpha(colliderMesh, mainTex, alphaThreshold);
                }

                // --- SURFACE OFFSET ---
                if (surfaceOffset > 0.00001f) {
                    colliderMesh.RecalculateNormals(); 
                    Vector3[] v = colliderMesh.vertices; 
                    Vector3[] n = colliderMesh.normals;
                    if (n.Length == v.Length) { 
                        for(int i=0; i<v.Length; i++) v[i] -= n[i] * surfaceOffset; 
                        colliderMesh.vertices = v; 
                    }
                }

                colliderMesh.RecalculateBounds();
                
                // --- VALIDATION ---
                if (!IsMeshValidForPhysics(colliderMesh)) 
                { 
                    CleanupOldMesh(colliderMesh);
                    
                    Mesh oldMeshRef = m_MeshCollider.sharedMesh;
                    m_MeshCollider.sharedMesh = null;
                    CleanupOldMesh(oldMeshRef, "Decal_Collider");
                    return; 
                }
                
                // --- ASSIGNMENT ---
                Mesh oldColliderMesh = m_MeshCollider.sharedMesh;
                m_MeshCollider.sharedMesh = null; // Detach first
                
                CleanupOldMesh(oldColliderMesh, "Decal_Collider");
                
                m_MeshCollider.sharedMesh = colliderMesh;
                m_MeshCollider.convex = false; 
                return; 
            }
            
            // ---------------------------------------------------------
            // MODE B: GRID PROJECTION (Plane)
            // ---------------------------------------------------------
            
            bool originalConvexState = m_MeshCollider.convex;
            var originalMaterial = m_MeshCollider.sharedMaterial;
            
            // Calculate effective subdivisions for the grid
            Vector2 generationBaseExtent = raycastGridExtent * colliderScale;
            float averageExtentScale = (generationBaseExtent.x + generationBaseExtent.y) / 2.0f;
            int effectiveColliderSubdivisions = Mathf.Clamp(Mathf.RoundToInt(colliderSubdivisions * averageExtentScale), 3, colliderSubdivisions);

            // Generate Grid
            GenerateGrid(effectiveColliderSubdivisions, generationBaseExtent, false, out var colliderVertices, out var colliderNormals, out var colliderUVs, out var colliderHitFlags);
            colliderMesh = BuildPlaneMeshFromGrid(colliderVertices, colliderNormals, colliderUVs, colliderHitFlags, effectiveColliderSubdivisions);
            
            // Make Double-Sided for physics
            if (colliderMesh != null && colliderMesh.triangles.Length > 0)
            {
                int[] originalTriangles = colliderMesh.triangles;
                int numOriginalTriangles = originalTriangles.Length;
                int[] newTriangles = new int[numOriginalTriangles * 2];
                Array.Copy(originalTriangles, newTriangles, numOriginalTriangles);
                for (int i = 0; i < numOriginalTriangles; i += 3)
                {
                    int newIndex = numOriginalTriangles + i;
                    newTriangles[newIndex]     = originalTriangles[i];
                    newTriangles[newIndex + 1] = originalTriangles[i + 2];
                    newTriangles[newIndex + 2] = originalTriangles[i + 1];
                }
                colliderMesh.SetTriangles(newTriangles, 0, true);
            }

            // Alpha Masking for Grid
            if (!originalConvexState)
            {
                Texture2D maskTex = null;
                if (m_MeshRenderer != null && m_MeshRenderer.sharedMaterial != null)
                {
                    maskTex = m_MeshRenderer.sharedMaterial.mainTexture as Texture2D;
                }
                
                if (maskTex != null && maskTex.isReadable)
                {
                    colliderMesh = MaskMeshByAlpha(colliderMesh, maskTex, alphaThreshold);
                }
            }

            EnsureMeshIsValidForCollider(ref colliderMesh, generationBaseExtent);
            if (!colliderMesh.name.Contains("Fallback")) colliderMesh.name = "Decal_Collider" + (colliderMesh.vertexCount == 0 ? "_Empty" : "");
            colliderMesh.MarkDynamic();

            // --- ASSIGNMENT (GRID) ---
            Mesh oldGridMesh = m_MeshCollider.sharedMesh;
            m_MeshCollider.sharedMesh = null; 
            CleanupOldMesh(oldGridMesh, "Decal_Collider");
            
            if (colliderMesh != null && colliderMesh.vertexCount >= 3 && colliderMesh.triangles.Length >= 3) 
            {
                m_MeshCollider.sharedMesh = null; 
                m_MeshCollider.sharedMesh = colliderMesh;
                m_MeshCollider.convex = originalConvexState; 
                m_MeshCollider.enabled = true;
            } 
            else 
            {
                m_MeshCollider.enabled = false; 
                m_MeshCollider.sharedMesh = null;
            }
            m_MeshCollider.sharedMaterial = originalMaterial;
        }

        /// <summary>
        /// Caches references to required components.
        /// </summary>
        private void CacheComponents()
        {
            if (!sourceSpriteRenderer) sourceSpriteRenderer = GetComponent<SpriteRenderer>();

            m_MeshFilter = GetComponent<MeshFilter>();
            m_MeshRenderer = GetComponent<MeshRenderer>();
            m_MeshCollider = GetComponent<MeshCollider>();
            m_SkinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
            
            if (m_MeshCollider != null) m_MeshCollider.hideFlags = HideFlags.HideInInspector;
            
            // Legacy Error Check
            if (sourceSpriteRenderer == null)
            {
                if (!m_MeshFilter) Debug.LogError($"[DecalCollider] MeshFilter missing on '{name}'.", this);
                if (!m_MeshRenderer) Debug.LogError($"[DecalCollider] MeshRenderer missing on '{name}'.", this);
            }
        }

        /// <summary>
        /// Destroys previously generated meshes to prevent memory leaks.
        /// </summary>
        private void ClearMeshes()
        {
            CacheComponents();
            if (m_MeshFilter != null)
            {
                CleanupOldMesh(m_MeshFilter.sharedMesh, "Decal_Visual");
                m_MeshFilter.sharedMesh = null;
            }
            if (m_MeshCollider != null)
            {
                CleanupOldMesh(m_MeshCollider.sharedMesh, "Decal_");
                m_MeshCollider.sharedMesh = null;
            }
        }
        
        /// <summary>
        /// Safely destroys a mesh object, handling both Editor and Play mode.
        /// </summary>
        private static void CleanupOldMesh(Mesh oldMesh)
        {
            if (oldMesh == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.DestroyObjectImmediate(oldMesh);
            else Destroy(oldMesh);
#else
            Destroy(oldMesh);
#endif
        }

        /// <summary>
        /// Safely destroys a mesh object if its name starts with a specific prefix.
        /// </summary>
        private static void CleanupOldMesh(Mesh oldMesh, string requiredPrefix)
        {
            if (oldMesh != null && oldMesh.name.StartsWith(requiredPrefix)) CleanupOldMesh(oldMesh);
        }
        
        /// <summary>
        /// Checks if the transform has changed using Unity's built-in optimized flag.
        /// </summary>
        private bool HasTransformChanged()
        {
            // Unity's built-in "hasChanged" flag is significantly faster than manual vector comparison.
            // However, this flag is set to true whenever the transform changes; we must reset it to false after checking.
            if (transform.hasChanged)
            {
                transform.hasChanged = false;
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Handles baking a SkinnedMeshRenderer into a static mesh for the collider.
        /// </summary>
        private void HandleSkinnedMeshBaking()
        {
            if (m_SkinnedMeshRenderer && m_SkinnedMeshRenderer.sharedMesh != null)
            {
                Mesh bakedMesh = new Mesh { name = "Baked_SkinnedMesh", hideFlags = HideFlags.HideAndDontSave };
                m_SkinnedMeshRenderer.BakeMesh(bakedMesh, true);
                
                if (m_MeshCollider != null)
                {
                    CleanupOldMesh(m_MeshCollider.sharedMesh, "Baked_SkinnedMesh");
                    m_MeshCollider.sharedMesh = bakedMesh;
                }
                else
                {
                    CleanupOldMesh(bakedMesh);
                }
            }
            else if (m_MeshCollider?.sharedMesh != null && m_MeshCollider.sharedMesh.name == "Baked_SkinnedMesh")
            {
                CleanupOldMesh(m_MeshCollider.sharedMesh);
                m_MeshCollider.sharedMesh = null;
            }
        }
        
        /// <summary>
        /// Checks if the mesh is valid for physics (no NaNs, sufficient volume).
        /// </summary>
        private static bool IsMeshValidForPhysics(Mesh mesh)
        {
            if (mesh == null) return false;
            if (mesh.vertexCount < 3) return false;
            if (mesh.triangles.Length < 3) return false;

            // 1. NaN Check
            Vector3[] verts = mesh.vertices;
            foreach (var v in verts)
            {
                if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                    float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z))
                {
                    return false;
                }
            }

            // 2. Bounds Check
            Bounds b = mesh.bounds;
            if (b.size.sqrMagnitude < 0.000001f) return false;

            return true;
        }
        #endregion

        #region Mesh Construction
        
        /// <summary>
        /// Ensures the generated mesh is valid for a MeshCollider. If not, creates a simple fallback quad.
        /// </summary>
        private void EnsureMeshIsValidForCollider(ref Mesh mesh, Vector2 fallbackGridSize)
        {
            bool needsRemake = mesh == null || mesh.vertexCount < 3 ||
                               mesh.triangles == null || mesh.triangles.Length < 3 ||
                               new HashSet<Vector3>(mesh.vertices).Count < 3;

            if (!needsRemake) return;

            Debug.LogWarning($"[DecalCollider] Generated collider mesh for '{name}' was invalid. "
                             + "Creating fallback quad inside the projection box.", this);

            float boxHalfW = size.x * 0.5f;
            float boxHalfH = size.y * 0.5f;

            float halfW = Mathf.Min(fallbackGridSize.x * 0.5f, boxHalfW);
            float halfH = Mathf.Min(fallbackGridSize.y * 0.5f, boxHalfH);

            Vector3 projDirLocal = GetBaseDirectionVector(projectionDirection).normalized;
            GetOrthogonalBasis(transform.TransformDirection(projDirLocal), transform.up, transform.right, out var rightW, out var upW);
            Vector3 rightL = transform.InverseTransformDirection(rightW).normalized;
            Vector3 upL    = transform.InverseTransformDirection(upW).normalized;

            Vector3 centerL = meshOffset;

            Vector3[] v = new Vector3[4];
            v[0] = centerL - rightL * halfW - upL * halfH;
            v[1] = centerL + rightL * halfW - upL * halfH;
            v[2] = centerL - rightL * halfW + upL * halfH;
            v[3] = centerL + rightL * halfW + upL * halfH;

            int[] tris = { 0, 2, 1, 1, 2, 3 };

            CleanupOldMesh(mesh, "Decal_Collider");
            mesh = new Mesh
            {
                hideFlags = HideFlags.HideAndDontSave,
                vertices  = v,
                triangles = tris,
                normals   = new[] { -projDirLocal, -projDirLocal, -projDirLocal, -projDirLocal },
                name      = "Decal_Collider_Fallback"
            };
            mesh.RecalculateBounds();
        }

        /// <summary>
        /// Constructs a mesh from the generated grid data.
        /// </summary>
        private static Mesh BuildPlaneMeshFromGrid(Vector3[] vertices, Vector3[] normals, Vector2[] uvs, bool[] validVertexFlags, int subdivisions)
        {
            var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            if (vertices == null || vertices.Length < 4)
            {
                mesh.name = "EmptyGridMesh";
                return mesh;
            }

            int quadsPerRow = subdivisions;
            int verticesPerRow = subdivisions + 1;
            var triangles = new List<int>(quadsPerRow * quadsPerRow * 6);

            for (int r = 0; r < quadsPerRow; r++) 
            {
                for (int c = 0; c < quadsPerRow; c++) 
                {
                    int i0 = r * verticesPerRow + c;
                    int i1 = i0 + 1;
                    int i2 = (r + 1) * verticesPerRow + c;
                    int i3 = i2 + 1;

                    if (i3 >= validVertexFlags.Length) continue;
                    
                    if (validVertexFlags[i0] && validVertexFlags[i2] && validVertexFlags[i1]) 
                    {
                        triangles.Add(i0); triangles.Add(i2); triangles.Add(i1);
                    }
                    if (validVertexFlags[i1] && validVertexFlags[i2] && validVertexFlags[i3])
                    {
                        triangles.Add(i1); triangles.Add(i2); triangles.Add(i3);
                    }
                }
            }
            
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Removes triangles based on alpha, using optimized cached data access (O(1) array lookup).
        /// </summary>
        private Mesh MaskMeshByAlpha(Mesh sourceMesh, Texture sourceTexture, float alphaCutoff)
        {
            if (sourceMesh == null || sourceMesh.vertexCount == 0 || sourceTexture == null) return sourceMesh;

            // Fetch optimized cached data instead of using raw Texture2D
            var textureData = GetReadableTextureData(sourceTexture);
            if (textureData == null || textureData.Colors == null) return sourceMesh;

            Vector3[] verts = sourceMesh.vertices;
            Vector2[] uvs = sourceMesh.uv;
            int[] tris = sourceMesh.triangles;

            if (uvs == null || uvs.Length != verts.Length || tris == null || tris.Length == 0) return sourceMesh;

            // Initialize list with capacity to avoid resizing overhead
            var keptTris = new List<int>(tris.Length);

            int w = textureData.Width;
            int h = textureData.Height;
            Color32[] colors = textureData.Colors;
    
            // Convert float threshold (0-1) to byte (0-255) for faster comparison
            byte alphaThresholdByte = (byte)(alphaCutoff * 255);

            for (int i = 0; i < tris.Length; i += 3)
            {
                int i0 = tris[i], i1 = tris[i + 1], i2 = tris[i + 2];
                if (i0 >= uvs.Length || i1 >= uvs.Length || i2 >= uvs.Length) continue;

                // Fast Lookup: Access array directly instead of slow GetPixelBilinear
                if (IsAlphaPass(uvs[i0], w, h, colors, alphaThresholdByte) &&
                    IsAlphaPass(uvs[i1], w, h, colors, alphaThresholdByte) &&
                    IsAlphaPass(uvs[i2], w, h, colors, alphaThresholdByte))
                {
                    keptTris.Add(i0); keptTris.Add(i1); keptTris.Add(i2);
                }
            }

            // If no triangles were removed, return original mesh to save memory
            if (keptTris.Count == tris.Length) return sourceMesh;

            var maskedMesh = new Mesh
            {
                hideFlags = HideFlags.HideAndDontSave,
                vertices = verts,
                normals = sourceMesh.normals,
                uv = uvs,
                name = sourceMesh.name + "_AlphaMasked"
            };

            maskedMesh.SetTriangles(keptTris, 0, true);
            maskedMesh.RecalculateBounds();
            return maskedMesh;
        }

// Helper method for fast array lookup
        private bool IsAlphaPass(Vector2 uv, int w, int h, Color32[] colors, byte threshold)
        {
            // Clamp UVs to 0-1 range and map to pixel coordinates
            int x = Mathf.Clamp((int)(uv.x * w), 0, w - 1);
            int y = Mathf.Clamp((int)(uv.y * h), 0, h - 1);
    
            // Calculate 1D array index from 2D coordinates
            int index = y * w + x;
    
            // Check bounds and alpha value
            if (index >= 0 && index < colors.Length)
            {
                return colors[index].a >= threshold;
            }
            return true; // Return true on error to avoid deleting triangles accidentally
        }

        #endregion
        
        #region Static Utilities
        
        private static Mesh SubdivideQuadLinearly(Mesh sourceMesh, int density)
        {
            if (sourceMesh.vertexCount != 4) return null;

            Vector3[] sVerts = sourceMesh.vertices;
            Vector2[] sUVs = sourceMesh.uv;
            Color32[] sCols = sourceMesh.colors32;
            
            if (sCols == null || sCols.Length == 0)
            {
                sCols = new Color32[4];
                for(int i=0; i<4; i++) sCols[i] = new Color32(255,255,255,255);
            }

            Vector3 v0 = sVerts[0]; Vector3 v1 = sVerts[1]; Vector3 v2 = sVerts[2]; Vector3 v3 = sVerts[3];
            Vector2 uv0 = sUVs[0]; Vector2 uv1 = sUVs[1]; Vector2 uv2 = sUVs[2]; Vector2 uv3 = sUVs[3];

            int steps = Mathf.Max(1, density);
            int vertsPerRow = steps + 1;
            int totalVerts = vertsPerRow * vertsPerRow;
            
            Vector3[] newVerts = new Vector3[totalVerts];
            Vector3[] newNorms = new Vector3[totalVerts];
            Vector2[] newUVs = new Vector2[totalVerts];
            Color32[] newCols = new Color32[totalVerts];
            int[] newTris = new int[steps * steps * 6];

            for (int y = 0; y <= steps; y++)
            {
                float v = (float)y / steps;
                for (int x = 0; x <= steps; x++)
                {
                    float u = (float)x / steps;
                    int index = y * vertsPerRow + x;

                    Vector3 pBot = Vector3.Lerp(v0, v3, u);
                    Vector3 pTop = Vector3.Lerp(v1, v2, u);
                    newVerts[index] = Vector3.Lerp(pBot, pTop, v);
                    
                    newNorms[index] = Vector3.back; 

                    Vector2 uvBot = Vector2.Lerp(uv0, uv3, u);
                    Vector2 uvTop = Vector2.Lerp(uv1, uv2, u);
                    newUVs[index] = Vector2.Lerp(uvBot, uvTop, v);

                    newCols[index] = Color32.Lerp(sCols[0], sCols[2], 0.5f); 
                }
            }

            int tIndex = 0;
            for (int y = 0; y < steps; y++)
            {
                for (int x = 0; x < steps; x++)
                {
                    int i0 = y * vertsPerRow + x; int i1 = i0 + 1;
                    int i2 = (y + 1) * vertsPerRow + x; int i3 = i2 + 1;

                    newTris[tIndex++] = i0; newTris[tIndex++] = i2; newTris[tIndex++] = i1;
                    newTris[tIndex++] = i1; newTris[tIndex++] = i2; newTris[tIndex++] = i3;
                }
            }

            Mesh resultMesh = new Mesh
            {
                name = "Linear_Subdivided_Quad"
            };
            if (totalVerts > 65000) resultMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            
            resultMesh.vertices = newVerts; resultMesh.normals = newNorms;
            resultMesh.uv = newUVs; resultMesh.colors32 = newCols;
            resultMesh.triangles = newTris;
            resultMesh.RecalculateBounds();
            return resultMesh;
        }
        
        /// <summary>
        /// Checks if the decal's bounds are visible to the Main Camera using Frustum Planes.
        /// Uses a conservative approximation (Bounding Sphere/Box) to be fast.
        /// </summary>
        private bool IsVisibleToMainCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return true; // Safety: If no camera, assume visible to be safe.

            // Calculate a rough world-space bounding box for the decal
            // We use a generous size to account for rotation without expensive calculations.
    
            float maxSide = Mathf.Max(size.x, size.y);
            float maxScale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
            float worldRadius = maxSide * maxScale * 0.75f; // Rough radius estimate
    
            // Expand to include the projection depth
            float depth = maxDistance * Mathf.Abs(transform.lossyScale.z);
    
            // Create a bounding box that encapsulates the decal volume
            Vector3 worldCenter = transform.TransformPoint(center + new Vector3(0, 0, maxDistance * 0.5f));
            Vector3 sizeVector = new Vector3(worldRadius * 2, worldRadius * 2, depth);
            Bounds bounds = new Bounds(worldCenter, sizeVector);

            // Fast Frustum Check
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
            return GeometryUtility.TestPlanesAABB(planes, bounds);
        }
        
        /// <summary>
        /// Calculates an orthogonal basis (right and up vectors) from a given forward vector and preferred axes.
        /// </summary>
        private static void GetOrthogonalBasis(Vector3 forward, Vector3 preferredUp, Vector3 preferredRight, out Vector3 right, out Vector3 up)
        {
            if (Mathf.Abs(Vector3.Dot(forward, preferredUp)) < 0.999f)
            {
                right = Vector3.Cross(preferredUp, forward).normalized;
                up = Vector3.Cross(forward, right).normalized;
            }
            else
            {
                right = Vector3.Cross(forward, preferredRight).normalized;
                up = Vector3.Cross(right, forward).normalized;
            }
        }
        
        /// <summary>
        /// Calculates a stable 'up' vector that is orthogonal to the projection direction.
        /// </summary>
        private static Vector3 GetOrthogonalUpVector(Transform tf, Vector3 projDir)
        {
            Vector3 up = Mathf.Abs(Vector3.Dot(projDir, tf.up)) < 0.999f ? tf.up :
                Mathf.Abs(Vector3.Dot(projDir, tf.right)) < 0.999f ? tf.right :
                tf.forward;
            
            if (Mathf.Abs(Vector3.Dot(projDir, up.normalized)) > 0.999f)
            {
                up = Vector3.Cross(projDir, Vector3.right).sqrMagnitude > KEpsilon
                    ? Vector3.Cross(projDir, Vector3.right).normalized
                    : Vector3.forward;
            }
            return up;
        }
        
        /// <summary>
        /// Converts a ProjectionDirection enum value to a base, normalized Vector3.
        /// </summary>
        public static Vector3 GetBaseDirectionVector(ProjectionDirection dirEnum)
        {
            return dirEnum switch
            {
                ProjectionDirection.Up => Vector3.up,
                ProjectionDirection.Down => Vector3.down,
                ProjectionDirection.Forward => Vector3.forward,
                ProjectionDirection.Back => Vector3.back,
                ProjectionDirection.Right => Vector3.right,
                ProjectionDirection.Left => Vector3.left,
                ProjectionDirection.ForwardUp => (Vector3.forward + Vector3.up).normalized,
                ProjectionDirection.ForwardDown => (Vector3.forward + Vector3.down).normalized,
                ProjectionDirection.BackUp => (Vector3.back + Vector3.up).normalized,
                ProjectionDirection.BackDown => (Vector3.back + Vector3.down).normalized,
                ProjectionDirection.RightUp => (Vector3.right + Vector3.up).normalized,
                ProjectionDirection.RightDown => (Vector3.right + Vector3.down).normalized,
                ProjectionDirection.LeftUp => (Vector3.left + Vector3.up).normalized,
                ProjectionDirection.LeftDown => (Vector3.left + Vector3.down).normalized,
                ProjectionDirection.ForwardRight => (Vector3.forward + Vector3.right).normalized,
                ProjectionDirection.ForwardLeft => (Vector3.forward - Vector3.right).normalized,
                ProjectionDirection.BackRight => (Vector3.back + Vector3.right).normalized,
                ProjectionDirection.BackLeft => (Vector3.back - Vector3.right).normalized,
                _ => Vector3.down // Default case
            };
        }

        /// <summary>
        /// Calculates the projection vector based on the component's current settings.
        /// </summary>
        public Vector3 GetProjectionVector(Transform t)
        {
            Vector3 baseDirection = GetBaseDirectionVector(projectionDirection);
            return projectionSpace switch
            {
                ProjectionSpace.World => baseDirection,
                ProjectionSpace.Local => t.TransformDirection(baseDirection),
                _ => t.TransformDirection(baseDirection)
            };
        }

        #endregion
    }
}
