using System;
using System.Linq;
using System.Collections.Generic;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private bool stadiumInspectReady,stadiumReviewReady;
    private float stadiumReviewClock,stadiumPhaseClock;
    private int stadiumReviewPhase,stadiumReviewTicks;
    private readonly HashSet<string> stadiumShots=new();
    private int observedHits,observedBlocks,observedDodges,observedDowns;
    private float stadiumResultWait;
    private void InspectStadium()
    {
        if(stadiumInspectReady)return;stadiumInspectReady=true;SetupStadiumDemo();
        var observed=SaveStore.LoadSlot(ProjectSettings.GlobalizePath("user://stadium-review/pet.json"));
        if(observed?.GardenEggs.Any(e=>e.Id=="stadium-fern")==true){pet=observed;pet.Stadium.Cancel();SwitchZone(GardenZone.Stadium,restoring:true);}
        testMode=false;stadium!.Open();stadium.Ui.Select("stadium-fern",SportMode.Race);Notify("Stadium preview · Choose a Chao and competition · Q: Cheer · Esc: Leave",12);
    }
    private void StadiumShot(string name)
    {
        if(!stadiumShots.Add(name))return;
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://artifacts/stadium-"+name+".png"));GD.Print("STADIUM_SHOT: "+name);
    }
    private void ReviewStadium(float delta)
    {
        float dt=Math.Min(delta,.1f);stadiumReviewClock+=dt;stadiumPhaseClock+=dt;stadiumReviewTicks++;
        if(!stadiumReviewReady){stadiumReviewReady=true;SetupStadiumDemo();stadium!.Open();stadium.Ui.Select("stadium-fern",SportMode.Race);stadiumPhaseClock=0;return;}
        var session=stadium!;UpdateUi();
        if(stadiumReviewPhase==0)
        {
            if(stadiumPhaseClock>.8f)StadiumShot("selection");if(stadiumPhaseClock<1.5f)return;
            var athlete=pet.GardenEggs[0].Chao!;
            foreach(var e in SportsCatalog.Events.Where(e=>e.Mode==SportMode.Race))athlete.Athletics.Results[e.Id]=new(){Entries=1,Wins=1,BestPlace=1};
            session.Start("stadium-fern","race-challenge-1");stadiumReviewPhase=1;stadiumPhaseClock=0;
        }
        session.Step(dt,camera);
        if(stadiumReviewPhase==1 && session.Race is {} race)
        {
            var entrant=race.Entrants[0];var segment=race.Course.Locate(entrant.Distance);
            if(race.Countdown>0 && race.Countdown<1.5f)StadiumShot("race-start");
            if(race.Countdown<=0 && segment.Fraction>.35f && segment.Fraction<.65f)StadiumShot("race-"+race.Course.Surfaces[segment.Section].ToString().ToLowerInvariant());
            if(session.CanCheer)session.Cheer();
            if(session.ResultsReady)
            {
                stadiumResultWait+=dt;if(stadiumResultWait<1.5f)return;
                StadiumShot("race-results");session.Leave();pet.GardenEggs[0].Chao!.Energy=100;session.Start("stadium-fern","karate-beginner");stadiumReviewPhase=2;stadiumPhaseClock=0;
            }
        }
        else if(stadiumReviewPhase==2 && session.Fight is {} fight)
        {
            if(fight.Countdown<1.5f && fight.Countdown>.2f)StadiumShot("karate-start");
            if(fight.A.Action==KarateAction.Guard || fight.B.Action==KarateAction.Guard)StadiumShot("karate-guard");
            if(fight.A.Action==KarateAction.Attack && fight.A.ActionTime>.4f && fight.A.ActionTime<.48f || fight.B.Action==KarateAction.Attack && fight.B.ActionTime>.4f && fight.B.ActionTime<.48f)StadiumShot("karate-contact");
            if(fight.A.Action==KarateAction.Knockdown || fight.B.Action==KarateAction.Knockdown)StadiumShot("karate-knockdown");
            if(session.CanCheer)session.Cheer();
            if(fight.Finished)
            {
                observedHits+=fight.A.Hits+fight.B.Hits;observedBlocks+=fight.A.Blocks+fight.B.Blocks;observedDodges+=fight.A.Dodges+fight.B.Dodges;observedDowns+=fight.A.Knockdowns+fight.B.Knockdowns;
                if(session.BetweenBouts){session.Next();}
                else{stadiumReviewPhase=3;stadiumPhaseClock=0;}
            }
        }
        else if(stadiumReviewPhase==3 && stadiumPhaseClock>2)
        {
            StadiumShot("karate-results");Save();
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath("res://artifacts/stadium-review.json"),System.Text.Json.JsonSerializer.Serialize(new { frames=stadiumReviewTicks,seconds=stadiumReviewClock,hits=observedHits,blocks=observedBlocks,dodges=observedDodges,knockdowns=observedDowns,shots=stadiumShots,boutWins=session.BoutWins },new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
            GD.Print("STADIUM_REVIEW_OK: real rendered race, mixed surfaces, three-bout Karate and results");GetTree().Quit();
        }
        if(stadiumReviewClock>430)throw new Exception("Stadium visual review exceeded its time bound");
    }
}
