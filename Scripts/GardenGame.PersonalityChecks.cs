using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private static void AdvanceVisualCheck(Node3D art,float seconds)
    {for(float t=0;t<seconds;t+=1f/60)art._PhysicsProcess(Math.Min(1f/60,seconds-t));}
    private int personalityCheckFrame;
    private void RunPersonalityCheckMode()
    {
        if(++personalityCheckFrame<3)return;
        try{CheckPersonality();GetTree().Quit();}
        catch(Exception ex){GD.PushError("PERSONALITY_CHECK_FAILED: "+ex);GetTree().Quit(1);}
    }
    private void CheckPersonality()
    {
        var original=pet;var oldPetZone=petZone;var oldAt=petZonePosition;
        try
        {
            SetupGreenhouseDemo(true);SwitchZone(GardenZone.Garden,restoring:true);player.Position=new(8,0,10);friend.Position=new(1,0,10);
            var residents=world.GetChildren().OfType<GardenEgg>().Where(e=>e.Chao!=null).ToArray();
            for(int i=0;i<residents.Length;i++)residents[i].Position=new(2.4f+i*1.4f,0,10);
            AdvanceChaoMemories(.02f);
            var models=new[]{(ImportedChao)friendArt}.Concat(residents.Select(e=>(ImportedChao)e.Art)).ToArray();
            var eyes=models[0].FindChildren("*_Eye","MeshInstance3D",true,false).OfType<Node3D>().ToDictionary(n=>n,n=>n.Position);
            void Tick(ImportedChao model,ChaoExpression expression,int count=80)
            {
                model.GetNode<EmotionOrb>("MoodOrb").WaitForPetting();ImportedChao.SmileEyes(model,false);ImportedChao.HalfEyes(model,false);ImportedChao.CloseEyes(model,false);
                for(int i=0;i<count;i++){ImportedChao.MovePose(model,0,false,false,false,false,false,false);ImportedChao.Express(model,expression);model._PhysicsProcess(1f/60);}
            }
            foreach(var expression in Enum.GetValues<ChaoExpression>())
            {
                Tick(models[0],expression);if(models[0].Expression!=expression)throw new Exception("Expression arbitration lost "+expression+": "+models[0].Expression);
                if(eyes.Any(p=>p.Key.Position.DistanceTo(p.Value)>.0001f))throw new Exception("Face expressions displaced the original eyes");
            }
            Tick(models[0],ChaoExpression.Neutral);var head=(Node3D)models[0].FindChild("HeadPivot",true,false);
            for(int i=0;i<80;i++){ImportedChao.MovePose(models[0],0,false,false,false,false,false,false);ImportedChao.Attend(models[0],friend.Position+new Vector3(-20,20,-20),100);models[0]._PhysicsProcess(1f/60);}
            if(Mathf.Abs(head.Rotation.Y)>.56f || Mathf.Abs(head.Rotation.X)>.41f)throw new Exception("Attention exceeded neck limits");
            Tick(models[0],ChaoExpression.Neutral,160);
            if(Mathf.Abs(head.Rotation.Y)>.4f)throw new Exception("Attention failed to release to neutral observation");
            var blinkTimes=models.ToDictionary(m=>m,m=>new List<int>());var previously=models.ToDictionary(m=>m,m=>false);
            for(int frame=0;frame<1200;frame++)foreach(var model in models)
            {
                ImportedChao.MovePose(model,0,false,false,false,false,false,false);model._PhysicsProcess(1f/60);
                bool blink=model.BlinkAmount>.1f;if(blink && !previously[model])blinkTimes[model].Add(frame);previously[model]=blink;
            }
            if(blinkTimes.Values.Any(times=>times.Count<2) || blinkTimes.Values.Select(times=>string.Join(',',times)).Distinct().Count()!=models.Length)throw new Exception("Blinking is missing or synchronized");
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath("res://artifacts/personality-check.json"),System.Text.Json.JsonSerializer.Serialize(new { blinkFrames=blinkTimes.Values,simulationFrames=1200,models=models.Length },new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
            for(int i=0;i<90;i++){ImportedChao.SleepPose(models[0],i/60f,pet.Manners);ImportedChao.Express(models[0],ChaoExpression.Delighted);models[0]._PhysicsProcess(1f/60);}
            if(models[0].Expression!=ChaoExpression.Sleepy || models[0].BlinkAmount>.01f)throw new Exception("Joy or blinking overrode sleeping eyes");
            for(int i=0;i<90;i++){ImportedChao.MovePose(models[0],i*.2f,true,true,false,false,false,false);ImportedChao.Express(models[0],ChaoExpression.Delighted);models[0]._PhysicsProcess(1f/60);}
            if(models[0].Expression!=ChaoExpression.Afraid || models[0].PoseKind!=ChaoPoseKind.Water)throw new Exception("Swimming distress lost its pose or priority");
            for(int i=0;i<80;i++){ImportedChao.MovePose(models[0],0,false,false,false,false,false,false);models[0]._PhysicsProcess(1f/60);}
            var right=(Node3D)models[0].FindChild("PaddleR",true,false);var before=right.Quaternion;
            ImportedChao.ThinkPose(models[0],4);models[0]._PhysicsProcess(1f/60);
            if(before.AngleTo(right.Quaternion)>.8f)throw new Exception("Standing-to-thinking arms snapped");
            ImportedChao.InstrumentHands(models[0],friend.Position+new Vector3(-.1f,.18f,.13f),friend.Position+new Vector3(.1f,.18f,.13f));
            var grip=right.Transform;models[0]._PhysicsProcess(1f/60);
            if(right.Transform.Origin.DistanceTo(grip.Origin)>.0001f)throw new Exception("Generic blending moved a gripping hand off its instrument");
            if(pet.History.Events.Count(e=>e.Kind==ChaoMemoryKind.FirstVisit && e.Area=="garden")!=1 || residents.Any(e=>e.Chao!.History.Events.Count==0))throw new Exception("Garden histories were not individual or recorded actual visits");
            showStats=true;OpenMemories();
            if(memoryPetPicker.ItemCount!=3 || memoryText.Text.Contains("windfall-meadow") || !memoryText.Text.Contains("Visited Mori Garden"))throw new Exception("Memory card is missing live pets or exposing technical IDs");
            memoriesOpen=false;memoriesPanel.Hide();
            Save();var restored=SaveStore.Load(savePath,out _);
            if(restored.History.Events.Count!=pet.History.Events.Count || restored.GardenEggs.Any(e=>e.Chao!.History.Events.Count==0))throw new Exception("One pet's memory was lost on saving");
            travel.ExpeditionArea=WorldCatalog.Meadow;SwitchZone(GardenZone.Expedition,restoring:true);
            var hiker=expeditionTeam!.Members[0];hiker.Body.Position=expeditionTeam.Navigation.Nearest(new(-20,0,-10));var hikerArt=(ImportedChao)hiker.Art;
            ImportedChao.BindPresentation(hikerArt,hiker.Pet.State,false,hiker.Pet.Id);
            for(int i=0;i<90;i++){ImportedChao.MovePose(hikerArt,0,false,false,false,false,false,false);hikerArt._PhysicsProcess(1f/60);}
            foreach(string side in new[]{"L","R"})
            {
                var sole=(Node3D)hikerArt.FindChild("Foot"+side,true,false);var at=sole.GlobalPosition;
                var ground=GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(at+Vector3.Up,at-Vector3.Up,1));
                if(ground.Count==0)throw new Exception("Stance foot has no hill surface");
                float gap=hikerArt.LowestSole(sole)-((Vector3)ground["position"]).Y;
                if(gap<-.035f || gap>.06f)throw new Exception("Stance foot lost hill contact: "+gap);
            }
            GD.Print("PERSONALITY_OK: ten expressions, original eye anchors, limited/released attention, independent blinking, sleeping/swimming priorities, upright transitions, exact grips, real garden visits, per-pet memory UI and saves");
        }
        finally{pet=original;petZone=oldPetZone;petZonePosition=oldAt;SwitchZone(GardenZone.Garden,restoring:true);showStats=false;}
    }
}
