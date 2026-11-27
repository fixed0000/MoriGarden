using System;

namespace Mori;

public enum ChaoIdleGesture { None, LookAround, PawFidget, GentleSway }
internal readonly record struct IdleGestureInput(ChaoPoseKind Pose,bool Held=false,bool Special=false,bool Physical=false,bool Attention=false,bool Paused=false);
public sealed record IdleGestureProfile(float LookChance,float PawChance,float SwayChance,float YawAmplitude,float PitchAmplitude,float PawAmplitude,float SwayAmplitude,float SolitudeAdjustment);
public readonly record struct IdleGestureOffsets(float HeadYaw,float HeadPitch,float LeftPaw,float RightPaw,float Sway,float Solitude);
public sealed record IdleGestureDiagnostic(string CurrentIdleGesture,double GestureElapsed,double GestureDuration,double TimeUntilNextGesture,
    float CurrentGestureAmplitude,float CuriousContribution,float EnergeticContribution,float CarefreeContribution,float SolitudePoseAdjustment,
    string CurrentPose,bool Suppressed,string SuppressionReason,int Started,int Completed);

// Local visual scheduling only. No PetState, game events, Godot, shared RNG or clock.
internal sealed class ChaoIdleGestureController
{
    private readonly Random random;
    private bool eligible;
    private double lastTime=double.NaN,idleSince,nextAt,startAt,duration,pauseAt=double.NaN;
    private float direction=1,period=4,solitude;
    private IdleGestureProfile activeProfile=Profile(null);
    private ChaoIdleGesture previous,penultimate;
    internal ChaoIdleGesture Current { get; private set; }
    internal IdleGestureOffsets Offsets { get; private set; }
    internal IdleGestureDiagnostic Diagnostic { get; private set; }=new("None",0,0,0,0,0,0,0,0,"Unavailable",true,"Not updated",0,0);
    internal int Started { get; private set; }
    internal int Completed { get; private set; }
    internal double LastStartedAt=>startAt;
    internal double LastCompletedAt { get; private set; }=double.NegativeInfinity;
    internal ChaoIdleGestureController(string id,int? seed=null)
    {
        uint hash=2166136261;foreach(char c in "MoriGarden/idle/v1/"+id){hash=unchecked((hash^(byte)c)*16777619);hash=unchecked((hash^(byte)(c>>8))*16777619);}
        random=new(seed ?? unchecked((int)hash));
    }
    private static float Positive(int? value)=>Math.Max(0,Math.Clamp(value ?? 0,-100,100)/100f);
    internal static IdleGestureProfile Profile(ChaoPersonality? p)
    {
        float c=Positive(p?.Curious),e=Positive(p?.Energetic),f=Positive(p?.Carefree),s=Positive(p?.Solitude);
        return new(.10f+.25f*c,.08f+.22f*e,.10f+.20f*f,.12f*c,.04f*c,.04f+.06f*e,.025f+.020f*f,.04f*s);
    }
    internal static string Suppression(IdleGestureInput input,ChaoMoodSnapshot? mood)
    {
        if(input.Pose!=ChaoPoseKind.Idle)return "Protected "+input.Pose;
        if(input.Held)return "Held";
        if(input.Physical)return "Physical transition or movement";
        if(input.Special)return "Action or emotional animation owns pose";
        if(mood==null)return "No bound mood";
        if(mood.DominantEmotion is ChaoEmotion.Afraid or ChaoEmotion.Frustrated)return "Protective mood: "+mood.DominantEmotion;
        if(mood.DominantEmotion==ChaoEmotion.Sad)return mood.EmotionIntensity>=.60f ? "Strong sadness" : "Sadness: original pose and expression only";
        return "";
    }
    internal void Cancel(double now,string reason="Protected action")
    {Current=ChaoIdleGesture.None;Offsets=default;solitude=0;eligible=false;nextAt=now+2;Diagnostic=Diagnostic with{CurrentIdleGesture="None",GestureElapsed=0,GestureDuration=0,CurrentGestureAmplitude=0,TimeUntilNextGesture=2,SolitudePoseAdjustment=0,Suppressed=true,SuppressionReason=reason};}
    internal static IdleGestureOffsets Sample(ChaoIdleGesture gesture,IdleGestureProfile profile,double elapsed,double seconds,float sign=1,float swayPeriod=4)
    {
        float u=(float)Math.Clamp(elapsed/seconds,0,1),envelope=MathF.Pow(MathF.Sin(MathF.PI*u),2);
        if(u<=0 || u>=1)return default;
        return gesture switch
        {
            ChaoIdleGesture.LookAround=>new(profile.YawAmplitude*envelope*MathF.Sin(MathF.Tau*u)*sign,profile.PitchAmplitude*envelope*MathF.Sin(MathF.PI*u),0,0,0,0),
            ChaoIdleGesture.PawFidget=>new(0,0,profile.PawAmplitude*envelope*MathF.Sin(MathF.Tau*u)*sign,-profile.PawAmplitude*.65f*envelope*MathF.Sin(MathF.Tau*u)*sign,0,0),
            ChaoIdleGesture.GentleSway=>new(0,0,0,0,profile.SwayAmplitude*envelope*MathF.Sin(MathF.Tau*(float)elapsed/swayPeriod)*sign,0),
            _=>default
        };
    }
    internal IdleGestureOffsets Update(double now,IdleGestureInput input,ChaoPersonality? personality,ChaoMoodSnapshot? mood)
    {
        if(!double.IsFinite(now) || now<0 || !double.IsNaN(lastTime) && now<lastTime)throw new ArgumentException("Invalid presentation time");
        double elapsed=double.IsNaN(lastTime) ? 0 : now-lastTime;lastTime=now;
        string reason=Suppression(input,mood);var profile=Profile(personality);
        if(reason.Length>0){Cancel(now,reason);Publish(now,input,profile,reason);return Offsets;}
        if(input.Paused)
        {if(double.IsNaN(pauseAt))pauseAt=now;Diagnostic=Diagnostic with{Suppressed=true,SuppressionReason="Paused"};return Offsets;}
        if(!double.IsNaN(pauseAt)){double gap=now-pauseAt;idleSince+=gap;nextAt+=gap;startAt+=gap;pauseAt=double.NaN;elapsed=0;}
        if(!eligible){eligible=true;idleSince=now;nextAt=now+2+random.NextDouble()*2;}
        bool pawAllowed=mood!.DominantEmotion!=ChaoEmotion.Sleepy;
        if(Current==ChaoIdleGesture.LookAround && input.Attention || Current==ChaoIdleGesture.PawFidget && !pawAllowed)
        {Current=ChaoIdleGesture.None;nextAt=now+6+random.NextDouble()*4;}
        for(int events=0;events<64;events++)
        {
            if(Current!=ChaoIdleGesture.None)
            {
                if(now<startAt+duration)break;
                LastCompletedAt=startAt+duration;Completed++;Current=ChaoIdleGesture.None;
            }
            if(now<nextAt)break;
            double at=nextAt;float look=input.Attention ? 0 : profile.LookChance,paw=pawAllowed ? profile.PawChance : 0;
            double roll=random.NextDouble();ChaoIdleGesture selected=roll<look ? ChaoIdleGesture.LookAround : roll<look+paw ? ChaoIdleGesture.PawFidget : roll<look+paw+profile.SwayChance ? ChaoIdleGesture.GentleSway : ChaoIdleGesture.None;
            if(selected!=ChaoIdleGesture.None && selected==previous && selected==penultimate)
            {
                double sum=(selected!=ChaoIdleGesture.LookAround ? look : 0)+(selected!=ChaoIdleGesture.PawFidget ? paw : 0)+(selected!=ChaoIdleGesture.GentleSway ? profile.SwayChance : 0);
                if(sum>0){double alt=random.NextDouble()*sum;selected=selected!=ChaoIdleGesture.LookAround && (alt-=look)<0 ? ChaoIdleGesture.LookAround : selected!=ChaoIdleGesture.PawFidget && (alt-=paw)<0 ? ChaoIdleGesture.PawFidget : ChaoIdleGesture.GentleSway;}
            }
            if(selected==ChaoIdleGesture.None){nextAt=at+6+random.NextDouble()*4;continue;}
            penultimate=previous;previous=selected;Current=selected;Started++;startAt=at;duration=1+random.NextDouble()*1.5;activeProfile=profile;direction=random.Next(2)==0 ? -1 : 1;period=3.5f+(float)random.NextDouble()*1.5f;nextAt=startAt+duration+6+random.NextDouble()*4;
        }
        double postureTime=Math.Max(0,now-idleSince-2)-Math.Max(0,now-elapsed-idleSince-2);
        solitude+=(profile.SolitudeAdjustment-solitude)*(1-MathF.Exp(-6*(float)postureTime));
        Offsets=(Current==ChaoIdleGesture.None ? default : Sample(Current,activeProfile,now-startAt,duration,direction,period)) with{Solitude=solitude};
        Publish(now,input,profile,"");return Offsets;
    }
    private void Publish(double now,IdleGestureInput input,IdleGestureProfile p,string reason)
    {
        float amplitude=Math.Max(Math.Max(Math.Abs(Offsets.HeadYaw),Math.Abs(Offsets.HeadPitch)),Math.Max(Math.Max(Math.Abs(Offsets.LeftPaw),Math.Abs(Offsets.RightPaw)),Math.Abs(Offsets.Sway)));
        Diagnostic=new(Current.ToString(),Current==ChaoIdleGesture.None ? 0 : Math.Max(0,now-startAt),Current==ChaoIdleGesture.None ? 0 : duration,
            Math.Max(0,nextAt-now),amplitude,
            p.LookChance-.10f,p.PawChance-.08f,p.SwayChance-.10f,solitude,input.Pose.ToString(),reason.Length>0,reason,Started,Completed);
    }
}
