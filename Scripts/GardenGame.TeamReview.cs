using System;
using System.Linq;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private float teamReviewTime;
    private int teamReviewStage;
    private void ReviewExpeditionTeam(float dt)
    {
        teamReviewTime+=Mathf.Min(dt,.1f);
        if(teamReviewStage==0)
        {
            pet.GardenEggs.Clear();pet.Run=160;pet.Bond=80;pet.Energy=90;pet.Curiosity=90;pet.Initiative=850;
            pet.GardenEggs.Add(new() { Id="review-black",Hatched=true,Color="Black",Chao=new() { Name="Noir",Run=140,Bond=65,Sociability=95,Energy=85 } });
            pet.GardenEggs.Add(new() { Id="review-yellow",Hatched=true,Color="Yellow",Chao=new() { Name="Sunny",Run=170,Bond=80,Sociability=80,Curiosity=-60,Energy=20 } });
            pet.Expedition.LastTeamIds=ExpeditionRoster.Available(pet).Select(p=>p.Id).ToList();
            var link=pet.SocialLink("main","review-black",true)!;link.Met=true;link.Score=60;
            SwitchZone(GardenZone.Expedition);player.Position=new(0,0,18);expeditionTeam!.Reposition(player.Position);
            cameraRig.Distance=7;cameraRig.Pitch=.28f;cameraRig.Yaw=0;
            teamReviewStage=1;
        }
        if(teamReviewStage==1 && teamReviewTime>2)
        {
            GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://artifacts/expedition-team.png"));
            expeditionTeam!.Command("Follow",new(0,0,11));player.Position=new(0,0,11);teamReviewStage=2;
        }
        if(teamReviewStage==2 && teamReviewTime>17)
        {
            GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://artifacts/expedition-team-follow.png"));
            expeditionTeam!.Command("Wait",player.Position);teamReviewStage=3;
        }
        if(teamReviewStage==3 && teamReviewTime>24)
        {
            GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://artifacts/expedition-team-wait.png"));
            SwitchZone(GardenZone.Lobby);ChooseExpeditionTeam();teamReviewStage=4;
        }
        if(teamReviewStage==4 && teamReviewTime>25)
        {
            GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://artifacts/expedition-team-picker.png"));
            GD.Print("TEAM_REVIEW_OK: three live companions, follow, wait, roster captures");GetTree().Quit();
        }
        cameraRig.Follow(player.Position,dt,true);teamUi.Update(expeditionTeam,!teamUi.Choosing);UpdateUi();
    }
}
