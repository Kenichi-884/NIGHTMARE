// ─────────────────────────────────────────────────────────────
//  DecalCollider.API.cs – Extended DecalCollider helper API 🛠
//  (all methods now fully documented for IntelliSense)
// ─────────────────────────────────────────────────────────────
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_ANDROID || UNITY_IOS || UNITY_WEBGL
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;             // ⏱️ Performance timing
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;      // 📊 Memory usage

namespace DecalCollider.Runtime
{
    /// <summary>
    /// Partial class that extends <see cref="DecalCollider"/> with a fully featured
    /// runtime/editor API: profiling, hit statistics, adaptive subdivision, etc.
    /// </summary>
    public partial class DecalCollider
    {
        #region Fields & structs ────────────────────────────────────────────────

        /// <summary>Callbacks invoked for every cached <see cref="RaycastHit"/>.</summary>
        private readonly List<Action<RaycastHit>> m_OnHitCallbacks = new();

        /// <summary>Filters executed before a hit is stored or callbacks are fired.</summary>
        private readonly List<Func<RaycastHit, bool>> m_HitFilters = new();

        /// <summary>Caches all successful <see cref="RaycastHit"/> entries during rebuild.</summary>
        private readonly List<RaycastHit> m_HitCache = new();

        private float m_LastRebuildTimeMS;
        private int   m_RebuildCounter;

        /// <summary>
        /// Snapshot of key statistics collected right after the last successful rebuild.
        /// </summary>
        public struct RebuildStats
        {
            /// <summary>Total visual triangles on the mesh side.</summary>
            public int TrianglesVisual;

            /// <summary>Total physics triangles on the collider side.</summary>
            public int TrianglesCollider;

            /// <summary>Total number of rays cast during the last rebuild.</summary>
            public int RaysCast;

            /// <summary>Total number of rays that hit a surface.</summary>
            public int RaysHit;

            /// <summary>Rebuild duration in milliseconds.</summary>
            public double BuildTimeMS;

            /// <summary>Runtime memory footprint (visual + collider mesh) in kilobytes.</summary>
            public float MemoryKb;
        }
        private RebuildStats m_LastStats;

        #endregion
        
#if UNITY_EDITOR
        #region Editor Utilities ───────────────────────────────────────────────

        /// <summary>
        /// Saves the currently generated visual mesh as a static asset file.
        /// Useful for baking decals into static meshes to remove the component overhead.
        /// </summary>
        /// <param name="path">The folder path relative to the Assets folder (default: "Assets/DecalMeshes/").</param>
        public void SaveMeshToAsset(string path = "Assets/DecalMeshes/")
        {
            if (VisualMesh == null)
            {
                Debug.LogWarning("No Visual Mesh to save.");
                return;
            }

            if (!System.IO.Directory.Exists(path))
                System.IO.Directory.CreateDirectory(path);

            string fileName = $"{path}{gameObject.name}_Decal_{System.DateTime.Now.Ticks}.asset";
            
            Mesh meshToSave = Instantiate(VisualMesh); // Create a copy
            UnityEditor.AssetDatabase.CreateAsset(meshToSave, fileName);
            UnityEditor.AssetDatabase.SaveAssets();
            
            Debug.Log($"[DecalCollider] Mesh saved successfully to: {fileName}");
        }

        #endregion
#endif

        #region Public properties ───────────────────────────────────────────────

        /// <summary>
        /// Reference to the current visual mesh, or <c>null</c> if none.
        /// </summary>
        public Mesh VisualMesh => m_MeshFilter ? m_MeshFilter.sharedMesh : null;

        /// <summary>
        /// Reference to the current collider mesh, or <c>null</c> if none.
        /// </summary>
        public Mesh ColliderMesh => m_MeshCollider ? m_MeshCollider.sharedMesh : null;

        /// <summary>
        /// World-space bounds of the visual mesh. Returns <see cref="Bounds.empty"/> if unavailable.
        /// </summary>
        public Bounds VisualBoundsWS => VisualMesh ? TransformBounds(VisualMesh.bounds) : default;

        /// <summary>
        /// World-space bounds of the collider mesh. Returns <see cref="Bounds.empty"/> if unavailable.
        /// </summary>
        public Bounds ColliderBoundsWS => ColliderMesh ? TransformBounds(ColliderMesh.bounds) : default;

