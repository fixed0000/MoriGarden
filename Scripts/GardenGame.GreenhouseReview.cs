using System;
using System.Linq;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private float greenhouseReviewTime;
    private int greenhouseReviewStage;
    private bool greenhouseInspectReady;
    private void SetupGreenhouseDemo(bool meadow)
    {
        pet=new PetState { Name="Mori",Run=220,Bond=90,Curiosity=95,Initiative=900,Intelligence=950,Sociability=90,Energy=95 };
        pet.GardenEggs.Clear();
        pet.GardenEggs.Add(new() { Id="greenhouse-fern",Hatched=true,Color="Yellow",Chao=new() { Name="Fern",Run=150,Bond=70,Curiosity=-40,Initiative=150,Intelligence=100,Sociability=65,Energy=90 } });
        pet.GardenEggs.Add(new() { Id="greenhouse-pip",Hatched=true,Color="Black",Chao=new() { Name="Pip",Run=210,Bond=85,Curiosity=85,Initiative=850,Intelligence=800,Sociability=100,Energy=95 } });
        pet.Expedition.LastTeamIds=ExpeditionRoster.Available(pet).Select(p=>p.Id).ToList();
        foreach(var id in pet.Expedition.LastTeamIds.Where(id=>id!="main")){var relation=pet.SocialLink("main",id,true)!;relation.Met=true;relation.Score=60;}
        carrying=false;petZone=GardenZone.Garden;travel.ExpeditionArea=meadow ? WorldCatalog.Meadow : WorldCatalog.Greenhouse;
        SwitchZone(GardenZone.Expedition,restoring:true);
        player.Position=meadow ? new(3,WindfallMeadow.Height(3,-16),-16) : new(0,.02f,17);
        expeditionTeam!.Reposition(player.Position);
        cameraRig.Yaw=0;cameraRig.Distance=7.5f;cameraRig.Pitch=.3f;cameraRig.Follow(player.Position,0,true);
    }
    private void InspectGreenhouse()
    {
        if(greenhouseInspectReady)return;
        greenhouseInspectReady=true;SetupGreenhouseDemo(false);testMode=false;
        Notify("Forgotten Greenhouse · Demo save · E: Operate · F: Ask a Chao",10);
    }
    private void ReviewGreenhouse(float dt)
    {
        greenhouseReviewTime+=Mathf.Min(dt,.1f);
        void Shot(string name)=>GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://artifacts/greenhouse-"+name+".png"));
        if(greenhouseReviewStage==0){SetupGreenhouseDemo(true);greenhouseReviewStage=1;}
        if(transition>=0)AdvanceTransition(dt);
        avatarWalk?.Arms(false,0,0);MissionPlayerPose();UpdateUi();
        if(greenhouseReviewStage==1)
        {
            camera.GlobalPosition=new(3,4,-8);camera.LookAt(new Vector3(3,2,-23));
            if(greenhouseReviewTime>3)
            {
                Shot("entrance");portalCooldown=0;player.Position=new(3,0,-21);CheckPortals();greenhouseReviewStage=2;
            }
        }
        else if(greenhouseReviewStage==2 && zone==GardenZone.Expedition && travel.ExpeditionArea==WorldCatalog.Greenhouse && transition<0)
        {
            camera.GlobalPosition=new(11,4.4f,18);camera.LookAt(new Vector3(-1,1,-3));
            if(greenhouseReviewTime>8)
            {
                Shot("interior");player.Position=new(-7.7f,.02f,8.6f);player.Rotation=new(0,Mathf.Pi,0);Interact();greenhouseReviewStage=3;
            }
        }
        else if(greenhouseReviewStage==3)
        {
            camera.GlobalPosition=new(-4.3f,1.8f,9.5f);camera.LookAt(new Vector3(-7.7f,.55f,7));
            if(greenhouseReviewTime>11){Shot("intake");greenhouseReviewStage=4;}
        }
        else if(greenhouseReviewStage==4)
        {
            camera.GlobalPosition=new(-5,2.4f,6);camera.LookAt(new Vector3(-10,-.05f,0));
            if(greenhouseReviewTime>17)
            {
                Shot("channels");player.Position=new(7,.02f,.7f);player.Rotation=new(0,Mathf.Pi,0);expeditionTeam!.Reposition(player.Position);greenhouseReviewStage=5;
            }
        }
        else if(greenhouseReviewStage==5)
        {
            camera.GlobalPosition=new(3,2.5f,3);camera.LookAt(new Vector3(7,.5f,-1));
            if(greenhouseReviewTime>23){SiteMission!.Ask(player,expeditionTeam!);greenhouseReviewStage=6;}
        }
        else if(greenhouseReviewStage==6)
        {
            camera.GlobalPosition=new(3,2.3f,2);camera.LookAt(new Vector3(7,.45f,-1));
            if(SiteMission!.Progress.Element(GreenhouseRules.Diverter).Value==2 && greenhouseReviewTime>37)
            {
                Shot("chao-mechanism");var crate=SiteMission.Counterweight;var plate=SiteMission.Elements.First(e=>e.Kind==PuzzleElementKind.Plate);
                player.Position=crate.Position+(crate.Position-plate.Position).Normalized()*.95f;greenhouseReviewStage=7;
            }
        }
        else if(greenhouseReviewStage==7)
        {
            var crate=SiteMission!.Counterweight;var plate=SiteMission.Elements.First(e=>e.Kind==PuzzleElementKind.Plate);
            var direction=plate.Position-crate.Position;direction.Y=0;
            if(direction.Length()>.5f)
            {
                var step=direction.Normalized()*dt*.9f;crate.Push(player,step);player.MoveAndCollide(step);
                player.Rotation=new(0,Mathf.Atan2(direction.X,direction.Z),0);
            }
            camera.GlobalPosition=new(11,3,-1);camera.LookAt(new Vector3(6,.25f,-7));
            if(SiteMission.PlateDown && ExpeditionCompanion.Flat(crate.Position,plate.Position)<.9f && greenhouseReviewTime>45){Shot("filter");greenhouseReviewStage=8;}
        }
        else if(greenhouseReviewStage==8)
        {
            camera.GlobalPosition=new(-7,3,-1);camera.LookAt(new Vector3(0,.9f,-9));
            if(SiteMission!.Progress.Completed && greenhouseReviewTime>70)
            {
                Shot("restored");Save();
                GD.Print($"GREENHOUSE_REVIEW_OK: player actions {SiteMission.PlayerActions}, Chao actions {SiteMission.ChaoActions}, reward {pet.SeedCount(FruitKinds.Moonblossom)}, completed {SiteMission.Progress.Completed}, counterweight {SiteMission.Counterweight.Position}");
                GetTree().Quit();
            }
        }
        if(greenhouseReviewTime>125){GD.PushError($"GREENHOUSE_REVIEW_FAILED: stage {greenhouseReviewStage}, state {SiteMission?.Progress.Element(GreenhouseRules.Intake).Value}/{SiteMission?.Progress.Element(GreenhouseRules.Diverter).Value}, crate {SiteMission?.Counterweight.Position}");GetTree().Quit(1);}
    }
}
