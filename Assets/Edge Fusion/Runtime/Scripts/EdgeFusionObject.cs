using UnityEngine;
using System.Collections.Generic;

namespace EdgeFusion {

    public enum IncludeMode {
        OnlyThisObject,
        IncludeChildren
    }

    [AddComponentMenu("Kronnect/Edge Fusion/Edge Fusion Object")]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [HelpURL("https://kronnect.com/guides/edge-fusion-introduction/")]
    public class EdgeFusionObject : MonoBehaviour {

        public bool overrideRadius;
        [Tooltip("Custom blend radius in meters (0 = disables blending for this object)")]
        [Range(0f, 0.5f)]
        public float customRadius = 0.05f;

        public bool overrideObjectId;
        [Tooltip("Custom Object ID (0 = auto, >0 = override). Objects with the same ID are considered the same for inter-object detection.")]
        public int customObjectId;
        [Tooltip("Use a random Object ID, instead of an Id derived from object position")]
        public bool useRandomId;
        [Tooltip("Assign a different random ID to each child renderer")]
        public bool randomIdPerChild;

        [Tooltip("Disable intra-object fusion for this object/children while keeping inter-object fusion.")]
        public bool disallowIntraObjectFusion;

        public IncludeMode includeMode = IncludeMode.OnlyThisObject;
        public LayerMask childLayerMask = ~0;

        static MaterialPropertyBlock props;
        static readonly List<Material> sharedMaterialsBuffer = new List<Material>(8);

        public List<Renderer> renderers { get; private set; } = new List<Renderer>();
        [System.NonSerialized] public Material nonInstancingMaterial;
        bool initialized;

        void BuildRendererList () {
            renderers.Clear();

            switch (includeMode) {
                case IncludeMode.OnlyThisObject:
                    Renderer thisRenderer = GetComponent<Renderer>();
                    if (thisRenderer != null) {
                        renderers.Add(thisRenderer);
                    }
                    break;
                case IncludeMode.IncludeChildren:
                    GetComponentsInChildren(true, renderers);
                    for (int i = renderers.Count - 1; i >= 0; i--) {
                        Renderer r = renderers[i];
                        if (r == null || ((1 << r.gameObject.layer) & childLayerMask) == 0) {
                            renderers.RemoveAt(i);
                        }
                    }
                    break;
            }

            ValidateRendererMaterials();
        }

        void OnEnable () {
            if (initialized || !Application.isPlaying) {
                Refresh();
            }
        }

        void Start () {
            if (Application.isPlaying) {
                Refresh();
                initialized = true;
            }
        }

        void OnValidate () {
            customObjectId = Mathf.Abs(customObjectId);
            Refresh();
        }

        void OnDisable () {
            UpdateObjectProperties(false);
        }

        void OnDestroy () {
            renderers.Clear();
        }

        int GetRandomId () {
            return Random.Range(1, 851);
        }

        public void Refresh () {
            BuildRendererList();
            UpdateObjectProperties(enabled);
        }

        void UpdateObjectProperties (bool isEnabled) {
            if (props == null) props = new MaterialPropertyBlock();

            bool isActive = gameObject.activeInHierarchy && isEnabled;
            int rnd = useRandomId && !randomIdPerChild ? GetRandomId() : 0;
            int rendererCount = renderers.Count;
            for (int i = 0; i < rendererCount; i++) {
                Renderer renderer = renderers[i];
                if (renderer == null) continue;
                renderer.GetPropertyBlock(props);
                float objectId = 0;
                float radius = 0;
                float disallowIntraFusion = 0f;
                if (isActive) {
                    if (overrideRadius) {
                        radius = customRadius > 0 ? customRadius : 1;
                    }
                    if (overrideObjectId) {
                        if (useRandomId) {
                            objectId = randomIdPerChild ? GetRandomId() : rnd;
                        }
                        else if (customObjectId > 0) {
                            objectId = customObjectId;
                        }
                    }
                    disallowIntraFusion = disallowIntraObjectFusion ? 1f : 0f;
                }
                props.SetFloat(ShaderParams.EdgeFusionRadius, radius);
                props.SetFloat(ShaderParams.CustomObjectId, objectId);
                props.SetFloat(ShaderParams.DisallowIntraFusion, disallowIntraFusion);
                renderer.SetPropertyBlock(props);
            }
        }

        void ValidateRendererMaterials () {
            nonInstancingMaterial = null;
            int rendererCount = renderers.Count;
            for (int i = 0; i < rendererCount; i++) {
                Renderer r = renderers[i];
                if (r == null) continue;
                sharedMaterialsBuffer.Clear();
                r.GetSharedMaterials(sharedMaterialsBuffer);
                int matCount = sharedMaterialsBuffer.Count;
                for (int j = 0; j < matCount; j++) {
                    Material mat = sharedMaterialsBuffer[j];
                    if (mat == null) continue;
                    if (!mat.enableInstancing) {
                        nonInstancingMaterial = mat;
                        sharedMaterialsBuffer.Clear();
                        return;
                    }
                }
            }
            sharedMaterialsBuffer.Clear();
        }

    }
}