        /// <summary>Duration, in milliseconds, of the most recent rebuild.</summary>
        public float LastRebuildTimeMS => m_LastRebuildTimeMS;

        /// <summary>How many times <see cref="ForceRebuild"/> has completed since this component was enabled.</summary>
        public int RebuildCounter => m_RebuildCounter;

        /// <summary>Read-only profiling data for the most recent rebuild.</summary>
        public RebuildStats LastRebuildStats => m_LastStats;

        /// <summary>
        /// Converts a local <see cref="Bounds"/> to world space, preserving size and orientation.
        /// </summary>
        /// <param name="b">Local-space bounds.</param>
        /// <returns>Transformed world-space bounds.</returns>
        private Bounds TransformBounds(Bounds b)
        {
            Vector3 c = transform.TransformPoint(b.center);
            Vector3 s = Vector3.Scale(b.size, transform.lossyScale);
            return new Bounds(c, s);
        }

        #endregion

        #region Rebuild helpers ─────────────────────────────────────────────────

        /// <summary>
        /// Triggers a rebuild immediately. When <paramref name="synchronous"/> is
        /// <c>false</c>, the rebuild is performed as a coroutine to avoid
        /// single-frame stalls.
        /// </summary>
        /// <param name="synchronous">
        /// <c>true</c> for blocking rebuild, <c>false</c> to use <see cref="AsyncRebuild"/>.
        /// </param>
        public void ForceRebuild(bool synchronous = true)
        {
            if (synchronous) InternalRebuildMeasured();
            else             StartCoroutine(AsyncRebuild(null));
        }

        /// <summary>
        /// Coroutine-based rebuild that yields at least once and optionally
        /// reports progress in the <c>[0,1]</c> range.
        /// </summary>
        /// <param name="onProgress">
        /// Progress callback; may be <c>null</c>. Called with <c>1f</c> when finished.
        /// </param>
        public IEnumerator AsyncRebuild(Action<float> onProgress)
        {
            yield return null;                   // allow one frame for UI responsiveness
            InternalRebuildMeasured(onProgress);
        }

        /// <summary>
        /// Internal rebuild wrapper that measures time, memory and hit statistics.
        /// </summary>
        /// <param name="onProgress">
        /// Optional progress callback; may be <c>null</c>.
        /// </param>
        private void InternalRebuildMeasured(Action<float> onProgress = null)
        {
            Stopwatch sw = Stopwatch.StartNew();

            onProgress?.Invoke(0.05f);
            ClearHitCache();                     // reset previous hit data

            onProgress?.Invoke(0.10f);
            RebuildSafe();                       // invoke original rebuild logic

            sw.Stop();
            m_LastRebuildTimeMS = sw.ElapsedMilliseconds;
            m_RebuildCounter++;

            long mem = 0;
            if (VisualMesh)   mem += Profiler.GetRuntimeMemorySizeLong(VisualMesh);
            if (ColliderMesh) mem += Profiler.GetRuntimeMemorySizeLong(ColliderMesh);

            m_LastStats = new RebuildStats
            {
                TrianglesVisual   = VisualMesh   ? VisualMesh.triangles.Length   / 3 : 0,
                TrianglesCollider = ColliderMesh ? ColliderMesh.triangles.Length / 3 : 0,
                RaysCast          = m_RayStarts.Count,
                RaysHit           = m_HitCache.Count,
                BuildTimeMS       = m_LastRebuildTimeMS,
                MemoryKb          = mem / 1024f
            };

            onProgress?.Invoke(1f);

            // notify subscribers
            foreach (var cb in m_OnHitCallbacks)
                foreach (var hit in m_HitCache)
                    cb?.Invoke(hit);
        }

        #endregion

        #region Mesh statistics ────────────────────────────────────────────────

