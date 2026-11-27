using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.IO;
using Mori;

internal static class ChaoVocalChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var neutral=new ChaoMoodSnapshot(0,.5f,ChaoEmotion.Neutral,0);var p=new ChaoPersonality();
        check(ChaoVoiceProfile.For("main",p)==ChaoVoiceProfile.For("main",p),"Vocal A stable ID profile");
        check(Enumerable.Range(0,100).Select(i=>ChaoVoiceProfile.For("pet"+i,p).BasePitchMultiplier).Distinct().Count()>95,"Vocal A distinct identity pitch");
        check(ChaoVoiceProfile.For("a",new(){Energetic=-100}).EnergeticAdjustment==.92f && ChaoVoiceProfile.For("a",new(){Energetic=100}).EnergeticAdjustment==1.08f,"Vocal E exact Energetic range");
        check(ChaoVoiceProfile.For("a",new(){Curious=-100}).CuriousAdjustment==.94f && ChaoVoiceProfile.For("a",new(){Curious=100}).CuriousAdjustment==1.06f,"Vocal F exact Curious range");
        var freqA=new ChaoVoicePolicy("same",14);var freqB=new ChaoVoicePolicy("same",14);bool frequency=true;
        for(int i=0;i<1000;i++)frequency&=freqA.Interval(new(){Energetic=100,Curious=100})==freqB.Interval(new(){Energetic=-100,Curious=-100});
        check(frequency,"Vocal frequency uses Chatty alone, not Energetic or old Curiosity");
        var minInterval=new ChaoVoicePolicy("min",7);check(Enumerable.Range(0,10000).All(_=>minInterval.Interval(new(){Chatty=100})>=15),"Vocal D every sampled interval respects 15s minimum");
        bool pitch=true;foreach(int v in new[]{-100,0,100})foreach(var kind in Enum.GetValues<ChaoVocalKind>())foreach(int id in Enumerable.Range(0,100))
        {var profile=ChaoVoiceProfile.For("pet"+id,new(){Energetic=v,Curious=-v});foreach(float jitter in new[]{-.1f,0,.1f})pitch&=profile.BasePitchMultiplier is >=.94f and <=1.06f && profile.Pitch(kind,jitter) is >=.8f and <=1.35f;}
        check(pitch,"Vocal G pitch bounds every kind/trait extreme");
        var expected=new ChaoVocalKind?[]{ChaoVocalKind.Neutral,ChaoVocalKind.Happy,ChaoVocalKind.Excited,ChaoVocalKind.Curious,ChaoVocalKind.Sad,ChaoVocalKind.Sad,ChaoVocalKind.Frustrated,ChaoVocalKind.Startled,null};
        check(Enum.GetValues<ChaoEmotion>().Select(ChaoVoicePolicy.FromMood).SequenceEqual(expected),"Vocal H all nine mood mappings");
        var state=new PetState{Joy=50,Energy=90,Fullness=90};state.ObserveEvent(ChaoStateEvent.PositiveSocial,"test","one");int count=state.VocalSignals.Count;
        state.ObserveEvent(ChaoStateEvent.PositiveSocial,"test","two");check(count==1 && state.VocalSignals.Count==1,"Vocal I suppressed Mood duplicate adds no signal");
        state.ObserveEvent(ChaoStateEvent.PositiveSocial,"other-source","one");check(state.VocalSignals.Count==1,"Vocal I same logical contact deduplicated across sources");
        state.ObserveEvent(ChaoStateEvent.FoodBite,"bite");state.ObserveEvent(ChaoStateEvent.PlayStarted,"start");check(state.VocalSignals.Count==1,"Vocal I FoodBite/PlayStarted silent");
        var policy=new ChaoVoicePolicy("main",3);policy.Step(0,p,neutral,false,false,true,"",new(),0);
        check(policy.Step(.1f,p,neutral,false,false,true,"",state.VocalSignals,0)?.Kind==ChaoVocalKind.Happy,"Vocal event accepted exactly once");
        for(int i=0;i<8;i++){var mailbox=new ChaoVocalSignals();mailbox.Accept(ChaoStateEvent.PositiveSocial,"new"+i,null,.2+i*.2);check(policy.Step(.2f,p,neutral,false,false,true,"",mailbox,.2+i*.2)==null,"Vocal J cooldown prevents repeated contact voice "+i);}
        var old=new ChaoVocalSignals();old.Accept(ChaoStateEvent.DiscoveryNoticed,"old",null,0);check(old.Take(3.01)==null,"Vocal K stale signal discarded");
        for(int i=0;i<1000;i++)old.Accept(ChaoStateEvent.DiscoveryNoticed,"x"+i,null,0);check(old.Count==8,"Vocal S bounded mailbox");
        old.Clear();check(old.Count==0,"Vocal S clear runtime mailbox");
        var pri=new ChaoVoicePolicy("social",1);pri.Request(ChaoVocalKind.Neutral,ChaoVocalPriority.Animation,"introduction",p);
        check(pri.Request(ChaoVocalKind.Happy,ChaoVocalPriority.Ambient,"ambient",p)==null && pri.Diagnostic(0).LastVocalSource=="introduction","Vocal L animation reply priority");
        var music=new ChaoVoicePolicy("music",1);var box=new ChaoVocalSignals();music.Step(0,p,neutral,false,false,false,"Music",box,0);bool silent=true;
        for(int i=0;i<100;i++)silent&=music.Step(1,p,neutral,false,false,false,"Music",box,i)==null;
        check(silent && music.Diagnostic(0).AmbientCount==0,"Vocal M music blocks ambient");
        music.Step(.01f,p,neutral,false,false,true,"",box,100);
        check(music.Diagnostic(0).SuppressionReason=="","Vocal diagnostics clear an obsolete action suppression immediately");
        var asleep=new ChaoVoicePolicy("sleep",1);var sleepbox=new ChaoVocalSignals();var kinds=new List<ChaoVocalKind>();
        for(int i=0;i<40;i++){sleepbox.Accept(ChaoStateEvent.PlayFinished,"x"+i,null,i);if(asleep.Step(1,p,neutral,true,false,true,"",sleepbox,i) is {} d)kinds.Add(d.Kind);}
        check(kinds.Count>0 && kinds.All(k=>k==ChaoVocalKind.SleepBreath),"Vocal N sleep breathing only");
        sleepbox.Accept(ChaoStateEvent.PlayFinished,"sleep-end",null,40);check(asleep.Step(.1f,p,neutral,false,false,true,"",sleepbox,40)==null && sleepbox.Count==0,"Vocal O wake drops sleep backlog and reschedules");
        check(asleep.Diagnostic(0).TimeUntilNextAmbientVoice>=15,"Vocal O wake fresh minimum delay");
        state.EnsureStatisticsTree();state.EnsurePersonalityTree();var saved=JsonSerializer.Serialize(state);var model=new ChaoVoicePolicy("main");for(int i=0;i<100;i++)model.Step(1,p,state.Mood.Snapshot,false,false,true,"",state.VocalSignals,state.Mood.Now);
        check(saved==JsonSerializer.Serialize(state),"Vocal P/Q no gameplay or save mutation");
        var replacement=JsonSerializer.Deserialize<PetState>(saved)!;replacement.KeepCurrentRuntimeFrom(state);
        check(ReferenceEquals(replacement.VocalSignals,state.VocalSignals),"Vocal R editor rebind carries same mailbox");
        check(!saved.Contains("VoicePitch") && !saved.Contains("VocalSignals") && !saved.Contains("VoiceTimer"),"Vocal Q runtime absent from JSON");
        List<string> Sequence(int seed){var v=new ChaoVoicePolicy("det",seed);var b=new ChaoVocalSignals();var result=new List<string>();for(int i=0;i<600;i++){var d=v.Step(1,p,neutral,false,false,true,"",b,i);if(d!=null)result.Add(i+":"+d);}return result;}
        check(Sequence(5).SequenceEqual(Sequence(5)) && !Sequence(5).SequenceEqual(Sequence(6)),"Vocal T deterministic independent random stream");
        var protect=new ChaoVoicePolicy("protected",1);var protectedSignals=new ChaoVocalSignals();protect.Step(0,p,neutral,false,false,false,"Grip",protectedSignals,0);protectedSignals.Accept(ChaoStateEvent.PlayFinished,"done",null,1);
        check(protect.Step(1,p,neutral,false,false,false,"Grip",protectedSignals,1)==null && protectedSignals.Count==1,"Vocal completed action signal waits briefly for safe pose");
        check(protect.Step(.1f,p,neutral,false,false,true,"",protectedSignals,1.1)?.Kind==ChaoVocalKind.Excited,"Vocal safe completion consumes one pending signal");
        var reloaded=JsonSerializer.Deserialize<PetState>(saved)!;check(reloaded.VocalSignals.Count==0,"Vocal save/load cannot replay runtime events");
        var a=new ChaoVoicePolicy("a",3);var b=new ChaoVoicePolicy("b",3);a.Step(10,p,neutral,false,false,true,"",new(),0);check(b.Diagnostic(0).TimeUntilNextAmbientVoice==0,"Vocal B independent timers");
        var profiles=new[]{new ChaoPersonality{Chatty=-100,Energetic=-50},new ChaoPersonality(),new ChaoPersonality{Chatty=100,Energetic=50,Curious=50}};
        var results=new List<object>();var counts=new double[3];
        for(int profile=0;profile<3;profile++)
        {
            int total=0;double minGap=double.PositiveInfinity,sounding=0;float max=0;var distribution=new Dictionary<string,int>();
            for(int trial=0;trial<1000;trial++)
            {
                var v=new ChaoVoicePolicy("stat",trial);var mb=new ChaoVocalSignals();double last=-1000;
                for(int t=0;t<=600;t++)
                {
                    var mood=neutral with{DominantEmotion=(ChaoEmotion)((t/60)%9)};
                    if(v.Step(t==0 ? 0 : 1,profiles[profile],mood,false,false,true,"",mb,t) is not {} d)continue;
                    if(last>=0)minGap=Math.Min(minGap,t-last);last=t;total++;sounding+=d.Duration/d.Pitch;max=Math.Max(max,d.Pitch);
                    string key=d.Kind.ToString();distribution[key]=distribution.GetValueOrDefault(key)+1;
                }
            }
            counts[profile]=total/1000d;
            check(minGap>=15 && max<=1.35 && sounding/(1000*600)<.06,"Vocal statistics bounds profile "+profile);
            results.Add(new{profile=new[]{"Quiet","Neutral","Vocal"}[profile],periods=1000,seconds=600,meanCount=counts[profile],minGap,maxPitch=max,dutyFraction=sounding/600000,distribution});
        }
        check(counts[2]>counts[1] && counts[1]>counts[0],"Vocal C statistics Chatty ordering 1000 independent 600s periods");
        var outFile=Environment.GetEnvironmentVariable("MORI_VOCAL_RESULTS");if(!string.IsNullOrEmpty(outFile))File.WriteAllText(outFile,JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
    }
}
