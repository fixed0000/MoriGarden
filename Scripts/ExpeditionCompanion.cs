using System;
using Godot;

namespace Mori;

internal sealed class ExpeditionCompanion
{
    internal ExpeditionPet Pet { get; private set; }
    internal CharacterBody3D Body { get; }
    internal Node3D Art { get; private set; }
    internal ExpeditionIntent Intent { get; private set; }=ExpeditionIntent.Observe;
    internal ExpeditionCompanion? Partner { get; set; }
    internal float SocialContact { get; set; }
    internal float SocialApproach { get; set; }
    internal bool WalkedTogether { get; set; }
    internal float Remaining { get; private set; }
    internal Vector3 Goal { get; private set; }
    internal bool Busy=>rising.Active || think>0 || (Remaining>0 && Intent is ExpeditionIntent.Rest or ExpeditionIntent.Sleep or ExpeditionIntent.Social or ExpeditionIntent.Investigate);
    internal bool ReadyForDiscovery=>!rising.Active && !lying && think<=0 && !ImportedChao.Pausing(Art);
    internal bool Transitioning=>rising.Active || ImportedChao.Pausing(Art);
    internal float NavigationFailure=>failures;
    internal float TargetTolerance { get; set; }=.45f;
    internal bool Sleeping=>Intent==ExpeditionIntent.Sleep && resting>2 && !rising.Active;
    internal int Recoveries { get; private set; }
    internal readonly Random Random;
    private readonly ChaoMotion motion=new() { GardenPond=false };
    private readonly ChaoWander steering=new() { GardenLandmarks=false };
    private readonly ChaoRising rising=new();
    private readonly ChaoVocal voice=new();
    private Vector3[] path=Array.Empty<Vector3>();
    private int waypoint;
    private float repath, elapsed, phase, stuck, think, resting;
    private bool lying, sitRest;
    private float failures, hiddenStuck;
    private EmotionOrb? Orb=>Art.GetNodeOrNull<EmotionOrb>("MoodOrb");
    internal string RouteDebug=>$"goal {Goal}, waypoint {waypoint}/{path.Length} {(waypoint<path.Length ? path[waypoint] : Vector3.Zero)}, speed {motion.GroundSpeed}, blocked {motion.BlockedTime}, stuck {stuck}, heading {Body.Rotation.Y}";

