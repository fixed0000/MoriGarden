using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mori;

internal sealed class GardenCourtship
{
    private sealed record Bloom(CharacterBody3D Actor,Node3D Art,PetState Pet,string Color,Node3D Flowers);
    private sealed record Pair(Bloom First,Bloom Second,Vector3 Origin,Vector3 Axis,Vector3 SecondStart)
    {
        public float Time;
        public bool Laid;
    }

    private readonly Dictionary<CharacterBody3D,Bloom> blooms=new();
    private readonly Dictionary<CharacterBody3D,float> placed=new();
    private Pair? pair;

    public bool IsPaired(CharacterBody3D actor) => pair!=null && (pair.First.Actor==actor || pair.Second.Actor==actor);
    public bool IsBlooming(CharacterBody3D actor) => blooms.ContainsKey(actor);
    public void PutDown(CharacterBody3D actor) => placed[actor]=1.5f;

    public void Suspend(CharacterBody3D actor)
    {
        if (blooms.Remove(actor,out var bloom)) bloom.Flowers.QueueFree();
        placed.Remove(actor);
    }

    public void Clear()
    {
        foreach (var bloom in blooms.Values)
        {
            ImportedChao.SetHappy(bloom.Art,false);
            bloom.Flowers.QueueFree();
        }
        blooms.Clear();
        placed.Clear();
        pair=null;
    }

    public bool Sit(CharacterBody3D actor,Node3D art,PetState pet,string color,Node3D world,float dt)
    {
        if (IsPaired(actor)) return true;
        if (pet.CourtshipReadiness<100 || GardenArt.IsPondWater(actor.Position))
        {
            if (blooms.ContainsKey(actor)) Suspend(actor);
            return false;
        }
        if (!blooms.TryGetValue(actor,out var bloom))
        {
            var support=actor.MoveAndCollide(Vector3.Down*.10f,testOnly:true);
            if (support==null || support.GetNormal().Y<.65f) return false;
            if (pet.BloomRemaining<=0) pet.BloomRemaining=300;
            bloom=new(actor,art,pet,color,MakeFlowers(world,actor.Position));
            blooms.Add(actor,bloom);
        }
        pet.BloomRemaining=Mathf.Max(0,pet.BloomRemaining-dt);
        if (pet.BloomRemaining<=0)
        {
            pet.CourtshipReadiness=0;
            Suspend(actor);
            return false;
        }
        bloom.Flowers.Scale=Vector3.One*Mathf.Lerp(.02f,1,Mathf.Clamp((300-pet.BloomRemaining)/3,0,1));
        ImportedChao.CourtshipPose(art,0,0);
        return true;
    }

