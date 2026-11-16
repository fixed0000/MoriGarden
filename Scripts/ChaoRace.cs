using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Mori;

public sealed class RaceCourse
{
    public RaceSurface[] Surfaces {get;}
    public Vector2[] Points {get;}
    public float[] Lengths {get;}
    public float Length=>Lengths.Sum();
    public RaceCourse(SportsEvent competition)
    {
        Surfaces=competition.Sections;Points=new Vector2[Surfaces.Length+1];Lengths=new float[Surfaces.Length];
        var size=competition.Category switch{"Beginner"=>(12f,8f),"Swim"=>(16f,11f),"Fly"=>(17f,10f),"Run"=>(15f,12f),"Power"=>(13f,10f),_=>(16f,13f)};
        for(int i=0;i<Points.Length;i++){float angle=-MathF.PI/2+i*MathF.Tau/Surfaces.Length;Points[i]=new(MathF.Cos(angle)*(size.Item1+(competition.Level-1)*.4f),22+MathF.Sin(angle)*(size.Item2+(competition.Level-1)*.4f));}
        for(int i=0;i<Lengths.Length;i++)Lengths[i]=Vector2.Distance(Points[i],Points[i+1]);
    }
    public (int Section,float Fraction) Locate(float distance)
    {
        for(int i=0;i<Lengths.Length;i++){if(distance<=Lengths[i] || i==Lengths.Length-1)return(i,Math.Clamp(distance/Lengths[i],0,1));distance-=Lengths[i];}
        return(0,0);
    }
    public Vector2 At(float distance,float lane)
    {
        var (section,fraction)=Locate(distance);
        Vector2 Offset(int index)
        {
            int count=Lengths.Length,at=index%count;
            var before=Vector2.Normalize(Points[at]-Points[(at+count-1)%count]);
            var after=Vector2.Normalize(Points[at+1]-Points[at]);
            var normalBefore=new Vector2(-before.Y,before.X);var normalAfter=new Vector2(-after.Y,after.X);
            var bisector=Vector2.Normalize(normalBefore+normalAfter);
            return bisector/Math.Max(.3f,Vector2.Dot(bisector,normalAfter))*lane;
        }
        return Vector2.Lerp(Points[section]+Offset(section),Points[section+1]+Offset(section+1),fraction);
    }
}
public sealed class RaceEntrant
{
    public string Id {get;}
    public string Color {get;}
    public PetState Pet {get;}
    public float Distance {get;internal set;}
    public float Fatigue {get;internal set;}
    public float Capacity=>45+Pet.Stamina*.085f;
    public float FinishedAt {get;internal set;}=-1;
    public float Speed {get;internal set;}
    public float Delay {get;internal set;}
    public int Place {get;internal set;}
    public string Reaction {get;internal set;}="";
    internal float ReactionTime,Decision,Form,Boost;
    internal readonly Random Random;
    internal int LastPosition;
    public RaceEntrant(string id,string color,PetState pet,int seed){Id=id;Color=color;Pet=pet;Random=new(seed);Form=.97f+(float)Random.NextDouble()*.06f;Fatigue=Capacity*Math.Clamp(pet.Energy/100,0,1);}
}
public sealed class ChaoRace
{
    public RaceCourse Course {get;}
    public List<RaceEntrant> Entrants {get;}
    public float Time {get;private set;}
    public float Countdown=>Math.Max(0,3-Time);
    public bool Finished {get;private set;}
    public float CheerCooldown {get;private set;}
    public int Cheers {get;private set;}
    public IReadOnlyList<RaceEntrant> Standings=>Entrants.OrderBy(e=>e.FinishedAt<0 ? 1 : 0).ThenBy(e=>e.FinishedAt<0 ? -e.Distance : e.FinishedAt).ToArray();
    public ChaoRace(SportsEvent competition,IEnumerable<RaceEntrant> entrants){Course=new(competition);Entrants=entrants.ToList();if(Entrants.Count<2)throw new ArgumentException("A race needs several entrants.");}
    public static float Pace(PetState pet,RaceSurface surface,float staminaFraction)
    {
        int skill=surface switch{RaceSurface.Swim=>pet.Swim,RaceSurface.Fly=>pet.Fly,RaceSurface.Power=>pet.Power,_=>pet.Run};
        float baseSpeed=(surface==RaceSurface.Swim ? .72f : surface==RaceSurface.Power ? .65f : 1.0f)+skill*.0048f;
        float tired=.48f+.52f*Math.Clamp(staminaFraction*2,0,1);
        return baseSpeed*tired*(.72f+.28f*pet.Energy/100)*(.97f+.03f*pet.Joy/100)*(1+pet.Intelligence/990f*.035f);
    }
    public bool Cheer(string id)
    {
        var runner=Entrants.FirstOrDefault(e=>e.Id==id);if(Finished || Countdown>0 || CheerCooldown>0 || runner==null || runner.FinishedAt>=0)return false;
        float bond=Math.Clamp(runner.Pet.Bond/100f+runner.Pet.RelationshipWith()/200f,0,1);
        runner.Boost=.025f+.075f*bond;runner.Reaction="cheer";runner.ReactionTime=2;runner.Fatigue=Math.Min(runner.Capacity,runner.Fatigue+2+bond*3);
        CheerCooldown=9;Cheers++;return true;
    }
    public void Step(float dt)
    {
        if(Finished || dt<=0 || !float.IsFinite(dt))return;
        while(dt>0){float step=Math.Min(dt,.05f);Advance(step);dt-=step;if(Finished)break;}
    }
    private void Advance(float dt)
    {
        Time+=dt;CheerCooldown=Math.Max(0,CheerCooldown-dt);if(Countdown>0)return;
        foreach(var racer in Entrants)
        {
            racer.ReactionTime=Math.Max(0,racer.ReactionTime-dt);if(racer.ReactionTime==0){racer.Reaction="";racer.Boost=0;}
            if(racer.FinishedAt>=0)continue;
            var (section,part)=Course.Locate(racer.Distance);var surface=Course.Surfaces[section];
            racer.Decision-=dt;
            if(racer.Decision<=0)
            {
                racer.Decision=2.5f+(float)racer.Random.NextDouble()*2;
                if(racer.Random.NextDouble()<.025f*(1-racer.Pet.Luck/1200f)){racer.Delay=.25f+(float)racer.Random.NextDouble()*.3f;racer.Reaction="stumble";racer.ReactionTime=.8f;}
            }
            float tactic=1+(racer.Pet.Initiative/990f-.5f)*.08f;
            if(racer.Fatigue/racer.Capacity<.3f)tactic=1-(.05f+racer.Pet.Intelligence/990f*.05f);
            float target=Pace(racer.Pet,surface,racer.Fatigue/racer.Capacity)*racer.Form*tactic;
            if(surface==RaceSurface.Rest)target*=.8f;
            if(racer.Delay>0){racer.Delay=Math.Max(0,racer.Delay-dt);target*=.15f;}
            target*=1+racer.Boost;
            racer.Speed+=(target-racer.Speed)*(1-MathF.Exp(-4*dt));float previous=racer.Distance;racer.Distance=Math.Min(Course.Length,racer.Distance+racer.Speed*dt);
            racer.Fatigue=Math.Clamp(racer.Fatigue+dt*(surface==RaceSurface.Rest ? 5 : -(.65f+racer.Speed*.38f)*(1-racer.Pet.Intelligence/990f*.20f)),0,racer.Capacity);
            if(racer.Distance>=Course.Length)racer.FinishedAt=Time-3-dt+(Course.Length-previous)/Math.Max(.001f,racer.Speed);
        }
        int place=1;foreach(var racer in Standings)
        {
            if(racer.LastPosition>0 && place<racer.LastPosition && racer.ReactionTime<=0){racer.Reaction="overtake";racer.ReactionTime=1;}
            racer.LastPosition=place;racer.Place=place++;
        }
        if(Entrants.All(e=>e.FinishedAt>=0) || Time>=185)
        {Finished=true;foreach(var racer in Entrants){racer.Reaction=racer.Place==1 ? "win" : "loss";racer.ReactionTime=10;}}
    }
}
