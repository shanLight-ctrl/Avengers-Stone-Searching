using TMPro;
using UnityEngine;

/// <summary>
/// Drives the view of Earth outside the ship window: burning while the stones are scattered,
/// recovering as they are restored, calm once the mission is complete.
/// </summary>
public class EarthStatus : MonoBehaviour
{
    public Renderer earthRenderer;
    public Light windowGlow;
    public TextMeshPro label;

    public Color crisisColor = new Color(1f, 0.2f, 0.1f);
    public Color healedColor = new Color(0.55f, 0.8f, 1f);
    public float crisisGlow = 3f;
    public float healedGlow = 2f;
    public float changeSpeed = 0.25f;

    static readonly int HealthId = Shader.PropertyToID("_Health");

    MaterialPropertyBlock _block;
    float _health;
    float _target;
    int _restored;
    int _total = 6;
    bool _complete;
    float _noiseSeed;

    void Awake()
    {
        _block = new MaterialPropertyBlock();
        _noiseSeed = Random.value * 100f;
    }

    void Start()
    {
        Apply();
        RefreshLabel();
    }

    /// <summary>Called whenever the number of stones in the gauntlet changes.</summary>
    public void SetProgress(int restored, int total)
    {
        _restored = restored;
        _total = Mathf.Max(1, total);
        _target = 0.75f * restored / _total;
        RefreshLabel();
    }

    /// <summary>Called once all stones are home.</summary>
    public void Complete()
    {
        _complete = true;
        _target = 1f;
        RefreshLabel();
    }

    void Update()
    {
        if (!Mathf.Approximately(_health, _target))
        {
            _health = Mathf.MoveTowards(_health, _target, changeSpeed * Time.deltaTime);
            Apply();
        }

        // The window light flickers while Earth is in trouble and settles as it heals.
        if (windowGlow != null)
        {
            float flicker = Mathf.Lerp(0.6f, 1f, Mathf.PerlinNoise(_noiseSeed, Time.time * 3f));
            float steady = Mathf.Lerp(crisisGlow * flicker, healedGlow, _health);
            windowGlow.intensity = steady;
        }
    }

    void Apply()
    {
        if (earthRenderer != null)
        {
            earthRenderer.GetPropertyBlock(_block);
            _block.SetFloat(HealthId, _health);
            earthRenderer.SetPropertyBlock(_block);
        }

        if (windowGlow != null)
            windowGlow.color = Color.Lerp(crisisColor, healedColor, _health);
    }

    void RefreshLabel()
    {
        if (label == null) return;

        if (_complete)
            label.text = "<color=#7FE8FF>EARTH - STABLE</color>\n<size=60%>The storms are fading.</size>";
        else if (_restored <= 0)
            label.text = "<color=#FF5A3C>EARTH - CRITICAL</color>\n<size=60%>Fires are spreading. The stones are scattered.</size>";
        else
            label.text = "<color=#FFB040>EARTH - UNSTABLE</color>\n<size=60%>" + _restored + " of " + _total + " stones restored</size>";
    }
}
