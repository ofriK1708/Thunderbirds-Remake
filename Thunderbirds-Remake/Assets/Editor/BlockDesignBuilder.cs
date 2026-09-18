using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Thunderbirds.Rules;

public static class BlockDesignBuilder
{
    private const string Folder="Assets/Art/Blocks";
    private static Material material;
    private static Transform visuals;
    private static string meshPath;
    private static int order;

    [MenuItem("Thunderbirds/Create Block Demo")]
    public static void BuildFromMenu()
    {
        if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())Build();
    }
    public static void Build()
    {
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/ShipGeometry/ShipSurface.mat");
        if(material==null)throw new Exception("Missing geometry material.");
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        var wall=MakePrefab(false);var light=MakePrefab(true);
        var old=GameObject.Find("Gameplay Blocks");
        if(old!=null)UnityEngine.Object.DestroyImmediate(old);
        var root=new GameObject("Gameplay Blocks");
        Place(wall,root.transform,"Floating Wall - Left Shelf",new Vector2(-9,-3.5f),new Vector2Int(6,1));
        Place(wall,root.transform,"Floating Wall - Upper Shelf",new Vector2(5.5f,1.5f),new Vector2Int(7,1));
        Place(light,root.transform,"Light Block - Push and Catch",new Vector2(-9,-2),new Vector2Int(2,2));
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
        // Exercise the same registration path used when entering Play Mode.
        var controller=GameObject.Find("GameController").GetComponent<ShipController>();
        controller.SendMessage("Start",SendMessageOptions.RequireReceiver);
        if(!controller.enabled)throw new Exception("Demo registration failed.");
        foreach(var block in UnityEngine.Object.FindObjectsByType<LevelBlock>(FindObjectsSortMode.None))
            if(block.GetComponentsInChildren<MeshRenderer>().Length<5)throw new Exception("Incomplete block visuals.");
        Render(GameObject.Find("Main Camera").GetComponent<Camera>());
        Debug.Log("BLOCK_DEMO_OK: prefabs, visuals and actual runtime registration verified.");
    }
    private static void Place(GameObject prefab,Transform parent,string name,Vector2 position,Vector2Int size)
    {
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);
        go.name=name;go.transform.position=position;
        go.GetComponent<LevelBlock>().Configure(go.GetComponent<LevelBlock>().Kind,size);
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.GetComponent<LevelBlock>());
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform.Find("Visuals"));
    }
    private static GameObject MakePrefab(bool light)
    {
        string name=light?"LightBlock":"FixedWall";
        var root=new GameObject(name);visuals=new GameObject("Visuals").transform;visuals.SetParent(root.transform,false);
        meshPath=Folder+"/"+name+".asset";order=2;
        Rect("Outline",light?"073C43":"332D23",0,0,1,1);
        Rect("Body",light?"087F87":"A88750",0,0,.94f,.9f);
        Rect("Upper bevel",light?"76D6CB":"E2C083",0,.455f,.94f,.05f);
        Rect("Lower bevel",light?"075762":"695034",0,-.455f,.94f,.05f);
        Rect("Left bevel",light?"31ADA9":"C5A167",-.455f,0,.035f,.87f);
        Rect("Right bevel",light?"09616A":"795F3B",.455f,0,.035f,.87f);
        if(light)
        {
            Rect("Panel shadow","075F68",0,0,.76f,.68f);
            Rect("Panel face","178E94",0,.02f,.68f,.57f);
            Rect("Cell seam vertical","53B9B2",0,0,.012f,.86f);
            Rect("Cell seam horizontal","53B9B2",0,0,.86f,.012f);
            Rect("Lift badge","063D48",0,.02f,.27f,.26f);
            Part("Lift chevron left","B4EFE1",-.08f,.01f,0,.10f,0,.025f,-.045f,-.025f);
            Part("Lift chevron right","B4EFE1",0,.10f,.08f,.01f,.045f,-.025f,0,.025f);
        }
        else
        {
            Rect("Recessed stone panel","887047",0,0,.75f,.58f);
            Rect("Panel upper edge","C4A56B",0,.28f,.75f,.035f);
            Rect("Central joint","615039",0,0,.018f,.6f);
            Rect("Anchor plate","473D2D",0,0,.13f,.32f);
            Rect("Anchor pin","DDC18B",0,0,.035f,.17f);
        }
        for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)
        {
            Rect("Bolt seat "+x+" "+y,light?"06515A":"5A4832",x*.375f,y*.35f,.075f,.075f);
            Rect("Bolt "+x+" "+y,light?"B3E5D7":"DBC18F",x*.375f,y*.35f,.027f,.027f);
        }
        root.AddComponent<LevelBlock>().Configure(light?BodyKind.LightBlock:BodyKind.Wall,new Vector2Int(2,light?2:1));
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/"+name+".prefab");
        UnityEngine.Object.DestroyImmediate(root);return prefab;
    }
    private static void Rect(string name,string hex,float x,float y,float w,float h)
    {Part(name,hex,x-w/2,y-h/2,x+w/2,y-h/2,x+w/2,y+h/2,x-w/2,y+h/2);}
    private static void Part(string name,string hex,params float[] xy)
    {
        int n=xy.Length/2;var vertices=new Vector3[n];var colors=new Color[n];var triangles=new int[(n-2)*3];
        ColorUtility.TryParseHtmlString("#"+hex,out var c);if(QualitySettings.activeColorSpace==ColorSpace.Linear)c=c.linear;
        Vector3 center=Vector3.zero;
        for(int i=0;i<n;i++)center+=new Vector3(xy[i*2],xy[i*2+1],0)/n;
        for(int i=0;i<n;i++){vertices[i]=new Vector3(xy[i*2],xy[i*2+1],0)-center;colors[i]=c;}
        for(int i=0;i<n-2;i++){triangles[i*3]=0;triangles[i*3+1]=i+1;triangles[i*3+2]=i+2;}
        Mesh mesh=null;
        foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(meshPath))if(asset is Mesh m && m.name==name)mesh=m;
        if(mesh==null){mesh=new Mesh{name=name};if(AssetDatabase.LoadMainAssetAtPath(meshPath)==null)AssetDatabase.CreateAsset(mesh,meshPath);else AssetDatabase.AddObjectToAsset(mesh,meshPath);}
        mesh.Clear();mesh.vertices=vertices;mesh.colors=colors;mesh.triangles=triangles;mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
        var go=new GameObject(name);go.transform.SetParent(visuals,false);go.transform.localPosition=center;
        go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial=material;renderer.sortingOrder=order++;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
    }
    private static void Render(Camera camera)
    {
        var target=new RenderTexture(1920,1080,24);var texture=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        var old=RenderTexture.active;var cameraTarget=camera.targetTexture;
        try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1920,1080),0,0);texture.Apply();Directory.CreateDirectory("Logs/BlockDesign");File.WriteAllBytes("Logs/BlockDesign/blocks-preview.png",texture.EncodeToPNG());}
        finally{camera.targetTexture=cameraTarget;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(texture);}
    }
}
