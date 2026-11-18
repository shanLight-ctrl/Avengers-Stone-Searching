using UnityEngine;

/// <summary>Flickers a Light like a failing emergency lamp (noise plus random brief dropouts).</summary>
[RequireComponent(typeof(Light))]
public class FlickerLight : MonoBehaviour
{
    public float minFactor = 0.35f;
    public float maxFactor = 1f;
    public float speed = 7f;
    [Tooltip("Chance per second of a short blackout.")]
    public float dropoutChance = 0.4f;

    Light _light;
    float _base;
    float _seed;
    float _offTimer;

    void Awake()
    {
        _light = GetComponent<Light>();
        _base = _light.intensity;
        _seed = Random.value * 100f;
    }

    void Update()
    {
        if (_offTimer > 0f)
        {
            _offTimer -= Time.deltaTime;
            _light.intensity = 0f;
            return;
        }

        if (Random.value < dropoutChance * Time.deltaTime)
            _offTimer = Random.Range(0.04f, 0.18f);

        float n = Mathf.PerlinNoise(_seed, Time.time * speed);
        _light.intensity = _base * Mathf.Lerp(minFactor, maxFactor, n);
    }
}
