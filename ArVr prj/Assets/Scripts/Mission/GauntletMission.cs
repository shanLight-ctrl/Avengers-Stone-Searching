using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Tracks the six gauntlet sockets. When every socket holds a stone the ship's power returns.
/// </summary>
public class GauntletMission : MonoBehaviour
{
    public XRSocketInteractor[] sockets;
    public TextMeshPro statusText;
    public Light[] emergencyLights;
    public Light[] powerLights;
    public Renderer[] powerFixtures;
    public Light surgeLight;
    public EarthStatus earth;

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    float[] _emergencyBase;
    float[] _powerTarget;
    int _restored = -1;
    bool _complete;

    void Start()
    {
        _emergencyBase = new float[emergencyLights != null ? emergencyLights.Length : 0];
        for (int i = 0; i < _emergencyBase.Length; i++)
            if (emergencyLights[i] != null) _emergencyBase[i] = emergencyLights[i].intensity;

        _powerTarget = new float[powerLights != null ? powerLights.Length : 0];
        for (int i = 0; i < _powerTarget.Length; i++)
        {
            if (powerLights[i] == null) continue;
            _powerTarget[i] = powerLights[i].intensity;
            powerLights[i].intensity = 0f;
        }

        if (surgeLight != null) surgeLight.intensity = 0f;
    }

    void Update()
    {
        if (_complete || sockets == null) return;

        int count = 0;
        foreach (var s in sockets)
            if (s != null && s.hasSelection) count++;

        if (count == _restored) return;
        _restored = count;
        RefreshText();
        if (earth != null) earth.SetProgress(_restored, sockets.Length);

        if (_restored >= sockets.Length)
            StartCoroutine(PowerUp());
    }

    void RefreshText()
    {
        if (statusText == null) return;
        statusText.text = "STONES RESTORED  " + _restored + " / " + sockets.Length +
                          "\n<size=55%>Find the stones in the wreckage and return them to the gauntlet</size>";
    }

    IEnumerator PowerUp()
    {
        _complete = true;
        if (earth != null) earth.Complete();

        if (statusText != null)
            statusText.text = "<color=#7FE8FF>ALL STONES RESTORED</color>\n<size=55%>Ship power returning...</size>";

        // Stop the emergency lamps flickering so we can fade them out cleanly.
        if (emergencyLights != null)
            foreach (var l in emergencyLights)
            {
                if (l == null) continue;
                var f = l.GetComponent<FlickerLight>();
                if (f != null) f.enabled = false;
            }

        const float duration = 4f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;

            for (int i = 0; i < _emergencyBase.Length; i++)
                if (emergencyLights[i] != null)
                    emergencyLights[i].intensity = Mathf.Lerp(_emergencyBase[i], 0f, k);

            for (int i = 0; i < _powerTarget.Length; i++)
                if (powerLights[i] != null)
                    powerLights[i].intensity = Mathf.Lerp(0f, _powerTarget[i], k * k);

            if (surgeLight != null)
                surgeLight.intensity = Mathf.Sin(k * Mathf.PI) * 12f;

            SetFixtureGlow(Mathf.Lerp(0f, 2.5f, k));
            yield return null;
        }

        if (surgeLight != null) surgeLight.intensity = 0f;
        SetFixtureGlow(2.5f);

        if (statusText != null)
            statusText.text = "<color=#7FE8FF>MISSION COMPLETE</color>\n<size=55%>The stones are home. The ship is alive again.</size>";
    }

    void SetFixtureGlow(float k)
    {
        if (powerFixtures == null) return;
        var block = new MaterialPropertyBlock();
        foreach (var r in powerFixtures)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor(EmissionId, new Color(0.6f, 0.85f, 1f) * k);
            r.SetPropertyBlock(block);
        }
    }
}
