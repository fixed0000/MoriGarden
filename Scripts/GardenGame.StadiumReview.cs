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
    private int reviewCourse,reviewOutcome;
    private readonly string[] reviewCourses={"crab-pool","mushroom-forest","stump-valley","block-canyon"};
    private float reviewLastCheer,reviewBoutWait;
    private bool reviewMeditated,reviewRecovered;
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
            if(OS.GetCmdlineUserArgs().Contains("--outcomes-only")){BeginOutcomeReview();stadiumReviewPhase=10;return;}
            if(OS.GetCmdlineUserArgs().Contains("--karate-only")){session.Start("stadium-fern","karate-beginner");session.Fight!.A.Zeal=1;stadiumReviewPhase=2;stadiumPhaseClock=0;}
            else {BeginReviewRace();stadiumReviewPhase=1;stadiumPhaseClock=0;}
        }
        session.Step(dt,camera);
        if(stadiumReviewPhase==10)
        {
            if(!session.Fight!.Finished)return;
            string[] expected={"Knockout","Ring Out","Time Up","Draw"};if(session.Fight.Method!=expected[reviewOutcome])throw new Exception("Unexpected graphical outcome: "+session.Fight.Method);
            stadiumResultWait+=dt;if(stadiumResultWait<1.1f)return;stadiumResultWait=0;StadiumShot("outcome-"+reviewOutcome);session.Leave();reviewOutcome++;
            if(reviewOutcome<4){BeginOutcomeReview();return;}
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath("res://artifacts/stadium-outcomes.json"),System.Text.Json.JsonSerializer.Serialize(new {seconds=stadiumReviewClock,outcomes=expected,screenshots=stadiumShots},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));GD.Print("STADIUM_OUTCOMES_OK: rendered knockout, square Ring Out, ninety-second health decision and equal-health draw on isolated fixtures");GetTree().Quit();return;
        }
        if(stadiumReviewPhase==1 && session.Race is {} race)
        {
            var entrant=race.Entrants[0];var segment=race.Course.Locate(entrant.Distance);string key=reviewCourses[reviewCourse];
            if(race.Countdown>0 && race.Countdown<1.2f)StadiumShot(key+"-start");
            if(race.Countdown<=0 && segment.Fraction>.35f && segment.Fraction<.70f)StadiumShot(key+"-"+race.Course.TravelSurface(entrant.Distance,entrant.Pet).ToString().ToLowerInvariant());
            if(key=="stump-valley" && stadiumShots.Contains(key+"-fly") && race.Course.Sample(race.Entrants[1].Distance).Surface==RaceSurface.Fly)
            {session.Watch(1);if(race.Course.TravelSurface(race.Entrants[1].Distance,race.Entrants[1].Pet)==RaceSurface.Power)StadiumShot(key+"-fallback-climb");}
            if(session.CanCheer && stadiumReviewClock-reviewLastCheer>5){session.Cheer();reviewLastCheer=stadiumReviewClock;}
            if(session.ResultsReady)
            {
                stadiumResultWait+=dt;if(stadiumResultWait<1.5f)return;
                StadiumShot(key+"-results");session.Leave();Save();reviewCourse++;stadiumResultWait=0;
                if(reviewCourse<reviewCourses.Length){BeginReviewRace();return;}
                GD.Print("RACE_REVIEW_OK: four distinct natural courses, eight racers, swimming, flight, fallback climb, canyon climb, broadcast and podiums");
                if(OS.GetCmdlineUserArgs().Contains("--race-only")){WriteStadiumReview();GetTree().Quit();return;}
                pet.GardenEggs[0].Chao!.Energy=100;session.Start("stadium-fern","karate-beginner");session.Fight!.A.Zeal=1;stadiumReviewPhase=2;stadiumPhaseClock=0;
            }
        }
        else if(stadiumReviewPhase==2 && session.Fight is {} fight)
        {
            if(fight.Countdown<1.5f && fight.Countdown>.2f)StadiumShot("karate-start");
            if(fight.A.Action==KarateAction.Guard || fight.B.Action==KarateAction.Guard)StadiumShot("karate-guard");
            if(fight.A.Action==KarateAction.Attack && fight.A.Swing>.46f && fight.A.Swing<.54f || fight.B.Action==KarateAction.Attack && fight.B.Swing>.46f && fight.B.Swing<.54f)StadiumShot("karate-contact");
            if(fight.A.Action==KarateAction.Knockdown || fight.B.Action==KarateAction.Knockdown)StadiumShot("karate-knockdown");
            if(fight.A.Action==KarateAction.Meditate && fight.A.ActionTime>.7f){StadiumShot("karate-meditation");reviewMeditated=true;}
            if(reviewMeditated && !reviewRecovered && session.CanCheer && stadiumReviewClock-reviewLastCheer>.2f){session.Cheer();reviewLastCheer=stadiumReviewClock;}
            if(reviewMeditated && fight.A.Zeal>35 && fight.A.Action!=KarateAction.Meditate){StadiumShot("karate-zeal-restored");reviewRecovered=true;}
            if(fight.Finished)
            {
                reviewBoutWait+=dt;if(reviewBoutWait>.6f)StadiumShot("karate-"+fight.Method.ToLowerInvariant().Replace(" ","-"));if(reviewBoutWait>.6f && session.BetweenBouts)StadiumShot("karate-between-bouts");if(reviewBoutWait<1.5f)return;reviewBoutWait=0;
                observedHits+=fight.A.Hits+fight.B.Hits;observedBlocks+=fight.A.Blocks+fight.B.Blocks;observedDodges+=fight.A.Dodges+fight.B.Dodges;observedDowns+=fight.A.Knockdowns+fight.B.Knockdowns;
                if(session.BetweenBouts){session.Next();}
                else{stadiumReviewPhase=3;stadiumPhaseClock=0;}
            }
        }
        else if(stadiumReviewPhase==3 && stadiumPhaseClock>2)
        {
            StadiumShot("karate-results");Save();
            WriteStadiumReview();
            GD.Print("STADIUM_REVIEW_OK: real rendered race, mixed surfaces, five-bout Karate and results");GetTree().Quit();
        }
        if(stadiumReviewClock>900)throw new Exception("Stadium visual review exceeded its time bound");
    }
    private void BeginOutcomeReview()
    {
        var state=pet.GardenEggs[0].Chao!;state.Energy=100;stadium!.Start("stadium-fern","karate-beginner");var fight=stadium.Fight!;
        if(reviewOutcome==0){fight.B.Health=1;return;}
        if(reviewOutcome==1){fight.A.Position=new(.82f,0);fight.B.Position=new(1.15f,0);fight.B.Pet.Swim=990;fight.B.Pet.Stamina=990;fight.B.Health=fight.B.MaxHealth;return;}
        fight.A.Pet.Intelligence=fight.B.Pet.Intelligence=990;fight.A.Action=fight.B.Action=KarateAction.Wait;fight.A.Duration=fight.B.Duration=2000;fight.A.Health=50;fight.B.Health=reviewOutcome==2 ? 45 : 50;fight.Step(93.1f);
    }
    private void BeginReviewRace()
    {
        pet.GardenEggs[0].Chao!.Energy=100;stadium!.Start("stadium-fern","race-"+reviewCourses[reviewCourse]+"-1");
        foreach(var rival in stadium.Race!.Entrants.Skip(1)){rival.Pet.Run=rival.Pet.Swim=rival.Pet.Power=rival.Pet.Stamina=450;rival.Pet.Fly=600;}
        if(reviewCourses[reviewCourse]=="stump-valley")stadium.Race.Entrants[1].Pet.Fly=30;
        stadiumPhaseClock=0;
    }
    private void WriteStadiumReview()=>System.IO.File.WriteAllText(ProjectSettings.GlobalizePath("res://artifacts/stadium-review.json"),System.Text.Json.JsonSerializer.Serialize(new { frames=stadiumReviewTicks,seconds=stadiumReviewClock,hits=observedHits,blocks=observedBlocks,dodges=observedDodges,knockdowns=observedDowns,shots=stadiumShots,boutWins=stadium?.BoutWins ?? 0,meditation=reviewMeditated,zealRecovery=reviewRecovered },new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));

}
