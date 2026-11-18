#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Tools > Cosmic > Build Crashed Ship Scene
/// Builds Assets/Scenes/CrashedShip.unity: a wrecked, half-dead starship interior with story logs,
/// hologram "memory echoes", six glowing stones hidden in the wreckage and a gauntlet on a stand
/// that the player must restore them to. Safe to re-run: it rebuilds the scene from scratch.
/// </summary>
public static class CrashedShipSceneBuilder
{
    const string ScenePath = "Assets/Scenes/CrashedShip.unity";
    const string AssetDir = "Assets/Scenes/CrashedShip_Assets";
    const string RigPath = "Assets/Samples/XR Interaction Toolkit/3.1.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
    const string PackRoot = "Assets/Dzeruza/MinimalScifiPack/3DModels/";
    const string WallMatPath = "Assets/Dzeruza/MinimalScifiPack/Textures&Materials/M_Trim_Walls.mat";

    // Room size (metres). X = width, Z = depth, H = height.
    const float W = 16f;
    const float D = 16f;
    const float H = 5f;

    static readonly Vector3 SpawnPos = new Vector3(0f, 0f, -6.5f);
    static readonly Vector3 PedestalPos = new Vector3(0f, 0f, 5.2f);

    static readonly Color[] StoneColors =
    {
        new Color(0.65f, 0.25f, 1f),    // violet
        new Color(0.2f, 0.5f, 1f),      // blue
        new Color(1f, 0.15f, 0.15f),    // red
        new Color(0.2f, 1f, 0.4f),      // green
        new Color(1f, 0.85f, 0.15f),    // amber
        new Color(1f, 0.45f, 0.1f),     // orange
    };

    static readonly List<Light> _emergency = new List<Light>();
    static readonly List<Light> _power = new List<Light>();
    static readonly List<Renderer> _fixtures = new List<Renderer>();
    static readonly List<XRSocketInteractor> _sockets = new List<XRSocketInteractor>();
    static readonly Dictionary<string, Material> _saved = new Dictionary<string, Material>();
    static TextMeshPro _statusText;
    static Light _surge;
    static Renderer _earthRenderer;
    static Light _windowGlow;
    static TextMeshPro _earthLabel;

    // ------------------------------------------------------------------ entry point

