using UnityEngine;

/// <summary>
/// TVモニターメッシュに SecurityCameraSystem の映像を貼り付けるコンポーネント。
///
/// ■ セットアップ手順
///   1. TV モニターの Mesh (Renderer持ち) にこのコンポーネントを追加
///   2. テクスチャプロパティ名を確認（URP = "_BaseMap" / Standard = "_MainTex"）
///   3. JumpScare・ポーズ等の Canvas は Screen Space - Overlay のまま残す
///      → それらは絶対にTVに映らない（World に存在しないため）
///
/// ■ HUD を TV に表示したい場合
///   - TV メッシュの少し前面に World Space Canvas を配置する
///   - Canvas の Layer を "MonitorUI" 等にして Security Camera の
///     culling mask から除外すると、カメラには映らずメインカメラにだけ映る
/// </summary>
[RequireComponent(typeof(Renderer))]
public class TVMonitorDisplay : MonoBehaviour
{
    [Tooltip("シェーダーのテクスチャプロパティ名。URP Lit = _BaseMap, Standard = _MainTex")]
    [SerializeField] private string textureProperty = "_BaseMap";

    [Tooltip("ONにするとエミッション(_EmissionColor)にも同じRTを貼り、画面が発光して見える。")]
    [SerializeField] private bool applyToEmission = true;

    [Tooltip("エミッション強度。0で発光なし。")]
    [SerializeField, Range(0f, 3f)] private float emissionIntensity = 1.0f;

    private Renderer       _rend;
    private Material       _mat;
    private RenderTexture  _lastRT; // 変化時のみマテリアルを更新

    void Start()
    {
        _rend = GetComponent<Renderer>();
        _mat  = _rend.material;
    }

    void LateUpdate()
    {
        if (_mat == null) return;

        var rt = SecurityCameraSystem.Instance?.MonitorRenderTexture;
        if (rt == null || rt == _lastRT) return; // RT が変わっていなければスキップ
        _lastRT = rt;

        _mat.SetTexture(textureProperty, rt);
        if (applyToEmission)
        {
            _mat.EnableKeyword("_EMISSION");
            _mat.SetTexture("_EmissionMap", rt);
            _mat.SetColor("_EmissionColor", Color.white * emissionIntensity);
        }
    }

    void OnDestroy()
    {
        // インスタンス化したマテリアルを解放
        if (_mat != null) Destroy(_mat);
    }
}