    public void Advance(float dt,bool roomForEgg,Action<Vector3,string> layEgg,Action<CharacterBody3D,CharacterBody3D>? pairBond=null)
    {
        if (pair==null && roomForEgg)
        {
            foreach (var actor in placed.Keys)
            {
                if (!blooms.TryGetValue(actor,out var second)) continue;
                var first=blooms.Values.Where(b=>b.Actor!=actor && b.Pet.BloomRemaining<299.8f)
                    .OrderBy(b=>b.Actor.Position.DistanceSquaredTo(actor.Position))
                    .FirstOrDefault(b=>CanMeet(b.Actor,actor));
                if (first==null) continue;
                var axis=actor.Position-first.Actor.Position;
                axis.Y=0;
                axis=axis.LengthSquared()>.0025f ? axis.Normalized() : Vector3.Back;
                pair=new(first,second,first.Actor.Position,axis,actor.Position);
                first.Pet.BloomRemaining=second.Pet.BloomRemaining=0;
                break;
            }
        }
        foreach (var actor in placed.Keys.ToArray())
        {
            placed[actor]-=dt;
            if (placed[actor]<=0) placed.Remove(actor);
        }
        if (pair is not { } meeting) return;
        meeting.Time+=dt;
        var firstActor=meeting.First.Actor;
        var secondActor=meeting.Second.Actor;
        float yaw=Mathf.Atan2(meeting.Axis.X,meeting.Axis.Z);
        firstActor.Rotation=new(0,yaw,0);
        secondActor.Rotation=new(0,yaw+Mathf.Pi,0);
        float time=meeting.Time;
        if (time<2.2f)
        {
            float approach=Mathf.SmoothStep(0,1,Mathf.Clamp(time/.85f,0,1));
            firstActor.Position=meeting.Origin-meeting.Axis*(.18f*approach);
            secondActor.Position=meeting.SecondStart.Lerp(meeting.Origin+meeting.Axis*.18f,approach);
            float rub=time>.85f ? Mathf.Sin((time-.85f)*15)*.24f : 0;
            ImportedChao.CourtshipPose(meeting.First.Art,rub,0);
            ImportedChao.CourtshipPose(meeting.Second.Art,-rub,0);
            return;
        }
        float recoil=Mathf.SmoothStep(0,1,Mathf.Clamp((time-2.2f)/.48f,0,1));
        firstActor.Position=meeting.Origin-meeting.Axis*Mathf.Lerp(.18f,.70f,recoil);
        secondActor.Position=meeting.Origin+meeting.Axis*Mathf.Lerp(.18f,.70f,recoil);
        if (!meeting.Laid && time>=2.68f)
        {
            meeting.Laid=true;
            meeting.First.Pet.CourtshipReadiness=meeting.Second.Pet.CourtshipReadiness=0;
            pairBond?.Invoke(firstActor,secondActor);
            layEgg(meeting.Origin,GD.Randf()<.5f ? meeting.First.Color : meeting.Second.Color);
        }
        float jump=meeting.Laid ? Mathf.Abs(Mathf.Sin((time-2.68f)*8)) : Mathf.Sin(recoil*Mathf.Pi)*.85f;
        ImportedChao.CourtshipPose(meeting.First.Art,0,jump);
        ImportedChao.CourtshipPose(meeting.Second.Art,0,jump);
        if (meeting.Laid)
        {
            ImportedChao.SetHappy(meeting.First.Art,true);
            ImportedChao.SetHappy(meeting.Second.Art,true);
        }
        if (time<7.68f) return;
        ImportedChao.SetHappy(meeting.First.Art,false);
        ImportedChao.SetHappy(meeting.Second.Art,false);
        Suspend(firstActor);
        Suspend(secondActor);
        pair=null;
    }

    private static bool CanMeet(CharacterBody3D first,CharacterBody3D second)
    {
        if (first.Position.DistanceTo(second.Position)>1.55f || Mathf.Abs(first.Position.Y-second.Position.Y)>.13f) return false;
        var ray=PhysicsRayQueryParameters3D.Create(first.Position+Vector3.Up*.24f,second.Position+Vector3.Up*.24f,1);
        return first.GetWorld3D().DirectSpaceState.IntersectRay(ray).Count==0;
    }

    private static Node3D MakeFlowers(Node3D world,Vector3 at)
    {
        var flowers=new Node3D { Position=at };
        world.AddChild(flowers);
        Color[] colors={new("e98ca2"),new("f5d86c"),new("d4b2ec"),new("eae9d1"),new("ef8c61")};
        for (int i=0;i<14;i++)
        {
            float angle=i*2.40f;
            float radius=.38f+i*.040f;
            var pos=new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
            float height=.07f+(i%4)*.023f;
            GardenArt.Cylinder(flowers,pos+Vector3.Up*(height*.5f),.007f,height,new("558045"));
            var top=pos+Vector3.Up*height;
            int petals=3+i%4;
            for (int j=0;j<petals;j++)
            {
                var around=Vector3.Back.Rotated(Vector3.Up,j*Mathf.Tau/petals);
                GardenArt.Ball(flowers,top+around*.026f,new(.038f,.016f+(i%3)*.011f,.038f),colors[i%colors.Length]);
            }
            GardenArt.Ball(flowers,top+Vector3.Up*.008f,new(.027f,.021f,.027f),new("edb746"));
        }
        return flowers;
    }
}
