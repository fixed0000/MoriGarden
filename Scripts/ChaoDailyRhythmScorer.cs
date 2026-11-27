using System;
using System.Linq;

namespace Mori;

public sealed record GardenRhythmContext(double GardenSeconds,string AreaId,string StableChaoId,bool UseGardenTime,double PersonalPhaseOffset=0)
{
    public bool Enabled=>UseGardenTime && AreaId is "garden" or "coastal-cove" && double.IsFinite(GardenSeconds) && !string.IsNullOrWhiteSpace(StableChaoId);
    // FNV-1a over explicitly ordered UTF-16 LE bytes, independent of runtime hashes.
    public static double OffsetFor(string id)
    {
        if(string.IsNullOrWhiteSpace(id))return 0;
        uint hash=2166136261;
        foreach(char c in id){hash=unchecked((hash^(byte)c)*16777619);hash=unchecked((hash^(byte)(c>>8))*16777619);}
        return -20+40*(hash/(double)uint.MaxValue);
    }
    public static GardenRhythmContext ForPet(double seconds,string area,string id,bool enabled=true)=>new(seconds,area,id,enabled,OffsetFor(id));
}
public sealed record DailyRhythmScore(ChaoActivity Activity,float Adjustment,float DawnContribution,float DayContribution,float DuskContribution,float NightContribution,string Reason);
public sealed record NightRestEligibility(bool Allowed,string Reason);
public static class ChaoDailyRhythmScorer
{
    public static DailyRhythmScore Evaluate(ChaoActivity activity,GardenRhythmContext? context)
    {
        if(context is not {Enabled:true})return new(activity,0,0,0,0,0,"No active garden time context");
        var w=GardenDayCycle.Evaluate(context.GardenSeconds+Math.Clamp(context.PersonalPhaseOffset,-20,20)).Weights;
        (float dawn,float day,float dusk,float night)=activity switch
        {ChaoActivity.Sleep=>(.025f,0,.035f,.080f),ChaoActivity.Play=>(.020f,.050f,.015f,-.025f),
            ChaoActivity.Socialize=>(.010f,.030f,.045f,-.015f),ChaoActivity.Leisure=>(.050f,.035f,.025f,-.025f),_=>(0,0,0,0)};
        float a=w.Dawn*dawn,b=w.Day*day,c=w.Dusk*dusk,d=w.Night*night;
        return new(activity,Math.Clamp(a+b+c+d,-.05f,.08f),a,b,c,d,"Personal phase weights; existing needs and availability remain authoritative");
    }
    public static NightRestEligibility RestEligibility(float energy,GardenRhythmContext? context)
    {
        if(energy<58)return new(true,"Ordinary fatigue: Energy < 58");
        if(energy>75)return new(false,"Not tired enough: Energy > 75");
        if(context is not {Enabled:true})return new(false,"Ordinary daytime threshold: Energy must be < 58");
        return GardenDayCycle.Evaluate(context.GardenSeconds).NightIntensity>=.60f
            ? new(true,"Mild night fatigue: Energy <= 75 and actual night >= 0.60")
            : new(false,"Actual night < 0.60: Energy must be < 58");
    }
    public static object Capture(PetState pet,GardenRhythmContext? context)=>new
    {
        active=context?.Enabled ?? false, gardenSeconds=context?.GardenSeconds,areaId=context?.AreaId,
        cycle=context is {Enabled:true} ? GardenDayCycle.Evaluate(context.GardenSeconds) : null,
        personalPhaseOffset=context?.PersonalPhaseOffset ?? 0,energy=pet.Energy,
        restEligibility=RestEligibility(pet.Energy,context),safePlace=pet.GardenSleepAvailability,
        adjustments=Enum.GetValues<ChaoActivity>().Select(a=>Evaluate(a,context)).ToArray(),
        habits=pet.ActivityHabitDiagnostics()
    };
}
