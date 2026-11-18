using UnityEngine;

/// <summary>Pulses a stone's emission and its child point light.</summary>
public class StoneGlow : MonoBehaviour
{
    public Color color = Color.white;
    public float pulseSpeed = 2f;
    public float minGlow = 1.5f;
    public float maxGlow = 3.5f;

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    Renderer _renderer;
    Light _light;
    MaterialPropertyBlock _block;
    float _phase;

    void Awake()
    {
        _renderer = GetComponentInChildren<Renderer>();
        _light = GetComponentInChildren<Light>();
        _block = new MaterialPropertyBlock();
        _phase = Random.value * 6.28f;
    }

    void Update()
    {
        float t = (Mathf.Sin(Time.time * pulseSpeed + _phase) + 1f) * 0.5f;
        float k = Mathf.Lerp(minGlow, maxGlow, t);

        if (_renderer != null)
        {
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(EmissionId, color * k);
            _renderer.SetPropertyBlock(_block);
        }

        if (_light != null)
        {
            _light.color = color;
            _light.intensity = k * 0.5f;
        }
    }
}
