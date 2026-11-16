using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Mori;

public readonly record struct RaceRoutePoint(Vector3 Position,RaceSurface Surface,int Section,float Fraction);
public sealed class RaceCourse
{
    public string Key {get;}
    public Vector3[] Anchors {get;}
    public RaceSurface[] Surfaces {get;}
    public Vector2[] Points=>Anchors.Select(p=>new Vector2(p.X,p.Z)).ToArray();
    public float[] Lengths {get;}
    public float Length {get;}
    public const float Width=5.2f,LaneSpacing=.55f;
    private readonly List<(float Distance,RaceRoutePoint Point)> samples=new();
    public IReadOnlyList<(float Distance,RaceRoutePoint Point)> Samples=>samples;
    public RaceCourse(SportsEvent competition)
    {
        Key=competition.Course.Length>0 ? competition.Course : "mushroom-forest";
        (Anchors,Surfaces)=Key switch {
            "crab-pool"=>(new Vector3[]{new(0,0,0),new(0,0,5),new(0,-.14f,9),new(2,-.14f,30),new(-1,-.14f,46),new(0,0,51),new(0,.1f,57)},new[]{RaceSurface.Run,RaceSurface.Run,RaceSurface.Swim,RaceSurface.Swim,RaceSurface.Run,RaceSurface.Run}),
            "stump-valley"=>(new Vector3[]{new(0,0,0),new(0,.4f,8),new(1,3.2f,18),new(0,2.4f,32),new(-2,2.6f,44),new(0,2.7f,57)},new[]{RaceSurface.Run,RaceSurface.Run,RaceSurface.Fly,RaceSurface.Run,RaceSurface.Run}),
            "block-canyon"=>(new Vector3[]{new(0,0,0),new(1,0,12),new(1,3.2f,12.4f),new(-2,3.2f,28),new(-2,5.8f,28.4f),new(0,5.8f,43),new(0,6,53)},new[]{RaceSurface.Run,RaceSurface.Power,RaceSurface.Run,RaceSurface.Power,RaceSurface.Run,RaceSurface.Run}),
            _=>(new Vector3[]{new(0,0,0),new(0,.1f,8),new(3,.5f,20),new(-2,1.1f,33),new(2,1.8f,47),new(0,2.4f,61)},new[]{RaceSurface.Run,RaceSurface.Run,RaceSurface.Run,RaceSurface.Run,RaceSurface.Run})};
        Lengths=new float[Surfaces.Length];float distance=0;Vector3 previous=Anchors[0];samples.Add((0,new(previous,Surfaces[0],0,0)));
        for(int section=0;section<Surfaces.Length;section++)for(int i=1;i<=40;i++)
        {
            float t=i/40f;var at=Curve(section,t);float delta=Vector3.Distance(previous,at);distance+=delta;Lengths[section]+=delta;
            samples.Add((distance,new(at,Surfaces[section],section,t)));previous=at;
        }
        Length=distance;
    }
    private Vector3 Curve(int section,float t)
    {
        var a=Anchors[section];var b=Anchors[section+1];
        if(Surfaces[section] is RaceSurface.Power or RaceSurface.Fly)return Vector3.Lerp(a,b,t);
        var before=section>0 && Surfaces[section-1]==RaceSurface.Run ? Anchors[section-1] : a-(b-a);
        var after=section+1<Surfaces.Length && Surfaces[section+1]==RaceSurface.Run ? Anchors[section+2] : b+(b-a);
        var tangentA=(b-before)*.5f;var tangentB=(after-a)*.5f;
        return (2*t*t*t-3*t*t+1)*a+(t*t*t-2*t*t+t)*tangentA+(-2*t*t*t+3*t*t)*b+(t*t*t-t*t)*tangentB;
    }
    public (int Section,float Fraction) Locate(float distance)
    {
        var point=Sample(distance);return(point.Section,point.Fraction);
    }
    public RaceRoutePoint Sample(float distance)
    {
        distance=Math.Clamp(distance,0,Length);int lo=0,hi=samples.Count-1;
        while(lo+1<hi){int middle=(lo+hi)/2;if(samples[middle].Distance<distance)lo=middle;else hi=middle;}
        var a=samples[lo];var b=samples[hi];float t=(distance-a.Distance)/Math.Max(.00001f,b.Distance-a.Distance);
        // A section boundary belongs to the segment being entered.
        return new(Vector3.Lerp(a.Point.Position,b.Point.Position,t),b.Point.Surface,b.Point.Section,a.Point.Section==b.Point.Section ? float.Lerp(a.Point.Fraction,b.Point.Fraction,t) : b.Point.Fraction*t);
    }
    public Vector2 At(float distance,float lane){var point=Position(distance,lane,null);return new(point.X,point.Z);}
    public Vector3 Tangent(float distance)
    {
        var delta=Sample(Math.Min(Length,distance+.08f)).Position-Sample(Math.Max(0,distance-.08f)).Position;
        if(delta.LengthSquared()<.00001f)delta=Vector3.UnitZ;return Vector3.Normalize(delta);
    }
    public RaceSurface TravelSurface(float distance,PetState pet)
    {
        var point=Sample(distance);if(point.Surface!=RaceSurface.Fly)return point.Surface;
        if(pet.Fly>=450 || point.Fraction<.25f+Math.Clamp(pet.Fly/450f,0,1)*.53f)return RaceSurface.Fly;
        return point.Fraction is >=.78f and <.95f ? RaceSurface.Power : point.Fraction>=.95f ? RaceSurface.Run : pet.Fly<120 ? RaceSurface.Swim : RaceSurface.Fly;
    }
    public Vector3 Position(float distance,float lane,PetState? pet)
    {
        var p=Sample(distance);var at=p.Position;var tangent=Tangent(distance);var flat=new Vector3(tangent.X,0,tangent.Z);
        if(flat.LengthSquared()<.001f)flat=Vector3.UnitZ;flat=Vector3.Normalize(flat);at+=new Vector3(flat.Z,0,-flat.X)*lane;
        if(p.Surface==RaceSurface.Fly && pet!=null)
        {
            if(pet.Fly>=450)at.Y=float.Lerp(Anchors[p.Section].Y,Anchors[p.Section+1].Y,p.Fraction)+MathF.Sin(p.Fraction*MathF.PI)*.25f;
            else
            {
                float strength=Math.Clamp(pet.Fly/450f,0,1);float landing=.25f+strength*.53f;float landingHeight=pet.Fly<120 ? -.22f : float.Lerp(.12f,Anchors[p.Section+1].Y,strength*strength);
                if(p.Fraction<landing)at.Y=float.Lerp(Anchors[p.Section].Y,landingHeight,p.Fraction/landing);
                else if(p.Fraction<.78f)at.Y=landingHeight;
                else if(p.Fraction<.95f){var wall=Vector3.Lerp(Anchors[p.Section],Anchors[p.Section+1],.78f);at.X=wall.X+flat.Z*lane;at.Z=wall.Z-flat.X*lane;at.Y=float.Lerp(landingHeight,Anchors[p.Section+1].Y,(p.Fraction-.78f)/.17f);}
                else {var wall=Vector3.Lerp(Anchors[p.Section],Anchors[p.Section+1],.78f);var end=Anchors[p.Section+1];var surface=Vector3.Lerp(wall,end,(p.Fraction-.95f)/.05f);at.X=surface.X+flat.Z*lane;at.Z=surface.Z-flat.X*lane;at.Y=end.Y;}
            }
        }
        return at;
    }
}
