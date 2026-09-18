using Thunderbirds.Rules;
using UnityEngine;

[ExecuteAlways,DisallowMultipleComponent]
public sealed class LevelBlock:MonoBehaviour
{
    [SerializeField] private BodyKind kind=BodyKind.LightBlock;
    [SerializeField] private Vector2Int size=new Vector2Int(2,2);
    public BodyKind Kind=>kind;
    public Vector2Int Size=>size;
    public void Configure(BodyKind value,Vector2Int dimensions){kind=value;size=dimensions;RefreshVisuals();}
    private void Update(){if(!Application.isPlaying)RefreshVisuals();}
    private void RefreshVisuals()
    {
        size.x=Mathf.Max(1,size.x);size.y=Mathf.Max(1,size.y);
        var visuals=transform.Find("Visuals");
        if(visuals!=null)visuals.localScale=new Vector3(size.x,size.y,1);
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color=kind==BodyKind.Wall?new Color(1,.75f,.3f):Color.cyan;
        Gizmos.DrawWireCube(transform.position,new Vector3(size.x,size.y,.1f));
    }
}
