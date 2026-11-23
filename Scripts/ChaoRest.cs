using Godot;
using System.Linq;

namespace Mori;

internal sealed class ChaoRest
{
    public static readonly Vector3 Bed = new(5,0,4);
    public const float MinimumSleep = 35;
    private static readonly Vector3[] Slots={
        new(-.39f,0,.33f),new(.39f,0,.33f),new(-.39f,0,-.36f),new(.39f,0,-.36f)
    };
    public System.Collections.Generic.Dictionary<int,ChaoRest> Spaces { get; set; }=new();
    private int slot=-1;
    private Vector3 approach;
    private Vector3 destination;
    private Vector3 hopStart;
    private float hop;
    private float timeout, facing;
    private readonly ChaoSettling settling=new();
    private bool landed;
    internal bool GroundRest { get; private set; }
    internal string LastFailureReason { get; private set; }="";
    internal int FreeSlots=>Enumerable.Range(0,Slots.Length).Count(i=>!Spaces.ContainsKey(i));
    private readonly ChaoWander detour=new();
    public bool HandlesPose => GroundRest || hop>0;
    private readonly ChaoMotion walk=new();
    public bool Requested { get; private set; }

    public void Request(Vector3 actor)
    {
        if (Requested || slot>=0) return;
        GroundRest=false;LastFailureReason="";
        float nearest=float.MaxValue;
        for (int i=0;i<Slots.Length;i++)
        {
            float distance=actor.DistanceSquaredTo(Bed+Slots[i]);
            if (Spaces.ContainsKey(i) || distance>=nearest) continue;
            slot=i; nearest=distance;
        }
        if (slot<0) return;
        Spaces[slot]=this;
        Vector3 direction=actor-Bed;
        direction.Y=0;
        direction=direction.LengthSquared()<.02f ? Vector3.Back : direction.Normalized();
        approach=Bed+direction*2.05f;
        destination=Bed+Slots[slot]+Vector3.Up*.43f;
        hop=0; landed=false; settling.Cancel();
        walk.Reset();
        timeout=35;
        Requested=true;
    }
    internal bool RequestGround(CharacterBody3D actor)
    {
        if(Requested || !GardenActivityAdapter.SafeRestFloor(actor))return false;
        GroundRest=true;LastFailureReason="";approach=destination=actor.Position;hop=0;landed=true;settling.Cancel();timeout=35;Requested=true;return true;
    }

    public void Cancel()
    {
        Requested=false; hop=0; settling.Cancel();
        if (slot>=0 && Spaces.TryGetValue(slot,out var owner) && owner==this) Spaces.Remove(slot);
        slot=-1;
    }

    public bool Step(CharacterBody3D actor,PetState chao,float dt,out bool reached,Node3D? art=null)
    {
        reached=false;
        if (!Requested) return false;
        timeout-=dt;
        if (timeout<=0) { LastFailureReason="Rest approach timed out";Cancel(); return false; }
        if(GroundRest)
        {
            if(settling.Step(actor,art,chao.Manners,dt)){Requested=false;reached=true;}
            return true;
        }
        Vector3 delta=approach-actor.Position; delta.Y=0;
        if (hop<=0 && delta.Length()>.19f)
        {
            bool water=walk.IsSwimmingAt(actor.Position);
            var route=walk.RouteOffUpperPlatform(actor.Position,approach)-actor.Position;
            route.Y=0;
            if (!water) route=detour.Steer(actor,route,dt);
            walk.Step(actor,route,chao,false,water,dt,true,out _);
            if (route.LengthSquared()>.01f)
                actor.Rotation=new(0,Mathf.LerpAngle(actor.Rotation.Y,Mathf.Atan2(route.X,route.Z),1-Mathf.Exp(-9*dt)),0);
            return true;
        }
        if (hop<=0)
        {
            hopStart=actor.Position;
            var forward=destination-hopStart;
            facing=Mathf.Atan2(forward.X,forward.Z);
            actor.Rotation=new(0,facing,0);

        }
        if (!landed)
        {
            hop=Mathf.Min(.48f,hop+dt);
            float t=hop/.48f;
            actor.Rotation=new(0,facing,0);
            actor.Position=hopStart.Lerp(destination,t)+Vector3.Up*(Mathf.Sin(t*Mathf.Pi)*.38f);
            if (art!=null) ImportedChao.MovePose(art,hop*10,false,false,false,false,true,false);
            if (t<1) return true;
            var hit=actor.GetWorld3D().DirectSpaceState.IntersectRay(
                PhysicsRayQueryParameters3D.Create(destination+Vector3.Up,destination-Vector3.Up*.3f,1));
            if (hit.Count>0) actor.Position=(Vector3)hit["position"];
            landed=true;
        }
        if (settling.Step(actor,art,chao.Manners,dt)) { Requested=false; hop=0; reached=true; }
        return true;
    }
}
