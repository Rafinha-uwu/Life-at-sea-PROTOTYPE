using UnityEngine;

[ExecuteAlways]
public class SkyboxController : MonoBehaviour
{
    [SerializeField] Transform _Sun = default;
    [SerializeField] Transform _Moon = default;

    static readonly int SunDir = Shader.PropertyToID("_SunDir");
    static readonly int MoonDir = Shader.PropertyToID("_MoonDir");
    static readonly int MoonSpaceMatrix = Shader.PropertyToID("_MoonSpaceMatrix");

    void LateUpdate()
    {
        // Points toward object
        if (_Sun != null)
            Shader.SetGlobalVector(SunDir, -_Sun.forward);

        if (_Moon == null) return;

        Shader.SetGlobalVector(MoonDir, -_Moon.forward);
        Shader.SetGlobalMatrix(MoonSpaceMatrix, new Matrix4x4(
            -_Moon.forward, _Moon.up, -_Moon.right, Vector4.zero).transpose);
    }
}
