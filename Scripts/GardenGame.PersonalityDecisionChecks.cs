using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private bool personalityDecisionCheckStarted;
    private sealed class DecisionMetrics
    {
        public Dictionary<ChaoActivity,int> Choices { get; }=new();
        public Dictionary<ChaoActivity,double> Seconds { get; }=new();
        public double HungerSum, FatigueSum, BoredomSum, SocialSum;
        public int Samples, LastSelections;
        public void Sample(PetState p,float dt)
        {
            var c=p.GardenActivity;
            if(c?.SelectedActivity is {} a)
            {Seconds[a]=Seconds.GetValueOrDefault(a)+dt;if(c.Selections!=LastSelections)Choices[a]=Choices.GetValueOrDefault(a)+c.Selections-LastSelections;}
            if(c!=null)LastSelections=c.Selections;
            HungerSum+=p.Needs.Hunger;FatigueSum+=p.Needs.Fatigue;BoredomSum+=p.Needs.Boredom;SocialSum+=p.SocialNeed;Samples++;
        }
        public object Result(PetState p)=>new{p.Name,Choices,Seconds,p.GardenActivity!.Selections,p.GardenActivity.FailedAttempts,
            p.GardenActivity.Interruptions,p.GardenActivity.CompletedRepeats,p.GardenActivity.LastFailureReason,
            AverageNeeds=new{Hunger=HungerSum/Samples,Fatigue=FatigueSum/Samples,Boredom=BoredomSum/Samples,SocialNeed=SocialSum/Samples}};
    }
    private async void RunPersonalityDecisionCheck()
    {
        if(personalityDecisionCheckStarted)return;personalityDecisionCheckStarted=true;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
            SetPhysicsProcess(false);GD.Seed(1450);GetWindow().Title="MoriGarden — GENERATED PERSONALITY DECISION TEST";
            PetState Actor(string name,ChaoPersonality traits)=>new(){Name=name,Run=180,Fullness=80,Energy=80,Fun=65,SocialNeed=35,PersonalitySchemaVersion=1,Personality=traits,FavoriteToy="Rattle"};
            pet=Actor("Explorer",new(){Curious=90,Energetic=65,Carefree=30,Chatty=-30,Solitude=40});
            pet.GardenEggs.Clear();
            pet.GardenEggs.Add(new(){Id="decision-social",Hatched=true,Color="White",X=21.5f,Z=14,Chao=Actor("Social",new(){Chatty=90,Solitude=-70,Curious=20,Energetic=40})});
            pet.GardenEggs.Add(new(){Id="decision-playful",Hatched=true,Color="Yellow",X=23,Z=14,Chao=Actor("Playful",new(){Energetic=90,Carefree=85,Curious=20})});
            petZone=GardenZone.Garden;carrying=false;callTime=0;mainActivity=null;
            SwitchZone(GardenZone.Garden,restoring:true);friend.Position=new(20,0,14);friend.Visible=true;
            player.Position=new(35,0,25);wanderTarget=friend.Position;wanderTime=0;
            ImportedChao.ClearActionPause(friendArt);ImportedChao.CloseEyes(friendArt,false);
            for(int i=0;i<3;i++)
            {
                var food=new FruitTree{IsFeeder=true,SingleUse=true};world.AddChild(food);food.Place(new(20+i*1.5f,.18f,14.8f));
                var toy=new ToyCar{IsRattle=true};world.AddChild(toy);toy.Position=new(20+i*1.5f,.02f,13.3f);
            }
            var pets=new[]{pet}.Concat(pet.GardenEggs.Select(e=>e.Chao!)).ToArray();var traits=pets.Select(p=>p.Personality!.Copy()).ToArray();
            var metrics=pets.Select(_=>new DecisionMetrics()).ToArray();
            bool ensembleFixture=OS.GetCmdlineUserArgs().Contains("--ensemble-fixture");
            int seconds=ensembleFixture ? 180 : 600;
            if(ensembleFixture)
            {
                foreach(var state in pets)state.SocialNeed=70;
                foreach(var pair in new[]{("main","decision-social"),("main","decision-playful"),("decision-social","decision-playful")})
                {var link=pet.SocialLink(pair.Item1,pair.Item2,true)!;link.Met=link.Introduced=true;}
                for(int i=0;i<3;i++){var flute=new ToyCar{IsFlute=true};world.AddChild(flute);flute.Position=new(20+i*1.5f,.02f,14.3f);}
                await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
                await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
                // Supplemental availability scenario: an existing performer is
                // setup, while the other two still make their own decisions.
                var lead=world.GetChildren().OfType<ToyCar>().First(t=>t.IsFlute && t.Position.X==20);
                if(!lead.BeginPlay(friend,pet))throw new Exception("Ensemble fixture could not start its existing performer");
            }
            for(int frame=0;frame<seconds*30;frame++)
            {
                const float dt=1f/30;
                pet.Tick(dt,sleeping || moriIslandNap.IsNapping);
                foreach(var toy in world.GetChildren().OfType<ToyCar>())toy.Advance(dt);
                musicGroup.Advance(dt,pet,BallParticipants(),SocialCandidates(true),world.GetChildren().OfType<ToyCar>(),PauseForSocial,allowStart:false);
                ballGame.Advance(dt,pet,BallParticipants(),SocialCandidates(true),world.GetChildren().OfType<ToyCar>(),PauseForSocial);
                UpdateEggs(dt);MoveFriend(dt);introductions.Advance(dt,pet,SocialCandidates,()=>{},allowStart:false);ResolveChaoContacts();
                for(int i=0;i<3;i++)metrics[i].Sample(pets[i],dt);
                if(frame%1800==0)GD.Print($"PERSONALITY_GARDEN_PROGRESS: {frame/30}s");
                if(frame%30==0)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            }
            if(pets.Where((p,i)=>!p.Personality!.Equals(traits[i])).Any())throw new Exception("Innate personality changed during real actions");
            if(pets.Any(p=>p.GardenActivity==null || p.GardenActivity.Selections<2 || p.GardenActivity.Selections>180))throw new Exception("Live decision cycles stalled or thrashed");
            string directory=Path.GetDirectoryName(savePath)!;
            var results=pets.Select((p,i)=>metrics[i].Result(p)).ToArray();
            File.WriteAllText(Path.Combine(directory,"personality-decisions.json"),JsonSerializer.Serialize(new{Seconds=seconds,EnsembleFixture=ensembleFixture,Actors=results,PersonalityUnchanged=true},new JsonSerializerOptions{WriteIndented=true}));
            GD.Print("PERSONALITY_GARDEN_STATS: "+JsonSerializer.Serialize(results));
            Save();var loaded=SaveStore.Load(savePath,out _);if(!loaded.Personality!.Equals(traits[0]) || loaded.GardenEggs.Count!=2 || loaded.GardenEggs.Where((e,i)=>!e.Chao!.Personality!.Equals(traits[i+1])).Any())throw new Exception("Save changed roster/personality");
            RefreshNeedsDebug();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory,"personality-decisions.png"));
            GD.Print($"PERSONALITY_GARDEN_OK: three live personalities, {seconds}s existing executors, metrics, unchanged innate data and save; ensemble fixture={ensembleFixture}");GetTree().Quit();
        }
        catch(Exception error){GD.PushError("PERSONALITY_GARDEN_FAILED: "+error);GetTree().Quit(1);}
    }
}
