using UnityEngine;

/// <summary>A flickering, slowly turning hologram: a memory echo of the crew.</summary>
public class HologramEcho : MonoBehaviour
{
    public Renderer[] renderers;
    public Color color = new Color(0.3f, 0.85f, 1f, 0.35f);
    public float spinDegreesPerSecond = 18f;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    MaterialPropertyBlock _block;
    float _seed;
    Vector3 _baseScale;
    float _glitchTimer;

    void Awake()
    {
        _block = new MaterialPropertyBlock();
        _seed = Random.value * 100f;
        _baseScale = transform.localScale;
    }

    void Update()
    {
        transform.Rotate(0f, spinDegreesPerSecond * Time.deltaTime, 0f, Space.Self);

        float flicker = Mathf.Lerp(0.35f, 1f, Mathf.PerlinNoise(_seed, Time.time * 6f));

        // Occasional horizontal glitch.
        _glitchTimer -= Time.deltaTime;
        if (_glitchTimer <= 0f)
        {
            if (Random.value < 0.15f)
            {
                transform.localScale = new Vector3(_baseScale.x * Random.Range(1.05f, 1.3f), _baseScale.y, _baseScale.z);
                _glitchTimer = Random.Range(0.03f, 0.08f);
            }
            else
            {
                transform.localScale = _baseScale;
                _glitchTimer = Random.Range(0.2f, 1.2f);
            }
        }

        if (renderers == null) return;

        Color c = color;
        c.a = color.a * flicker;
        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, c);
            r.SetPropertyBlock(_block);
        }
    }
}
