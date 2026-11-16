using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mori;

internal sealed class StadiumView
{
    internal Node3D Root {get;}
    private readonly List<(CharacterBody3D Body,ImportedChao Art)> actors=new();
    private readonly int[] impacts=new int[2];
    private Vector3 cameraAt;
    private bool cameraReady,released;
    private readonly SphereShape3D cameraShell=new(){Radius=.13f};
    private float endAge;
    internal float MaxHitGap {get;private set;}
    internal int ContactSamples {get;private set;}
    internal StadiumView(Node3D world){Root=new Node3D{Name="CompetitionView"};world.AddChild(Root);Root.TreeExiting+=ReleaseResources;}
    internal IReadOnlyList<CharacterBody3D> Bodies=>actors.Select(a=>a.Body).ToArray();
    private void Actor(string id,string color,PetState state)
    {
        var body=new CharacterBody3D{Name="Athlete"+actors.Count,CollisionLayer=0,CollisionMask=1};Root.AddChild(body);
        body.AddChild(new CollisionShape3D{Position=Vector3.Up*.18f,Shape=new SphereShape3D{Radius=.16f}});
        var art=(ImportedChao)GardenEgg.BuildChaoArt(body,state,color);ImportedChao.BindPresentation(art,state,false,id);ImportedChao.ApplyDevelopment(art,state);
        var name=new Label3D{Text=state.Name,Position=Vector3.Up*.58f,FontSize=22,PixelSize=.002f,Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,Modulate=new("fff1c7")};body.AddChild(name);actors.Add((body,art));
    }
    internal void BuildRace(ChaoRace race){Root.Position=new(100,0,0);StadiumArt.Race(Root,race.Course);foreach(var racer in race.Entrants)Actor(racer.Id,racer.Color,racer.Pet);}
    internal void BuildKarate(ChaoKarate fight){Root.Position=new(-100,0,0);StadiumArt.Karate(Root);Actor(fight.A.Id,fight.A.Color,fight.A.Pet);Actor(fight.B.Id,fight.B.Color,fight.B.Pet);}
    private void Camera(Camera3D camera,Vector3 desired,Vector3 focus,float dt)
    {
        var space=Root.GetWorld3D().DirectSpaceState;
        Vector3 Clear(Vector3 at)
        {
            var motion=at-focus;using var query=new PhysicsShapeQueryParameters3D{Shape=cameraShell,CollisionMask=1,Transform=new(Basis.Identity,focus),Motion=motion};var safe=space.CastMotion(query);
            return focus+motion*Mathf.Max(0,safe[0]-.012f);
        }
        desired=Clear(desired);cameraAt=cameraReady ? cameraAt.Lerp(desired,1-Mathf.Exp(-4*dt)) : desired;cameraAt=Clear(cameraAt);cameraReady=true;camera.GlobalPosition=cameraAt;
        if(cameraAt.DistanceSquaredTo(focus)>.0001f)camera.LookAt(focus);
    }

