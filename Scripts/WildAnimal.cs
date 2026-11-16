using System;
using System.Linq;
using Godot;

namespace Mori;

internal enum WildAnimalIntent { Idle,Wander,Observe,Approach,Flee,Interact,Return }
internal partial class WildAnimal : TrainingAnimal,IDiscoveryInteraction
{
    internal WildlifeHabitat Habitat { get; init; }=null!;
    internal AnimalSpecies Species { get; init; }=null!;
    internal DiscoveryInterest Interest { get; private set; }=null!;
    internal WildAnimalIntent Intent { get; private set; }
    internal float Trust { get; private set; }
    internal bool Helped { get; private set; }
    internal int Generation { get; private set; }
    internal bool Present { get; private set; }
    private Random random=new(1);
    private Vector3 goal,lastPosition;
    private Vector3[] path=Array.Empty<Vector3>();
    private int pathIndex;
    private float stateTime,clock,nextPath,stuck,helpCooldown;
    private bool placed;
    internal bool Fleeing=>Intent==WildAnimalIntent.Flee;
    internal bool ReadyToJoin=>Present && !Fleeing && Trust>=.78f;
    public override void _Ready()
    {
        base._Ready();CollisionLayer=4;CollisionMask=1;FloorSnapLength=.3f;FloorMaxAngle=.8f;
    }
    internal void BuildInterest(int generation)
    {
        int seed=17;foreach(char ch in Habitat.Id)seed=unchecked(seed*31+ch);random=new(seed+generation);
        Generation=generation;
        Interest=new DiscoveryInterest { Definition=new("animal-"+Habitat.Id,DiscoveryKind.Animal,new(Home.X,Home.Y,Home.Z),9,1.15f,
            DiscoveryReaction.Watch|DiscoveryReaction.Inspect|DiscoveryReaction.Share,RepeatSeconds:900,LookHeight:.2f,AreaId:Habitat.Area,Revision:generation),Interaction=this,VisibilityRoot=this };
        AddChild(Interest);
    }
    internal void SetPresent(bool present)
    { Present=Visible=present;Interest.Available=present;CollisionLayer=present ? 4u : 0u;if(!present)Velocity=Vector3.Zero; }
    internal void Respawn(Vector3 at,int generation)
    {
        Position=goal=at;placed=true;path=Array.Empty<Vector3>();Trust=0;Helped=false;Generation=generation;
        Interest.Definition=Interest.Definition with { Revision=generation };SetPresent(true);Change(WildAnimalIntent.Idle,3);
        ResetPhysicsInterpolation();
    }
    private void Change(WildAnimalIntent intent,float seconds)
    { Intent=intent;stateTime=seconds;nextPath=0;path=Array.Empty<Vector3>();pathIndex=0; }
    private static float Flat(Vector3 a,Vector3 b)=>new Vector2(a.X-b.X,a.Z-b.Z).Length();
    internal void Step(float dt,CharacterBody3D player,ExpeditionTeam team,WildlifeWorld world)
    {
        if(!Present)return;
        var nav=team.Navigation;clock+=dt;stateTime-=dt;nextPath-=dt;helpCooldown=Mathf.Max(0,helpCooldown-dt);
        if(!placed)
        {
            var safe=world.SafeHome(this,nav);if(safe==null){SetPresent(false);return;}
            Position=goal=safe.Value;placed=true;ResetPhysicsInterpolation();
        }
        bool seesPlayer=Flat(Position,player.Position)<7 && world.VisibleTo(player.GlobalPosition+Vector3.Up*.9f,this);
        float distance=Flat(Position,player.Position),playerSpeed=new Vector2(player.Velocity.X,player.Velocity.Z).Length();
        var near=team.Members.Where(p=>!p.Sleeping && Flat(p.Body.Position,Position)<4).OrderBy(p=>Flat(p.Body.Position,Position)).FirstOrDefault();
        bool researching=team.Discoveries.Sessions.Any(s=>s.Item==Interest && Flat(s.Pet.Body.Position,Position)<9);
        if(seesPlayer && distance<5)world.SaveData.Wildlife.Meet(Species.Id);
        if(seesPlayer && distance<4 && playerSpeed<4.3f && !Fleeing)
            Trust=Mathf.Min(1,Trust+AnimalEncounterRules.CalmRate(Species,playerSpeed)*dt);
        if(seesPlayer && distance<1.1f+Species.Caution*2 && playerSpeed>4.3f && Intent!=WildAnimalIntent.Flee)
        {
            Trust=Mathf.Max(0,Trust-.2f);Change(WildAnimalIntent.Flee,3.5f);
            goal=world.ChoosePoint(this,nav,Position+(Position-player.Position).Normalized()*3.4f);
            Interest.Available=false;
        }
        if(!Fleeing && (researching || seesPlayer && distance<2.6f))
        {
            if(Intent is not (WildAnimalIntent.Observe or WildAnimalIntent.Interact))Change(WildAnimalIntent.Observe,5);
            goal=Position;
        }
        if(stateTime<=0)
        {
            Interest.Available=true;
            if(Flat(Position,Home)>Habitat.Radius+1){Change(WildAnimalIntent.Return,8);goal=world.ChoosePoint(this,nav,Home);}
            else if(near!=null && random.NextDouble()<Species.Curiosity && !researching)
            { Change(WildAnimalIntent.Approach,4);goal=world.ChoosePoint(this,nav,near.Body.Position+(Position-near.Body.Position).Normalized()*1.4f); }
            else if(random.NextDouble()<.7){Change(WildAnimalIntent.Wander,5+(float)random.NextDouble()*4);goal=world.ChoosePoint(this,nav,null);}
            else {Change(WildAnimalIntent.Idle,3+(float)random.NextDouble()*4);goal=Position;}
        }
        bool moving=Intent is WildAnimalIntent.Wander or WildAnimalIntent.Approach or WildAnimalIntent.Flee or WildAnimalIntent.Return;
        Vector3 travel=Vector3.Zero;
        if(moving && Flat(Position,goal)>.3f)
        {
            if(nextPath<=0){path=nav.Path(Position,goal);pathIndex=path.Length>1 ? 1 : 0;nextPath=2;}
            while(pathIndex<path.Length && Flat(Position,path[pathIndex])<.3f)pathIndex++;
            if(pathIndex<path.Length)travel=(path[pathIndex]-Position)*new Vector3(1,0,1);
            float speed=Species.Speed*(Fleeing ? 1.45f : 1);
            if(travel.Length()>.01f)travel=travel.Normalized()*speed;
        }
        foreach(var pet in team.Members)
            if(Flat(Position,pet.Body.Position)<.55f)travel+=(Position-pet.Body.Position).Normalized()*.7f;
        if(distance<.65f)travel+=(Position-player.Position).Normalized()*.7f;
        travel.Y=0;
        if(travel.Length()>.01f)
        {
            var ahead=Position+travel.Normalized()*.4f;ahead.Y=nav.Height(ahead.X,ahead.Z)+.02f;
            if(!nav.Safe(ahead)){travel=Vector3.Zero;nextPath=0;}
        }
        if(travel.Length()>.04f)Rotation=new(0,Mathf.LerpAngle(Rotation.Y,Mathf.Atan2(travel.X,travel.Z),1-Mathf.Exp(-5*dt)),0);
        else if(seesPlayer && Intent==WildAnimalIntent.Observe)Rotation=new(0,Mathf.LerpAngle(Rotation.Y,Mathf.Atan2(player.Position.X-Position.X,player.Position.Z-Position.Z),1-Mathf.Exp(-2*dt)),0);
        float ground=nav.Height(Position.X,Position.Z);
        Velocity=new(Mathf.Lerp(Velocity.X,travel.X,1-Mathf.Exp(-7*dt)),IsOnFloor() ? -.3f : Velocity.Y-12*dt,Mathf.Lerp(Velocity.Z,travel.Z,1-Mathf.Exp(-7*dt)));
        MoveAndSlide();
        float actual=Flat(Position,lastPosition)/Mathf.Max(dt,.001f);lastPosition=Position;
        if(moving && travel.Length()>.2f && actual<.04f)stuck+=dt;else stuck=0;
        if(stuck>2 || Position.Y<ground-.2f){Change(WildAnimalIntent.Return,4);goal=world.ChoosePoint(this,nav,Home);stuck=0;}
        AnimateWild(dt,Mathf.Min(actual,2),clock,Species.Skill=="Fly" && actual>.2f);
    }
    public Vector3? ApproachPoint=>Position+GlobalBasis.Z*.95f;
    public DiscoveryPlan Prepare(DiscoveryPlan plan,ExpeditionCompanion pet)=>plan with { ThoughtSeconds=Mathf.Max(4,plan.ThoughtSeconds-(pet.Pet.State.History.KnowsAnimal(Species.Id) ? 1 : 0)-1.5f*pet.Pet.State.Intelligence/990f) };
    public float InspectSeconds(PetState state)=>AnimalEncounterRules.Thought(state);
    public bool Inspected(ExpeditionCompanion pet,bool requested)
    {
        if(!Present || Fleeing || Flat(Position,pet.Body.Position)>1.9f || helpCooldown>0)return false;
        var hit=GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(pet.Body.Position+Vector3.Up*.25f,Position+Vector3.Up*.2f,1));
        if(hit.Count>0)return false;
        Trust=Mathf.Min(1,Trust+AnimalEncounterRules.ChaoHelp(pet.Pet.State));Helped=true;helpCooldown=25;
        pet.Pet.State.History.Record(ChaoMemoryKind.AnimalSeen,"animal-"+Species.Id,Habitat.Area,$"Made friends with a {Species.Name.ToLowerInvariant()}.",.7f,.75f);
        Change(WildAnimalIntent.Interact,5);goal=Position;ChaoSounds.Play(pet.Art,"voice");
        pet.Art.GetNodeOrNull<EmotionOrb>("MoodOrb")?.Celebrate();return true;
    }
    public void Pose(ExpeditionCompanion pet,float time)
    { DiscoveryPresentation.Look(pet,Position+Vector3.Up*.2f,1f/60); }
}
