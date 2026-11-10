using Godot;

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
    private float timeout;
    private readonly ChaoMotion walk=new();
    public bool Requested { get; private set; }

    public void Request(Vector3 actor)
    {
        if (Requested || slot>=0) return;
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
        approach=Bed+direction*1.86f;
        destination=Bed+Slots[slot]+Vector3.Up*.43f;
        hop=0;
        walk.Reset();
        timeout=18;
        Requested=true;
    }

    public void Cancel()
    {
        Requested=false; hop=0;
        if (slot>=0 && Spaces.TryGetValue(slot,out var owner) && owner==this) Spaces.Remove(slot);
        slot=-1;
    }

    public bool Step(CharacterBody3D actor,PetState chao,float dt,out bool reached)
    {
        reached=false;
        if (!Requested) return false;
        timeout-=dt;
        if (timeout<=0) { Cancel(); return false; }
        Vector3 delta=approach-actor.Position; delta.Y=0;
        if (hop<=0 && delta.Length()>.26f)
        {
            bool water=walk.IsSwimmingAt(actor.Position);
            var route=walk.RouteOffUpperPlatform(actor.Position,approach)-actor.Position;
            route.Y=0;
            walk.Step(actor,route,chao,false,water,dt,true,out _);
            return true;
        }
        if (hop<=0) hopStart=actor.Position;
        hop=Mathf.Min(.48f,hop+dt);
        float t=hop/.48f;
        actor.Position=hopStart.Lerp(destination,t)+Vector3.Up*(Mathf.Sin(t*Mathf.Pi)*.38f);
        if (t>=1)
        {
            var query=PhysicsRayQueryParameters3D.Create(destination+Vector3.Up,destination-Vector3.Up*.3f,1);
            var hit=actor.GetWorld3D().DirectSpaceState.IntersectRay(query);
            if (hit.Count>0) actor.Position=(Vector3)hit["position"];
            Requested=false; hop=0; reached=true;
        }
        return true;
    }
}
