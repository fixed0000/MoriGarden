using System;
using System.Collections.Generic;
using System.Linq;

namespace Mori;

public enum RaceAction { Ready,Move,Fall,Prone,Rise,Finish }
public sealed class RaceEntrant
{
    public string Id {get;}
    public string Color {get;}
    public PetState Pet {get;}
    public float Distance {get;internal set;}
    public float Fatigue {get;internal set;}
    public float Capacity=>30+Pet.Stamina*.10f;
    public float FinishedAt {get;internal set;}=-1;
    public float Speed {get;internal set;}
    public float Delay {get;internal set;}
    public int Place {get;internal set;}
    public string Reaction {get;internal set;}="";
    public RaceAction Action {get;internal set;}
    public float TripAge {get;internal set;}
    public float BoostRemaining {get;internal set;}
    public bool Blocked=>Delay>0;
    internal float ReactionTime,Decision,Form;
    internal readonly Random Random;
    internal int LastPosition;
    public RaceEntrant(string id,string color,PetState pet,int seed){Id=id;Color=color;Pet=pet;Random=new(seed);Form=.98f+(float)Random.NextDouble()*.04f;Fatigue=Capacity*Math.Clamp(pet.Energy/100,0,1);}
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
    public ChaoRace(SportsEvent competition,IEnumerable<RaceEntrant> entrants)
    {
        Course=new(competition);Entrants=entrants.ToList();if(Entrants.Count is <2 or >8 || Entrants.Select(e=>e.Id).Distinct().Count()!=Entrants.Count)throw new ArgumentException("A race needs 2–8 distinct entrants.");
        int place=1;foreach(var racer in Entrants)racer.Place=place++;
    }
    public static float Pace(PetState pet,RaceSurface surface,float staminaFraction)
    {
        int skill=surface switch{RaceSurface.Swim=>pet.Swim,RaceSurface.Fly=>pet.Fly,RaceSurface.Power=>pet.Power,_=>pet.Run};
        float speed=surface switch{RaceSurface.Swim=>.36f+skill*.0042f,RaceSurface.Power=>.19f+skill*.0023f,RaceSurface.Fly=>.65f+skill*.0045f,_=>.65f+skill*.0048f};
        if(surface==RaceSurface.Run && pet.Run<50)speed*=.55f;
        // Running out of race energy removes at least 80% of the normal pace.
        float tired=staminaFraction<=0 ? .18f : .82f+.18f*Math.Clamp(staminaFraction*3,0,1);
        return speed*tired*(.82f+.18f*pet.Energy/100)*(.98f+.02f*pet.Joy/100);
    }
    public bool CanCheer(string id)
    {
        var runner=Entrants.FirstOrDefault(e=>e.Id==id);return !Finished && Countdown<=0 && CheerCooldown<=0 && runner!=null && runner.FinishedAt<0 && runner.Fatigue>=2.5f && !runner.Blocked && Course.TravelSurface(runner.Distance,runner.Pet) is not (RaceSurface.Puzzle or RaceSurface.Choice);
    }
    public bool Cheer(string id)
    {
        if(!CanCheer(id))return false;var runner=Entrants.First(e=>e.Id==id);
        runner.Fatigue=Math.Max(0,runner.Fatigue-2.5f);runner.BoostRemaining=.65f;runner.Reaction="cheer";runner.ReactionTime=.7f;CheerCooldown=.16f;Cheers++;return true;
    }
    public void Trip(RaceEntrant racer){if(racer.FinishedAt>=0)return;racer.Delay=1.8f;racer.TripAge=0;racer.Reaction="stumble";racer.ReactionTime=1.8f;racer.BoostRemaining=0;racer.Action=RaceAction.Fall;racer.Speed=0;}
    public void Step(float dt)
    {
        if(Finished || dt<=0 || !float.IsFinite(dt))return;
        while(dt>0){float step=Math.Min(dt,1f/60);Advance(step);dt-=step;if(Finished)break;}
    }
    private void Advance(float dt)
    {
        Time+=dt;CheerCooldown=Math.Max(0,CheerCooldown-dt);if(Countdown>0)return;
        foreach(var racer in Entrants)
        {
            racer.ReactionTime=Math.Max(0,racer.ReactionTime-dt);if(racer.ReactionTime==0)racer.Reaction="";
            if(racer.FinishedAt>=0){racer.Action=RaceAction.Finish;continue;}
            racer.Fatigue=Math.Max(0,racer.Fatigue-dt*(.34f+racer.Speed*.085f)*(racer.BoostRemaining>0 ? 2.4f : 1));
            if(racer.Blocked){racer.Delay=Math.Max(0,racer.Delay-dt);racer.TripAge+=dt;racer.Action=racer.TripAge<.35f ? RaceAction.Fall : racer.TripAge<1.2f ? RaceAction.Prone : RaceAction.Rise;racer.Speed=0;continue;}
            var surface=Course.TravelSurface(racer.Distance,racer.Pet);racer.Decision-=dt;
            if(racer.Decision<=0)
            {
                racer.Decision=4+(float)racer.Random.NextDouble()*3;
                if(surface==RaceSurface.Run && racer.Random.NextDouble()<.045f*(1-racer.Pet.Luck/1100f)){Trip(racer);continue;}
            }
            float pace=Pace(racer.Pet,surface,racer.Fatigue/racer.Capacity)*racer.Form*(.985f+racer.Pet.Initiative/990f*.03f);
            if(racer.BoostRemaining>0 && racer.Fatigue>0)pace*=1.65f;
            racer.BoostRemaining=Math.Max(0,racer.BoostRemaining-dt);racer.Action=RaceAction.Move;
            racer.Speed+=(pace-racer.Speed)*(1-MathF.Exp(-6*dt));float previous=racer.Distance;racer.Distance=Math.Min(Course.Length,racer.Distance+racer.Speed*dt);
            if(racer.Distance>=Course.Length){racer.FinishedAt=Time-3-dt+(Course.Length-previous)/Math.Max(.001f,racer.Speed);racer.Action=RaceAction.Finish;}
        }
        int place=1;foreach(var racer in Standings){if(racer.LastPosition>0 && place<racer.LastPosition && racer.ReactionTime<=0){racer.Reaction="overtake";racer.ReactionTime=.6f;}racer.LastPosition=place;racer.Place=place++;}
        if(Entrants.All(e=>e.FinishedAt>=0) || Time>=420)Finished=true;
    }
}
