using System;

namespace Mori;

public enum GardenDayPhase { Dawn, Day, Dusk, Night }
public readonly record struct GardenPhaseWeights(float Dawn,float Day,float Dusk,float Night);
public sealed record GardenDaySnapshot(double CyclePosition,GardenDayPhase CurrentPhase,float DaylightIntensity,
    float NightIntensity,GardenPhaseWeights Weights,float Angle,float Altitude,float Twilight);

// The existing sky's unequal solar arcs; behavior shares its clock, never owns one.
public static class GardenDayCycle
{
    public const double Period=600,ClockOffset=145,BlendWindow=20;
    public static double GetCyclePosition(double seconds)
    {
        if(!double.IsFinite(seconds))throw new ArgumentException("Invalid garden time");
        return ((seconds%Period+ClockOffset)%Period+Period)%Period;
    }
    private static float Smooth(float start,float end,float value)
    {float t=Math.Clamp((value-start)/(end-start),0,1);return t*t*(3-2*t);}
    public static GardenDayPhase GetPhase(double seconds)=>Evaluate(seconds).CurrentPhase;
    public static GardenDaySnapshot Evaluate(double seconds)
    {
        double position=GetCyclePosition(seconds);float cycle=(float)position;
        float fraction=cycle<410 ? .5f*cycle/410 : .5f+.5f*(cycle-410)/190;
        float angle=MathF.Tau*fraction,altitude=MathF.Sin(angle);
        float light=Smooth(-.07f,.09f,altitude),twilight=1-Smooth(.012f,.13f,MathF.Abs(altitude));
        GardenDayPhase phase=position<20 || position>=580 ? GardenDayPhase.Dawn : position<390 ? GardenDayPhase.Day : position<430 ? GardenDayPhase.Dusk : GardenDayPhase.Night;
        var weights=phase switch
        {GardenDayPhase.Dawn=>new GardenPhaseWeights(1,0,0,0),GardenDayPhase.Day=>new(0,1,0,0),GardenDayPhase.Dusk=>new(0,0,1,0),_=>new(0,0,0,1)};
        // Short twilight plateaus centered on the existing horizon crossings.
        if(position is >=0 and <=40){float t=Smooth(0,40,cycle);weights=new(1-t,t,0,0);}
        else if(position is >=370 and <=410){float t=Smooth(370,410,cycle);weights=new(0,1-t,t,0);}
        else if(position is >=410 and <=450){float t=Smooth(410,450,cycle);weights=new(0,0,1-t,t);}
        else if(position>=560){float t=Smooth(560,600,cycle);weights=new(t,0,0,1-t);}
        return new(position,phase,light,1-light,weights,angle,altitude,twilight);
    }
}
