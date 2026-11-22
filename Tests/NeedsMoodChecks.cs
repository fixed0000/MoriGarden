using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mori;

internal static class NeedsMoodChecks
{
    private static PetState Normal() => new(){Fullness=80,Energy=80,Fun=80,Joy=50};
    private static NeedPriority Need(PetState p,ChaoNeed kind,NeedActionContext context=NeedActionContext.None) => p.Needs.Evaluate(context).Priorities.Single(n=>n.Need==kind);
    internal static void Run(Action<bool,string> check)
    {
        var p=Normal();
        check(p.Needs.Hunger==20 && p.Needs.Fatigue==20 && p.Needs.Boredom==20 && p.SocialNeed==20,"Needs A: three derived meters and initial social need are twenty");
        p.Fullness=10;check(Need(p,ChaoNeed.Hunger) is {Value:90,Level:NeedUrgency.Critical},"Needs B: strong hunger is ninety and critical");
        p.Energy=15;check(Need(p,ChaoNeed.Fatigue) is {Value:85,Level:NeedUrgency.Critical},"Needs C: low energy produces critical fatigue");
        p.Fun=5;var priorities=p.Needs.Evaluate().Priorities;
        check(priorities.Count==4 && priorities.All(n=>float.IsFinite(n.Weight) && n.Weight is >=0 and <=1) && Need(p,ChaoNeed.Hunger).Weight>Need(p,ChaoNeed.Boredom).Weight,"Needs D: competing urgent needs have finite bounded weights with essential bias");
        p=Normal();p.Fullness=65;p.Fun=0;
        check(Need(p,ChaoNeed.Boredom).Weight>Need(p,ChaoNeed.Hunger).Weight,"Maximum boredom can outweigh moderate hunger; priorities are not a hard fixed order");
        foreach(var kind in Enum.GetValues<ChaoNeed>())foreach(int boundary in new[]{25,50,75})
        {
            p=Normal();
            void Set(float value){switch(kind){case ChaoNeed.Hunger:p.Fullness=100-value;break;case ChaoNeed.Fatigue:p.Energy=100-value;break;case ChaoNeed.Boredom:p.Fun=100-value;break;default:p.SocialNeed=value;break;}}
            Set(boundary-1);int below=(int)Need(p,kind).Level;
            Set(boundary);bool entered=(int)Need(p,kind).Level==below+1;
            Set(boundary-3);bool held=(int)Need(p,kind).Level==below+1;
            Set(boundary-5);held&=(int)Need(p,kind).Level==below+1;
            Set(boundary-6);bool exited=(int)Need(p,kind).Level==below;
            check(entered && held && exited,$"Needs E: {kind} enters at {boundary}, holds at {boundary-5}, exits below it");
        }
        p=Normal();p.Fullness=26;check(Need(p,ChaoNeed.Hunger).Level==NeedUrgency.High,"Hunger 74 starts High");
        p.Fullness=25;check(Need(p,ChaoNeed.Hunger).Level==NeedUrgency.Critical,"Hunger 75 enters Critical");
        p.Fullness=28;check(p.Needs.Hunger==72 && Need(p,ChaoNeed.Hunger).Level==NeedUrgency.Critical,"Hunger 72 retains Critical without altering the actual value");
        p.Fullness=31;check(Need(p,ChaoNeed.Hunger).Level==NeedUrgency.High,"Hunger 69 leaves Critical");
        p.Fullness=100;check(Need(p,ChaoNeed.Hunger).Level==NeedUrgency.Low,"A large recovery can cross multiple urgency boundaries");
        p=Normal();p.Fullness=20;
        check(!Need(p,ChaoNeed.Hunger,NeedActionContext.ShortSafe).CanInterruptCurrentAction && Need(p,ChaoNeed.Hunger,NeedActionContext.LongOptional).CanInterruptCurrentAction && !Need(p,ChaoNeed.SocialNeed,NeedActionContext.LongOptional).CanInterruptCurrentAction,"Urgency recommends interrupting long optional actions only; short safe actions remain protected");
        p=Normal();float before=Need(p,ChaoNeed.Hunger).Weight;p.AdvanceCurrentState(100,false);
        check(Need(p,ChaoNeed.Hunger).Weight>before && Need(p,ChaoNeed.Hunger).LevelSeconds==100,"Equal meter values gain a small age-based urgency bonus");
        for(int i=0;i<1000;i++)p.Needs.Evaluate();
        check(Need(p,ChaoNeed.Hunger).LevelSeconds==100,"Reading diagnostics cannot advance need duration");
        p=Normal();p.AdvanceCurrentState(600,false);check(Math.Abs(p.SocialNeed-32)<.001f,"Awake social need rises twelve points per ten-minute garden day");
        p=Normal();p.AdvanceCurrentState(600,true);check(Math.Abs(p.SocialNeed-20.6f)<.001f,"Sleeping social need grows slowly without accruing awake loneliness");
        p=Normal();p.AdvanceCurrentState(1800,false);check(Math.Abs(p.SocialNeed-65)<.001f,"Prolonged solitude adds pressure only after twenty awake minutes");
        var chunks=Normal();for(int i=0;i<1800;i++)chunks.AdvanceCurrentState(1,false);
        check(Math.Abs(p.SocialNeed-chunks.SocialNeed)<.02f,"Long and small update steps integrate the loneliness threshold consistently");
        p=Normal();p.SocialNeed=90;p.ObserveEvent(ChaoStateEvent.PositiveSocial,"friend");float relieved=p.SocialNeed;
        for(int i=0;i<1000;i++)p.ObserveEvent(ChaoStateEvent.PositiveSocial,"friend");
        check(relieved==82 && p.SocialNeed==82 && p.Sociability==50,"Positive social contact relieves current need once without modifying Sociability");
        p.AdvanceCurrentState(10000,false);check(p.SocialNeed==100,"Social need is bounded at one hundred even during long absence");
        p=Normal();p.Feed();check(p.Fullness==100 && p.Mood.Snapshot.DominantEmotion==ChaoEmotion.Happy,"Feeding changes Fullness once and emits happiness");
        p=Normal();p.FeedBite(false);check(p.Fullness==86,"A bite still adds exactly six fullness points");
        p=Normal();p.Tick(2,true);check(p.Energy==90 && p.Fullness==80-2*.035f && p.Fun==80 && p.Joy==50-2*.018f,"Sleep retains the original energy, hunger, joy and fun formulas");
        p=Normal();float stamina=p.StaminaAbility;p.Tick(2,false);
        check(Math.Abs(p.Energy-(80-2*.045f/(1+stamina*.002f)))<.0001f && Math.Abs(p.Fun-79.86f)<.0001f,"Awake need drain retains stamina scaling and existing fun drain");
        p=Normal();p.ObserveEvent(ChaoStateEvent.PositiveSocial,"friend");
        check(p.Mood.Snapshot.DominantEmotion==ChaoEmotion.Happy,"Needs F: positive contact starts a temporary Happy reaction");
        p.AdvanceCurrentState(9,false);check(p.Mood.Snapshot.DominantEmotion==ChaoEmotion.Neutral && p.Mood.ActiveReactions.Count==0,"Temporary happiness expires naturally");
        p=Normal();p.Joy=10;p.Mood.React(ChaoEmotion.Happy,.3f,12,"background",30);p.Mood.React(ChaoEmotion.Afraid,.9f,2,"surprise",95);
        check(p.Mood.Snapshot.DominantEmotion==ChaoEmotion.Afraid,"Needs G: a strong brief reaction overlays background happiness");
        p.AdvanceCurrentState(3,false);check(p.Mood.Snapshot.DominantEmotion==ChaoEmotion.Happy,"After a brief reaction, an unexpired background reaction resumes");
        p.AdvanceCurrentState(10,false);check(p.Mood.Snapshot.DominantEmotion==ChaoEmotion.Sad,"After all reactions expire, mood returns to current Joy rather than always Neutral");
        p=Normal();p.Mood.React(ChaoEmotion.Happy,.5f,2,"same",30);
        for(int i=0;i<210;i++){p.Mood.React(ChaoEmotion.Happy,.5f,2,"same",30);p.AdvanceCurrentState(.01f,false);}
        check(p.Mood.ActiveReactions.Count==0,"Repeated events cannot indefinitely extend a reaction's expiration");
        for(int i=0;i<1000;i++)p.Mood.React(ChaoEmotion.Curious,.5f,10,"source-"+i,i%100);
        check(p.Mood.ActiveReactions.Count<=ChaoMood.ReactionCapacity,"Temporary reaction storage is bounded under event bursts");
        p.AdvanceCurrentState(200,false);check(p.Mood.ActiveReactions.Count==0,"Old emotional events are removed, never queued for later playback");
        p=Normal();p.Energy=5;p.ObserveEvent(ChaoStateEvent.PlayFinished,"game");
        check(p.Mood.Snapshot.DominantEmotion==ChaoEmotion.Sleepy,"Critical fatigue suppresses a weak positive reaction while needs remain independent");
        p.ObserveEvent(ChaoStateEvent.Frightened,"impact");check(p.Mood.Snapshot.DominantEmotion==ChaoEmotion.Afraid,"A brief strong fear can overlay awake fatigue");
        p.AdvanceCurrentState(.1f,true);check(p.Mood.Snapshot.DominantEmotion==ChaoEmotion.Sleepy,"Actual sleep protects the sleeping state from temporary reactions");
        p=Normal();p.AdvanceCurrentState(1,true);p.AdvanceCurrentState(1,false);
        check(!p.Mood.Sleeping && p.Mood.ActiveReactions.Any(r=>r.Source=="wake-naturally"),"Natural awakening emits a short reaction only after actual sleep");
        p=Normal();p.AdvanceCurrentState(1,true);p.ObserveEvent(ChaoStateEvent.WakeDisturbed,"pickup");
        check(!p.Mood.Sleeping && p.Mood.Snapshot.DominantEmotion==ChaoEmotion.Frustrated,"Interrupted sleep produces frustration without changing personality or energy");
        p=Normal();p.EnsurePersonality();var innate=p.Personality!.Copy();
        for(int i=0;i<1000;i++){p.Fullness=50;p.FeedBite(i%4==3);p.Tick(.1f,i%2==0);p.ObserveEvent(ChaoStateEvent.PositiveSocial,"friend");p.Pet();}
        check(p.Personality!.Equals(innate),"Needs H: one thousand food, sleep and social event cycles preserve all thirteen innate scales");
        var pets=new[]{Normal(),Normal(),Normal()};pets[0].ObserveEvent(ChaoStateEvent.Frightened,"impact");pets[1].ObserveEvent(ChaoStateEvent.PositiveSocial,"friend");pets[2].Fullness=5;
        check(pets[0].Mood.Snapshot.DominantEmotion==ChaoEmotion.Afraid && pets[1].Mood.Snapshot.DominantEmotion==ChaoEmotion.Happy && pets[2].Mood.Snapshot.DominantEmotion==ChaoEmotion.Frustrated && pets[0].SocialNeed==20 && pets[1].SocialNeed==12,"Needs I: three pets own independent needs, reactions and social meters");
        SaveChecks(check,pets);
    }
    private static void SaveChecks(Action<bool,string> check,PetState[] pets)
    {
        string directory=Path.Combine(Path.GetTempPath(),"MoriNeedsMood-"+Guid.NewGuid());Directory.CreateDirectory(directory);string file=Path.Combine(directory,"pet.json");
        try
        {
            var root=pets[0];root.Name="Mori";root.GardenEggs.Clear();root.GardenEggs.Add(new(){Id="friend-1",Hatched=true,Chao=pets[1]});root.GardenEggs.Add(new(){Id="friend-2",Hatched=true,Chao=pets[2]});root.Tick(2,false);
            SaveStore.Save(file,root);var canonical=JsonSerializer.SerializeToNode(root);var read=SaveStore.Load(file,out _);
            check(JsonNode.DeepEquals(canonical,JsonSerializer.SerializeToNode(read)) && read.GardenEggs.Select(e=>e.Id).SequenceEqual(new[]{"friend-1","friend-2"}),"Needs J: update/save/load preserves every persistent meter and pet identity");
            check(read.Mood.ActiveReactions.Count==0 && !canonical!.AsObject().ContainsKey("Mood") && !canonical.AsObject().ContainsKey("Needs") && !File.ReadAllText(file).Contains("ExpiresAt"),"Saved data excludes reactions, urgency history and visual timers");
            File.WriteAllText(file,"{\"Name\":\"Mori\",\"Fullness\":66,\"Energy\":77,\"Joy\":44,\"Fun\":55,\"GardenEggs\":[{\"Id\":\"old-friend\",\"Hatched\":true,\"Chao\":{\"Name\":\"Friend\",\"Fullness\":33}}]}");
            read=SaveStore.Load(file,out _);
            check(read.Fullness==66 && read.Energy==77 && read.Joy==44 && read.Fun==55 && read.SocialNeed==20 && read.GardenEggs.Single().Id=="old-friend" && read.GardenEggs[0].Chao!.SocialNeed==20,"Needs K: legacy meters are untouched and each old pet gains social need twenty");
            read.Mood.React(ChaoEmotion.Curious,.6f,10,"editor-retain",35);
            var roster=ExpeditionRoster.Available(read).ToDictionary(e=>e.Id,e=>e.State);var bridge=new ChaoEditorBridge(directory);bridge.Poll(roster,true);
            var state=JsonNode.Parse(File.ReadAllText(Path.Combine(directory,"chao-editor-state.json")))!;var personality=read.Personality!.Copy();
            File.WriteAllText(Path.Combine(directory,"chao-editor-request.json"),JsonSerializer.Serialize(new{session=state["session"]!.GetValue<string>(),id="needs-edit",target="main",patch=new{Fullness=85f,Energy=82f,Joy=48f,SocialNeed=37f}}));
            var updated=bridge.Poll(roster,true)!;
            check(updated!=null && updated.Personality!.Equals(personality) && updated.Needs.Hunger==15 && updated.Needs.Fatigue==18 && updated.Joy==48 && updated.SocialNeed==37 && updated.GardenEggs.Single().Id=="old-friend","Needs L: editor meter patches update derived needs without touching personality or roster");
            check(updated!.Mood.ActiveReactions.Any(r=>r.Source=="editor-retain") && updated.Mood.Snapshot.DominantEmotion==ChaoEmotion.Curious,"An editor update keeps current reactions while rebinding needs to the edited pet");
            check(state["chao"]![0]!["currentState"]!["priorities"]!.AsArray().Count==4 && state["chao"]![0]!["currentState"]!["mood"]!["DominantEmotion"]!=null,"Editor diagnostics expose four priorities, mood and temporary reactions without new persistent copies");
            foreach(float invalid in new[]{-1f,101f,float.NaN})
            {
                updated!.SocialNeed=invalid;bool rejected=false;string original=File.ReadAllText(file);try{SaveStore.Save(file,updated);}catch(ArgumentException){rejected=true;}
                check(rejected && File.ReadAllText(file)==original && !File.Exists(file+".tmp"),"An invalid social meter cannot overwrite an existing save");
            }
        }
        finally{foreach(string path in Directory.GetFiles(directory))File.Delete(path);Directory.Delete(directory);}
    }
}
