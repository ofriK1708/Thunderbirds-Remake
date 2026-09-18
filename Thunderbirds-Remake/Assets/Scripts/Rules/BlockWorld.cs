using System;
using System.Collections.Generic;

namespace Thunderbirds.Rules
{
    public enum BodyKind { Ship, Wall, LightBlock }
    public struct CellBox
    {
        public int X,Y,Width,Height;
        public int Right=>X+Width;
        public int Top=>Y+Height;
        public CellBox(int x,int y,int width,int height){X=x;Y=y;Width=width;Height=height;}
        public CellBox Offset(int x,int y)=>new CellBox(X+x,Y+y,Width,Height);
        public bool Overlaps(CellBox b)=>X<b.Right && Right>b.X && Y<b.Top && Top>b.Y;
        public bool RestsOn(CellBox b)=>Y==b.Top && X<b.Right && Right>b.X;
    }
    public sealed class GridBody
    {
        public int Id{get;}
        public BodyKind Kind{get;}
        public CellBox Box{get;internal set;}
        public int Capacity{get;}
        public int Weight=>Box.Width*Box.Height;
        public GridBody(int id,BodyKind kind,CellBox box,int capacity=0){Id=id;Kind=kind;Box=box;Capacity=capacity;}
    }
    // Pure rules: cell occupancy is independent of rendering and Unity physics.
    public sealed class BlockWorld
    {
        private readonly List<GridBody> bodies=new List<GridBody>();
        private readonly CellBox bounds;
        public IReadOnlyList<GridBody> Bodies=>bodies;
        public string LastRefusal{get;private set;}
        public BlockWorld(CellBox bounds){this.bounds=bounds;}
        public void Add(GridBody body)
        {
            if(body.Box.Width<=0 || body.Box.Height<=0 || !Inside(body.Box))
                throw new ArgumentException("Body has an invalid size or lies outside the board.");
            foreach(var other in bodies)
                if(other.Id==body.Id || body.Box.Overlaps(other.Box))
                    throw new ArgumentException("Duplicate ID or overlapping starting bodies.");
            bodies.Add(body);
        }
        private bool Inside(CellBox b)=>b.X>=bounds.X && b.Y>=bounds.Y && b.Right<=bounds.Right && b.Top<=bounds.Top;
        private List<GridBody> BlocksBottomFirst()
        {
            var result=bodies.FindAll(b=>b.Kind==BodyKind.LightBlock);
            result.Sort((a,b)=>a.Box.Y!=b.Box.Y?a.Box.Y.CompareTo(b.Box.Y):a.Id.CompareTo(b.Id));
            return result;
        }
        public Dictionary<int,int> Carriers()
        {
            var result=new Dictionary<int,int>();
            foreach(var block in BlocksBottomFirst())
            {
                if(block.Box.Y==bounds.Y) continue;
                int? carrier=null;bool supported=false,sharedOrStatic=false;
                foreach(var support in bodies)
                {
                    if(support==block || !block.Box.RestsOn(support.Box))continue;
                    supported=true;int id;
                    if(support.Kind==BodyKind.Ship)id=support.Id;
                    else if(support.Kind==BodyKind.LightBlock && result.TryGetValue(support.Id,out int owner))id=owner;
                    else{sharedOrStatic=true;break;}
                    if(carrier.HasValue && carrier.Value!=id){sharedOrStatic=true;break;}
                    carrier=id;
                }
                if(supported && !sharedOrStatic && carrier.HasValue)result.Add(block.Id,carrier.Value);
            }
            return result;
        }
        public void StepGravity()
        {
            // Bottom first makes each unsupported stack fall one cell per step.
            foreach(var body in BlocksBottomFirst())
            {
                var target=body.Box.Offset(0,-1);if(!Inside(target))continue;
                bool blocked=false;
                foreach(var other in bodies)
                    if(other!=body && target.Overlaps(other.Box)){blocked=true;break;}
                if(!blocked)body.Box=target;
            }
        }
        public bool TryMove(int shipId,int dx,int dy)
        {
            if(Math.Abs(dx)+Math.Abs(dy)!=1)throw new ArgumentException("Movement must be one cardinal cell.");
            var ship=bodies.Find(b=>b.Id==shipId && b.Kind==BodyKind.Ship);
            if(ship==null)throw new ArgumentException("Ship not found.");
            LastRefusal=null;
            var carriers=Carriers();var cargo=new HashSet<GridBody>();
            foreach(var body in bodies)
                if(carriers.TryGetValue(body.Id,out int owner) && owner==shipId)cargo.Add(body);
            // An obstruction can strip off cargo sideways/down; upward lifts refuse.
            if(dy<=0)
            {
                bool release=false;
                foreach(var body in cargo)
                {
                    var target=body.Box.Offset(dx,dy);
                    if(!Inside(target)){release=true;break;}
                    foreach(var obstacle in bodies)
                        if(obstacle!=ship && !cargo.Contains(obstacle) && target.Overlaps(obstacle.Box)){release=true;break;}
                    if(release)break;
                }
                if(release)cargo.Clear();
            }
            var moving=new HashSet<GridBody>(cargo){ship};bool changed;
            do
            {
                changed=false;var pass=new List<GridBody>(moving);
                foreach(var body in pass)
                {
                    var target=body.Box.Offset(dx,dy);
                    if(!Inside(target))return Refuse("Board boundary");
                    foreach(var other in bodies)
                    {
                        if(moving.Contains(other))continue;
                        bool hit=target.Overlaps(other.Box);
                        bool riding=body.Kind==BodyKind.LightBlock && other.Box.RestsOn(body.Box);
                        if(!hit && !riding)continue;
                        if(other.Kind!=BodyKind.LightBlock)return Refuse("Fixed obstacle");
                        if(hit && dy<0)return Refuse("Cannot push a block downward");
                        moving.Add(other);changed=true;
                    }
                }
            }while(changed);
            int load=0;
            foreach(var body in moving)if(body.Kind==BodyKind.LightBlock)load+=body.Weight;
            if(dy>=0 && load>ship.Capacity)return Refuse("Load exceeds ship capacity");
            // Atomic commit: a refused push/lift never moves part of a chain.
            foreach(var body in moving)body.Box=body.Box.Offset(dx,dy);
            return true;
        }
        private bool Refuse(string reason){LastRefusal=reason;return false;}
    }
}
