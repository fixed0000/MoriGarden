using System;
using Godot;

namespace Mori;

internal enum PuzzleElementKind { Switch, Rotary, Plate, Reset }
internal partial class PuzzleElement : Node3D,IDiscoveryInteraction
{
    internal string Id { get; init; }="";
    internal string Title { get; init; }="";
    internal PuzzleElementKind Kind { get; init; }
    internal Func<PuzzleElementState> State { get; set; }=null!;
    internal Func<bool> Locked { get; set; }=()=>false;
    internal Action<PuzzleElement,ExpeditionCompanion?> Changed { get; set; }=(_,_)=>{};
    internal DiscoveryInterest Interest { get; private set; }=null!;
    internal bool Pressed { get; set; }
    private Node3D handle=null!;
    private MeshInstance3D indicator=null!;
    internal Vector3 Contact=>Kind==PuzzleElementKind.Plate ? GlobalPosition+Vector3.Up*.08f : handle.ToGlobal(new(0,-.06f,.35f));
    internal void Build()
    {
        Interest=new DiscoveryInterest { Definition=new("greenhouse/"+Id,DiscoveryKind.Unknown,new(Position.X,0,Position.Z),10,1.7f,
            DiscoveryReaction.Inspect|DiscoveryReaction.Touch|DiscoveryReaction.Share,LookHeight:Kind==PuzzleElementKind.Plate ? .12f : .42f,AreaId:WorldCatalog.Greenhouse),
            Interaction=Kind==PuzzleElementKind.Reset ? null : this,VisibilityRoot=this };
        AddChild(Interest);
        if(Kind==PuzzleElementKind.Plate)
        {
            GreenhouseArt.Box(this,new(0,.015f,0),new(2.1f,.035f,2.1f),new("646d56"));
            handle=GreenhouseArt.Box(this,new(0,.045f,0),new(1.8f,.025f,1.8f),new("ac9a6a"));
            for(int i=-1;i<=1;i++)GreenhouseArt.Box(this,new(i*.42f,.065f,0),new(.04f,.008f,1.5f),new("dbcea1"));
        }
        else
        {
            GreenhouseArt.Box(this,new(0,.22f,0),new(.7f,.44f,.65f),new("616e59"),true);
            handle=new Node3D { Position=new(0,.36f,.35f) };AddChild(handle);
            if(Kind==PuzzleElementKind.Rotary)
            {
                var wheel=GardenArt.Shape(handle,new TorusMesh { InnerRadius=.21f,OuterRadius=.27f,Rings=16,RingSegments=8 },Vector3.Zero,Vector3.One,new("a58a56"));wheel.Rotation=new(Mathf.Pi/2,0,0);
                for(int i=0;i<3;i++)GreenhouseArt.Beam(handle,new(0,0,0),new(Mathf.Sin(i*Mathf.Tau/3)*.24f,Mathf.Cos(i*Mathf.Tau/3)*.24f,0),.025f,new("a58a56"));
            }
            GreenhouseArt.Beam(handle,Vector3.Zero,new(0,-.06f,.35f),.04f,new("b49559"));
            foreach(int side in new[]{-1,1})GardenArt.Ball(handle,new(side*.09f,-.06f,.35f),Vector3.One*.09f,new("d0b777"));
        }
        indicator=GardenArt.Ball(this,new(.29f,.45f,.26f),Vector3.One*.10f,new("7e5745"));
        GreenhouseArt.Plaque(this,new(0,1.12f,-.42f),Title,Kind==PuzzleElementKind.Reset ? 3.8f : 3.1f);
    }
    internal bool Accessible(CharacterBody3D actor,bool player=true)
    {
        if(Locked() || !IsInsideTree() || ExpeditionCompanion.Flat(actor.GlobalPosition,GlobalPosition)>(player ? 2.1f : 2.5f))return false;
        var delta=GlobalPosition-actor.GlobalPosition;delta.Y=0;
        if(player && delta.Length()>.4f && actor.GlobalBasis.Z.Dot(delta.Normalized())<.1f)return false;
        var ray=PhysicsRayQueryParameters3D.Create(actor.GlobalPosition+Vector3.Up*(player ? 1 : .3f),Contact,1);
        var hit=GetWorld3D().DirectSpaceState.IntersectRay(ray);
        return hit.Count==0 || hit["collider"].AsGodotObject() is Node node && IsAncestorOf(node);
    }
    internal bool Activate(ExpeditionCompanion? chao=null,bool requested=false)
    {
        if(Locked())return false;
        var state=State();
        if(chao!=null && Kind==PuzzleElementKind.Rotary && state.Value==2)return false;
        if(Kind==PuzzleElementKind.Switch)
        {
            if(chao!=null && state.Value==1)return false;
            state.Value=1-state.Value;
        }
        else if(Kind==PuzzleElementKind.Rotary)
            state.Value=chao!=null && (requested || chao.Random.NextDouble()<chao.Pet.State.IntelligenceAbility/990f) ? 2 : (state.Value+1)%3;
        else if(Kind==PuzzleElementKind.Plate)return Pressed;
        Changed(this,chao);ChaoSounds.Play(this,"mood");return true;
    }
    public DiscoveryPlan Prepare(DiscoveryPlan plan,ExpeditionCompanion pet)=>plan with { ThoughtSeconds=PuzzleReasoning.ThoughtSeconds(pet.Pet.State) };
    public float InspectSeconds(PetState state)=>Kind==PuzzleElementKind.Plate ? 20 : 4+(1-state.IntelligenceAbility/990f)*3;
    public Vector3? ApproachPoint=>Kind==PuzzleElementKind.Plate ? null : GlobalPosition+GlobalBasis.Z*.92f+Vector3.Up*.02f;
    public bool Inspected(ExpeditionCompanion pet,bool requested)
    {
        if(!Accessible(pet.Body,false) || Kind!=PuzzleElementKind.Plate && pet.Body.Position.DistanceTo(Contact)>.75f || !requested && pet.Random.NextDouble()>PuzzleReasoning.ActionChance(pet.Pet.State))return false;
        return Activate(pet,requested);
    }
    public void Pose(ExpeditionCompanion pet,float time)
    {
        if(Kind==PuzzleElementKind.Plate)return;
        float reach=Mathf.SmoothStep(0,1,Mathf.Min(time/.8f,1));
        ImportedChao.ReceivePose(pet.Art,reach*.6f);
        if(pet.Body.Position.DistanceTo(Contact)<.75f)ImportedChao.InstrumentHands(pet.Art,Contact-GlobalBasis.X*.07f,Contact+GlobalBasis.X*.07f);
    }
    internal void Visual(float dt,int revision)
    {
        if(State==null)return;
        float target=Kind==PuzzleElementKind.Rotary ? -State().Value*Mathf.Tau/3 : Kind==PuzzleElementKind.Plate ? (Pressed ? -.018f : 0) : State().Value*.85f;
        if(Kind==PuzzleElementKind.Plate)handle.Position=new(0,Mathf.Lerp(handle.Position.Y,.045f+target,1-Mathf.Exp(-10*dt)),0);
        else handle.Rotation=new(0,0,Mathf.LerpAngle(handle.Rotation.Z,target,1-Mathf.Exp(-5*dt)));
        var paint=(StandardMaterial3D)indicator.MaterialOverride;paint.AlbedoColor=Locked() || Pressed || State().Value>0 ? new("90b780") : new("91644e");
        if(Interest.Definition.Revision!=revision)Interest.Definition=Interest.Definition with { Revision=revision };
    }
}

internal sealed class PlayerPuzzleAction
{
    internal PuzzleElement? Target { get; private set; }
    internal float Time { get; private set; }
    internal float Blend=>Target==null ? 0 : Mathf.Sin(Mathf.Min(Time/1.5f,1)*Mathf.Pi);
    private Vector3 start;
    private int expectedValue;
    internal bool Begin(PuzzleElement target,CharacterBody3D player)
    { if(Target!=null || !target.Accessible(player))return false;Target=target;Time=0;start=player.Position;expectedValue=target.State().Value;return true; }
    internal void Cancel(){Target=null;Time=0;}
    internal void Step(float dt,CharacterBody3D player)
    {
        if(Target==null)return;
        if(!GodotObject.IsInstanceValid(Target) || !Target.Accessible(player) || ExpeditionCompanion.Flat(start,player.Position)>.65f){Cancel();return;}
        Time+=dt;
        if(Time>=1.5f){if(Target.State().Value==expectedValue)Target.Activate();Cancel();}
    }
}
