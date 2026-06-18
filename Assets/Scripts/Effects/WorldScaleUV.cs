using UnityEngine;

/// <summary>
/// スケールで引き伸ばされた Cube のテクスチャ伸びを MaterialPropertyBlock で補正する。
/// 同じマテリアルを共有したまま使えるため Draw Call が増えない。
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class WorldScaleUV : MonoBehaviour
{
    [SerializeField] float texelsPerUnit = 1f;

    Renderer   _r;
    MaterialPropertyBlock _mpb;

    void OnEnable()
    {
        _r   = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
        Apply();
    }

    void Apply()
    {
        Vector3 s = transform.lossyScale;

        // 最も薄い軸を法線方向とみなし、残り 2 軸でタイリングを決める
        float tilingX, tilingY;
        if (s.x <= s.y && s.x <= s.z)       // 薄い軸 = X → 表面は YZ
            (tilingX, tilingY) = (s.z, s.y);
        else if (s.y <= s.x && s.y <= s.z)  // 薄い軸 = Y → 表面は XZ
            (tilingX, tilingY) = (s.x, s.z);
        else                                 // 薄い軸 = Z → 表面は XY
            (tilingX, tilingY) = (s.x, s.y);

        _mpb.SetVector("_BaseMap_ST",
            new Vector4(tilingX * texelsPerUnit, tilingY * texelsPerUnit, 0f, 0f));
        _r.SetPropertyBlock(_mpb);
    }

#if UNITY_EDITOR
    void Update() => Apply();
#endif
}