    [MenuItem("Tools/Cosmic/Build Crashed Ship Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
        if (rigPrefab == null)
        {
            EditorUtility.DisplayDialog("Crashed Ship", "XR Origin prefab not found at:\n" + RigPath +
                "\n\nImport the XR Interaction Toolkit 'Starter Assets' sample first.", "OK");
            return;
        }

        AssetDatabase.DeleteAsset(AssetDir);
        AssetDatabase.CreateFolder("Assets/Scenes", "CrashedShip_Assets");

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Random.InitState(1337);
        _emergency.Clear();
        _power.Clear();
        _fixtures.Clear();
        _sockets.Clear();
        _saved.Clear();
        _statusText = null;
        _surge = null;
        _earthRenderer = null;
        _windowGlow = null;
        _earthLabel = null;

        BuildAtmosphere();
        BuildOutside();
        BuildShell();
        BuildProps();
        BuildGauntlet();
        BuildStones();
        BuildStory();
        BuildPlayer(rigPrefab);
        BuildPostFx();
        BuildMission();

        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);

        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool found = false;
        foreach (var s in scenes)
        {
            if (s.path == ScenePath) { s.enabled = true; found = true; }
        }
        if (!found) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CrashedShip] Scene built and saved to " + ScenePath);
        EditorUtility.DisplayDialog("Crashed Ship", "Scene built:\n" + ScenePath + "\n\nPress Play to try it.", "OK");
    }

    // ------------------------------------------------------------------ atmosphere

    static void BuildAtmosphere()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.04f, 0.045f, 0.065f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.02f;
        RenderSettings.fogColor = new Color(0.05f, 0.06f, 0.09f);

        var sky = new Material(Shader.Find("Skybox/Procedural")) { name = "Sky_Space" };
        sky.EnableKeyword("_SUNDISK_NONE");
        sky.SetFloat("_SunSize", 0f);
        sky.SetFloat("_AtmosphereThickness", 0.2f);
        sky.SetColor("_SkyTint", new Color(0.05f, 0.06f, 0.12f));
        sky.SetColor("_GroundColor", Color.black);
        sky.SetFloat("_Exposure", 0.25f);
        RenderSettings.skybox = Save(sky);

        // Cold light of a nearby planet, only reaching the floor through the hull breach.
        var moon = MakeLight("Moonlight", LightType.Directional, new Vector3(0f, 10f, 0f), new Color(0.5f, 0.65f, 1f), 0.45f, 0f);
        moon.transform.rotation = Quaternion.Euler(70f, -20f, 0f);
        moon.shadows = LightShadows.Soft;
        RenderSettings.sun = moon;

        // Shaft of light through the breach.
        var shaft = MakeLight("BreachShaft", LightType.Spot, new Vector3(-3f, H + 1f, 1f), new Color(0.6f, 0.8f, 1f), 10f, 14f);
        shaft.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        shaft.spotAngle = 60f;
        shaft.shadows = LightShadows.Soft;

        // Dust drifting in the shaft and across the hall.
        MakeDust("DustMotes_Breach", new Vector3(-3f, 2.5f, 1f), new Vector3(5f, 5f, 5f), 70f);
        MakeDust("DustMotes_Hall", new Vector3(0f, 2f, 0f), new Vector3(14f, 3.5f, 14f), 40f);

        // Stars, visible through the hull breach and the window.
        MakeStars();

        // Sparks from broken conduits.
        MakeSparks("Sparks_Breach", new Vector3(-5.2f, 4.7f, 3.4f));
        MakeSparks("Sparks_North", new Vector3(5.5f, 4.7f, 7.4f));
    }

    // ------------------------------------------------------------------ ship shell

    static void BuildShell()
    {
        var shell = new GameObject("Shell").transform;
        var metal = Lit("Metal_Dark", new Color(0.1f, 0.11f, 0.13f), 0.8f, 0.35f, Color.black);

        // Floor
        Prim(PrimitiveType.Cube, "Floor", new Vector3(0f, -0.21f, 0f), new Vector3(W, 0.4f, D), Wall("Floor", new Vector2(W / 3f, D / 3f), 0.5f), shell);
        TileFloor(shell);

        // Walls
        BuildNorthWallWithWindow(shell);
        Prim(PrimitiveType.Cube, "Wall_S", new Vector3(0f, H / 2f, -D / 2f - 0.2f), new Vector3(W + 0.8f, H, 0.4f), Wall("Wall_S", new Vector2(W / 3f, H / 3f), 0.65f), shell);
        Prim(PrimitiveType.Cube, "Wall_E", new Vector3(W / 2f + 0.2f, H / 2f, 0f), new Vector3(0.4f, H, D), Wall("Wall_E", new Vector2(D / 3f, H / 3f), 0.65f), shell);
        Prim(PrimitiveType.Cube, "Wall_W", new Vector3(-W / 2f - 0.2f, H / 2f, 0f), new Vector3(0.4f, H, D), Wall("Wall_W", new Vector2(D / 3f, H / 3f), 0.65f), shell);

        // Ceiling with a breach: hole spans x -5.5..-0.5, z -1.5..3.5.
        var ceilMat = Wall("Ceiling", new Vector2(W / 3f, D / 3f), 0.45f);
        Prim(PrimitiveType.Cube, "Ceiling_A", new Vector3(-6.75f, H + 0.15f, 0f), new Vector3(2.5f, 0.3f, D), ceilMat, shell);
        Prim(PrimitiveType.Cube, "Ceiling_B", new Vector3(3.75f, H + 0.15f, 0f), new Vector3(8.5f, 0.3f, D), ceilMat, shell);
        Prim(PrimitiveType.Cube, "Ceiling_C", new Vector3(-3f, H + 0.15f, -4.75f), new Vector3(5f, 0.3f, 6.5f), ceilMat, shell);
        Prim(PrimitiveType.Cube, "Ceiling_D", new Vector3(-3f, H + 0.15f, 5.75f), new Vector3(5f, 0.3f, 4.5f), ceilMat, shell);

        // Ribs, pillars.
        Prim(PrimitiveType.Cube, "Beam_1", new Vector3(0f, H - 0.25f, -5f), new Vector3(W, 0.5f, 0.35f), metal, shell);
        Prim(PrimitiveType.Cube, "Beam_2", new Vector3(0f, H - 0.25f, 5f), new Vector3(W, 0.5f, 0.35f), metal, shell);
        foreach (float x in new[] { -7.5f, 7.5f })
            foreach (float z in new[] { -7.5f, 0f, 7.5f })
                Prim(PrimitiveType.Cube, "Pillar", new Vector3(x, H / 2f, z), new Vector3(0.6f, H, 0.6f), metal, shell);

        // A snapped beam dangling into the breach.
        var dangling = Prim(PrimitiveType.Cube, "Beam_Broken", new Vector3(-3f, 4.1f, 0.6f), new Vector3(0.4f, 0.4f, 4.2f), metal, shell);
        dangling.transform.rotation = Quaternion.Euler(28f, 20f, 8f);

        // The torn-off hull plate that fell in and leans against the floor.
        var slab = Prim(PrimitiveType.Cube, "CrashSlab", new Vector3(-3.2f, 2.3f, 1.0f), new Vector3(3.6f, 0.25f, 5f), Wall("CrashSlab", new Vector2(1.2f, 1.7f), 0.5f), shell);
        slab.transform.rotation = Quaternion.Euler(35f, 10f, 0f);

        // Debris pile.
        var debrisMat = Wall("Debris", new Vector2(0.5f, 0.5f), 0.4f);
        for (int i = 0; i < 14; i++)
        {
            float s = Random.Range(0.25f, 0.75f);
            var pos = new Vector3(Random.Range(-5f, -1f), s * 0.5f, Random.Range(2f, 4.6f));
            var d = Prim(PrimitiveType.Cube, "Debris_" + i, pos, new Vector3(s, s * Random.Range(0.3f, 0.9f), s * Random.Range(0.6f, 1.3f)), debrisMat, shell);
            d.transform.rotation = Random.rotation;
            // Keep the pile resting on the floor.
            var b = d.GetComponent<Renderer>().bounds;
            d.transform.position += new Vector3(0f, -b.min.y, 0f);
        }

        // Emergency lamps (red, flickering) along the side walls.
        int idx = 0;
        foreach (float x in new[] { -7.2f, 7.2f })
        {
            foreach (float z in new[] { -5f, 0f, 5f })
            {
                var fixMat = Lit("EmergencyFixture_" + idx, new Color(0.2f, 0.02f, 0.02f), 0f, 0.3f, new Color(1f, 0.1f, 0.05f) * 1.5f);
                Prim(PrimitiveType.Cube, "EmergencyFixture_" + idx, new Vector3(x, H - 0.4f, z), new Vector3(0.3f, 0.12f, 0.4f), fixMat, shell, false);
                var l = MakeLight("EmergencyLight_" + idx, LightType.Point, new Vector3(x * 0.93f, H - 0.7f, z), new Color(1f, 0.15f, 0.1f), 3f, 8f);
                if (idx % 2 == 0) l.gameObject.AddComponent<FlickerLight>();
                _emergency.Add(l);
                idx++;
            }
        }

        // Ceiling light strips that come on when power returns.
        Vector3[] stripPos =
        {
            new Vector3(-4f, H - 0.15f, -3f), new Vector3(4f, H - 0.15f, -3f),
            new Vector3(4f, H - 0.15f, 3f), new Vector3(0f, H - 0.15f, 6.5f)
        };
        for (int i = 0; i < stripPos.Length; i++)
        {
            var stripMat = Lit("PowerStrip_" + i, new Color(0.15f, 0.17f, 0.2f), 0f, 0.4f, Color.black);
            var strip = Prim(PrimitiveType.Cube, "PowerStrip_" + i, stripPos[i], new Vector3(1.8f, 0.06f, 0.22f), stripMat, shell, false);
            _fixtures.Add(strip.GetComponent<Renderer>());
            var l = MakeLight("PowerLight_" + i, LightType.Point, stripPos[i] + new Vector3(0f, -0.5f, 0f), new Color(0.65f, 0.85f, 1f), 4f, 10f);
            _power.Add(l);
        }
    }

    /// <summary>North wall with a big window cut into it, a frame, a status label and the Earth-light.</summary>
    static void BuildNorthWallWithWindow(Transform shell)
    {
        const float x0 = 2.6f;      // window left edge
        const float x1 = 7.2f;      // window right edge
        const float sill = 1.3f;
        const float top = 3.9f;
        float cx = (x0 + x1) / 2f;
        float ww = x1 - x0;
        float wz = D / 2f + 0.2f;   // wall centre (inner face is at z = D/2)

        // Four wall pieces around the opening.
        float leftWidth = x0 + W / 2f + 0.4f;
        Prim(PrimitiveType.Cube, "Wall_N_Left", new Vector3((-W / 2f - 0.4f + x0) / 2f, H / 2f, wz), new Vector3(leftWidth, H, 0.4f), Wall("Wall_N_Left", new Vector2(leftWidth / 3f, H / 3f), 0.65f), shell);
        float rightWidth = W / 2f + 0.4f - x1;
        Prim(PrimitiveType.Cube, "Wall_N_Right", new Vector3((x1 + W / 2f + 0.4f) / 2f, H / 2f, wz), new Vector3(rightWidth, H, 0.4f), Wall("Wall_N_Right", new Vector2(rightWidth / 3f, H / 3f), 0.65f), shell);
        Prim(PrimitiveType.Cube, "Wall_N_Below", new Vector3(cx, sill / 2f, wz), new Vector3(ww, sill, 0.4f), Wall("Wall_N_Below", new Vector2(ww / 3f, sill / 3f), 0.65f), shell);
        Prim(PrimitiveType.Cube, "Wall_N_Above", new Vector3(cx, (top + H) / 2f, wz), new Vector3(ww, H - top, 0.4f), Wall("Wall_N_Above", new Vector2(ww / 3f, (H - top) / 3f), 0.65f), shell);

        // Glass (faint, so Earth shows through) and a metal frame.
        var glass = MakeUnlit("WindowGlass", new Color(0.5f, 0.7f, 0.9f, 0.05f), false);
        Prim(PrimitiveType.Cube, "WindowGlass", new Vector3(cx, (sill + top) / 2f, wz), new Vector3(ww, top - sill, 0.03f), glass, shell);

        var frame = Lit("WindowFrame", new Color(0.08f, 0.09f, 0.1f), 0.85f, 0.4f, Color.black);
        float fz = D / 2f - 0.05f;
        Prim(PrimitiveType.Cube, "WindowFrame_Top", new Vector3(cx, top, fz), new Vector3(ww + 0.3f, 0.18f, 0.25f), frame, shell);
        Prim(PrimitiveType.Cube, "WindowFrame_Bottom", new Vector3(cx, sill, fz), new Vector3(ww + 0.3f, 0.18f, 0.25f), frame, shell);
        Prim(PrimitiveType.Cube, "WindowFrame_Left", new Vector3(x0, (sill + top) / 2f, fz), new Vector3(0.18f, top - sill + 0.18f, 0.25f), frame, shell);
        Prim(PrimitiveType.Cube, "WindowFrame_Right", new Vector3(x1, (sill + top) / 2f, fz), new Vector3(0.18f, top - sill + 0.18f, 0.25f), frame, shell);
        Prim(PrimitiveType.Cube, "WindowFrame_Mullion", new Vector3(cx, (sill + top) / 2f, fz), new Vector3(0.1f, top - sill, 0.2f), frame, shell);

        // Label above the window, readable from inside (text faces +Z, viewer looks along +Z).
        _earthLabel = Text("EarthLabel", "EARTH", 2f, Color.white, new Vector3(cx, (top + H) / 2f, D / 2f - 0.03f), Quaternion.identity, new Vector2(4.4f, 0.9f), shell);

        // Light cast into the room by Earth: red while it burns, cool blue once healed.
        _windowGlow = MakeLight("WindowGlow", LightType.Spot, new Vector3(cx, 2.6f, D / 2f - 0.5f), new Color(1f, 0.2f, 0.1f), 3f, 12f);
        _windowGlow.spotAngle = 100f;
        _windowGlow.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
    }

    /// <summary>The ship has crashed on a dead moon: rocky ground, scattered rocks, distant hills, and Earth in the sky.</summary>
    static void BuildOutside()
    {
        var outside = new GameObject("Outside").transform;
        var moon = Lit("MoonRock", new Color(0.2f, 0.18f, 0.17f), 0f, 0.1f, Color.black);

        // Ground sits just below the ship's floor so it never covers the interior.
        Prim(PrimitiveType.Cube, "MoonGround", new Vector3(0f, -0.45f, 0f), new Vector3(600f, 0.8f, 600f), moon, outside);

        // Rocks, keeping the view from the window clear.
        for (int i = 0; i < 45; i++)
        {
            float ang = Random.Range(0f, 6.2832f);
            float r = Random.Range(13f, 80f);
            float rx = Mathf.Cos(ang) * r;
            float rz = Mathf.Sin(ang) * r;
            if (rx > -6f && rx < 16f && rz > 8f && rz < 70f) continue;

            float s = Random.Range(0.4f, 3f);
            var type = Random.value < 0.5f ? PrimitiveType.Sphere : PrimitiveType.Cube;
            var rock = Prim(type, "Rock_" + i, new Vector3(rx, -0.05f + s * 0.2f, rz),
                new Vector3(s, s * Random.Range(0.5f, 1f), s * Random.Range(0.7f, 1.4f)), moon, outside, false);
            rock.transform.rotation = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f));
        }

        // Distant hills ringing the horizon, leaving the sky behind the window open for Earth.
        float[] hillAngles = { 60f, 100f, 140f, 180f, 220f, 260f, 300f, 340f };
        for (int i = 0; i < hillAngles.Length; i++)
        {
            float a = hillAngles[i] * Mathf.Deg2Rad;
            var pos = new Vector3(Mathf.Sin(a) * 230f, -6f, Mathf.Cos(a) * 230f);
            var hill = Prim(PrimitiveType.Sphere, "Hill_" + i, pos,
                new Vector3(Random.Range(60f, 110f), Random.Range(22f, 34f), Random.Range(40f, 70f)), moon, outside, false);
            hill.transform.rotation = Quaternion.Euler(0f, hillAngles[i], 0f);
        }

        // Earth, low over the horizon, framed by the window.
        var shader = Shader.Find("Cosmic/EarthStatus");
        Material earthMat;
        if (shader != null)
        {
            earthMat = new Material(shader) { name = "Earth" };
        }
        else
        {
            Debug.LogWarning("[CrashedShip] Shader 'Cosmic/EarthStatus' not found; Earth will be a plain blue sphere.");
            earthMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Earth" };
            earthMat.SetColor("_BaseColor", new Color(0.1f, 0.3f, 0.8f));
        }
        earthMat = Save(earthMat);

        var earth = Prim(PrimitiveType.Sphere, "Earth", new Vector3(62f, 18f, 170f), Vector3.one * 42f, earthMat, outside, false);
        earth.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        _earthRenderer = earth.GetComponent<Renderer>();
    }

    static void TileFloor(Transform parent)
    {
        // Measure the pack's floor tile, then lay it across the room if it looks like a sane size.
        var probe = Place("Floor/Prefabs/SM_Trim_Floor_8x8M_A1.prefab", Vector3.zero, 0f, null);
        if (probe == null) return;
        var b = GetBounds(probe);
        Object.DestroyImmediate(probe);

        float step = Mathf.Max(b.size.x, b.size.z);
        if (step < 2f || step > 20f) return;

        int nx = Mathf.CeilToInt(W / step);
        int nz = Mathf.CeilToInt(D / step);
        for (int ix = 0; ix < nx; ix++)
        {
            for (int iz = 0; iz < nz; iz++)
            {
                var pos = new Vector3(-W / 2f + step * (ix + 0.5f), 0f, -D / 2f + step * (iz + 0.5f));
                var tile = Place("Floor/Prefabs/SM_Trim_Floor_8x8M_A1.prefab", pos, 0f, parent);
                if (tile != null) tile.name = "FloorTile_" + ix + "_" + iz;
            }
        }
    }

    // ------------------------------------------------------------------ props

    static void BuildProps()
    {
        var props = new GameObject("Props").transform;
        PlaceProp("Props/Prefabs/SM_Box1.prefab", new Vector3(-6.5f, 0f, -6.5f), 10f, props);
        PlaceProp("Props/Prefabs/SM_Box1.prefab", new Vector3(-6.0f, 0f, -5.7f), 35f, props);
        PlaceProp("Props/Prefabs/SM_Canister1.prefab", new Vector3(6.8f, 0f, -6.5f), 0f, props);
        PlaceProp("Props/Prefabs/SM_Trim_L_Container1.prefab", new Vector3(6.3f, 0f, -3.2f), 90f, props);
        PlaceProp("Props/Prefabs/SM_Trim_L_Container2.prefab", new Vector3(-6.4f, 0f, 5.8f), 0f, props);
        PlaceProp("Props/Prefabs/SM_Canister1.prefab", new Vector3(5.5f, 0f, 6.3f), 0f, props);
        PlaceProp("Props/Prefabs/SM_Box1.prefab", new Vector3(6.9f, 0f, 3.5f), 80f, props);
        PlaceProp("Props/Prefabs/SM_Box1.prefab", new Vector3(3.2f, 0f, -6.8f), 0f, props);
        PlaceProp("Props/Prefabs/SM_Canister1.prefab", new Vector3(-1.5f, 0f, -7.0f), 0f, props);
    }

    static void PlaceProp(string rel, Vector3 pos, float yaw, Transform parent)
    {
        Place(rel, pos, yaw, parent);
    }

    // ------------------------------------------------------------------ gauntlet

    static void BuildGauntlet()
    {
        var gold = Lit("Gauntlet_Gold", new Color(0.62f, 0.45f, 0.12f), 0.9f, 0.55f, Color.black);
        var dark = Lit("Pedestal_Dark", new Color(0.09f, 0.1f, 0.12f), 0.85f, 0.4f, Color.black);

        var root = new GameObject("GauntletDisplay");
        root.transform.position = PedestalPos;

        // The stand that holds the gauntlet. Nobody wears it.
        Prim(PrimitiveType.Cylinder, "Pedestal_Base", new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.45f, 0.9f), dark, root.transform);
        Prim(PrimitiveType.Cylinder, "Pedestal_Cradle", new Vector3(0f, 0.97f, -0.3f), new Vector3(0.55f, 0.07f, 0.55f), dark, root.transform);

        var g = new GameObject("Gauntlet");
        g.transform.SetParent(root.transform, false);
        g.transform.localPosition = new Vector3(0f, 1.3f, 0f);
        g.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);

        // Back of the hand, cuff, fingers, thumb.
        Prim(PrimitiveType.Cube, "BackOfHand", Vector3.zero, new Vector3(0.62f, 0.06f, 0.5f), gold, g.transform);
        var cuff = Prim(PrimitiveType.Cylinder, "Cuff", new Vector3(0f, 0f, -0.35f), new Vector3(0.5f, 0.18f, 0.5f), gold, g.transform);
        cuff.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        foreach (float x in new[] { -0.225f, -0.075f, 0.075f, 0.225f })
        {
            var f = Prim(PrimitiveType.Capsule, "Finger", new Vector3(x, 0f, 0.33f), new Vector3(0.07f, 0.13f, 0.07f), gold, g.transform);
            f.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }
        var thumb = Prim(PrimitiveType.Capsule, "Thumb", new Vector3(0.36f, 0f, 0f), new Vector3(0.08f, 0.12f, 0.08f), gold, g.transform);
        thumb.transform.localRotation = Quaternion.Euler(90f, 35f, 0f);

        // Six sockets on the back of the hand.
        Vector3[] slots =
        {
            new Vector3(-0.225f, 0.03f, 0.1f), new Vector3(-0.075f, 0.03f, 0.1f),
            new Vector3(0.075f, 0.03f, 0.1f), new Vector3(0.225f, 0.03f, 0.1f),
            new Vector3(-0.075f, 0.03f, -0.12f), new Vector3(0.075f, 0.03f, -0.12f)
        };

        for (int i = 0; i < slots.Length; i++)
        {
            var ringMat = Lit("SlotRing_" + i, new Color(0.05f, 0.05f, 0.06f), 0.5f, 0.5f, StoneColors[i] * 0.6f);
            Prim(PrimitiveType.Cylinder, "SlotRing_" + i, slots[i] + new Vector3(0f, 0.003f, 0f), new Vector3(0.15f, 0.004f, 0.15f), ringMat, g.transform, false);

            var socketGo = new GameObject("Socket_" + i);
            socketGo.transform.SetParent(g.transform, false);
            socketGo.transform.localPosition = slots[i] + new Vector3(0f, 0.045f, 0f);
            var col = socketGo.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.07f;
            var socket = socketGo.AddComponent<XRSocketInteractor>();
            _sockets.Add(socket);
        }

        // Status readout above the stand, facing the player.
        _statusText = Text("StatusText", "STONES RESTORED  0 / 6", 2.4f, new Color(0.55f, 1f, 1f),
            PedestalPos + new Vector3(0f, 2.55f, 1.0f), Quaternion.identity, new Vector2(4.5f, 1.4f), null);

        // Focus light on the gauntlet and a light that surges when the mission completes.
        var spot = MakeLight("GauntletSpot", LightType.Spot, new Vector3(0f, 4.4f, 3.2f), new Color(1f, 0.9f, 0.7f), 8f, 8f);
        spot.spotAngle = 45f;
        spot.transform.rotation = Quaternion.LookRotation(PedestalPos + new Vector3(0f, 1.3f, 0f) - spot.transform.position);

        _surge = MakeLight("SurgeLight", LightType.Point, PedestalPos + new Vector3(0f, 1.6f, -0.3f), new Color(0.8f, 0.95f, 1f), 0f, 10f);
    }

    // ------------------------------------------------------------------ stones

    static void BuildStones()
    {
        var parent = new GameObject("Stones").transform;
        Vector3[] spots =
        {
            new Vector3(-6.7f, 0.08f, -0.8f),
            new Vector3(4.8f, 0.08f, -1.5f),
            new Vector3(0.2f, 0.08f, 3.6f),
            new Vector3(6.6f, 0.08f, -0.9f),
            new Vector3(-4.6f, 0.08f, 6.8f),
            new Vector3(1.0f, 0.08f, -6.8f),
        };

        for (int i = 0; i < spots.Length; i++)
        {
            Color c = StoneColors[i];
            var root = new GameObject("Stone_" + i);
            root.transform.SetParent(parent, false);
            root.transform.position = spots[i];

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.3f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            var col = root.AddComponent<SphereCollider>();
            col.radius = 0.07f;
            root.AddComponent<XRGrabInteractable>();

            var mat = Lit("Stone_" + i, c * 0.5f, 0.2f, 0.9f, c * 2f);
            Prim(PrimitiveType.Sphere, "Visual", Vector3.zero, Vector3.one * 0.11f, mat, root.transform, false);

            var l = MakeLight("Glow", LightType.Point, spots[i], c, 1.5f, 3f, root.transform);
            l.transform.localPosition = Vector3.zero;

            var glow = root.AddComponent<StoneGlow>();
            glow.color = c;
        }
    }

    // ------------------------------------------------------------------ story: logs + echoes

    static void BuildStory()
    {
        var story = new GameObject("Story").transform;

        // Terminals bolted to the walls. 'wp' is a point on the inner wall face, 'n' the inward normal.
        Terminal(story, new Vector3(-8f, 1.6f, -3f), Vector3.right,
            "<b>SHIP LOG 0417</b>\n\nWe have all six stones. Every world we cross, something hunts us for them.\n\nWe keep running.");
        Terminal(story, new Vector3(8f, 1.6f, 1.6f), Vector3.left,
            "<b>SHIP LOG 0431</b>\n\nEngines are failing. The gauntlet stand is still aboard.\n\nIf we go down, whoever finds this must put the stones back where they belong.");
        Terminal(story, new Vector3(-4f, 1.6f, 8f), Vector3.back,
            "<b>SHIP LOG 0433</b>\n\nImpact in three minutes. A dead moon below, and Earth burning in the window.\n\nThe stones are scattered through the hull. Restore them. Earth is counting on you.");

        // Hologram memory echoes of the crew, standing near each terminal.
        Echo(story, new Vector3(-6f, 0f, -3f), 1.0f);
        Echo(story, new Vector3(5.6f, 0f, 1.6f), 1.15f);
        Echo(story, new Vector3(-3.2f, 0f, 6.4f), 0.9f);
    }

    static void Terminal(Transform parent, Vector3 wallPoint, Vector3 inward, string message)
    {
        var housingMat = Lit("TerminalHousing", new Color(0.06f, 0.07f, 0.08f), 0.8f, 0.4f, Color.black);
        var screenMat = Lit("TerminalScreen", new Color(0.02f, 0.06f, 0.07f), 0f, 0.6f, new Color(0.05f, 0.3f, 0.35f));
        Quaternion rot = Quaternion.LookRotation(-inward);

        var housing = Prim(PrimitiveType.Cube, "Terminal", wallPoint + inward * 0.06f, new Vector3(1.9f, 1.2f, 0.12f), housingMat, parent);
        housing.transform.rotation = rot;
        var screen = Prim(PrimitiveType.Cube, "Terminal_Screen", wallPoint + inward * 0.125f, new Vector3(1.75f, 1.05f, 0.01f), screenMat, parent, false);
        screen.transform.rotation = rot;

        Text("Terminal_Text", message, 1.1f, new Color(0.55f, 1f, 1f), wallPoint + inward * 0.14f, rot, new Vector2(1.6f, 0.95f), parent);
    }

    static void Echo(Transform parent, Vector3 pos, float height)
    {
        var baseMat = Lit("EchoBase", new Color(0.05f, 0.06f, 0.07f), 0.8f, 0.4f, new Color(0.1f, 0.5f, 0.6f));
        var holoMat = MakeUnlit("EchoHolo", new Color(0.3f, 0.85f, 1f, 0.35f), true);

        var root = new GameObject("MemoryEcho");
        root.transform.SetParent(parent, false);
        root.transform.position = pos;

        Prim(PrimitiveType.Cylinder, "Projector", new Vector3(0f, 0.05f, 0f), new Vector3(0.7f, 0.05f, 0.7f), baseMat, root.transform);

        var figure = new GameObject("Figure");
        figure.transform.SetParent(root.transform, false);
        figure.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        figure.transform.localScale = new Vector3(1f, height, 1f);

        var body = Prim(PrimitiveType.Capsule, "Body", new Vector3(0f, 0.85f, 0f), new Vector3(0.38f, 0.6f, 0.26f), holoMat, figure.transform, false);
        var head = Prim(PrimitiveType.Sphere, "Head", new Vector3(0f, 1.62f, 0f), Vector3.one * 0.22f, holoMat, figure.transform, false);
        var armL = Prim(PrimitiveType.Capsule, "ArmL", new Vector3(-0.28f, 0.95f, 0f), new Vector3(0.12f, 0.4f, 0.12f), holoMat, figure.transform, false);
        var armR = Prim(PrimitiveType.Capsule, "ArmR", new Vector3(0.28f, 0.95f, 0f), new Vector3(0.12f, 0.4f, 0.12f), holoMat, figure.transform, false);

        var echo = figure.AddComponent<HologramEcho>();
        echo.renderers = new[]
        {
            body.GetComponent<Renderer>(), head.GetComponent<Renderer>(),
            armL.GetComponent<Renderer>(), armR.GetComponent<Renderer>()
        };
        echo.color = new Color(0.3f, 0.85f, 1f, 0.35f);
    }

    // ------------------------------------------------------------------ player, post fx, mission

    static void BuildPlayer(GameObject rigPrefab)
    {
        new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();

        var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
        rig.transform.SetPositionAndRotation(SpawnPos, Quaternion.identity);

        var cam = rig.GetComponentInChildren<Camera>(true);
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.farClipPlane = 1000f; // Earth and the hills are far away
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }
        else
        {
            Debug.LogWarning("[CrashedShip] No Camera found in the XR rig prefab.");
        }
    }

    static void BuildPostFx()
    {
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "CrashedShip_PostFx";
        AssetDatabase.CreateAsset(profile, AssetDir + "/CrashedShip_PostFx.asset");

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.9f);
        bloom.intensity.Override(1.2f);
        bloom.scatter.Override(0.7f);

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.38f);
        vignette.smoothness.Override(0.6f);

        var grade = profile.Add<ColorAdjustments>(true);
        grade.postExposure.Override(-0.2f);
        grade.saturation.Override(-15f);
        grade.colorFilter.Override(new Color(0.85f, 0.92f, 1f));

        var tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.ACES);

        var grain = profile.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Medium1);
        grain.intensity.Override(0.25f);

        // Volume components are sub-assets: save them with the profile or they vanish on reload.
        foreach (var c in profile.components)
            AssetDatabase.AddObjectToAsset(c, profile);
        EditorUtility.SetDirty(profile);

        var volGo = new GameObject("PostFX Volume");
        var vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = profile;
    }

    static void BuildMission()
    {
        var go = new GameObject("Mission");
        var m = go.AddComponent<GauntletMission>();
        m.sockets = _sockets.ToArray();
        m.statusText = _statusText;
        m.emergencyLights = _emergency.ToArray();
        m.powerLights = _power.ToArray();
        m.powerFixtures = _fixtures.ToArray();
        m.surgeLight = _surge;

        var earth = new GameObject("EarthStatus").AddComponent<EarthStatus>();
        earth.earthRenderer = _earthRenderer;
        earth.windowGlow = _windowGlow;
        earth.label = _earthLabel;
        m.earth = earth;
    }

    // ------------------------------------------------------------------ helpers: materials

    static Material Save(Material m)
    {
        // Same name twice in one build (e.g. the three terminals) reuses the first asset.
        if (_saved.TryGetValue(m.name, out var existing))
        {
            Object.DestroyImmediate(m);
            return existing;
        }

        string path = AssetDir + "/" + m.name + ".mat";
        AssetDatabase.CreateAsset(m, path);
        var saved = AssetDatabase.LoadAssetAtPath<Material>(path);
        _saved[saved.name] = saved;
        return saved;
    }

    static Material Lit(string name, Color color, float metallic, float smoothness, Color emission)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smoothness);
        if (emission.maxColorComponent > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        return Save(m);
    }

    static Material Wall(string name, Vector2 tiling, float brightness)
    {
        var src = AssetDatabase.LoadAssetAtPath<Material>(WallMatPath);
        Material m;
        if (src != null)
        {
            m = new Material(src);
        }
        else
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", new Color(0.3f, 0.32f, 0.36f));
        }
        m.name = name;
        m.SetTextureScale("_BaseMap", tiling);
        m.SetColor("_BaseColor", new Color(brightness, brightness, brightness * 1.05f, 1f));
        return Save(m);
    }

    static Material MakeUnlit(string name, Color color, bool additive)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
        MakeTransparent(m, color, additive);
        return Save(m);
    }

    static void MakeTransparent(Material m, Color color, bool additive)
    {
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", additive ? 2f : 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetColor("_BaseColor", color);
    }

    // ------------------------------------------------------------------ helpers: objects

    static GameObject Prim(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat, Transform parent, bool collider = true)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider)
        {
            var c = g.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
        }
        return g;
    }

    static Light MakeLight(string name, LightType type, Vector3 pos, Color color, float intensity, float range, Transform parent = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = type;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }

    static TextMeshPro Text(string name, string content, float size, Color color, Vector3 pos, Quaternion rot, Vector2 box, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, rot);
        var t = go.AddComponent<TextMeshPro>();
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.rectTransform.sizeDelta = box;
        return t;
    }

    static Bounds GetBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    /// <summary>Instantiates a pack prefab and stands it on the floor at (pos.x, pos.z), centred by its bounds.</summary>
    static GameObject Place(string rel, Vector3 pos, float yaw, Transform parent)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackRoot + rel);
        if (prefab == null)
        {
            Debug.LogWarning("[CrashedShip] Pack prefab not found: " + PackRoot + rel);
            return null;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, yaw, 0f));
        var b = GetBounds(go);
        go.transform.position = new Vector3(pos.x - b.center.x, pos.y - b.min.y, pos.z - b.center.z);
        return go;
    }

    // ------------------------------------------------------------------ helpers: particles

    static void MakeDust(string name, Vector3 pos, Vector3 area, float rate)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = 8f;
        main.startSpeed = 0.05f;
        main.startSize = 0.02f;
        main.maxParticles = 600;
        main.startColor = new Color(0.8f, 0.9f, 1f, 0.5f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = rate;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = area;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = ParticleMat("Particle_Dust", new Color(0.8f, 0.9f, 1f, 0.5f));
    }

    static void MakeSparks(string name, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // cone emits along +Z, so this points it down
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.duration = 2.6f;
        main.loop = true;
        main.startLifetime = 0.7f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSize = 0.025f;
        main.gravityModifier = 1.2f;
        main.startColor = new Color(1f, 0.7f, 0.25f, 1f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)18) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.03f;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = ParticleMat("Particle_Spark", new Color(1f, 0.75f, 0.3f, 1f));
    }

    static void MakeStars()
    {
        var go = new GameObject("Stars");
        go.transform.position = Vector3.zero;
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = 1000000f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
        main.maxParticles = 1500;
        main.startColor = new Color(0.85f, 0.9f, 1f, 1f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)1400) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 60f;
        shape.radiusThickness = 0f;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = ParticleMat("Particle_Star", new Color(0.9f, 0.95f, 1f, 1f));
    }

    static Material ParticleMat(string name, Color color)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = name };
        MakeTransparent(m, color, true);
        return Save(m);
    }
}
#endif
