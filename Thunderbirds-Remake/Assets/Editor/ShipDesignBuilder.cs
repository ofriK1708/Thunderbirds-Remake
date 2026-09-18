using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Authoring tool only. The resulting ships use ordinary Transforms,
// MeshFilters and MeshRenderers; no runtime generator or textures are needed.
public static class ShipDesignBuilder
{
    private const string GeometryFolder = "Assets/Art/ShipGeometry";
    private static Material material;
    private static GameObject model;
    private static string meshPath;
    private static float width;
    private static int layer;

    [MenuItem("Thunderbirds/Rebuild Ship Designs")]
    public static void BuildFromMenu()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Build();
    }

    public static void Build()
    {
        Directory.CreateDirectory(GeometryFolder);
        Directory.CreateDirectory("Assets/Prefabs");
        AssetDatabase.Refresh();
        var shader = Shader.Find("Thunderbirds/Ship Vertex Color");
        if (shader == null) throw new InvalidOperationException("Ship shader did not import.");
        string materialPath = GeometryFolder + "/ShipSurface.mat";
        material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.shader = shader;
        EditorUtility.SetDirty(material);

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        BuildKestrel();
        Install("Kestrel");
        BuildAtlas();
        Install("Atlas");
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        Verify();
        RenderPreview();
        Debug.Log("SHIP_DESIGNS_OK: both editable mesh prefabs installed and verified.");
    }

    private static void Begin(string name, float shipWidth)
    {
        width = shipWidth;
        layer = 2;
        model = new GameObject(name + " Visuals");
        meshPath = GeometryFolder + "/" + name + ".asset";
    }

    // Every polygon is convex and triangulated as a fan. Coordinates are
    // in gameplay units, normalized to the existing ship root's scale.
    private static void Part(string name, string hex, params float[] xy)
    {
        var go = new GameObject(name);
        go.transform.SetParent(model.transform, false);
        int count = xy.Length / 2;
        var vertices = new Vector3[count];
        var colors = new Color[count];
        ColorUtility.TryParseHtmlString("#" + hex, out var color);
        if (QualitySettings.activeColorSpace == ColorSpace.Linear) color = color.linear;
        for (int i = 0; i < count; i++)
        {
            vertices[i] = new Vector3(xy[i * 2] / width, xy[i * 2 + 1] / 2f, 0);
            colors[i] = color;
        }
        var triangles = new int[(count - 2) * 3];
        for (int i = 0; i < count - 2; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }
        // Preserve mesh object IDs when the authoring command is repeated.
        Mesh mesh = null;
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(meshPath))
            if (asset is Mesh candidate && candidate.name == name) mesh = candidate;
        if (mesh == null)
        {
            mesh = new Mesh { name = name };
            if (AssetDatabase.LoadMainAssetAtPath(meshPath) == null)
                AssetDatabase.CreateAsset(mesh, meshPath);
            else AssetDatabase.AddObjectToAsset(mesh, meshPath);
        }
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = layer++;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private static void Rect(string name, string color, float x, float y, float w, float h)
    {
        Part(name, color, x-w/2, y-h/2, x+w/2, y-h/2,
            x+w/2, y+h/2, x-w/2, y+h/2);
    }

    private static void BuildKestrel()
    {
        Begin("Kestrel", 2);
        Part("Dorsal fin outline", "162735", -.76f,.2f, -.72f,.87f, -.48f,.87f, .02f,.3f);
        Part("Dorsal fin blue", "368CAB", -.65f,.34f, -.62f,.76f, -.53f,.76f, -.16f,.34f);
        Part("Ventral wing outline", "162735", -.62f,-.22f, .38f,-.28f, -.29f,-.81f, -.69f,-.81f);
        Part("Ventral wing silver", "819BAD", -.53f,-.36f, .13f,-.36f, -.34f,-.69f, -.56f,-.69f);
        Rect("Rear engine housing", "162735", -.76f,-.02f,.34f,.76f);
        Rect("Engine rim", "537181", -.83f,-.02f,.15f,.57f);
        Rect("Ion exhaust cyan", "39D6EC", -.94f,-.02f,.09f,.41f);
        Rect("Ion exhaust core", "D5FCF5", -.955f,-.02f,.05f,.23f);
        Part("Hull outline", "162735", -.77f,-.36f, .44f,-.36f, .94f,-.06f, .94f,.08f, .4f,.45f, -.65f,.45f, -.84f,.18f);
        Part("Lower hull", "718EA0", -.69f,-.25f, .4f,-.25f, .81f,-.01f, .35f,.33f, -.6f,.33f, -.73f,.12f);
        Part("Upper hull silver", "E2EAE5", -.73f,.04f, .8f,.04f, .34f,.35f, -.6f,.35f);
        Part("Cockpit frame", "162735", -.02f,.08f, .63f,.08f, .28f,.37f, -.14f,.37f);
        Part("Cockpit glass", "43BFD9", .05f,.14f, .46f,.14f, .25f,.29f, -.02f,.29f);
        Part("Glass reflection", "BBF5F1", .03f,.26f, .29f,.26f, .25f,.29f, -.02f,.29f);
        Rect("Rescue blue stripe", "2388AC", -.19f,-.11f,.97f,.09f);
        Rect("Rescue badge vertical", "F4F5D9", -.43f,.17f,.065f,.2f);
        Rect("Rescue badge horizontal", "F4F5D9", -.43f,.17f,.19f,.065f);
        Rect("Intake dark", "233D4B", -.55f,-.34f,.4f,.14f);
        for (int i=0;i<3;i++) Rect("Intake slat " + i,"8DABB6",-.68f+i*.12f,-.34f,.04f,.1f);
        Rect("Landing strut", "233D4B", .27f,-.49f,.09f,.28f);
        Rect("Landing skid", "ACC0C4", .27f,-.66f,.49f,.09f);
        Rect("Nose beacon", "FADE80", .84f,.02f,.08f,.09f);
    }

    private static void BuildAtlas()
    {
        Begin("Atlas", 4);
        Part("Tail fin outline", "172D2C", -1.65f,.23f, -1.63f,.91f, -1.3f,.91f, -.69f,.27f);
        Part("Tail fin green", "679575", -1.51f,.37f, -1.5f,.78f, -1.36f,.78f, -.96f,.37f);
        Rect("Left landing leg", "243C3B", -1.1f,-.69f,.16f,.38f);
        Rect("Right landing leg", "243C3B", 1.12f,-.69f,.16f,.38f);
        Rect("Left landing foot", "A4B8A1", -1.1f,-.91f,.64f,.13f);
        Rect("Right landing foot", "A4B8A1", 1.12f,-.91f,.64f,.13f);
        Rect("Rear engine outline", "172D2C", -1.69f,-.03f,.46f,.89f);
        Rect("Rear engine metal", "526D67", -1.79f,-.03f,.18f,.69f);
        Rect("Rear exhaust", "64DFD6", -1.93f,-.03f,.1f,.51f);
        Rect("Rear exhaust core", "D4F9DE", -1.96f,-.03f,.045f,.28f);
        Part("Cargo hull outline", "172D2C", -1.66f,-.48f, -1.25f,-.69f, 1.26f,-.69f, 1.92f,-.16f, 1.92f,.11f, 1.35f,.54f, -1.44f,.54f, -1.7f,.26f);
        Part("Cargo hull green", "527F62", -1.54f,-.4f, -1.22f,-.55f, 1.22f,-.55f, 1.78f,-.11f, 1.78f,.05f, 1.3f,.41f, -1.37f,.41f, -1.56f,.2f);
        Part("Upper armor highlight", "8BB48A", -1.5f,.19f, 1.59f,.19f, 1.29f,.42f, -1.36f,.42f);
        Rect("Load deck", "DCE1B5", -.36f,.53f,1.95f,.1f);
        Part("Cockpit armor", "C0D0B7", .7f,.16f, 1.69f,.16f, 1.18f,.66f, .69f,.66f, .51f,.42f);
        Part("Cockpit dark seal", "172D2C", .79f,.23f, 1.46f,.23f, 1.13f,.55f, .76f,.55f, .65f,.4f);
        Part("Cockpit glass", "55C8C7", .85f,.3f, 1.27f,.3f, 1.09f,.46f, .81f,.46f, .75f,.39f);
        Part("Cockpit glint", "C6F4DE", .8f,.4f, 1.16f,.4f, 1.09f,.46f, .81f,.46f);
        Rect("Cargo bay seal", "213B35", -.35f,-.2f,1.79f,.59f);
        Rect("Cargo bay panel", "385D4A", -.35f,-.2f,1.6f,.42f);
        for(int i=0;i<5;i++) Rect("Cargo rib " + i,"749476",-1.01f+i*.33f,-.2f,.07f,.42f);
        Rect("Safety stripe", "E5C768", -.35f,.07f,1.79f,.1f);
        Rect("Belly armor", "263F38", -.1f,-.55f,2.15f,.12f);
        Rect("Rescue badge vertical", "EFF0C9", -1.36f,-.07f,.08f,.27f);
        Rect("Rescue badge horizontal", "EFF0C9", -1.36f,-.07f,.25f,.08f);
        Rect("Front intake", "233C37", 1.3f,-.23f,.4f,.19f);
        for(int i=0;i<3;i++) Rect("Front intake slat " + i,"8AAA8A",1.17f+i*.13f,-.23f,.04f,.12f);
        Rect("Nose beacon", "FFE4A0", 1.8f,-.035f,.09f,.11f);
    }

    private static void Install(string shipName)
    {
        var ship = GameObject.Find(shipName);
        if (ship == null) throw new InvalidOperationException("Missing ship: " + shipName);
        // Replace only the child owned by this tool, preserving gameplay roots.
        var previous = ship.transform.Find(shipName + " Visuals");
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
        var prefab = PrefabUtility.SaveAsPrefabAsset(model, "Assets/Prefabs/" + shipName + "Visuals.prefab");
        UnityEngine.Object.DestroyImmediate(model);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, ship.transform);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        var placeholder = ship.GetComponent<SpriteRenderer>();
        if (placeholder != null) placeholder.enabled = false;
    }

    private static void Verify()
    {
        foreach (string name in new[] { "Kestrel", "Atlas" })
        {
            var ship = GameObject.Find(name);
            var renderers = ship.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length < 20) throw new Exception("Missing geometry: " + name);
            foreach (var renderer in renderers)
            {
                if (renderer.sharedMaterial == null || renderer.sharedMaterial.shader == null)
                    throw new Exception("Missing material: " + renderer.name);
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                foreach (var vertex in mesh.vertices)
                    if (Mathf.Abs(vertex.x) > .5001f || Mathf.Abs(vertex.y) > .5001f)
                        throw new Exception("Geometry exceeds footprint: " + renderer.name);
            }
        }
        var controller = new SerializedObject(GameObject.Find("GameController").GetComponent<ShipController>());
        foreach (string field in new[] { "input", "kestrel", "atlas" })
            if (controller.FindProperty(field).objectReferenceValue == null)
                throw new Exception("Broken gameplay reference: " + field);
        if (ShaderUtil.ShaderHasError(material.shader)) throw new Exception("Ship shader has compilation errors.");
    }

    private static void RenderPreview()
    {
        var preview = EditorSceneManager.NewPreviewScene();
        RenderTexture target = null;
        Texture2D texture = null;
        var oldTarget = RenderTexture.active;
        try
        {
            foreach (string name in new[] { "Kestrel", "Atlas" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + "Visuals.prefab");
                var copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                copy.transform.position = new Vector3(name == "Kestrel" ? -3 : 1.3f, 0, 0);
                copy.transform.localScale = new Vector3(name == "Kestrel" ? 2 : 4, 2, 1);
            }
            var cameraObject = new GameObject("Preview Camera");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, preview);
            var camera = cameraObject.AddComponent<Camera>();
            camera.scene = preview;
            camera.transform.position = new Vector3(-.35f, 0, -10);
            camera.orthographic = true;
            camera.orthographicSize = 2.3f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f,.065f,.09f,1);
            target = new RenderTexture(1600, 800, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture = new Texture2D(1600,800,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,1600,800),0,0);
            texture.Apply();
            Directory.CreateDirectory("Logs/ShipDesign");
            File.WriteAllBytes("Logs/ShipDesign/ships-preview.png",texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = oldTarget;
            EditorSceneManager.ClosePreviewScene(preview);
            if (target != null) UnityEngine.Object.DestroyImmediate(target);
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
