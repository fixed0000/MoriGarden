using System;
using System.Linq;
using Godot;

namespace Mori;

// One pair only. The existing garden adapter owns category selection and leases.
internal sealed class ChaoCasualSocial
{
    internal readonly ChaoCasualSocialRuntime Runtime=new();
    private ChaoIntroductions.Candidate? a,b;
    private readonly ChaoRising riseA=new(),riseB=new();
    private bool risePrepared;
    private int sounds;
    private float gait;
    private string availability="Inactive";
    internal bool Active=>Runtime.Active;
    internal int AuthoredSignals=>sounds;
    internal bool Uses(CharacterBody3D actor)=>Active && (a?.Actor==actor || b?.Actor==actor);
    private static bool Valid(ChaoIntroductions.Candidate c)=>GodotObject.IsInstanceValid(c.Actor) && c.Actor.IsInsideTree() && !c.Actor.IsQueuedForDeletion() && GodotObject.IsInstanceValid(c.Art) && c.Art.IsInsideTree() && !c.Art.IsQueuedForDeletion();
    private static float Flat(Vector3 x,Vector3 y)=>new Vector2(x.X-y.X,x.Z-y.Z).Length();
    internal static bool Visible(CharacterBody3D x,CharacterBody3D y)
    {
        if(!GodotObject.IsInstanceValid(x) || !GodotObject.IsInstanceValid(y))return false;
        return x.GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(x.GlobalPosition+Vector3.Up*.24f,y.GlobalPosition+Vector3.Up*.24f,1)).Count==0;
    }
    private static bool Floor(CharacterBody3D actor,Vector3 at,float height)
    {
        if(GardenArt.IsPondWater(at))return false;
        var hit=actor.GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(at+Vector3.Up*.45f,at-Vector3.Up*.25f,1));
        return hit.Count>0 && ((Vector3)hit["normal"]).Y>.7f && Mathf.Abs(((Vector3)hit["position"]).Y-height)<.08f;
    }
    internal static bool SafeSegment(CharacterBody3D actor,Vector3 goal,CharacterBody3D? partner=null)
    {
        if(!GardenActivityAdapter.SafeRestFloor(actor))return false;
        var delta=goal-actor.Position;delta.Y=0;
        foreach(var body in actor.GetTree().GetNodesInGroup("GardenChao").OfType<CharacterBody3D>())
        {
            if(body==actor || body==partner || !body.IsInsideTree() || body.GetParent()!=actor.GetParent())continue;
            var offset=body.Position-actor.Position;offset.Y=0;
            float fraction=delta.LengthSquared()>.001f ? Mathf.Clamp(offset.Dot(delta)/delta.LengthSquared(),0,1) : 0;
            if(Flat(body.Position,actor.Position+delta*fraction)<.50f)return false;
        }
        for(int i=0;i<=8;i++)if(!Floor(actor,actor.Position.Lerp(goal,i/8f),actor.Position.Y))return false;
        return actor.GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(actor.Position+Vector3.Up*.22f,goal+Vector3.Up*.22f,1)).Count==0;
    }
    internal bool CanStart(PetState root,ChaoIntroductions.Candidate x,ChaoIntroductions.Candidate y,bool readyA,bool readyB)
    {
        Runtime.Clock(root.GardenSeconds);
        if(!Valid(x) || !Valid(y)){availability="Missing participant";return false;}
        availability=Runtime.Eligibility(root,x.Id,x.Pet,readyA,y.Id,y.Pet,readyB,Flat(x.Actor.Position,y.Actor.Position),x.Actor.Position.Y-y.Actor.Position.Y);
        if(availability!="Available")return false;
        if(!Visible(x.Actor,y.Actor) || !SafeSegment(x.Actor,y.Actor.Position,y.Actor)){availability="Blocked, wet or unsupported direct approach";return false;}
        return true;
    }
    internal bool TryStart(PetState root,ChaoIntroductions.Candidate x,ChaoIntroductions.Candidate y,bool readyA,bool readyB)
    {
        if(!CanStart(root,x,y,readyA,readyB) || !Runtime.Begin(root,x.Id,x.Pet,readyA,y.Id,y.Pet,readyB,Flat(x.Actor.Position,y.Actor.Position),x.Actor.Position.Y-y.Actor.Position.Y))return false;
        a=x;b=y;sounds=0;gait=0;risePrepared=false;return true;
    }
    internal void Advance(float dt,PetState root,Func<ChaoIntroductions.Candidate,bool> ready)
    {
        Runtime.Clock(root.GardenSeconds);if(!Active || a==null || b==null)return;
        if(!Valid(a) || !Valid(b)){Cancel(reason:"Participant disappeared");return;}
        float distance=Flat(a.Actor.Position,b.Actor.Position);
        if(!ready(a) || !ready(b) || !a.Pet.CanAct || !b.Pet.CanAct || a.Pet.Energy<25 || b.Pet.Energy<25 || a.Pet.Fullness<=25 || b.Pet.Fullness<=25 ||
            distance>3 || distance<.45f || Mathf.Abs(a.Actor.Position.Y-b.Actor.Position.Y)>.16f || !Visible(a.Actor,b.Actor) || !SafeSegment(a.Actor,b.Actor.Position,b.Actor))
        {Cancel(reason:"Participant or direct approach became unavailable");return;}
        availability="Available; pair reserved";
        var direction=b.Actor.Position-a.Actor.Position;direction.Y=0;direction=direction.Normalized();
        Face(a,direction,dt);Face(b,-direction,dt);
        ImportedChao.Attend(a.Art,b.Actor.Position+Vector3.Up*.27f,65);ImportedChao.Attend(b.Art,a.Actor.Position+Vector3.Up*.27f,65);
        bool moving=Runtime.Phase==CasualSocialPhase.Approach && distance>.85f;
        if(moving)
        {
            var motion=direction*Mathf.Min(.80f*dt,distance-.80f);
            var result=a.Actor.MoveAndCollide(motion);
            if(result!=null){Cancel(reason:"Approach collision");return;}
            gait+=dt*7;
        }
        if(Runtime.Phase==CasualSocialPhase.Face && !risePrepared)
        {
            risePrepared=true;
            if(ImportedChao.IsCrawling(a.Art) || a.Art.Basis.Y.Normalized().Dot(Vector3.Up)<.8f)riseA.Begin(a.Art);
            if(ImportedChao.IsCrawling(b.Art) || b.Art.Basis.Y.Normalized().Dot(Vector3.Up)<.8f)riseB.Begin(b.Art);
        }
        bool rising=riseA.Active || riseB.Active;
        if(rising)
        {
            riseA.Step(a.Art,dt);riseB.Step(b.Art,dt);
            if(!riseA.Active && !riseB.Active){ImportedChao.ClearActionPause(a.Art);ImportedChao.ClearActionPause(b.Art);}
        }
        else
        {
            ImportedChao.MovePose(a.Art,gait,moving,false,false,false,false,false);ImportedChao.MovePose(b.Art,0,false,false,false,false,false,false);
            if(Runtime.Phase==CasualSocialPhase.React)
            {a.Art.Rotation+=new Vector3(.025f*Mathf.Sin(Runtime.PhaseSeconds*Mathf.Pi),0,0);b.Art.Rotation+=new Vector3(.025f*Mathf.Sin(Runtime.PhaseSeconds*Mathf.Pi),0,0);}
        }
        if(Runtime.Phase==CasualSocialPhase.Exchange)
        {
            if(sounds==0){ChaoSounds.Play(a.Art,"voice");sounds++;}
            if(sounds==1 && Runtime.PhaseSeconds>=1){ChaoSounds.Play(b.Art,"voice");sounds++;}
        }
        Runtime.Advance(dt,Flat(a.Actor.Position,b.Actor.Position),!rising);
        if(!Active)Cleanup();
    }
    internal void Cancel(CharacterBody3D? actor=null,string reason="Player interaction or world transition")
    {if(!Active || actor!=null && !Uses(actor))return;Runtime.Cancel(reason);Cleanup();}
    private void Cleanup()
    {
        riseA.Cancel();riseB.Cancel();
        foreach(var c in new[]{a,b})if(c!=null && Valid(c))
        {ImportedChao.ClearActionPause(c.Art);ImportedChao.MovePose(c.Art,0,false,false,false,false,false,false);c.Art.GetNodeOrNull<EmotionOrb>("MoodOrb")?.SetIdea(null,false);}
        a=b=null;
        availability=Runtime.Phase==CasualSocialPhase.Finish ? "Inactive; completed" : "Inactive; "+Runtime.LastCancellationReason;
    }
    private static void Face(ChaoIntroductions.Candidate c,Vector3 toward,float dt)
    {float yaw=Mathf.Atan2(toward.X,toward.Z);c.Actor.Rotation=new(0,Mathf.LerpAngle(c.Actor.Rotation.Y,yaw,1-Mathf.Exp(-7*dt)),0);}
    internal void RebindPlayer(CharacterBody3D actor,PetState state,Node3D art)
    {
        if(!Uses(actor))return;
        var old=a!.Actor==actor ? a : b!;
        if(old.Art!=art){Cancel(reason:"Visual model replaced");return;}
        var current=old with{Pet=state,Art=art};if(a.Actor==actor)a=current;else b=current;Runtime.Rebind(old.Id,state);
    }
    internal CasualSocialDiagnostic Diagnostic(string id)=>Runtime.Diagnostic(id,a!=null && b!=null && Valid(a) && Valid(b) ? Flat(a.Actor.Position,b.Actor.Position) : 0,availability);
}