        /// <summary>
        /// Calculates the average triangle area (in cm²) for either the visual
        /// or collider mesh.
        /// </summary>
        /// <param name="useColliderMesh">
        /// <c>true</c> to inspect the collider mesh; <c>false</c> for the visual mesh.
        /// </param>
        /// <returns>Average area in square centimetres. Returns <c>0</c> if mesh is null.</returns>
        public float GetAverageTriArea(bool useColliderMesh)
        {
            Mesh m = useColliderMesh ? ColliderMesh : VisualMesh;
            if (m == null) return 0f;

            int[] tris = m.triangles;
            Vector3[] v = m.vertices;
            double areaSum = 0d; int triCount = 0;

            for (int i = 0; i < tris.Length; i += 3)
            {
                if (i + 2 >= tris.Length) break;
                Vector3 a = v[tris[i]], b = v[tris[i + 1]], c = v[tris[i + 2]];
                areaSum += Vector3.Cross(b - a, c - a).magnitude * 0.5;
                triCount++;
            }
            return triCount == 0 ? 0f : (float)(areaSum / triCount) * 10000f; // m² → cm²
        }

        /// <summary>
        /// Calculates the ratio of degenerate (zero-area) triangles on a mesh.
        /// </summary>
        /// <param name="useColliderMesh">
        /// <c>true</c> to inspect the collider mesh; <c>false</c> for the visual mesh.
        /// </param>
        /// <returns>A value in <c>[0,1]</c>; <c>0</c> when mesh is null.</returns>
        public float GetDegenerateTriRatio(bool useColliderMesh)
        {
            Mesh m = useColliderMesh ? ColliderMesh : VisualMesh;
            if (m == null) return 0f;

            int[] tris = m.triangles; Vector3[] v = m.vertices;
            int degenerate = 0, total = tris.Length / 3;

            for (int i = 0; i < tris.Length; i += 3)
            {
                if (i + 2 >= tris.Length) break;
                Vector3 a = v[tris[i]], b = v[tris[i + 1]], c = v[tris[i + 2]];
                if (Vector3.Cross(b - a, c - a).sqrMagnitude < KEpsilon) degenerate++;
            }
            return total == 0 ? 0f : (float)degenerate / total;
        }

        #endregion

        #region Raycast & hit cache ────────────────────────────────────────────

        /// <summary>
        /// Returns a copy of all hit points in world space.
        /// </summary>
        /// <returns>Enumerable sequence of <see cref="Vector3"/> points.</returns>
        public IEnumerable<Vector3> GetRaycastHits() => m_HitCache.ConvertAll(h => h.point);

        /// <summary>
        /// Returns the unique set of <see cref="Transform"/> instances hit during
        /// the last rebuild.
        /// </summary>
        public List<Transform> GetHitTransforms()
        {
            List<Transform> list = new();
            foreach (var h in m_HitCache)
                if (h.transform && !list.Contains(h.transform))
                    list.Add(h.transform);
            return list;
        }

        /// <summary>
        /// Collects all unique <see cref="Material"/> instances belonging to hit renderers.
        /// </summary>
        public HashSet<Material> GetHitMaterialSet()
        {
            HashSet<Material> set = new();
            foreach (var h in m_HitCache)
                if (h.collider && h.collider.TryGetComponent(out Renderer r) && r.sharedMaterials != null)
                    foreach (var mat in r.sharedMaterials)
                        if (mat) set.Add(mat);
            return set;
        }

        /// <summary>
        /// Returns a dictionary mapping layer indices to hit counts.
        /// </summary>
        public Dictionary<int, int> GetLayerHitStatistics()
        {
            Dictionary<int, int> dict = new();
            foreach (var h in m_HitCache)
            {
                int layer = h.collider ? h.collider.gameObject.layer : -1;
                dict.TryAdd(layer, 0);
                dict[layer]++;
            }
            return dict;
        }

        /// <summary>
        /// Clears the internal hit cache. Does not trigger a rebuild.
        /// </summary>
        public void ClearHitCache() => m_HitCache.Clear();

        /// <summary>
        /// Registers a hit callback with an optional filter.
        /// </summary>
        /// <param name="filter">
        /// Predicate that must return <c>true</c> for a hit to pass through; cannot be null.
        /// </param>
        /// <param name="callback">
        /// The callback that will be invoked for each hit that passes the filter.
        /// </param>
        public void RegisterOnHitCallback(Func<RaycastHit, bool> filter, Action<RaycastHit> callback)
        {
            if (filter == null || callback == null) return;
            m_HitFilters.Add(filter);
            m_OnHitCallbacks.Add(hit => { if (filter(hit)) callback(hit); });
        }

        #endregion

        #region Alpha mask helpers ─────────────────────────────────────────────

