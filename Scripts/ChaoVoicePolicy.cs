using System;
using System.Collections.Generic;
using System.Linq;

namespace Mori;

public enum ChaoVocalKind { Neutral, Happy, Excited, Curious, Sad, Frustrated, Startled, SleepBreath, Snore }
internal enum ChaoVocalPriority { Sleep=1, Ambient=2, Emotional=3, Important=4, Animation=5 }
public sealed record ChaoVoiceProfile(float BasePitchMultiplier,float ChattyMultiplier,float EnergeticAdjustment,float CuriousAdjustment)
{
    internal static uint Hash(string id){uint h=2166136261;foreach(char c in "MoriGarden/voice/v1/"+id){h=unchecked((h^(byte)c)*16777619);h=unchecked((h^(byte)(c>>8))*16777619);}return h;}
    public static ChaoVoiceProfile For(string id,ChaoPersonality? p)=>new(.94f+.12f*(Hash(id)/(float)uint.MaxValue),1-.20f*T(p?.Chatty),1+.08f*T(p?.Energetic),1+.06f*T(p?.Curious));
    private static float T(int? x)=>Math.Clamp((x ?? 0)/100f,-1,1);
    public float Pitch(ChaoVocalKind kind,float variation=0)=>Math.Clamp(BasePitchMultiplier*(1+Math.Clamp(variation,-.025f,.025f))*
        (kind is ChaoVocalKind.Happy or ChaoVocalKind.Excited ? EnergeticAdjustment : kind==ChaoVocalKind.Curious ? CuriousAdjustment : 1),.80f,1.35f);
}
internal sealed record ChaoVocalSignal(ChaoVocalKind Kind,ChaoVocalPriority Priority,string Source,double At);
// Accepted Mood events only. Private PetState runtime ownership; editor rebind carries this same bounded mailbox.
internal sealed class ChaoVocalSignals
{
    private readonly List<ChaoVocalSignal> pending=new();
    private readonly HashSet<string> seen=new(StringComparer.Ordinal);
    private readonly Queue<string> order=new();
    internal int Count=>pending.Count;
    internal void Accept(ChaoStateEvent kind,string source,string? contact,double now)
    {
        ChaoVocalKind? voice=kind switch {
            ChaoStateEvent.MealFinished or ChaoStateEvent.PlayerContact or ChaoStateEvent.PositiveSocial or ChaoStateEvent.DiscoveryFinished=>ChaoVocalKind.Happy,
            ChaoStateEvent.PlayFinished=>ChaoVocalKind.Excited,ChaoStateEvent.DiscoveryNoticed=>ChaoVocalKind.Curious,
            ChaoStateEvent.UnpleasantContact or ChaoStateEvent.WakeDisturbed=>ChaoVocalKind.Frustrated,ChaoStateEvent.Frightened=>ChaoVocalKind.Startled,_=>null};
        if(voice==null)return;
        Expire(now);string key=kind+":"+(contact ?? source+"@"+now.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
        if(!seen.Add(key))return;order.Enqueue(key);while(order.Count>128)seen.Remove(order.Dequeue());
        if(pending.Count==8)pending.RemoveAt(0);
        pending.Add(new(voice.Value,kind is ChaoStateEvent.Frightened or ChaoStateEvent.UnpleasantContact or ChaoStateEvent.WakeDisturbed ? ChaoVocalPriority.Important : ChaoVocalPriority.Emotional,kind+":"+source,now));
    }
    internal void Expire(double now)=>pending.RemoveAll(s=>now-s.At>3 || now<s.At);
    internal ChaoVocalSignal? Take(double now,bool importantOnly=false){Expire(now);var s=pending.Where(s=>!importantOnly || s.Priority==ChaoVocalPriority.Important).OrderByDescending(s=>s.Priority).ThenByDescending(s=>s.At).FirstOrDefault();if(s!=null)pending.Clear();return s;}
    internal void Clear()=>pending.Clear();
}
public sealed record ChaoVocalDiagnostic(string StableChaoId,float BasePitchMultiplier,float ChattyMultiplier,float EnergeticVocalAdjustment,
    string CurrentVocalKind,string LastVocalKind,string LastVocalSource,int CurrentPriority,double TimeUntilNextAmbientVoice,
    double EmotionalCooldownRemaining,string CurrentAudioChannel,bool WhetherVocalIsPlaying,string SuppressionReason,int NumberOfPendingSignals,
    int AmbientCount,int EmotionalCount,int SpecialCount,int SleepCount,int PriorityConflicts,float MaximumPitch);
internal sealed record ChaoVoiceDecision(ChaoVocalKind Kind,ChaoVocalPriority Priority,string Source,float Pitch,float Duration);
// One executor's pure scheduling/arbitration. No gameplay writes and no shared random stream.
internal sealed class ChaoVoicePolicy
{
    internal const double MinimumAmbientInterval=15,MinimumEmotionalInterval=4,SameReactionCooldown=10;
    private readonly Random random;
    private readonly Dictionary<ChaoVocalKind,double> lastReaction=new();
    private bool initialized,wasSleeping;
    private double now,nextAmbient,lastEmotion=double.NegativeInfinity,busyUntil,lastAmbient=double.NegativeInfinity;
    private ChaoVoiceDecision? current,last;
    private int ambient,emotional,special,sleep,conflicts;
    private float maxPitch;
    internal string Id {get;}
    internal string Suppression {get;private set;}="Not stepped";
    internal ChaoVoiceProfile Profile {get;private set;}
    internal ChaoVoicePolicy(string id,int? seed=null){Id=id;random=new(seed ?? unchecked((int)ChaoVoiceProfile.Hash(id)));Profile=ChaoVoiceProfile.For(id,null);}
    internal double Interval(ChaoPersonality? p)=>Math.Max(15,(19+20*random.NextDouble())*ChaoVoiceProfile.For(Id,p).ChattyMultiplier);
    internal static ChaoVocalKind? FromMood(ChaoEmotion emotion)=>emotion switch {
        ChaoEmotion.Happy=>ChaoVocalKind.Happy,ChaoEmotion.Excited=>ChaoVocalKind.Excited,ChaoEmotion.Curious=>ChaoVocalKind.Curious,
        ChaoEmotion.Sad or ChaoEmotion.Lonely=>ChaoVocalKind.Sad,ChaoEmotion.Frustrated=>ChaoVocalKind.Frustrated,ChaoEmotion.Afraid=>ChaoVocalKind.Startled,
        ChaoEmotion.Sleepy=>null,_=>ChaoVocalKind.Neutral};
    internal static float Duration(ChaoVocalKind kind)=>kind switch {ChaoVocalKind.Startled=>.24f,ChaoVocalKind.SleepBreath or ChaoVocalKind.Snore=>.82f,ChaoVocalKind.Frustrated=>.55f,ChaoVocalKind.Sad=>.36f,_=>.36f};
    internal ChaoVoiceDecision? Request(ChaoVocalKind kind,ChaoVocalPriority priority,string source,ChaoPersonality? p)
    {
        Profile=ChaoVoiceProfile.For(Id,p);
        if(!initialized){initialized=true;nextAmbient=now+Interval(p);}
        if(now<busyUntil && current!=null && priority!=ChaoVocalPriority.Animation){conflicts++;Suppression="Voice finishing / priority protected";return null;}
        if(priority is ChaoVocalPriority.Emotional or ChaoVocalPriority.Important && (now-lastEmotion<4 || lastReaction.TryGetValue(kind,out double at) && now-at<10))
        {Suppression="Emotional/same-reaction cooldown";return null;}
        if(priority==ChaoVocalPriority.Ambient && lastReaction.TryGetValue(kind,out double repeated) && now-repeated<10)
        {Suppression="Same-reaction cooldown";return null;}
        if(priority==ChaoVocalPriority.Ambient && now-lastAmbient<15){Suppression="Ambient minimum interval";return null;}
        var d=new ChaoVoiceDecision(kind,priority,source,Profile.Pitch(kind,(float)(random.NextDouble()-.5)*.04f),Duration(kind));
        current=last=d;busyUntil=now+d.Duration/d.Pitch;maxPitch=Math.Max(maxPitch,d.Pitch);Suppression="";
        if(priority==ChaoVocalPriority.Ambient){ambient++;lastAmbient=now;}
        else if(priority==ChaoVocalPriority.Animation)special++;
        else if(priority==ChaoVocalPriority.Sleep)sleep++;
        else {emotional++;lastEmotion=now;}
        if(priority is ChaoVocalPriority.Ambient or ChaoVocalPriority.Emotional or ChaoVocalPriority.Important)lastReaction[kind]=now;
        return d;
    }
    internal ChaoVoiceDecision? Step(float dt,ChaoPersonality? p,ChaoMoodSnapshot mood,bool sleeping,bool snore,bool allowed,string reason,ChaoVocalSignals signals,double eventNow,bool importantAllowed=false)
    {
        if(!float.IsFinite(dt) || dt<0)throw new ArgumentException("Invalid vocal interval");
        now+=dt;Profile=ChaoVoiceProfile.For(Id,p);if(now>=busyUntil)current=null;Suppression="";signals.Expire(eventNow);
        if(!initialized || sleeping!=wasSleeping)
        {bool transition=initialized;initialized=true;wasSleeping=sleeping;nextAmbient=now+(sleeping ? .8 : Interval(p));if(transition || sleeping)signals.Clear();current=null;busyUntil=now;Suppression="State transition warm-up";return null;}
        if(sleeping)
        {
            signals.Clear();if(!allowed){Suppression=reason;return null;}
            if(now<nextAmbient)return null;
            nextAmbient=now+(snore ? 3.5+random.NextDouble()*1.5 : 5+random.NextDouble()*3);
            return Request(snore ? ChaoVocalKind.Snore : ChaoVocalKind.SleepBreath,ChaoVocalPriority.Sleep,"sleep",p);
        }
        if(now<busyUntil){Suppression="Existing voice owns channel";return null;}
        var signal=allowed ? signals.Take(eventNow) : importantAllowed ? signals.Take(eventNow,true) : null;
        if(signal!=null){var d=Request(signal.Kind,signal.Priority,signal.Source,p);if(d!=null)return d;}
        if(!allowed){Suppression=reason;if(now>=nextAmbient)nextAmbient=now+Interval(p);return null;}
        if(now<nextAmbient)return null;
        nextAmbient=now+Interval(p);var kind=FromMood(mood.DominantEmotion);
        if(kind==null){Suppression="Awake Sleepy";return null;}
        return Request(kind.Value,ChaoVocalPriority.Ambient,"ambient mood",p);
    }
    internal ChaoVocalDiagnostic Diagnostic(int pending)=>new(Id,Profile.BasePitchMultiplier,Profile.ChattyMultiplier,Profile.EnergeticAdjustment,
        current?.Kind.ToString() ?? "None",last?.Kind.ToString() ?? "None",last?.Source ?? "None",current==null ? 0 : (int)current.Priority,
        Math.Max(0,nextAmbient-now),Math.Max(0,4-(now-lastEmotion)),"ChaoVoice",current!=null,Suppression,pending,ambient,emotional,special,sleep,conflicts,maxPitch);
}
