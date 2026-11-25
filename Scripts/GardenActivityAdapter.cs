using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mori;

internal sealed class GardenActivityClaims
{
    private readonly Dictionary<ulong,CharacterBody3D> targets=new();
    private readonly Dictionary<CharacterBody3D,Vector3> resting=new();
    internal bool Free(Node3D target,CharacterBody3D actor)=>!targets.TryGetValue(target.GetInstanceId(),out var owner) || !GodotObject.IsInstanceValid(owner) || owner==actor;
    internal bool Acquire(Node3D target,CharacterBody3D actor)
    {if(!Free(target,actor))return false;targets[target.GetInstanceId()]=actor;return true;}
    internal bool RestFree(CharacterBody3D actor)=>!resting.Any(e=>e.Key!=actor && GodotObject.IsInstanceValid(e.Key) && e.Value.DistanceTo(actor.Position)<.72f);
    internal void ReserveRest(CharacterBody3D actor)=>resting[actor]=actor.Position;
    internal void Release(CharacterBody3D actor)
    {foreach(var key in targets.Where(p=>p.Value==actor || !GodotObject.IsInstanceValid(p.Value)).Select(p=>p.Key).ToArray())targets.Remove(key);resting.Remove(actor);}
}
internal sealed record GardenActivityContext(Node3D World,bool MainGarden,string AreaId,IEnumerable<FruitTree> Fruits,IEnumerable<ToyCar> Toys,
    GardenActivityClaims Claims,Func<CharacterBody3D,int> SocialTargets,Func<CharacterBody3D,bool> StartSocial);

