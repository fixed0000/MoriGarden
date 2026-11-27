using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mori;

internal static class ChaoIdleGestureChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var idle=new IdleGestureInput(ChaoPoseKind.Idle);var mood=new ChaoMoodSnapshot(0,.5f,ChaoEmotion.Neutral,0);var all=new ChaoPersonality{Curious=100,Energetic=100,Carefree=100,Solitude=100};
        var c=new ChaoIdleGestureController("main",7);
        check(c.Update(0,new(ChaoPoseKind.Walk),all,mood)==default && c.Current==ChaoIdleGesture.None,"Idle gesture A/C: no offsets outside Idle");
        c=new("main",7);for(int frame=0;frame<60;frame++)c.Update(frame/30d,idle,all,mood);
        check(c.Started==0 && c.Offsets==default,"Idle gesture B: minimum two seconds before gesture/posture");
        for(int frame=60;frame<3000;frame++)c.Update(frame/30d,idle,all,mood);
        check(c.Started>0,"Idle gesture B: eligible Idle eventually selects real gestures");
        foreach(var pose in Enum.GetValues<ChaoPoseKind>().Where(p=>p!=ChaoPoseKind.Idle))
            check(c.Update(101+ (int)pose,new(pose),all,mood)==default && c.Diagnostic.Suppressed,"Idle gesture D/E/F: protected "+pose);
        check(new[]{idle with{Held=true},idle with{Special=true},idle with{Physical=true}}.All(input=>new ChaoIdleGestureController("blocked").Update(0,input,all,mood)==default),"Idle gesture: held, authored action and physical transition reject all new offsets");
        check(ChaoIdleGestureController.Profile(new(){Curious=100}).LookChance==.35f && ChaoIdleGestureController.Profile(new(){Curious=-100}).LookChance==.1f,"Idle gesture G: exact Curious .10/.35 chance");
        check(Math.Abs(ChaoIdleGestureController.Profile(new(){Energetic=100}).PawChance-.30f)<.000001 && ChaoIdleGestureController.Profile(new(){Energetic=-100}).PawChance==.08f,"Idle gesture H: exact Energetic .08/.30 chance");
        check(Math.Abs(ChaoIdleGestureController.Profile(new(){Carefree=100}).SwayChance-.30f)<.000001 && ChaoIdleGestureController.Profile(new(){Carefree=-100}).SwayChance==.10f,"Idle gesture I: exact Carefree .10/.30 chance");
        c=new("solitude");for(int f=0;f<600;f++)c.Update(f/60d,idle,all,mood);
        check(Math.Abs(c.Offsets.Solitude-.04f)<.00001 && ChaoIdleGestureController.Profile(new()).SolitudeAdjustment==0,"Idle gesture J: bounded +.04 static Solitude, zero neutral adjustment");
        var alone=ChaoIdleGestureController.Profile(new(){Solitude=100});var baseline=ChaoIdleGestureController.Profile(new());
        check(alone.LookChance==baseline.LookChance && alone.PawChance==baseline.PawChance && alone.SwayChance==baseline.SwayChance,"Idle gesture J: Solitude never changes gesture probabilities");
        var profile=ChaoIdleGestureController.Profile(all);bool bounded=true;
        foreach(var g in Enum.GetValues<ChaoIdleGesture>())for(int f=0;f<=1000;f++)
        {var v=ChaoIdleGestureController.Sample(g,profile,f/1000d*2.5,2.5);bounded&=Math.Abs(v.HeadYaw)<=.120001 && Math.Abs(v.HeadPitch)<=.040001 && Math.Abs(v.LeftPaw)<=.100001 && Math.Abs(v.RightPaw)<=.100001 && Math.Abs(v.Sway)<=.045001;}
        check(bounded,"Idle gesture K: all physical angle limits across full envelopes");
        check(Enum.GetValues<ChaoIdleGesture>().All(g=>ChaoIdleGestureController.Sample(g,profile,0,2.5)==default && ChaoIdleGestureController.Sample(g,profile,2.5,2.5)==default),"Idle gesture L: zero boundary offsets");
        c.Cancel(10,"Interaction");check(c.Current==ChaoIdleGesture.None && c.Offsets==default && c.Diagnostic.CurrentGestureAmplitude==0,"Idle gesture N: cancellation clears offsets immediately");
        c.Update(11,idle,all,mood);check(c.Started>0 && c.Current==ChaoIdleGesture.None,"Idle gesture N: returning to Idle does not resume old gesture");
        foreach(var emotion in new[]{ChaoEmotion.Afraid,ChaoEmotion.Frustrated,ChaoEmotion.Sad})
            foreach(float intensity in new[]{.2f,.6f,1})check(ChaoIdleGestureController.Suppression(idle,mood with{DominantEmotion=emotion,EmotionIntensity=intensity}).Length>0,"Idle gesture O: specified mood suppression "+emotion+" / "+intensity);
        var attentionController=new ChaoIdleGestureController("attention",5);bool lookBlocked=true,pawBlocked=true;
        for(int f=0;f<1000;f++)
        {
            var v=attentionController.Update(f*.1,idle with{Attention=true},all,mood);lookBlocked&=v.HeadYaw==0 && v.HeadPitch==0 && attentionController.Current!=ChaoIdleGesture.LookAround;
        }
        var sleepyController=new ChaoIdleGestureController("sleepy",5);for(int f=0;f<1000;f++){sleepyController.Update(f*.1,idle,all,mood with{DominantEmotion=ChaoEmotion.Sleepy,EmotionIntensity=.5f});pawBlocked&=sleepyController.Current!=ChaoIdleGesture.PawFidget;}
        check(lookBlocked,"Idle gesture: a real attention target forbids additional head scanning");
        check(pawBlocked,"Idle gesture: awake Sleepy never initiates PawFidget");
        List<string> Sequence(string id)
        {
            var controller=new ChaoIdleGestureController(id);var result=new List<string>();int prior=0;
            for(int f=0;f<1800;f++){controller.Update(f/10d,idle,all,mood);if(controller.Started!=prior){result.Add(controller.Current+"@"+controller.LastStartedAt.ToString("R"));prior=controller.Started;}}
            return result;
        }
        check(Sequence("same-id").SequenceEqual(Sequence("same-id")) && !Sequence("a").SequenceEqual(Sequence("b")),"Idle gesture P/Q: reproducible stable identity, separate actor streams");
        c=new("pause",4);double time=0;while(c.Current==ChaoIdleGesture.None){c.Update(time,idle,all,mood);time+=.01;}
        var frozen=c.Offsets;double before=c.Diagnostic.GestureElapsed;for(int f=0;f<100;f++)checkPause(c.Update(time+f*.1,idle with{Paused=true},all,mood)==frozen);
        void checkPause(bool ok){if(!ok)throw new Exception("Paused gesture advanced");}
        check(c.Diagnostic.GestureElapsed==before,"Idle gesture: paused offsets and visual elapsed time freeze");
        c.Update(time+10,idle,all,mood);check(c.Diagnostic.GestureElapsed-before<.021,"Idle gesture: resume continues without elapsed-time catch-up");
        var retained=c.Diagnostic;var retainedOffsets=c.Offsets;c.Update(time+10,idle,all.Copy(),mood);
        check(c.Diagnostic.GestureElapsed==retained.GestureElapsed && c.Current.ToString()==retained.CurrentIdleGesture && c.Offsets==retainedOffsets,"Idle gesture U: replacement personality data does not reset active controller");
        check(c.Update(time+11,new(ChaoPoseKind.Sleep,Paused:true),all,mood)==default,"Idle gesture: physical protection still overrides frozen decorative offsets");
        var atFps=new List<IdleGestureOffsets>();foreach(int fps in new[]{30,60,120})
        {c=new("fps",43);for(int f=0;f<=fps*80;f++)c.Update(f/(double)fps,idle,all,mood);atFps.Add(c.Offsets);}
        check(atFps.All(o=>Math.Abs(o.HeadYaw-atFps[0].HeadYaw)<.00001 && Math.Abs(o.HeadPitch-atFps[0].HeadPitch)<.00001 && Math.Abs(o.LeftPaw-atFps[0].LeftPaw)<.00001 && Math.Abs(o.RightPaw-atFps[0].RightPaw)<.00001 && Math.Abs(o.Sway-atFps[0].Sway)<.00001 && Math.Abs(o.Solitude-atFps[0].Solitude)<.00001),"Idle gesture: 30/60/120 FPS give same scheduled phase and amplitudes");
        var pet=new PetState{Personality=all,PersonalitySchemaVersion=1};pet.EnsureStatisticsTree();pet.Validate();string saved=JsonSerializer.Serialize(pet);var snapshot=pet.Mood.Snapshot;
        c=new("invariance");for(int f=0;f<10000;f++)c.Update(f/60d,idle,pet.Personality,pet.Mood.Snapshot);
        check(saved==JsonSerializer.Serialize(pet) && snapshot==pet.Mood.Snapshot,"Idle gesture R/S/T: 10000 updates preserve all personality, needs, mood, history, habits and JSON fields");
        var neutral=ChaoIdleGestureController.Profile(new());var other=new ChaoPersonality();foreach(var property in typeof(ChaoPersonality).GetProperties().Where(p=>p.Name is not ("Curious" or "Energetic" or "Carefree" or "Solitude")))property.SetValue(other,100);
        check(neutral==ChaoIdleGestureController.Profile(other),"Idle gesture: other nine traits add no new gesture effects");
        Statistics(check,idle,mood);
    }
    private static void Statistics(Action<bool,string> check,IdleGestureInput idle,ChaoMoodSnapshot mood)
    {
        var totals=new Dictionary<string,int[]>();var rows=new List<object>();
        foreach(string name in new[]{"Curious","Energetic","Carefree","Solitary"})
        {
            var p=new ChaoPersonality();typeof(ChaoPersonality).GetProperty(name=="Solitary" ? "Solitude" : name)!.SetValue(p,100);
            var counts=new int[4];double gapTotal=0,minGap=999,maxDuration=0,minDuration=999;int gaps=0;
            for(int trial=0;trial<1000;trial++)
            {
                var c=new ChaoIdleGestureController("controlled",trial);int started=0;var history=new List<ChaoIdleGesture>();
                for(int step=0;step<=180;step++)
                {
                    c.Update(step*.5,idle,p,mood);
                    if(c.Started==started)continue;started=c.Started;counts[(int)c.Current]++;history.Add(c.Current);
                    if(c.LastStartedAt<2 || c.Diagnostic.GestureDuration<1 || c.Diagnostic.GestureDuration>2.5)throw new Exception("Gesture timing escaped limits");
                    minDuration=Math.Min(minDuration,c.Diagnostic.GestureDuration);maxDuration=Math.Max(maxDuration,c.Diagnostic.GestureDuration);
                    if(double.IsFinite(c.LastCompletedAt)){double gap=c.LastStartedAt-c.LastCompletedAt;minGap=Math.Min(minGap,gap);gapTotal+=gap;gaps++;}
                    if(history.Count>=3 && history[^1]==history[^2] && history[^2]==history[^3])throw new Exception("Three consecutive identical gestures");
                }
            }
            totals[name]=counts;rows.Add(new{name,intervals=1000,secondsPerInterval=90,counts,minGap,meanGap=gapTotal/gaps,minDuration,maxDuration});
            check(counts.Skip(1).All(n=>n>0) && minGap>=6 && maxDuration<=2.5 && minDuration>=1,"Idle gesture statistics: 1000 independent intervals / "+name+"; alternatives, durations and cooldown retained");
        }
        check(totals["Curious"][1]>totals["Energetic"][1] && totals["Curious"][1]>totals["Carefree"][1],"Idle gesture statistics: Curious favors actual LookAround counts");
        check(totals["Energetic"][2]>totals["Curious"][2] && totals["Energetic"][2]>totals["Carefree"][2],"Idle gesture statistics: Energetic favors actual PawFidget counts");
        check(totals["Carefree"][3]>totals["Curious"][3] && totals["Carefree"][3]>totals["Energetic"][3],"Idle gesture statistics: Carefree favors actual GentleSway counts");
        string? output=Environment.GetEnvironmentVariable("MORI_IDLE_RESULTS");if(output!=null)File.WriteAllText(output,JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
    }
}