    internal void Race(ChaoRace race,string playerId,Camera3D camera,float dt)
    {
        endAge=race.Finished ? endAge+dt : 0;camera.Fov=50;
        bool ceremony=race.Finished && endAge>.45f;var podium=StadiumRaceArt.V(race.Course.Anchors[^1])+new Vector3(0,0,5);
        for(int i=0;i<actors.Count;i++)
        {
            var racer=race.Entrants[i];var point=racer.Traversal.Position;
            var next=race.Course.Position(Mathf.Min(race.Course.Length,racer.Distance+.15f),(i-(actors.Count-1)*.5f)*RaceCourse.LaneSpacing,racer.Pet);
            var (body,art)=actors[i];body.Position=StadiumRaceArt.V(point);var direction=StadiumRaceArt.V(next-point);
            if(racer.Traversal.Phase is RaceObstaclePhase.Climb or RaceObstaclePhase.Mount){var section=race.Course.Sample(racer.Distance).Section;direction=StadiumRaceArt.V(race.Course.Anchors[section+1]-race.Course.Anchors[section]);}
            if(new Vector2(direction.X,direction.Z).LengthSquared()>.00001f)body.Rotation=new(0,Mathf.LerpAngle(body.Rotation.Y,Mathf.Atan2(direction.X,direction.Z),1-Mathf.Exp(-8*dt)),0);
            ImportedChao.RacingPose(art,racer,race.Course.TravelSurface(racer.Distance,racer.Pet),race.Time,dt);
            if(racer.Traversal.Phase==RaceObstaclePhase.Run && racer.Action==RaceAction.Move){var tangent=race.Course.Tangent(racer.Distance);art.Rotation+=new Vector3(-Mathf.Atan2(tangent.Y,new Vector2(tangent.X,tangent.Z).Length()),0,0);}
            if(racer.Action is RaceAction.Fall or RaceAction.Prone or RaceAction.Rise)ImportedChao.SupportStadiumFloor(art);
            if(ceremony)
            {
                float x=racer.Place==1 ? 0 : racer.Place==2 ? -.85f : racer.Place==3 ? .85f : (racer.Place-6)*.7f;
                float height=racer.Place==1 ? .8f : racer.Place==2 ? .48f : racer.Place==3 ? .28f : 0;
                body.Position=podium+new Vector3(x,height,racer.Place<=3 ? 0 : -1.6f);body.Rotation=new(0,racer.Place<=3 ? Mathf.Pi : 0,0);
                ImportedChao.MovePose(art,0,false,false,false,false,false,false);ImportedChao.Express(art,racer.Place<=3 ? ChaoExpression.Delighted : ChaoExpression.Sad);
                if(racer.Place==1)art.Position+=Vector3.Up*Mathf.Abs(Mathf.Sin(endAge*4))*.05f;
            }
        }
        int selected=race.Entrants.FindIndex(e=>e.Id==playerId);var racerAt=race.Entrants[Math.Max(0,selected)];var at=actors[Math.Max(0,selected)].Body.Position;
        if(ceremony){var focus=Root.ToGlobal(podium+new Vector3(-.45f,.7f,0));Camera(camera,Root.ToGlobal(podium+new Vector3(-.45f,1.9f,-3.7f)),focus,dt);return;}
        if(race.Countdown>0){var start=StadiumRaceArt.V(race.Course.Anchors[0]);Camera(camera,Root.ToGlobal(start+new Vector3(1.8f,1.2f,4.3f)),Root.ToGlobal(start+Vector3.Up*.25f),dt);return;}
        var surface=race.Course.Sample(racerAt.Distance).Surface;
        var cameraOffset=surface==RaceSurface.Fly ? new Vector3(4.3f,3.5f,-4.8f) : surface==RaceSurface.Power ? new Vector3(3.5f,2.1f,-3.8f) : new Vector3(2.6f,1.9f,-3.4f);
        if(race.Time<5){float orbit=Mathf.Clamp((race.Time-3)/2,0,1)*Mathf.Pi;cameraOffset=new(4*Mathf.Sin(orbit),1.9f,4*Mathf.Cos(orbit));}
        var ahead=StadiumRaceArt.V(race.Course.Position(Mathf.Min(race.Course.Length,racerAt.Distance+1.2f),0,racerAt.Pet));
        var look=at.Lerp(ahead,.28f)+Vector3.Up*.3f;
        Camera(camera,Root.ToGlobal(at+cameraOffset),Root.ToGlobal(look),dt);
    }
    internal void Karate(ChaoKarate fight,string playerId,Camera3D camera,float dt)
    {
        endAge=fight.Finished ? endAge+dt : 0;
        var fighters=new[]{fight.A,fight.B};
        for(int i=0;i<2;i++)
        {
            var f=fighters[i];var other=fighters[1-i];var (body,art)=actors[i];body.Position=StadiumArt.Ring+new Vector3(f.Position.X,.03f,f.Position.Y);body.Rotation=new(0,Mathf.Atan2(f.Facing.X,f.Facing.Y),0);
            var target=Root.ToGlobal(StadiumArt.Ring+new Vector3(other.Position.X,.03f,other.Position.Y));
            if(fight.Finished && endAge>.3f && fight.Winner==f.Id)body.Rotation=new(0,Mathf.LerpAngle(Mathf.Atan2(f.Facing.X,f.Facing.Y),0,Mathf.SmoothStep(.3f,1,endAge)),0);
            body.Position+=Vector3.Up*f.OutsideHeight;
            ImportedChao.KaratePose(art,f,target,fight.Finished,fight.Winner==f.Id,endAge);
            if(f.Action is KarateAction.Knockdown or KarateAction.Rise || fight.Finished && fight.Winner!=f.Id && (!f.RingFalling || f.OutsideHeight<=-.35f))ImportedChao.SupportStadiumFloor(art);
            if(fight.Finished && fight.Winner==f.Id && endAge>.3f)ImportedChao.Attend(art,body.GlobalPosition+Vector3.Back*3+Vector3.Up*.3f,100);
            if(f.Impact!=impacts[i])
            {
                var impactNode=art.FindChild(f.StrikeKind is 2 or 4 ? "FootR" : f.StrikeKind==0 ? "PaddleL" : "PaddleR",true,false) as Node3D;
                if(impactNode!=null){var tip=f.StrikeKind is 2 or 4 ? impactNode.GlobalPosition : impactNode.ToGlobal(Vector3.Down*.0541f);var localContact=f.ImpactPoint;var contact=Root.ToGlobal(StadiumArt.Ring+new Vector3(localContact.X,.03f+(f.StrikeKind is 2 or 4 ? .13f : .20f),localContact.Y));float gap=tip.DistanceTo(contact);MaxHitGap=Mathf.Max(MaxHitGap,gap);ContactSamples++;if(gap>.22f)GD.Print($"SPORTS_CONTACT_GAP: {gap} kind={f.AttackKind} action={f.Action} age={f.ActionTime} ended={fight.Finished}");}
                impacts[i]=f.Impact;ChaoSounds.Play(art,"ball");
                var flash=new Node3D{Position=StadiumArt.Ring+new Vector3(f.ImpactPoint.X,.2f,f.ImpactPoint.Y)};Root.AddChild(flash);GardenEgg.ShellBurst(flash,new("fff1bb"),true);
                var fade=flash.CreateTween();fade.TweenInterval(.9);fade.TweenCallback(Callable.From(flash.QueueFree));
            }
        }
        var middle=(actors[0].Body.Position+actors[1].Body.Position)*.5f;float separation=actors[0].Body.Position.DistanceTo(actors[1].Body.Position);
        camera.Fov=48;float distance=2.35f+Mathf.Max(0,separation-.7f)*.80f+(fight.Countdown>0 ? .5f : 0);
        var center=middle.Lerp(StadiumArt.Ring,.18f);
        Camera(camera,Root.ToGlobal(center+new Vector3(.32f,.85f,distance)),Root.ToGlobal(center+Vector3.Up*.30f),dt);
        if(Root.FindChild("RefereeFlagL",true,false) is Node3D left)left.Rotation=new(0,0,fight.Finished && fight.Winner==fight.A.Id ? -.1f : -.65f);
        if(Root.FindChild("RefereeFlagR",true,false) is Node3D right)right.Rotation=new(0,0,fight.Finished && fight.Winner==fight.B.Id ? .1f : .65f);
    }

    internal void Celebrate(){if(actors.Count>0)actors[0].Art.GetNodeOrNull<EmotionOrb>("MoodOrb")?.SetIdea("!",true);}
    private void ReleaseResources(){if(released)return;released=true;cameraShell.Dispose();}
    internal void Dispose(){ReleaseResources();if(GodotObject.IsInstanceValid(Root)){Root.GetParent().RemoveChild(Root);Root.QueueFree();}}
}