// Connects the pure selector to the same executors used by both old update paths.
// Step/physics/animation remain in MoveFriend, GardenEgg and their components.
internal sealed class GardenActivityAdapter
{
    private readonly CharacterBody3D actor;
    private readonly Func<Node3D> art;
    private readonly ChaoInterests interests;
    private readonly ChaoRest rest;
    private readonly ChaoLeisure leisure;
    private readonly ChaoMotion motion;
    private readonly string stableId;
    internal readonly ChaoActivityCoordinator Coordinator;
    private GardenActivityContext context=null!;
    private PetState pet=null!;
    private bool started,extra,asleep,social,socialBall;
    private double nextScan;
    private ActivityAvailability[] available=Array.Empty<ActivityAvailability>();
    private NeedUrgency[] lastLevels=Array.Empty<NeedUrgency>();
    internal GardenActivityAdapter(CharacterBody3D actor,Func<Node3D> art,ChaoInterests interests,ChaoRest rest,ChaoLeisure leisure,ChaoMotion motion,string id)
    {
        stableId=id;this.actor=actor;this.art=art;this.interests=interests;this.rest=rest;this.leisure=leisure;this.motion=motion;
        int seed=17;foreach(char c in id)seed=unchecked(seed*31+c);Coordinator=new(new Random(seed));
    }
    internal bool Allows(ChaoActivity activity)=>Coordinator.SelectedActivity==activity;
    internal bool CanUseTarget(Node3D target)=>Usable(target);
    internal void Begin(float dt,PetState state,GardenActivityContext scene,bool sleeping,bool controlled,bool shared,bool extraLeisure)
    {
        pet=state;context=scene;
        leisure.BindContext(state,scene.AreaId,stableId);
        if(leisure.Active && (controlled || shared || motion.Airborne || motion.Recovering || !scene.MainGarden))leisure.Cancel(art());
        pet.GardenActivity=Coordinator;Coordinator.Advance(dt);asleep=sleeping;extra=extraLeisure;social=shared;
        if(controlled){context.Claims.Release(actor);Coordinator.Suspend("Player interaction or call");started=false;return;}
        if(Coordinator.SelectedActivity==null)Coordinator.Suspend("");
        if(shared)
        {Coordinator.Adopt(Coordinator.SelectedActivity==ChaoActivity.Play ? ChaoActivity.Play : ChaoActivity.Socialize,"shared social executor",ActivityInterruptibility.Atomic);started=true;return;}
        if(interests.Busy)
        {Coordinator.Adopt(socialBall ? ChaoActivity.Socialize : interests.Category,"ChaoInterests",interests.Interruptibility);started=true;return;}
        var ownToy=context.Toys.FirstOrDefault(t=>t.PlayedBy(actor));
        if(ownToy!=null){Coordinator.Adopt(socialBall || Coordinator.SelectedActivity==ChaoActivity.Socialize ? ChaoActivity.Socialize : ChaoActivity.Play,"ToyCar / "+ownToy.ItemName,ActivityInterruptibility.Atomic);started=true;return;}
        if(sleeping || rest.Requested){Coordinator.Adopt(ChaoActivity.Sleep,"ChaoRest",ActivityInterruptibility.Atomic);started=true;return;}
        if(extraLeisure){Coordinator.Adopt(ChaoActivity.Leisure,"existing garden excursion",ActivityInterruptibility.Atomic);started=true;return;}
        if(leisure.Active){Coordinator.Adopt(ChaoActivity.Leisure,"ChaoLeisure",leisure.Interruptibility);started=true;return;}
        if(Coordinator.SelectedActivity==ChaoActivity.Wander)Coordinator.Adopt(ChaoActivity.Wander,"ChaoWander",ActivityInterruptibility.LongOptional);
        if(started && Coordinator.SelectedActivity is {} a && a!=ChaoActivity.Wander)
        {
            // Rising/settling is already guarded by callers; do not complete it early.
            if(art() is ImportedChao model && model.PoseKind==ChaoPoseKind.Rise)return;
            string reason=a is ChaoActivity.Eat or ChaoActivity.Play ? interests.LastFailureReason : a==ChaoActivity.Sleep ? rest.LastFailureReason : a==ChaoActivity.Leisure ? leisure.LastFailureReason : "";
            context.Claims.Release(actor);if(reason.Length>0)Coordinator.Fail(reason);else Coordinator.Complete();
            started=socialBall=false;nextScan=0;
        }
    }
    internal void Select(bool mayChoose)
    {
        if(!mayChoose || asleep || social)return;
        var levels=pet.Needs.Evaluate().Priorities.Select(n=>n.Level).ToArray();
        if(Coordinator.Now>=nextScan || available.Length==0 || Coordinator.SelectedActivity==null || !levels.SequenceEqual(lastLevels))
        {available=Probe();lastLevels=levels;nextScan=Coordinator.Now+1;}
        if(Coordinator.ShouldInterrupt(pet,available))
        {
            interests.Cancel(actor,art());leisure.Cancel(art());context.Claims.Release(actor);
            Coordinator.Interrupt("Urgent physiological need");started=socialBall=false;
        }
        if(Coordinator.SelectedActivity==ChaoActivity.Wander && Coordinator.MinimumRemaining<=0)
        {Coordinator.Complete();started=false;}
        if(Coordinator.SelectedActivity!=null)return;
        // A failed starter cools down only its category; immediately consider another.
        for(int attempt=0;attempt<6;attempt++)
        {
            var choice=Coordinator.Choose(pet,available);if(choice==null)return;
            bool success=choice switch
            {
                ChaoActivity.Eat or ChaoActivity.Play=>BeginInterest(choice.Value),
                ChaoActivity.Sleep=>BeginRest(),
                ChaoActivity.Socialize=>context.StartSocial(actor) || BeginSocialBall(),
                ChaoActivity.Leisure=>StartLeisure(),
                _=>true
            };
            if(success){started=true;Coordinator.Adopt(choice.Value,choice==ChaoActivity.Wander ? "ChaoWander" : choice==ChaoActivity.Socialize ? "social executor" : choice.ToString()!,choice==ChaoActivity.Wander || choice==ChaoActivity.Leisure ? ActivityInterruptibility.LongOptional : choice==ChaoActivity.Sleep || choice==ChaoActivity.Socialize && !socialBall ? ActivityInterruptibility.Atomic : interests.Interruptibility);return;}
            context.Claims.Release(actor);Coordinator.Fail("Target became unavailable before start");available=Probe();
        }
    }
    private bool StartLeisure()=>leisure.Reconsider(actor);
    private bool BeginInterest(ChaoActivity activity)
    {
        if(!interests.BeginSelected(activity,actor,pet,context.Fruits,context.Toys,Usable))return false;
        if(interests.Target is {} target && context.Claims.Acquire(target,actor))return true;
        interests.Cancel(actor,art());return false;
    }
    private bool BeginSocialBall()
    {
        if(pet.Sociability<=50 || context.SocialTargets(actor)<=0)return false;
        bool ok=interests.BeginSelected(ChaoActivity.Play,actor,pet,Array.Empty<FruitTree>(),context.Toys.Where(t=>t.IsBeachBall),Usable);
        if(ok && interests.Target is {} target && context.Claims.Acquire(target,actor)){socialBall=true;return true;}
        if(ok)interests.Cancel(actor,art());return false;
    }
    private bool BeginRest()
    {
        if(context.MainGarden && rest.FreeSlots>0 && actor.Position.DistanceTo(ChaoRest.Bed)<10)
        {rest.Request(actor.Position);return rest.Requested;}
        if(!context.Claims.RestFree(actor) || !rest.RequestGround(actor))return false;
        context.Claims.ReserveRest(actor);return true;
    }
    private ActivityAvailability[] Probe()
    {
        bool dry=context.MainGarden ? !motion.IsSwimmingAt(actor.Position) : CoastalCove.Water.Depth(new(actor.Position.X,actor.Position.Z))<.02f;
        int food=dry && context.MainGarden ? ChaoInterests.CountTargets(ChaoActivity.Eat,actor,pet,context.Fruits,context.Toys,Usable) : 0;
        int toys=dry && context.MainGarden ? ChaoInterests.CountTargets(ChaoActivity.Play,actor,pet,context.Fruits,context.Toys,Usable) : 0;
        bool ground=dry && SafeRestFloor(actor) && context.Claims.RestFree(actor) && (!context.MainGarden || actor.Position.DistanceTo(ChaoRest.Bed)>1.2f || actor.Position.Y<.15f);
        int beds=context.MainGarden && actor.Position.DistanceTo(ChaoRest.Bed)<10 ? rest.FreeSlots : 0;
        int sleep=dry && pet.Energy<58 ? beds>0 ? beds : ground ? 1 : 0 : 0;
        int peers=dry && context.MainGarden ? context.SocialTargets(actor) : 0;
        int routes=dry && context.MainGarden && actor.Position.Y<.15f ? leisure.InspectTargets(actor) : 0;
        return new[]{A(ChaoActivity.Eat,food,"No reachable usable food",35,ActivityInterruptibility.Atomic),A(ChaoActivity.Sleep,sleep,"No safe free rest place or no rest need",35,ActivityInterruptibility.Atomic),
            A(ChaoActivity.Play,toys,"No free reachable suitable toy",60,ActivityInterruptibility.Atomic),A(ChaoActivity.Socialize,peers,"No eligible free social partner",22,ActivityInterruptibility.Atomic),
            A(ChaoActivity.Leisure,routes,"No available garden route on this surface",120,ActivityInterruptibility.LongOptional),A(ChaoActivity.Wander,1,"",5,ActivityInterruptibility.LongOptional)};
    }
    private static ActivityAvailability A(ChaoActivity activity,int count,string reason,float seconds,ActivityInterruptibility kind)=>new(activity,count>0,count>0 ? "Available" : reason,seconds,kind,count);
    private bool Usable(Node3D target)
    {
        if(!GodotObject.IsInstanceValid(target) || !context.Claims.Free(target,actor))return false;
        Vector3 point=target is FruitTree fruit && fruit.HasFruitOnGround ? fruit.FruitPosition : target.GlobalPosition;
        if(Mathf.Abs(point.Y-actor.Position.Y)>1 || GardenArt.IsPondWater(point))return false;
        for(int sample=1;sample<6;sample++)if(GardenArt.IsPondWater(actor.Position.Lerp(point,sample/6f)))return false;
        var hit=actor.GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(actor.GlobalPosition+Vector3.Up*.24f,point+Vector3.Up*.24f,1));
        if(hit.Count==0)return true;
        return hit["collider"].AsGodotObject() is Node node && (node==target || target.IsAncestorOf(node) &&
            (target is not FruitTree f || !f.HasFruitOnGround || node is CharacterBody3D));
    }
    internal static bool SafeRestFloor(CharacterBody3D actor)
    {
        foreach(var offset in new[]{Vector3.Zero,Vector3.Left*.32f,Vector3.Right*.32f,Vector3.Forward*.32f,Vector3.Back*.32f})
        {
            var at=actor.GlobalPosition+offset;
            var hit=actor.GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(at+Vector3.Up*.5f,at-Vector3.Up*.3f,1));
            if(hit.Count==0 || ((Vector3)hit["normal"]).Y<.7f || Mathf.Abs(((Vector3)hit["position"]).Y-actor.Position.Y)>.13f)return false;
        }
        return true;
    }
    internal void ReserveSocial()
    {interests.Cancel(actor,art());leisure.Cancel(art());context?.Claims.Release(actor);Coordinator.Adopt(ChaoActivity.Socialize,"shared social executor",ActivityInterruptibility.Atomic);started=true;}
    internal void Reset(){context?.Claims.Release(actor);Coordinator.Suspend("Garden transition");started=socialBall=false;nextScan=0;}
}
