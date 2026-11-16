using System;
using System.Collections.Generic;
using System.Numerics;

namespace Mori;

public enum RaceObstaclePhase { Run, Prepare, EnterWater, Swim, LeaveWater, Takeoff, Glide, FailedFlight, Climb, Mount, Land }

// Transient per-athlete choreography. Distance remains the race result coordinate;
// a gate owns that coordinate until its physical action has finished.
public sealed class RaceTraversal
{
    private readonly record struct Gate(float At,float End,RaceObstaclePhase Phase,float Seconds);
    private readonly List<Gate> gates=new();
    private int next;
    private Gate? active;
    private bool initialized;
    public RaceObstaclePhase Phase {get;private set;}
    public float Age {get;private set;}
    public float Progress=>active is {} gate ? Math.Clamp(Age/gate.Seconds,0,1) : 0;
    public bool OwnsMovement=>active!=null;
    public int CompletedActions {get;private set;}
    public float Travelled {get;private set;}
    public Vector3 Position {get;private set;}
    private void Initialize(RaceCourse course,PetState pet)
    {
        float start=0;
        for(int i=0;i<course.Surfaces.Length;i++)
        {
            float end=start+course.Lengths[i];var surface=course.Surfaces[i];
            if(surface==RaceSurface.Swim && (i==0 || course.Surfaces[i-1]!=surface))gates.Add(new(start-.20f,start+.20f,RaceObstaclePhase.EnterWater,.45f));
            if(surface==RaceSurface.Swim && (i+1==course.Surfaces.Length || course.Surfaces[i+1]!=surface))gates.Add(new(end-.12f,end+.22f,RaceObstaclePhase.LeaveWater,.55f));
            if(surface is RaceSurface.Power or RaceSurface.Fly)
            {
                gates.Add(new(start-.18f,start-.18f,RaceObstaclePhase.Prepare,.36f));
                if(surface==RaceSurface.Fly)
                {
                    gates.Add(new(start-.02f,start+.35f,RaceObstaclePhase.Takeoff,.34f));
                    if(pet.Fly<450)
                    {
                        float landing=start+course.Lengths[i]*course.FlightLanding(pet);
                        gates.Add(new(landing,landing,RaceObstaclePhase.Land,.42f));
                        float wall=start+course.Lengths[i]*.78f;
                        gates.Add(new(wall,wall,RaceObstaclePhase.Prepare,.36f));
                        float top=start+course.Lengths[i]*.95f;
                        gates.Add(new(top-.08f,top+.12f,RaceObstaclePhase.Mount,.60f));
                    }
                    else gates.Add(new(end-.18f,end+.12f,RaceObstaclePhase.Land,.38f));
                }
                else gates.Add(new(end-.20f,end+.18f,RaceObstaclePhase.Mount,.60f));
            }
            start=end;
        }
        gates.Sort((a,b)=>a.At.CompareTo(b.At));initialized=true;
    }
    public float Limit(RaceCourse course,RaceEntrant racer,float proposed)
    {
        if(!initialized)Initialize(course,racer.Pet);
        return next<gates.Count ? Math.Min(proposed,gates[next].At) : proposed;
    }
    public bool Tick(RaceCourse course,RaceEntrant racer,float dt)
    {
        if(!initialized)Initialize(course,racer.Pet);
        if(active==null && next<gates.Count && racer.Distance>=gates[next].At-.0001f){active=gates[next++];Age=0;}
        if(active is not {} gate)
        {
            Phase=course.TravelSurface(racer.Distance,racer.Pet) switch {RaceSurface.Swim=>RaceObstaclePhase.Swim,RaceSurface.Power=>RaceObstaclePhase.Climb,RaceSurface.Fly=>racer.Pet.Fly<450 && course.Sample(racer.Distance).Fraction>course.FlightLanding(racer.Pet)*.65f ? RaceObstaclePhase.FailedFlight : RaceObstaclePhase.Glide,_=>RaceObstaclePhase.Run};
            return false;
        }
        Phase=gate.Phase;Age+=dt;float t=Math.Clamp(Age/gate.Seconds,0,1);float smooth=t*t*(3-2*t);
        racer.Distance=gate.At==gate.End ? gate.At : gate.At+(gate.End-gate.At)*smooth;
        racer.Speed=0;racer.BoostRemaining=0;
        if(t>=1){active=null;CompletedActions++;}
        return true;
    }
    public void Place(RaceCourse course,RaceEntrant racer,float lane)
    {
        var at=course.Position(racer.Distance,lane,racer.Pet);
        if(Phase==RaceObstaclePhase.Takeoff && OwnsMovement)at.Y+=MathF.Sin(Progress*MathF.PI)*.16f;
        Travelled+=Vector3.Distance(Position,at);Position=at;
    }
}
