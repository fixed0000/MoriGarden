using System;
using System.Linq;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private int stadiumCheckFrames;
    private void RunStadiumCheckMode()
    {
        if(++stadiumCheckFrames<3)return;
        try{CheckStadium();GetTree().Quit();}catch(Exception ex){GD.PushError("STADIUM_CHECK_FAILED: "+ex);GetTree().Quit(1);}
    }
    private void SetupStadiumDemo()
    {
        pet=new PetState{Name="Mori",Run=200,Power=180,Stamina=220,Energy=100};pet.GardenEggs.Clear();
        pet.GardenEggs.Add(new(){Id="stadium-fern",Color="Green",Hatched=true,Chao=new(){Name="Fern",Run=650,Swim=650,Fly=650,Power=650,Stamina=650,Intelligence=550,Initiative=600,Aggressiveness=-15,Energy=100,Bond=90}});
        pet.GardenEggs.Add(new(){Id="stadium-pip",Color="Black",Hatched=true,Chao=new(){Name="Pip",Run=160,Swim=300,Fly=120,Power=80,Stamina=180,Initiative=150,Energy=100}});
        carrying=false;paused=pausedByFocus=false;slotMode=SlotMode.Closed;petZone=GardenZone.Garden;SwitchZone(GardenZone.Stadium,restoring:true);player.Position=new(0,0,2.8f);cameraRig.Follow(player.Position,0,true);portalCooldown=3;messageTime=0;
    }
    private void CheckStadium()
    {
        var original=pet;var originalZone=zone;var originalPetZone=petZone;var originalAt=petZonePosition;
        try
        {
            SetupStadiumDemo();var athlete=pet.GardenEggs[0].Chao!;int residentCount=pet.GardenEggs.Count;var session=stadium!;
            athlete.Energy=5;session.Start("stadium-fern","race-crab-pool-1");if(session.InCompetition)throw new Exception("Exhausted Chao entered the competition");athlete.Energy=100;
            Interact();if(!session.Ui.MenuVisible)throw new Exception("Stadium desk did not open its selector");session.Ui.Select("stadium-fern",SportMode.Race);session.Start("stadium-fern","race-crab-pool-1");
            if(session.Race?.Entrants.Count!=8 || !ReferenceEquals(session.Entrant!.State,athlete) || session.View!.Bodies.Count!=8 || friend.Visible)throw new Exception("Stadium did not load the selected real Chao and seven rivals");
            for(int i=0;i<2000 && !session.ResultsReady;i++)session.Step(.1f,camera);
            if(!session.ResultsReady || session.Prize==null || athlete.Athletics.Results["race-crab-pool-1"].Entries!=1 || pet.Athletics.Results.Count!=0)throw new Exception("Selected athlete race result was not awarded individually");
            int stars=pet.Stars;session.Step(.1f,camera);if(pet.Stars!=stars)throw new Exception("Results granted a repeated reward");
            session.Leave();athlete.Energy=100;session.Start("stadium-fern","karate-beginner");
            int bouts=0,contacts=0;float contactGap=0;
            for(int i=0;i<4000 && !session.ResultsReady;i++){session.Step(.1f,camera);contacts+=session.View!.ContactSamples;contactGap=Mathf.Max(contactGap,session.View.MaxHitGap);if(session.BetweenBouts){bouts++;session.Next();}}
            if(contacts==0 || contactGap>.22f)throw new Exception("Karate strikes missed their visual contact: "+contactGap);
            if(!session.ResultsReady || session.Prize==null || session.Fight==null || session.BoutWins!=5 || bouts!=4 || !athlete.Athletics.Cups.Contains("karate-beginner"))throw new Exception("Karate tournament failed: "+session.BoutWins+" wins / "+session.Fight?.Method);
            session.Leave();Save();var loaded=SaveStore.Load(savePath,out var warning);
            if(warning.Length>0 || loaded.GardenEggs[0].Chao!.Athletics.Cups.Count!=athlete.Athletics.Cups.Count || loaded.GardenEggs.Count!=residentCount)throw new Exception("Stadium save lost records or duplicated pets");
            paused=true;slotMode=SlotMode.Save;RefreshSaveSlots();
            for(int slot=0;slot<SaveSlotCount;slot++){saveSlots.Select(slot);SaveToSlot();if(SaveStore.LoadSlot(SlotPath(slot))?.GardenEggs[0].Chao?.Athletics.Cups.Contains("karate-beginner")!=true)throw new Exception("Sports history was lost in slot "+slot);}
            LoadFromSlot();session=stadium!;athlete=pet.GardenEggs.Single(e=>e.Id=="stadium-fern").Chao!;
            if(zone!=GardenZone.Stadium || stadium!.InCompetition || athlete.Athletics.Results["karate-beginner"].Wins!=1)throw new Exception("Loading a stadium slot did not restore its athlete safely");
            session.Start("main","race-crab-pool-1");if(!ReferenceEquals(session.Entrant!.State,pet))throw new Exception("Selecting Mori did not resolve the real main pet");session.Leave();
            athlete.Energy=100;session.Start("stadium-fern","race-crab-pool-1");Save();loaded=SaveStore.Load(savePath,out _);loaded.Stadium.Cancel();if(loaded.Stadium.ActiveTicket!=null)throw new Exception("Interrupted competition was not safely cancelled");session.Leave();
            SwitchZone(GardenZone.Garden,restoring:true);
            if(!world.GetChildren().OfType<ToyCar>().Any(t=>t.StadiumPrize && t.IsBeachBall))throw new Exception("Tournament toy was not integrated with the garden");
            if(!ReferenceEquals(pet.GardenEggs[0].Chao,athlete) || pet.GardenEggs.Count!=residentCount)throw new Exception("Garden athlete state was replaced");
            GD.Print("STADIUM_OK: real selected pet, eight racers, ordered finish, single receipt, five-bout Karate, visual strike contact, individual cups, all 20 slots and real load, interrupted entry, garden prize toy and unchanged resident identities");
        }
        finally{stadium?.Leave();pet=original;petZone=originalPetZone;petZonePosition=originalAt;SwitchZone(originalZone,restoring:true);}
    }
}
