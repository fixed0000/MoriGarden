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
    private bool cameraReady;
    private float endAge;
    internal float MaxHitGap {get;private set;}
    internal int ContactSamples {get;private set;}
    internal StadiumView(Node3D world){Root=new Node3D{Name="CompetitionView"};world.AddChild(Root);}
    internal IReadOnlyList<CharacterBody3D> Bodies=>actors.Select(a=>a.Body).ToArray();
    private void Actor(string id,string color,PetState state)
    {
        var body=new CharacterBody3D{Name="Athlete"+actors.Count,CollisionLayer=0,CollisionMask=1};Root.AddChild(body);
        body.AddChild(new CollisionShape3D{Position=Vector3.Up*.18f,Shape=new SphereShape3D{Radius=.16f}});
        var art=(ImportedChao)GardenEgg.BuildChaoArt(body,state,color);ImportedChao.BindPresentation(art,state,false,id);ImportedChao.ApplyDevelopment(art,state);
        var name=new Label3D{Text=state.Name,Position=Vector3.Up*.58f,FontSize=22,PixelSize=.002f,Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,Modulate=new("fff1c7")};body.AddChild(name);actors.Add((body,art));
    }
    internal void BuildRace(ChaoRace race){StadiumArt.Race(Root,race.Course);foreach(var racer in race.Entrants)Actor(racer.Id,racer.Color,racer.Pet);}
    internal void BuildKarate(ChaoKarate fight){StadiumArt.Karate(Root);Actor(fight.A.Id,fight.A.Color,fight.A.Pet);Actor(fight.B.Id,fight.B.Color,fight.B.Pet);}
    private void Camera(Camera3D camera,Vector3 desired,Vector3 focus,float dt)
    {cameraAt=cameraReady ? cameraAt.Lerp(desired,1-Mathf.Exp(-5*dt)) : desired;cameraReady=true;camera.GlobalPosition=cameraAt;camera.LookAt(focus);}
    internal void Race(ChaoRace race,string playerId,Camera3D camera,float dt)
    {
        for(int i=0;i<actors.Count;i++)
        {
            var racer=race.Entrants[i];var (section,fraction)=race.Course.Locate(racer.Distance);var point=race.Course.At(racer.Distance,(i-1.5f)*1.15f);
            var direction=race.Course.Points[section+1]-race.Course.Points[section];var surface=race.Course.Surfaces[section];
            var (body,art)=actors[i];float lift=surface==RaceSurface.Fly ? Mathf.Sin(fraction*Mathf.Pi)*1.3f : surface==RaceSurface.Power ? Mathf.Max(0,1-Mathf.Abs(fraction-.5f)*15)*.38f : 0;
            float swimBlend=surface==RaceSurface.Swim ? Mathf.SmoothStep(0,.06f,fraction)*(1-Mathf.SmoothStep(.94f,1,fraction)) : 0;
            body.Position=new(point.X,Mathf.Lerp(.18f,.115f,swimBlend)+lift,point.Y);body.Rotation=new(0,Mathf.LerpAngle(body.Rotation.Y,Mathf.Atan2(direction.X,direction.Y),1-Mathf.Exp(-7*dt)),0);
            bool moving=race.Countdown<=0 && racer.FinishedAt<0;
            ImportedChao.MovePose(art,racer.Distance*13,moving,surface==RaceSurface.Swim,moving && racer.Pet.Swim>=100,racer.Speed>3.1f,surface==RaceSurface.Fly,surface==RaceSurface.Fly);
            if(surface==RaceSurface.Power && Mathf.Abs(fraction-.5f)<.1f && moving)ImportedChao.ClimbPose(art,race.Time);
            if(race.Finished){ImportedChao.Pose(art,ChaoPoseKind.Idle);ImportedChao.Express(art,racer.Place==1 ? ChaoExpression.Delighted : ChaoExpression.Sad,1,.6f+racer.Pet.Sociability*.004f);art.GetNode<EmotionOrb>("MoodOrb").SetIdea(racer.Place==1 ? "!" : null,racer.Place==1);}
            else if(racer.Reaction.Length>0){ImportedChao.Express(art,racer.Reaction=="stumble" ? ChaoExpression.Surprised : ChaoExpression.Happy);}
        }
        int selected=race.Entrants.FindIndex(e=>e.Id==playerId);var at=actors[Math.Max(0,selected)].Body.Position;
        var lead=actors[race.Entrants.IndexOf(race.Standings[0])].Body.Position;var focus=at.Lerp(lead,.2f);
        Camera(camera,at+new Vector3(2.7f,1.6f,2.8f),focus+Vector3.Up*.3f,dt);
    }
    internal void Karate(ChaoKarate fight,string playerId,Camera3D camera,float dt)
    {
        endAge=fight.Finished ? endAge+dt : 0;
        var fighters=new[]{fight.A,fight.B};
        for(int i=0;i<2;i++)
        {
            var f=fighters[i];var other=fighters[1-i];var (body,art)=actors[i];body.Position=StadiumArt.Ring+new Vector3(f.Position.X,.03f,f.Position.Y);body.Rotation=new(0,Mathf.Atan2(f.Facing.X,f.Facing.Y),0);
            var target=StadiumArt.Ring+new Vector3(other.Position.X,.03f,other.Position.Y);
            ImportedChao.KaratePose(art,f,target,fight.Finished,fight.Winner==f.Id,endAge);
            if(f.Impact!=impacts[i])
            {
                var impactNode=art.FindChild(f.AttackKind==2 ? "FootR" : f.AttackKind==0 ? "PaddleL" : "PaddleR",true,false) as Node3D;
                if(impactNode!=null){var tip=f.AttackKind==2 ? impactNode.GlobalPosition : impactNode.ToGlobal(Vector3.Down*.0541f);float gap=tip.DistanceTo(target+Vector3.Up*.2f);MaxHitGap=Mathf.Max(MaxHitGap,gap);ContactSamples++;if(gap>.22f)GD.Print($"SPORTS_CONTACT_GAP: {gap} kind={f.AttackKind} action={f.Action} age={f.ActionTime} ended={fight.Finished}");}
                impacts[i]=f.Impact;ChaoSounds.Play(art,"ball");
                var flash=new Node3D{Position=StadiumArt.Ring+new Vector3(f.ImpactPoint.X,.2f,f.ImpactPoint.Y)};Root.AddChild(flash);GardenEgg.ShellBurst(flash,new("fff1bb"),true);
                var fade=flash.CreateTween();fade.TweenInterval(.9);fade.TweenCallback(Callable.From(flash.QueueFree));
            }
        }
        var middle=(actors[0].Body.Position+actors[1].Body.Position)*.5f;
        Camera(camera,middle+new Vector3(1.55f,1.15f,2.5f),middle+Vector3.Up*.22f,dt);
    }
    internal void Celebrate(){if(actors.Count>0)actors[0].Art.GetNodeOrNull<EmotionOrb>("MoodOrb")?.Celebrate();}
    internal void Dispose(){if(GodotObject.IsInstanceValid(Root)){Root.GetParent().RemoveChild(Root);Root.QueueFree();}}
}
