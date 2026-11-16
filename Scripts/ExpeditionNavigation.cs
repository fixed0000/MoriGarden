using System.Collections.Generic;
using Godot;

namespace Mori;

// One dry-land graph shared by the team. Geometry is sampled, not duplicated as a second landscape.
internal sealed class ExpeditionNavigation
{
    private readonly AStar3D graph=new();
    private readonly Dictionary<Vector2I,long> cells=new();
    private const float Grid=1.1f;
    private readonly Node3D world;
    internal string Area { get; }
    internal Vector2[] Landmarks=>ExpeditionArt.Landmarks(Area);
    internal float Height(float x,float z)=>ExpeditionArt.Height(x,z,Area);
    private readonly PhysicsShapeQueryParameters3D probe=new() { Shape=new SphereShape3D { Radius=.25f },CollisionMask=1 };
    public ExpeditionNavigation(Node3D world)
    {
        this.world=world;Area=ExpeditionArt.Area(world);
        for(int x=-32;x<=32;x++) for(int z=-24;z<=24;z++)
        {
            var p=new Vector3(x*Grid,0,z*Grid); p.Y=Height(p.X,p.Z)+.02f;
            if(!Safe(p)) continue;
            long id=graph.GetAvailablePointId(); graph.AddPoint(id,p); cells[new(x,z)]=id;
        }
        foreach(var (cell,id) in cells)
            foreach(var step in new[]{new Vector2I(1,0),new Vector2I(0,1),new Vector2I(1,1),new Vector2I(1,-1)})
                if(cells.TryGetValue(cell+step,out long other))
                {
                    if(step.X!=0 && step.Y!=0 && (!cells.ContainsKey(cell+new Vector2I(step.X,0)) || !cells.ContainsKey(cell+new Vector2I(0,step.Y))))continue;
                    var a=graph.GetPointPosition(id);var b=graph.GetPointPosition(other);
                    if(Mathf.Abs(a.Y-b.Y)<.6f && Safe(a.Lerp(b,.25f)) && Safe(a.Lerp(b,.5f)) && Safe(a.Lerp(b,.75f)))graph.ConnectPoints(id,other);
                }
    }
    public bool Safe(Vector3 point)
    {
        if(!point.IsFinite() || !ExpeditionArt.Bounds(Area).HasPoint(new(point.X,point.Z)) || ExpeditionArt.Water(Area).Depth(new(point.X,point.Z))>.015f)return false;
        probe.Transform=new Transform3D(Basis.Identity,point+Vector3.Up*.34f);
        return world.GetWorld3D().DirectSpaceState.IntersectShape(probe,1).Count==0;
    }
    public Vector3 Nearest(Vector3 at)
    {
        long id=graph.GetClosestPoint(at); return id<0 ? new(0,.02f,23) : graph.GetPointPosition(id);
    }
    public Vector3[] Path(Vector3 from,Vector3 to)
    {
        long a=graph.GetClosestPoint(from), b=graph.GetClosestPoint(to);
        return a<0 || b<0 ? System.Array.Empty<Vector3>() : graph.GetPointPath(a,b);
    }
    public void Repair(Vector3 from,Vector3 waypoint)
    {
        long a=graph.GetClosestPoint(from),b=graph.GetClosestPoint(waypoint);
        if(a>=0 && b>=0 && a!=b && graph.ArePointsConnected(a,b))graph.DisconnectPoints(a,b);
    }
}