    internal ExpeditionCompanion(Node3D parent,ExpeditionPet pet,Vector3 at,int slot)
    {
        Pet=pet;int seed=17;foreach(char letter in pet.Id)seed=unchecked(seed*31+letter);Random=new Random(seed);
        Body=new CharacterBody3D { Name="ExpeditionChao"+slot,Position=at,CollisionLayer=0,CollisionMask=1,FloorSnapLength=.18f };
        Body.AddChild(new CollisionShape3D { Position=new(0,.18f,0),Shape=new SphereShape3D { Radius=.17f } });
        parent.AddChild(Body); Body.AddToGroup("GardenChao");
        Art=GardenEgg.BuildChaoArt(Body,pet.State,pet.Color); ImportedChao.ApplyDevelopment(Art,pet.State);
        Goal=at; Remaining=2+slot*1.7f;
    }
    internal void Rebind(ExpeditionPet current)
    {
        bool changed=Pet.State.Form!=current.State.Form || Pet.State.CanAct!=current.State.CanAct || Pet.Color!=current.Color;
        Pet=current;
        if(!changed)return;
        Body.RemoveChild(Art);Art.QueueFree();Art=GardenEgg.BuildChaoArt(Body,current.State,current.Color);
        lying=false;rising.Cancel();SetIntent(ExpeditionIntent.Observe,Body.Position,3);
    }
    internal void SetIntent(ExpeditionIntent intent,Vector3 goal,float duration,bool idea=false)
    {
        if((lying || intent==ExpeditionIntent.Investigate && ImportedChao.IsCrawling(Art)) && !rising.Active) { rising.Begin(Art); lying=false; }
        ImportedChao.EndThink(Art); Orb?.SetIdea(null,false); Orb?.SetMealHappy(false);
        Intent=intent; Goal=goal; Remaining=duration; elapsed=0; resting=0; repath=0; think=idea ? 6.5f : 0;
        TargetTolerance=.45f;
        if(intent==ExpeditionIntent.Investigate)stuck=failures=hiddenStuck=0;
        sitRest=intent==ExpeditionIntent.Rest && Random.NextDouble()<.6;
        SocialContact=SocialApproach=0;WalkedTogether=false;
        path=Array.Empty<Vector3>(); waypoint=0;
    }
    internal void UpdateGoal(Vector3 goal) { if(Goal.DistanceTo(goal)>.65f) {Goal=goal; repath=0;} }
    internal void HoldSocial() => Remaining=Mathf.Max(Remaining,8);
    internal void Step(float dt,ExpeditionNavigation nav,Camera3D camera,Vector3 player)
    {
        var state=Pet.State;
        state.Tick(dt,Sleeping); voice.Step(Art,state,Sleeping,dt);
        if(Intent==ExpeditionIntent.Rest && lying)state.Energy=Mathf.Min(100,state.Energy+dt*.15f);
        ImportedChao.ApplyDevelopment(Art,state);
        Remaining-=dt; elapsed+=dt; repath-=dt;
        if(rising.Step(Art,dt) || ImportedChao.StepActionPause(Art,dt))return;
        if(think>0)
        {
            think-=dt; Face(Goal,dt);
            if(think>2.5f)ImportedChao.ThinkPose(Art,think);
            else { ImportedChao.EndThink(Art); Orb?.SetIdea(think>2 ? null : "!",think<=2); }
            if(think<=0) { ImportedChao.EndThink(Art); Orb?.SetIdea(null,false); Orb?.SetMealHappy(true); }
            return;
        }
        Vector3 delta=Goal-Body.Position;delta.Y=0;
        bool arriving=delta.Length()<TargetTolerance;
        if(!arriving && Intent is not ExpeditionIntent.Observe)
        {
            if(repath<=0)
            {
                path=nav.Path(Body.Position,Goal);waypoint=0;repath=2.5f;
                while(waypoint<path.Length-1 && Flat(Body.Position,path[waypoint])<.6f)waypoint++;
            }
            while(waypoint<path.Length-1 && Flat(Body.Position,path[waypoint])<.42f)waypoint++;
            var target=waypoint<path.Length ? path[waypoint] : Body.Position;
            if(TargetTolerance<.3f && waypoint==path.Length-1 && waypoint>=0 && Flat(Body.Position,target)<.65f)target=Goal;
            var toward=target-Body.Position; toward.Y=0;
            toward=ChaoMotion.AvoidNeighbors(Body,toward);
            var toPlayer=player-Body.Position;toPlayer.Y=0;
            if(toward.LengthSquared()>.03f && toPlayer.Length()<1.15f)
            {
                var aheadPlayer=toward.Normalized();var sidePlayer=aheadPlayer.Cross(Vector3.Up);
                float aheadDistance=aheadPlayer.Dot(toPlayer),across=sidePlayer.Dot(toPlayer);
                if(aheadDistance>0 && Mathf.Abs(across)<.65f)
                    toward=(aheadPlayer+sidePlayer*(across>.03f ? -1.5f : 1.5f)).Normalized()*toward.Length();
            }
            toward=steering.Steer(Body,toward,dt);
            var ahead=Body.Position+toward.Normalized()*.35f;ahead.Y=nav.Height(ahead.X,ahead.Z)+.02f;
            if(!nav.Safe(ahead))toward=Vector3.Zero;
            bool moving=motion.Step(Body,toward,state,Intent==ExpeditionIntent.Follow && delta.Length()>7,false,dt,false,out _);
            phase+=motion.GroundSpeed*dt*5.4f;
            ImportedChao.MovePose(Art,phase,moving,false,false,motion.Sprinting,motion.Airborne,motion.Flying);
            bool stalled=motion.GroundSpeed<.13f;
            stuck=stalled ? stuck+dt : Mathf.Max(0,stuck-dt*2);
            failures=stalled ? failures+dt : Mathf.Max(0,failures-dt);
            hiddenStuck=stalled && !camera.IsPositionInFrustum(Body.GlobalPosition+Vector3.Up*.2f) ? hiddenStuck+dt : 0;
            if(stuck>3) { nav.Repair(Body.Position,target);repath=0;stuck=0;Goal=nav.Nearest(Goal); }
            // Last resort only: prolonged failure, far away, and outside the camera frustum.
            if(failures>35 && hiddenStuck>12 && Flat(Body.Position,player)>25)
            { Body.Position=nav.Nearest(player+Vector3.Back*3);Body.ResetPhysicsInterpolation();motion.Reset();stuck=failures=hiddenStuck=0;Recoveries++; }
            return;
        }
        motion.Step(Body,Vector3.Zero,state,false,false,dt,false,out _);
        if(Intent is ExpeditionIntent.Rest or ExpeditionIntent.Sleep)
        {
            lying=true; resting+=dt;
            if(sitRest)
            {
                ImportedChao.StandForAction(Art);ImportedChao.CourtshipPose(Art,0,0);
                float blend=Mathf.SmoothStep(0,1,Mathf.Min(resting/1.5f,1));Art.Position*=blend;Art.Rotation*=blend;
            }
            else if(resting<2)ImportedChao.LieDown(Art,resting/2,state.Manners);
            else if(Intent==ExpeditionIntent.Sleep)ImportedChao.SleepPose(Art,resting-2,state.Manners);
            else ImportedChao.CloudPose(Art,resting-2);
        }
        else
        {
            ImportedChao.MovePose(Art,phase,false,false,false,false,false,false);
            if(Intent==ExpeditionIntent.Social && Partner!=null)
            {
                Face(Partner.Body.Position,dt); Orb?.SetMealHappy(true);
            }
            else if(Art.FindChild("HeadPivot",true,false) is Node3D head)
                head.Rotation=new(-.12f,Mathf.Sin(elapsed*.36f)*.35f,0);
        }
    }
    internal void Face(Vector3 target,float dt)
    {
        var d=target-Body.Position; if(d.LengthSquared()>.1f)Body.Rotation=new(0,Mathf.RotateToward(Body.Rotation.Y,Mathf.Atan2(d.X,d.Z),dt*1.65f),0);
    }
    internal static float Flat(Vector3 a,Vector3 b) { a.Y=b.Y=0;return a.DistanceTo(b); }
}
