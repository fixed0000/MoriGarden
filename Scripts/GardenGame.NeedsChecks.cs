using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private bool needsSceneStarted;
    private int needsSceneFrames;
    private void RunNeedsSceneCheck()
    {
        if(needsSceneStarted || ++needsSceneFrames<3)return;
        needsSceneStarted=true;CaptureNeedsSceneCheck();
    }
    private async void CaptureNeedsSceneCheck()
    {
        try
        {
            GetWindow().Title="MoriGarden — GENERATED NEEDS TEST";
            var state=new PetState{Name="Mood check",Run=160,Fullness=20,Energy=90,Fun=20,Joy=50,SocialNeed=80};
            state.EnsurePersonality();var innate=state.Personality!.Copy();
            friend.Position=new(11,0,8);
            var prop=world.GetChildren().OfType<ToyCar>().First(t=>t.IsRattle);
            if(!prop.BeginPlay(friend,state) || state.Mood.Snapshot.DominantEmotion!=ChaoEmotion.Happy)throw new Exception("Real toy start did not update mood");
            prop.Advance(prop.PlayDuration+.01f);
            if(state.Mood.Snapshot.DominantEmotion!=ChaoEmotion.Excited)throw new Exception("Real toy completion did not produce excitement");
            prop.CancelPlay(friend);
            for(int i=0;i<4;i++)state.FeedBite(i==3);
            if(state.Fullness!=44 || !state.Mood.ActiveReactions.Any(r=>r.Source.StartsWith("MealFinished:")))throw new Exception("Eating double-counted fullness or lost the final-bite event");
            state.AdvanceCurrentState(10,false);
            var peer=new PetState();float social=state.SocialNeed;
            ChaoExperience.Together(state,"main",state,"peer",peer,ChaoMemoryKind.Introduction,"introduction","garden",false);
            if(state.SocialNeed!=social-8 || peer.SocialNeed!=12)throw new Exception("Real positive social event did not relieve both pets independently");
            ImportedChao.BindPresentation(friendArt,state,false,"needs-test");ImportedChao.ApplyDevelopment(friendArt,state);
            var touch=new ChaoTouch();touch.Begin(state,friendArt,false);touch.Step(friendArt,1);AdvanceVisualCheck(friendArt,.1f);
            if(((ImportedChao)friendArt).Expression!=ChaoExpression.Neutral)throw new Exception("Mood overrode the original two-second neutral petting lead-in");
            touch.Cancel();
            state.Mood.React(ChaoEmotion.Afraid,.8f,3,"visual-check",95);
            ImportedChao.BindPresentation(friendArt,state,false,"needs-test");ImportedChao.ApplyDevelopment(friendArt,state);
            friendArt.GetNode<EmotionOrb>("MoodOrb").WaitForPetting();
            ImportedChao.CloseEyes(friendArt,false);ImportedChao.HalfEyes(friendArt,false);ImportedChao.SmileEyes(friendArt,false);
            ImportedChao.MovePose(friendArt,0,false,false,false,false,false,false);AdvanceVisualCheck(friendArt,.5f);
            if(((ImportedChao)friendArt).Expression!=ChaoExpression.Afraid)throw new Exception("Idle presentation did not use current emotional state");
            state.Tick(.1f,true);
            if(state.Mood.Snapshot.DominantEmotion!=ChaoEmotion.Sleepy)throw new Exception("Actual sleep did not protect current emotion");
            moriDistress.PickUp(friendArt,true,state);
            if(state.Mood.Sleeping || state.Mood.Snapshot.DominantEmotion!=ChaoEmotion.Afraid)throw new Exception("Interrupted sleep or background strong reaction was lost");
            state.AdvanceCurrentState(4,false);
            if(state.Mood.Snapshot.DominantEmotion!=ChaoEmotion.Frustrated)throw new Exception("Disturbed-wake reaction did not resume after the short fear");
            ChaoExperience.Study(state,new("needs-stone",DiscoveryKind.Stone,new(0,0,0),8,1,DiscoveryReaction.Inspect),false);
            if(!state.Mood.ActiveReactions.Any(r=>r.Source=="DiscoveryFinished:needs-stone") || !state.Personality!.Equals(innate))throw new Exception("Research event lost state or changed innate personality");
            pet=state;pet.GardenEggs.Clear();
            pet.GardenEggs.Add(new(){Id="needs-peer",Hatched=true,Chao=peer});
            pet.GardenEggs.Add(new(){Id="needs-third",Hatched=true,Chao=new(){Name="Third Chao",Energy=15}});
            RefreshNeedsDebug();
            if(needsDebugPicker?.ItemCount!=3 || !needsDebugText!.Text.Contains("SocialNeed") || !needsDebugText.Text.Contains("Frustrated"))throw new Exception("Developer view lost a pet or emotional state");
            string directory=Path.GetDirectoryName(savePath)!;
            var roster=ExpeditionRoster.Available(pet).ToDictionary(p=>p.Id,p=>p.State);new ChaoEditorBridge(directory).Poll(roster,false);
            SaveStore.Save(savePath,pet);var saved=SaveStore.Load(savePath,out _);
            if(saved.GardenEggs.Count!=2 || !saved.Personality!.Equals(innate) || saved.SocialNeed!=pet.SocialNeed || saved.Mood.ActiveReactions.Count!=0)throw new Exception("Scene save did not preserve durable state cleanly");
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory,"needs-diagnostic.png"));
            File.WriteAllText(Path.Combine(directory,"needs-scene-check.json"),JsonSerializer.Serialize(new{passed=true,realToyEvents=true,feedingCountedOnce=true,socialRelief=true,sleepAndWake=true,idlePresentation=true,personalityUnchanged=true,diagnosticPets=3,safeSave=true}));
            GD.Print("NEEDS_SCENE_OK: real toy/meal/social/research/sleep/wake events, idle expression, unchanged personality, three-pet diagnostic view, safe save/load");GetTree().Quit();
        }
        catch(Exception error){GD.PushError("NEEDS_SCENE_FAILED: "+error);GetTree().Quit(1);}
    }
}
