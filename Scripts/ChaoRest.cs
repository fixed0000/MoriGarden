using Godot;

namespace Mori;

internal sealed class ChaoRest
{
    public static readonly Vector3 Bed = new(5,0,4);
    public const float MinimumSleep = 35;
    private Vector3 approach;
    private Vector3 destination;
    private Vector3 hopStart;
    private float hop;
    private float timeout;
    public bool Requested { get; private set; }

    public void Request(Vector3 actor)
    {
        if (Requested) return;
        Vector3 direction=actor-Bed;
        direction.Y=0;
        direction=direction.LengthSquared()<.02f ? Vector3.Back : direction.Normalized();
        approach=Bed+direction*1.86f;
        destination=Bed+direction*.20f+Vector3.Up*.43f;
        hop=0;
        timeout=18;
        Requested=true;
    }

    public void Cancel() { Requested=false; hop=0; }

    public bool Step(CharacterBody3D actor,float dt,out bool reached)
    {
        reached=false;
        if (!Requested) return false;
        timeout-=dt;
        if (timeout<=0) { Cancel(); return false; }
        Vector3 delta=approach-actor.Position; delta.Y=0;
        if (hop<=0 && delta.Length()>.26f)
        {
            actor.Rotation=new(0,Mathf.Atan2(delta.X,delta.Z),0);
            var hit=actor.MoveAndCollide(delta.Normalized()*Mathf.Min(delta.Length(),dt*1.8f));
            if (hit!=null) actor.MoveAndCollide(hit.GetRemainder().Slide(hit.GetNormal()));
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
            Cancel(); reached=true;
        }
        return true;
    }
}
