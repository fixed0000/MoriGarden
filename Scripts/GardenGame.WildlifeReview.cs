using System;
using System.Linq;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private float wildlifeReviewTime,wildlifeStageTime;
    private int wildlifeReviewStage;
    private bool wildlifeInspectReady;
    private WildAnimal? reviewRabbit,reviewBird;
    private Vector3[] wildlifeReviewStarts=Array.Empty<Vector3>();
    private float[] wildlifeReviewTravel=Array.Empty<float>();
    private Vector3[] wildlifeReviewPrevious=Array.Empty<Vector3>();
    private void SetupWildlifeDemo()
    {
        SetupGreenhouseDemo(true);player.Position=new(0,0,23);player.Velocity=Vector3.Zero;
        cameraRig.Yaw=0;cameraRig.Pitch=.35f;cameraRig.Distance=8;cameraRig.Follow(player.Position,0,true);
        paused=pausedByFocus=false;portalCooldown=3;
    }
    private void InspectWildlife()
    {
        if(wildlifeInspectReady)return;wildlifeInspectReady=true;SetupWildlifeDemo();expeditionTeam!.Reposition(player.Position);testMode=false;
        Notify("Wildlife demo · Explore Windfall Meadow · E: Greet · F: Ask a Chao · B: Animal bag",12);
    }
    private void ReviewWildlife(float delta)
    {
        float dt=Mathf.Min(delta,.1f);wildlifeReviewTime+=dt;wildlifeStageTime+=dt;
        if(wildlifeReviewStage==0)
        {
            SetupWildlifeDemo();expeditionTeam!.Discoveries.AutoScan=false;
            reviewRabbit=Wildlife!.Animals.First(a=>a.Species.Id=="rabbit");reviewBird=Wildlife.Animals.First(a=>a.Species.Id=="songbird");
            wildlifeReviewStarts=Wildlife.Animals.Select(a=>a.Position).ToArray();wildlifeReviewPrevious=wildlifeReviewStarts.ToArray();wildlifeReviewTravel=new float[wildlifeReviewStarts.Length];Next();
        }
        if(Wildlife is {} wildlife)
            for(int i=0;i<wildlife.Animals.Count;i++)
            {
                var animal=wildlife.Animals[i];
                wildlifeReviewTravel[i]+=ExpeditionCompanion.Flat(wildlifeReviewPrevious[i],animal.Position);wildlifeReviewPrevious[i]=animal.Position;
                if(animal.Present && wildlifeStageTime>2 && Math.Abs(animal.Position.Y-WindfallMeadow.Height(animal.Position.X,animal.Position.Z))>.35f)
                    throw new Exception("Wild animal lost terrain contact: "+animal.Species.Id);
            }
        UpdateTrainingAnimals(dt);UpdateUi();
        void Next(){wildlifeReviewStage++;wildlifeStageTime=0;}
        void Shot(string name)=>GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://artifacts/wildlife-"+name+".png"));
        void Stand(WildAnimal animal,float distance)
        {
            var at=animal.Position+Vector3.Back*distance;at.Y=expeditionTeam!.Navigation.Height(at.X,at.Z);
            player.Position=at;Face(animal.Position);player.Velocity=Vector3.Zero;
        }
        switch(wildlifeReviewStage)
        {
            case 1:
                camera.GlobalPosition=new(-4,3,21);camera.LookAt(new Vector3(-8,.4f,15));
                if(wildlifeStageTime>15){Shot("habitat");Stand(reviewRabbit!,1.4f);Next();}break;
            case 2:
                camera.GlobalPosition=reviewRabbit!.Position+new Vector3(2.4f,1.3f,3.4f);camera.LookAt(reviewRabbit.Position+Vector3.Up*.25f);
                if(wildlifeStageTime>10 && reviewRabbit.ReadyToJoin)
                {
                    Face(reviewRabbit.Position);Interact();if(pet.Wildlife.Count("rabbit")!=1)throw new Exception("Live calm capture failed");
                    Shot("calm-encounter");Stand(reviewBird!,1.4f);expeditionTeam!.Reposition(player.Position);
                    Wildlife!.Ask(player,expeditionTeam);Stand(reviewBird!,5);Next();
                }break;
            case 3:
                camera.GlobalPosition=reviewBird!.Position+new Vector3(3.2f,1.7f,4);camera.LookAt(reviewBird.Position+Vector3.Up*.35f);
                if(reviewBird.Helped && wildlifeStageTime>18){Shot("chao-help");Stand(reviewBird,1.4f);Next();}break;
            case 4:
                if(wildlifeStageTime>5 && reviewBird!.ReadyToJoin)
                {
                    Face(reviewBird.Position);Interact();if(pet.Wildlife.Count("songbird")!=1)throw new Exception("Live assisted capture failed");
                    showBag=true;Next();
                }break;
            case 5:
                if(wildlifeStageTime>2)
                {
                    Shot("bag");Save();var loaded=SaveStore.Load(savePath,out _);
                    if(loaded.Wildlife.Total!=2)throw new Exception("Live encounter save failed");
                    SwitchZone(GardenZone.Garden);player.Position=new(1,0,4);friend.Position=new(1,0,3);friend.Visible=true;petZone=GardenZone.Garden;sleeping=false;Face(friend.Position);
                    bagAnimals.Select(Array.FindIndex(AnimalCatalog.All,a=>a.Id=="rabbit"));TakeSelectedAnimal();Next();
                }break;
            case 6:
                camera.GlobalPosition=new(3.2f,1.1f,5.2f);camera.LookAt(new Vector3(1,.4f,3));
                if(wildlifeStageTime>1 && wildlifeStageTime<4)Shot("garden-gift");
                if(wildlifeStageTime>12 && heldAnimal==null)
                {
                    if(pet.Wildlife.Count("rabbit")!=0 || pet.RecentAnimalSkill!="Run")throw new Exception("Live garden training failed");
                    if(wildlifeReviewTravel.Any(distance=>distance<1))throw new Exception("A live habitat animal never moved");
                    Save();Shot("training-result");
                    GD.Print("WILDLIFE_REVIEW_OK: solo rabbit, Chao-assisted songbird, bag persistence, garden gift/training; travel="+string.Join(",",wildlifeReviewTravel.Select(x=>x.ToString("F1"))));GetTree().Quit();
                }break;
        }
        if(wildlifeReviewTime>160)throw new Exception($"Live wildlife review timed out at stage {wildlifeReviewStage}, bird help={reviewBird?.Helped}, intent={reviewBird?.Intent}, trust={reviewBird?.Trust}");
    }
}