        /// <summary>
        /// Attempts to retrieve the main texture from the current material and
        /// cast it to <see cref="Texture2D"/>.
        /// </summary>
        public Texture2D GetAlphaMaskTexture()
            => m_MeshRenderer && m_MeshRenderer.sharedMaterial
               ? m_MeshRenderer.sharedMaterial.mainTexture as Texture2D
               : null;

        /// <summary>
        /// Samples alpha at a world position on the main texture. Returns –1 if
        /// the texture is unreadable or missing.
        /// </summary>
        /// <param name="worldPos">World-space position to sample.</param>
        /// <returns>Alpha value in <c>[0,1]</c> or <c>-1</c> on failure.</returns>
        public float SampleAlphaAt(Vector3 worldPos)
        {
            Texture2D tex = GetAlphaMaskTexture();
            if (tex == null || !tex.isReadable) return -1f;
            Vector2 uv = WorldToLocalUV(worldPos);
            return tex.GetPixelBilinear(uv.x, uv.y).a;
        }

        /// <summary>
        /// Sets <see cref="alphaThreshold"/> and optionally forces a rebuild.
        /// </summary>
        /// <param name="value">Threshold in <c>[0,1]</c>.</param>
        /// <param name="rebuild">
        /// If <c>true</c>, immediately triggers <see cref="ForceRebuild"/>.
        /// </param>
        public void SetAlphaThreshold(float value, bool rebuild = true)
        {
            alphaThreshold = Mathf.Clamp01(value);
            if (rebuild) ForceRebuild();
        }

        #endregion

        #region Geometry utilities ─────────────────────────────────────────────

        /// <summary>
        /// Converts a world-space point to decal UV space (0–1 range).
        /// </summary>
        /// <param name="worldPos">World-space point.</param>
        /// <returns>UV coordinate where <c>(0.5,0.5)</c> is the decal center.</returns>
        public Vector2 WorldToLocalUV(Vector3 worldPos)
        {
            Vector3 local = transform.InverseTransformPoint(worldPos) - meshOffset;
            GetProjectionBasis(out Vector3 r, out Vector3 up, out _);
            float u = Vector3.Dot(local, r) / Mathf.Max(KEpsilon, raycastGridExtent.x) + 0.5f;
            float v = Vector3.Dot(local, up) / Mathf.Max(KEpsilon, raycastGridExtent.y) + 0.5f;
            return new Vector2(u, v);
        }

        /// <summary>
        /// Tests whether a world-space point lies inside the current mask box.
        /// </summary>
        public bool IsPointInsideMask(Vector3 worldPos)
        {
            Vector3 local = transform.InverseTransformPoint(worldPos);
            Vector3 c = center;
            return
                Mathf.Abs(local.x - c.x) <= size.x * 0.5f &&
                Mathf.Abs(local.y - c.y) <= size.y * 0.5f &&
                Mathf.Abs(local.z - c.z) <= maxDistance * 0.5f;
        }

        /// <summary>
        /// Calculates local right, up and forward basis vectors of the decal
        /// projection space.
        /// </summary>
        public void GetProjectionBasis(out Vector3 right, out Vector3 up, out Vector3 fwd)
        {
            fwd = GetProjectionVector(transform).normalized;
            if (fwd.sqrMagnitude < KEpsilon) fwd = -transform.up;
            up  = Mathf.Abs(Vector3.Dot(fwd, transform.up)) < 0.999f ? transform.up : transform.right;
            right = Vector3.Cross(up, fwd).normalized;
            up    = Vector3.Cross(fwd, right).normalized;
            right = transform.InverseTransformDirection(right);
            up    = transform.InverseTransformDirection(up);
            fwd   = transform.InverseTransformDirection(fwd);
        }

        /// <summary>
        /// Projects a point onto the decal surface along the projection vector,
        /// returning the nearest valid surface point.
        /// </summary>
        public Vector3 ProjectPointOntoSurface(Vector3 worldPos)
        {
            Vector3 dir = GetProjectionVector(transform).normalized;
            if (Physics.Raycast(worldPos - dir * 0.01f, dir,
                                out var hit, maxDistance, wrapMask))
                return hit.point + hit.normal * surfaceOffset;
            return worldPos;
        }
        
