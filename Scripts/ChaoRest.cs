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
    private float timeout, settle, facing, arrivalYaw;
    private bool landed;
    private readonly ChaoWander detour=new();
    public bool HandlesPose => hop>0;
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
        approach=Bed+direction*2.05f;
        destination=Bed+Slots[slot]+Vector3.Up*.43f;
        hop=settle=0; landed=false;
        walk.Reset();
        timeout=35;
        Requested=true;
    }

    public void Cancel()
    {
        Requested=false; hop=0;
        if (slot>=0 && Spaces.TryGetValue(slot,out var owner) && owner==this) Spaces.Remove(slot);
        slot=-1;
    }

    public bool Step(CharacterBody3D actor,PetState chao,float dt,out bool reached,Node3D? art=null)
    {
        reached=false;
        if (!Requested) return false;
        timeout-=dt;
        if (timeout<=0) { Cancel(); return false; }
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
            arrivalYaw=facing+Mathf.Lerp(-Mathf.Pi,Mathf.Pi,GD.Randf());
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
        settle+=dt;
        actor.Rotation=new(0,Mathf.LerpAngle(facing,arrivalYaw,Mathf.SmoothStep(0,1,Mathf.Clamp(settle/.9f,0,1))),0);
        if (art!=null)
        {
            if (settle<2.2f)
            {
                ImportedChao.MovePose(art,settle*8,settle<.9f,false,false,false,false,false);
                if (art.FindChild("HeadPivot",true,false) is Node3D head)
                    head.Rotation=new(.45f,settle<.9f ? 0 : Mathf.Sin((settle-.9f)/1.3f*Mathf.Tau)*.55f,0);
            }
            else ImportedChao.LieDown(art,(settle-2.2f)/1.3f,chao.Manners);
        }
        if (settle>=3.5f) { Requested=false; hop=0; reached=true; }
        return true;
    }
}
