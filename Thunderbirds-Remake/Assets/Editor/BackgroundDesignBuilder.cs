using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Bakes editable, texture-free scenery. All geometry is decorative and
// has no collider, so it cannot change the grid-based movement rules.
public static class BackgroundDesignBuilder
{
    private const string Folder = "Assets/Art/Environment";
    private const string MeshPath = Folder + "/TombBackground.asset";
    private static GameObject root;
    private static Transform group;
    private static Material material;

    [MenuItem("Thunderbirds/Rebuild Background")]
    public static void BuildFromMenu()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Build();
    }

    public static void Build()
    {
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/ShipGeometry/ShipSurface.mat");
        if (material == null) throw new Exception("ShipSurface material is required.");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        var old = GameObject.Find("Tomb Background");
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        root = new GameObject("Tomb Background");

        Group("01 - Distant chamber");
        Poly("Chamber gradient", "182A30", -100, "080F17", -24,-14,24,-14,24,14,-24,14);
        Rect("Distant floor haze", "1A2B2D", -99,0,-7.1f,32,1.8f);
        Arch("Outer sunken arch",0,-8,10.8f,7.9f,"233237",-98);
        Arch("Outer arch shadow",0,-8,10.35f,7.45f,"101B22",-97);
        Arch("Second arch rim",0,-8,8.45f,5.95f,"1A2A2E",-96);
        Arch("Second arch interior",0,-8,8.08f,5.6f,"101C23",-95);
        Arch("Distant door surround",0,-8,4.25f,2.45f,"1C3032",-94);
        Arch("Distant door",0,-8,3.96f,2.15f,"101B23",-93);
        Rect("Distant door seam","293B3A",-92,0,-4,.035f,7);
        for(int i=0;i<4;i++)
        {
            Rect("Door inset left "+i,"18282D",-92,-2.25f,-2.4f-i*1.32f,2.7f,.035f);
            Rect("Door inset right "+i,"18282D",-92,2.25f,-2.4f-i*1.32f,2.7f,.035f);
        }

        Group("02 - Recessed masonry");
        for(int side=-1;side<=1;side+=2)
        {
            for(int col=0;col<2;col++)
            {
                float x=side*(11.75f+col*2.55f);
                for(int row=0;row<8;row++)
                {
                    float y=-6.9f+row*1.86f;
                    Rect("Masonry "+side+" "+col+" "+row,row%2==0?"263535":"223031",-85,x,y,2.43f,1.72f);
                    Rect("Masonry bevel "+side+" "+col+" "+row,"35413B",-84,x,y+.8f,2.43f,.045f);
                }
            }
            Rect("Inner pillar shadow "+side,"0D191F",-83,side*9.7f,-1.35f,.8f,13.3f);
            Rect("Inner pillar face "+side,"334038",-82,side*9.6f,-1.35f,.32f,13.3f);
            Rect("Inner pillar trim "+side,"526052",-81,side*9.46f,-1.35f,.045f,13.3f);
            for(int i=0;i<6;i++) Rect("Pillar seam "+side+" "+i,"142329",-80,side*9.6f,-6.7f+i*2.2f,.34f,.075f);
        }
        // Individually editable voussoirs trace the upper chamber arch.
        for(int i=0;i<11;i++)
        {
            float a=Mathf.PI*(.08f+i*.084f), b=a+.058f*Mathf.PI;
            float cx=0, cy=1.8f, rx=10.45f, ry=5.9f;
            Poly("Arch stone "+i,i%2==0?"39453C":"303D37",-79,null,
                cx+Mathf.Cos(a)*rx,cy+Mathf.Sin(a)*ry,
                cx+Mathf.Cos(b)*rx,cy+Mathf.Sin(b)*ry,
                cx+Mathf.Cos(b)*(rx-.28f),cy+Mathf.Sin(b)*(ry-.28f),
                cx+Mathf.Cos(a)*(rx-.28f),cy+Mathf.Sin(a)*(ry-.28f));
        }

        Group("03 - Ancient crest");
        Poly("Crest diamond","3D493E",-70,null,-.66f,6.38f,0,5.67f,.66f,6.38f,0,7.1f);
        Poly("Crest inset","14252A",-69,null,-.44f,6.38f,0,5.91f,.44f,6.38f,0,6.87f);
        Rect("Crest vertical","82906A",-68,0,6.38f,.055f,.64f);
        Rect("Crest horizontal","82906A",-68,0,6.38f,.48f,.055f);
        for(int side=-1;side<=1;side+=2)
        {
            Rect("Crest wing "+side,"4C5745",-69,side*1.6f,6.4f,1.65f,.055f);
            Rect("Crest wing lower "+side,"303F37",-69,side*1.3f,6.18f,1.05f,.035f);
        }

        Group("04 - Amber wall lanterns");
        for(int side=-1;side<=1;side+=2)
        {
            float x=side*12.5f;
            Poly("Light wash "+side,"AC874300",-65,"AC874320",x-.18f,3.8f,x+.18f,3.8f,x+1.5f,-3,x-1.5f,-3);
            Rect("Lantern backing "+side,"0B171C",-64,x,4.23f,.64f,1.62f);
            Rect("Lantern armor "+side,"65715A",-63,x,4.23f,.46f,1.44f);
            Rect("Lantern recess "+side,"302F25",-62,x,4.23f,.24f,1.17f);
            Rect("Lantern glow "+side,"E3B66A",-61,x,4.23f,.12f,1.02f);
            Rect("Lantern core "+side,"FFE8AE",-60,x,4.23f,.045f,.93f);
            Rect("Lantern cap "+side,"A09164",-59,x,4.98f,.66f,.12f);
            Rect("Lantern base "+side,"424B3D",-59,x,3.48f,.66f,.12f);
        }

        Group("05 - Sandstone frame");
        Rect("Left border foundation","342F28",-50,-17,0,2,20);
        Rect("Right border foundation","342F28",-50,17,0,2,20);
        for(int side=-1;side<=1;side+=2)
        {
            for(int i=0;i<10;i++)
            {
                float y=-9.1f+i*2;
                Rect("Frame block "+side+" "+i,i%2==0?"6B6147":"625A43",-49,side*17,y,1.9f,1.91f);
                Rect("Frame top edge "+side+" "+i,"97845A",-48,side*17,y+.91f,1.87f,.055f);
                Rect("Frame inner bevel "+side+" "+i,"87774F",-48,side*16.08f,y,.055f,1.85f);
            }
        }
        Rect("Ceiling stone","554F3C",-47,0,9.6f,32,1.2f);
        Rect("Ceiling lower bevel","958058",-46,0,9.04f,32,.08f);
        for(int i=0;i<16;i++) Rect("Ceiling joint "+i,"282C27",-45,-15+i*2,9.6f,.07f,1.05f);
        Rect("Below floor shadow","111B1D",-44,0,-9.5f,32,1);

        Group("06 - Floor surface detail");
        // This detail remains strictly inside the existing floor rectangle.
        Rect("Floor top lip","B39C6A",1,0,-8.035f,32,.065f);
        Rect("Floor lower bevel","3B3A2E",1,0,-8.94f,32,.12f);
        for(int i=0;i<16;i++)
        {
            float x=-16+i*2;
            Rect("Floor joint "+i,"393B30",1,x+.025f,-8.49f,.045f,.86f);
            Rect("Floor inset "+i,"776C4B",1,x+.97f,-8.48f,1.65f,.44f);
            Rect("Floor inset highlight "+i,"8B7C55",1,x+.97f,-8.28f,1.65f,.025f);
        }

        var prefab=PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/TombBackground.prefab");
        UnityEngine.Object.DestroyImmediate(root);
        root=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var floor=GameObject.Find("Floor").GetComponent<SpriteRenderer>();
        ColorUtility.TryParseHtmlString("#625C43",out var floorColor);
        floor.color=floorColor;
        var camera=GameObject.Find("Main Camera").GetComponent<Camera>();
        camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.03f,.05f,.07f,1);
        Verify();
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        Render(camera);
        Debug.Log("BACKGROUND_OK: geometry, sorting, gameplay references and preview verified.");
    }

    private static void Group(string name)
    {
        group=new GameObject(name).transform;
        group.SetParent(root.transform,false);
    }
    private static Color ColorOf(string hex)
    {
        ColorUtility.TryParseHtmlString("#"+hex,out var c);
        return QualitySettings.activeColorSpace==ColorSpace.Linear?c.linear:c;
    }
    private static void Rect(string name,string hex,int order,float x,float y,float w,float h)
    {
        Poly(name,hex,order,null,x-w/2,y-h/2,x+w/2,y-h/2,x+w/2,y+h/2,x-w/2,y+h/2);
    }
    private static void Arch(string name,float x,float bottom,float halfWidth,float top,string hex,int order)
    {
        Poly(name,hex,order,null,x-halfWidth,bottom,x+halfWidth,bottom,
            x+halfWidth,top-4.4f,x+halfWidth*.82f,top-1.7f,x+halfWidth*.45f,top-.35f,
            x,top,x-halfWidth*.45f,top-.35f,x-halfWidth*.82f,top-1.7f,x-halfWidth,top-4.4f);
    }
    private static void Poly(string name,string hex,int order,string topHex,params float[] xy)
    {
        int n=xy.Length/2;
        var v=new Vector3[n];var c=new Color[n];var t=new int[(n-2)*3];
        float lo=float.MaxValue,hi=float.MinValue;
        for(int i=0;i<n;i++){lo=Mathf.Min(lo,xy[i*2+1]);hi=Mathf.Max(hi,xy[i*2+1]);}
        var bottom=ColorOf(hex);var top=topHex==null?bottom:ColorOf(topHex);
        // Center the mesh pivot so individual scenery pieces are easy to move/scale.
        Vector3 center=Vector3.zero;
        for(int i=0;i<n;i++) center+=new Vector3(xy[i*2],xy[i*2+1],0)/n;
        for(int i=0;i<n;i++)
        {
            v[i]=new Vector3(xy[i*2],xy[i*2+1],0)-center;
            c[i]=Color.Lerp(bottom,top,Mathf.InverseLerp(lo,hi,xy[i*2+1]));
        }
        for(int i=0;i<n-2;i++){t[i*3]=0;t[i*3+1]=i+1;t[i*3+2]=i+2;}
        Mesh mesh=null;
        foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(MeshPath))
            if(asset is Mesh candidate && candidate.name==name) mesh=candidate;
        if(mesh==null)
        {
            mesh=new Mesh{name=name};
            if(AssetDatabase.LoadMainAssetAtPath(MeshPath)==null)AssetDatabase.CreateAsset(mesh,MeshPath);
            else AssetDatabase.AddObjectToAsset(mesh,MeshPath);
        }
        mesh.Clear();mesh.vertices=v;mesh.colors=c;mesh.triangles=t;mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
        var go=new GameObject(name);go.transform.SetParent(group,false);go.transform.localPosition=center;
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.sortingOrder=order;
        r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
        r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
    }
    private static void Verify()
    {
        if(root.GetComponentsInChildren<Collider>().Length!=0 || root.GetComponentsInChildren<Collider2D>().Length!=0)
            throw new Exception("Background must not affect collisions.");
        foreach(var r in root.GetComponentsInChildren<MeshRenderer>())
            if(r.sortingOrder>=2 || r.GetComponent<MeshFilter>().sharedMesh==null)
                throw new Exception("Invalid background geometry or sorting: "+r.name);
        var controller=new SerializedObject(GameObject.Find("GameController").GetComponent<ShipController>());
        foreach(string field in new[]{"input","kestrel","atlas"})
            if(controller.FindProperty(field).objectReferenceValue==null)throw new Exception("Missing reference: "+field);
        if(ShaderUtil.ShaderHasError(material.shader))throw new Exception("Background shader error.");
    }
    private static void Render(Camera camera)
    {
        var target=new RenderTexture(1920,1080,24);
        var texture=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        var old=RenderTexture.active;var oldCameraTarget=camera.targetTexture;
        try
        {
            camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            texture.ReadPixels(new Rect(0,0,1920,1080),0,0);texture.Apply();
            Directory.CreateDirectory("Logs/BackgroundDesign");
            File.WriteAllBytes("Logs/BackgroundDesign/background-preview.png",texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=oldCameraTarget;RenderTexture.active=old;
            UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