        /// <summary>
        /// Raycasts against the projection direction to find the exact point and normal
        /// on the surface corresponding to a given world position.
        /// </summary>
        /// <param name="worldPos">The reference world position (e.g., player foot position).</param>
        /// <param name="surfacePoint">Output: The projected point on the surface.</param>
        /// <param name="surfaceNormal">Output: The normal of the surface at that point.</param>
        /// <returns>True if a surface was found; otherwise false.</returns>
        public bool GetSurfaceInfoAt(Vector3 worldPos, out Vector3 surfacePoint, out Vector3 surfaceNormal)
        {
            surfacePoint = Vector3.zero;
            surfaceNormal = Vector3.up;

            Vector3 dir = GetProjectionVector(transform).normalized;
            // Raycast backwards from "behind" the target point to ensure we hit the surface
            Ray ray = new Ray(worldPos - dir * maxDistance, dir);
            
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance * 2f, wrapMask))
            {
                surfacePoint = hit.point + hit.normal * surfaceOffset;
                surfaceNormal = hit.normal;
                return true;
            }
            return false;
        }

        #endregion
        
        #region Dynamic Content Control ─────────────────────────────────────────

        /// <summary>
        /// Manually updates the Sprite content and triggers a rebuild immediately.
        /// Useful for disabling automatic 'Live Link' to save performance.
        /// </summary>
        /// <param name="newSprite">The new sprite to project.</param>
        public void SetSprite(Sprite newSprite)
        {
            if (!sourceSpriteRenderer)
            {
                Debug.LogWarning("[DecalCollider] SetSprite called but no Source SpriteRenderer found.");
                return;
            }

            if (sourceSpriteRenderer.sprite == newSprite) return;

            sourceSpriteRenderer.sprite = newSprite;
            RebuildSafe();
        }

        /// <summary>
        /// Manually updates the TextMeshPro content and triggers a rebuild immediately.
        /// </summary>
        /// <param name="text">The new text string to display.</param>
        public void SetText(string text)
        {
            if (sourceTMP == null)
            {
                Debug.LogWarning("[DecalCollider] SetText called but no Source TextMeshPro found.");
                return;
            }

            sourceTMP.text = text;
            sourceTMP.ForceMeshUpdate(); 
            RebuildSafe();
        }

        /// <summary>
        /// Instantly changes the vertex color of the decal mesh without a full rebuild.
        /// Extremely performant for effects like darkening blood, highlighting, or fading.
        /// </summary>
        /// <param name="newColor">The target color to apply to all vertices.</param>
        public void SetVertexColor(Color newColor)
        {
            // Update Standard Mesh
            if (m_MeshFilter != null && m_MeshFilter.sharedMesh != null)
            {
                ApplyColorToMesh(m_MeshFilter.sharedMesh, newColor);
            }
            
            // Update Virtual Mesh (Sprite Mode)
            if (m_VirtualMesh != null)
            {
                ApplyColorToMesh(m_VirtualMesh, newColor);
            }
        }

        /// <summary>
        /// Helper to apply a single color to an entire mesh.
        /// </summary>
        private void ApplyColorToMesh(Mesh m, Color32 c)
        {
            Color32[] colors = new Color32[m.vertexCount];
            for (int i = 0; i < colors.Length; i++) colors[i] = c;
            m.colors32 = colors;
        }

        /// <summary>
        /// Rotates the decal to look at a specific world position. 
        /// Useful for dynamic projectors (e.g., flashlights, security cameras).
        /// </summary>
        /// <param name="targetPosition">The world position to look at.</param>
        public void LookAtTarget(Vector3 targetPosition)
        {
            if (projectionDirection == ProjectionDirection.Forward)
            {
                transform.LookAt(targetPosition);
            }
            else
            {
                Vector3 dir = (targetPosition - transform.position).normalized;
                transform.rotation = Quaternion.LookRotation(dir);
            }
            
            if (!alwaysRebuild) RebuildSafe();
        }

        #endregion

        #region Misc utilities ────────────────────────────────────────────────

        /// <summary>
        /// Bakes a <see cref="SkinnedMeshRenderer"/> into a static mesh and
        /// assigns it to the collider.
        /// </summary>
        /// <param name="convex">Whether to set <see cref="MeshCollider.convex"/>.</param>
        public void BakeAndAttachCollider(bool convex)
        {
            if (!m_SkinnedMeshRenderer) return;
            Mesh baked = new Mesh { name = "Baked_SkinnedMesh", hideFlags = HideFlags.HideAndDontSave };
            m_SkinnedMeshRenderer.BakeMesh(baked, true);
            if (!m_MeshCollider) CacheComponents();
            m_MeshCollider.sharedMesh = baked;
            m_MeshCollider.convex     = convex;
        }

        /// <summary>
        /// Updates the mask box centre and size in one call, then forces a rebuild.
        /// </summary>
        public void SetMaskBox(Vector3 centerWs, Vector2 sizeWs)
        {
            center = transform.InverseTransformPoint(centerWs);
            size   = sizeWs;
            ForceRebuild(true);
        }

        /// <summary>
        /// Returns the current projection volume as a world-space <see cref="Bounds"/>.
        /// </summary>
        public Bounds GetProjectionBounds()
            => new(transform.TransformPoint(center),
                   Vector3.Scale(new Vector3(size.x, size.y, maxDistance), transform.lossyScale));

        /// <summary>
        /// Returns the current mesh and collider subdivision counts.
        /// </summary>
        public Vector2Int GetSubdivisionSettings()
            => new(meshSubdivisions, colliderSubdivisions);

        /// <summary>
        /// Estimates memory usage (KB) for a given subdivision value.
        /// </summary>
        /// <param name="overrideSubDiv">
        /// Subdivision level to test; &lt;=2 to use the current value.
        /// </param>
        public int EstimateMemoryFootprint(int overrideSubDiv = -1)
        {
            int sub = overrideSubDiv > 2 ? overrideSubDiv : meshSubdivisions;
            int verts = (sub + 1) * (sub + 1);
            int tris  = sub * sub * 2;
            const int vertSize = 12 * 2 + 8; // pos+normal+uv
            return (verts * vertSize + tris * 4 * 3) / 1024; // KB
        }

        /// <summary>
        /// Predicts rebuild time for a future subdivision value based on the
        /// last measured time.
        /// </summary>
        /// <param name="futureSubDiv">Subdivision level to estimate.</param>
        public float PredictRebuildTimeMS(int futureSubDiv)
        {
            if (m_LastStats.TrianglesVisual == 0) return 0f;
            float ratio = Mathf.Pow((float)futureSubDiv / meshSubdivisions, 2f);
            return m_LastRebuildTimeMS * ratio;
        }

        /// <summary>
        /// Automatically adjusts subdivision values to target a desired average
        /// triangle area (in cm²).
        /// </summary>
        /// <param name="targetAvgTriAreaCm2">Desired average area in cm².</param>
        public void SetAdaptiveSubdivisions(float targetAvgTriAreaCm2)
        {
            if (targetAvgTriAreaCm2 <= 0) return;
            float current = GetAverageTriArea(false);
            if (current <= 0) return;

            float ratio = current / targetAvgTriAreaCm2;
            int newSub = Mathf.Clamp(
                Mathf.RoundToInt(meshSubdivisions * Mathf.Sqrt(ratio)), 3, 64);
            if (newSub == meshSubdivisions) return;

            meshSubdivisions     = newSub;
            colliderSubdivisions = Mathf.Clamp(newSub / 4, 3, 64);
            ForceRebuild(true);
        }

        #endregion

        #region Debug draw ─────────────────────────────────────────────────────

        /// <summary>
        /// Draws debug rays at every cached hit point for quick visual inspection.
        /// </summary>
        /// <param name="color">Ray color.</param>
        /// <param name="sizeValue">Ray length.</param>
        /// <param name="duration">Duration in seconds.</param>
        public void DebugDrawHitPoints(Color color,
                                       float sizeValue = 0.02f,
                                       float duration  = 1f)
        {
            foreach (var h in m_HitCache)
                Debug.DrawRay(h.point, h.normal * sizeValue, color, duration);
        }

        #endregion

        #region Internal hit recorder ─────────────────────────────────────────-

        /// <summary>
        /// Internal helper to record a hit, respecting all registered filters.
        /// Must be called from <c>GenerateGrid</c> or equivalent.
        /// </summary>
        /// <param name="hit">The <see cref="RaycastHit"/> to record.</param>
        private void RecordHit(RaycastHit hit)
        {
            foreach (var filter in m_HitFilters)
                if (!filter(hit)) return;
            m_HitCache.Add(hit);
        }

        #endregion
    }
}
#endif