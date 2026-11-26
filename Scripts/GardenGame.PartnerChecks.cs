using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private bool partnerCheckStarted;
    private int partnerCheckFrames;
    private void RunPartnerChecks()
    {if(partnerCheckStarted || ++partnerCheckFrames<3)return;partnerCheckStarted=true;CheckPartnerScene();}
    private async void CheckPartnerScene()
    {
        try
        {
            SetPhysicsProcess(false);GD.Seed(1522);Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
            GetWindow().Title=OS.GetCmdlineUserArgs().Contains("--activity-habits-check") ? "MoriGarden — GENERATED ACTIVITY HABITS TEST" : "MoriGarden — GENERATED SOCIAL PARTNER TEST";
            ChaoIntroductions.Candidate[] actors=Array.Empty<ChaoIntroductions.Candidate>();
            var completed=new System.Collections.Generic.List<object>();
            int HabitCredits(PetState state,ChaoActivity activity)=>state.ActivityHabits.Entries.TryGetValue(activity.ToString(),out var entry) ? entry.ExperienceCount : 0;
            PetState State(string name)=>new(){Name=name,Fullness=80,Energy=80,Fun=40,Joy=70,SocialNeed=90,Run=180,Sociability=90,Dexterous=true,ActivityPreference=5,Personality=new(),PersonalitySchemaVersion=1};
            void ClearLeases()
            {
                foreach(var c in actors){AdapterFor(c.Actor).Coordinator.Suspend("");ImportedChao.ClearActionPause(c.Art);ImportedChao.ClearBallReaction(c.Art);}
            }
            async Task Fixture(bool met=false)
            {
                pet=State("Same name");pet.GardenEggs.Clear();
                pet.GardenEggs.Add(new(){Id="partner-b",Hatched=true,Color="White",X=21.2f,Z=14,Chao=State("Same name")});
                pet.GardenEggs.Add(new(){Id="partner-c",Hatched=true,Color="Black",X=20,Z=15.2f,Chao=State("Same name")});
                petZone=GardenZone.Garden;carrying=false;callTime=0;mainActivity=null;SwitchZone(GardenZone.Garden,restoring:true);
                friend.Position=new(20,0,14);friend.Visible=true;player.Position=new(40,0,30);wanderTarget=friend.Position;wanderTime=0;chaoMotion.Reset();
                await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
                actors=BallParticipants().OrderBy(c=>c.Id=="main" ? 0 : c.Id=="partner-b" ? 1 : 2).ToArray();
                if(actors.Length!=3)throw new Exception("Fixture did not bind three actual Chao");
                foreach(var c in actors)AdapterFor(c.Actor).Begin(0,c.Pet,ActivityContext,false,false,false,false);
                ClearLeases();
                for(int i=0;i<3;i++)for(int j=i+1;j<3;j++)
                {var l=pet.SocialLink(actors[i].Id,actors[j].Id,true)!;l.Met=l.Introduced=met;l.SetScoreFrom(actors[i].Id,0);l.SetScoreFrom(actors[j].Id,0);}
            }
            void Prefer(ChaoIntroductions.Candidate from,ChaoIntroductions.Candidate to)
            {
                pet.SocialLink(from.Id,to.Id,true)!.SetScoreFrom(from.Id,100);
                for(int n=0;n<6;n++)from.Pet.History.Record(ChaoMemoryKind.Pleasant,"prior-"+n,"garden","Prior personal experience.",1,1,new[]{to.Id});
            }
            ToyCar Instrument(CharacterBody3D actor,PetState state,bool flute)
            {var t=flute ? new ToyCar{IsFlute=true} : new ToyCar{IsDrums=true};world.AddChild(t);t.Position=actor.Position;if(!t.BeginPlay(actor,state))throw new Exception("Instrument could not begin");return t;}
            void AdvanceShared(float dt,bool social=true)
            {
                pet.Tick(dt,false);
                foreach(var t in world.GetChildren().OfType<ToyCar>())t.Advance(dt);
                musicGroup.Advance(dt,pet,BallParticipants(),SocialCandidates(true),world.GetChildren().OfType<ToyCar>(),PauseForSocial,allowStart:false);
                if(social)ballGame.Advance(dt,pet,BallParticipants(),SocialCandidates(true),world.GetChildren().OfType<ToyCar>(),PauseForSocial);
                UpdateEggs(dt);MoveFriend(dt);introductions.Advance(dt,pet,SocialCandidates,()=>{},allowStart:false);
            }
            await Fixture();for(int i=0;i<3;i++)Prefer(actors[i],actors[(i+1)%3]);
            int[][] introCounts=actors.Select(_=>new int[3]).ToArray();
            string untouched=JsonSerializer.Serialize(pet);
            for(int seed=0;seed<1000;seed++)for(int i=0;i<3;i++)
            {
                ClearLeases();actors[i].Pet.SocialSelection=new(new Random(seed+1711));
                if(!StartSocialActivity(actors[i].Actor))throw new Exception("Actual weighted introduction starter failed");
                int selected=Array.FindIndex(actors,c=>c.Id==actors[i].Pet.SocialSelection.LastChosenPartner);
                if(selected<0 || selected==i)throw new Exception("Introduction used wrong stable identity");introCounts[i][selected]++;introductions.Cancel();
            }
            if(untouched!=JsonSerializer.Serialize(pet))throw new Exception("Introduction choice/cancel mutated persisted state");
            for(int i=0;i<3;i++)if(introCounts[i][(i+1)%3]<=introCounts[i][(i+2)%3])throw new Exception("Native introduction selection ignored directional preferences");
            GD.Print("PARTNER_INTRO_COUNTS: "+JsonSerializer.Serialize(introCounts));
            ClearLeases();pet.SocialSelection=new(new Random(181));
            if(!StartSocialActivity(friend))throw new Exception("Real introduction did not start");
            string introduced=pet.SocialSelection.LastChosenPartner!;
            int introScoreBefore=pet.SocialLink("main",introduced)!.ScoreFrom("main");
            for(int n=0;n<1600 && introductions.Active;n++){AdvanceShared(.02f,false);if(n%180==0)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
            var peer=actors.Single(c=>c.Id==introduced);
            if(introductions.Active || pet.SocialLink("main",introduced)?.Introduced!=true ||
                pet.History.Events.Single(e=>e.Source=="introduction").Occurrences!=1 || peer.Pet.History.Events.Single(e=>e.Source=="introduction").Occurrences!=1 ||
                introductions.CanStart(pet,actors[0],peer))throw new Exception("Original introduction did not complete exactly once");
            completed.Add(new{activity="Introduction",initiator="main",partner=introduced,scoreBefore=introScoreBefore,
                scoreAfter=pet.SocialLink("main",introduced)!.ScoreFrom("main"),weights=pet.SocialSelection.LastCandidates,
                mainMemories=pet.History.Events.Where(e=>e.Source=="introduction").ToArray(),peerMemories=peer.Pet.History.Events.Where(e=>e.Source=="introduction").ToArray()});
            GD.Print("PARTNER_INTRO_OK: real Game acquisition, all original phases and one original Together record per Chao");
            if(actors.Any(c=>HabitCredits(c.Pet,ChaoActivity.Socialize)!=0))throw new Exception("One-time introductions taught a repeatable social habit");

            await Fixture(true);Prefer(actors[0],actors[1]);
            var leadB=Instrument(actors[1].Actor,actors[1].Pet,true);var leadC=Instrument(actors[2].Actor,actors[2].Pet,true);
            var drums=new ToyCar{IsDrums=true};world.AddChild(drums);drums.Position=friend.Position+Vector3.Forward*.4f;
            int[] musicCounts=new int[2];
            for(int seed=0;seed<1000;seed++)
            {
                pet.Fun=5;drums.BeginPlay(friend,pet);drums.CancelPlay(friend);drums.Position=friend.Position+Vector3.Forward*.4f;
                pet.SocialSelection=new(new Random(seed+1911));
                if(!musicGroup.TryJoin(pet,actors,new[]{actors[0]},new[]{leadB,leadC,drums}))throw new Exception("Valid musician/instrument choices missing");
                musicCounts[musicGroup.LeaderId=="partner-b" ? 0 : 1]++;musicGroup.Cancel();
            }
            if(musicCounts[0]<=musicCounts[1])throw new Exception("Music partner preference did not affect the real join executor");
            pet.Fun=5;drums.BeginPlay(friend,pet);drums.CancelPlay(friend);drums.Position=friend.Position+Vector3.Forward*.4f;ClearLeases();
            if(!musicGroup.TryJoin(pet,actors,new[]{actors[0]},new[]{leadB,leadC,drums}))throw new Exception("Real music join did not start");
            string musician=musicGroup.LeaderId!;PauseForSocial(actors[0]);
            string musicRelationships=JsonSerializer.Serialize(pet.SocialLinks);
            for(int n=0;n<2400 && musicGroup.Active;n++){AdvanceShared(.02f,false);if(n%180==0)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
            if(drums.PlayActor!=friend || musicGroup.Active)throw new Exception("Real thinking/approach/play handoff failed");
            var originalPet=pet;var roster=BallParticipants().ToDictionary(c=>c.Id,c=>c.Pet);
            string directory=Path.GetDirectoryName(savePath)!;var bridge=new ChaoEditorBridge(Path.Combine(directory,"editor"));bridge.Poll(roster,true);
            var snapshot=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"editor/chao-editor-state.json")));
            File.WriteAllText(Path.Combine(directory,"editor/chao-editor-request.json"),JsonSerializer.Serialize(new{session=snapshot.RootElement.GetProperty("session").GetString(),id="ensemble-edit",target="main",patch=new{Fullness=75}}));
            pet=bridge.Poll(roster,true) ?? throw new Exception("Live ensemble editor rebind failed");RebindGardenExecutors("main",pet);
            if(!ReferenceEquals(drums.PlayState,pet))throw new Exception("Instrument retained stale PetState");
            bool naturalNeedChecked=false;
            for(int n=0;n<4000 && !pet.History.Events.Any(e=>e.Source=="music-ensemble");n++)
            {
                pet.Tick(.02f,false);
                float before=pet.SocialNeed;foreach(var t in new[]{leadB,leadC,drums})t.Advance(.02f);
                if(pet.History.Events.Any(e=>e.Source=="music-ensemble")){naturalNeedChecked=pet.SocialNeed==before;break;}
                UpdateEggs(.02f);MoveFriend(.02f);if(n%180==0)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            }
            var musicPeer=actors.Single(c=>c.Id==musician).Pet;
            if(!naturalNeedChecked || pet.History.Events.Single(e=>e.Source=="music-ensemble").Participants.Single()!=musician ||
                musicPeer.History.Events.Single(e=>e.Source=="music-ensemble").Participants.Single()!="main" ||
                originalPet.History.Events.Any(e=>e.Source=="music-ensemble"))throw new Exception("Natural ensemble memory or rebind/need isolation failed");
            if(musicRelationships!=JsonSerializer.Serialize(pet.SocialLinks))throw new Exception("Music added relationship rewards");
            if(HabitCredits(pet,ChaoActivity.Socialize)!=1 || HabitCredits(musicPeer,ChaoActivity.Socialize)!=1 ||
                HabitCredits(pet,ChaoActivity.Play)!=0 || HabitCredits(musicPeer,ChaoActivity.Play)!=0)throw new Exception("Ensemble must teach only one Socialize habit per participant");
            completed.Add(new{activity="Music",initiator="main",partner=musician,weights=pet.SocialSelection.LastCandidates,
                mainMemories=pet.History.Events.Where(e=>e.Source=="music-ensemble").ToArray(),peerMemories=musicPeer.History.Events.Where(e=>e.Source=="music-ensemble").ToArray(),
                relationshipsBefore=musicRelationships,relationshipsAfter=JsonSerializer.Serialize(pet.SocialLinks),completionNeedDelta=0});
            GD.Print("PARTNER_MUSIC_OK: counts="+JsonSerializer.Serialize(musicCounts)+"; actual join, natural finish, two memories, no extra need reward, editor rebind");

            await Fixture(true);var first=Instrument(friend,pet,true);var second=Instrument(actors[1].Actor,actors[1].Pet,false);
            second.StartWith(first,"partner-b","main");first.CancelPlay(friend);second.Advance(61);
            if(actors.Any(c=>c.Pet.History.Events.Any(e=>e.Source=="music-ensemble")))throw new Exception("Forced ensemble cleanup recorded success");
            if(actors.Any(c=>c.Pet.ActivityHabits.Entries.Count!=0))throw new Exception("Cancelled ensemble taught a habit");
            await Fixture(true);first=Instrument(actors[1].Actor,actors[1].Pet,true);drums=new(){IsDrums=true};world.AddChild(drums);drums.Position=friend.Position+Vector3.Forward*.4f;
            if(!musicGroup.TryJoin(pet,actors,new[]{actors[0]},new[]{first,drums}))throw new Exception("Missing-actor fixture could not start");
            musicGroup.Advance(.1f,pet,new[]{actors[0]},Array.Empty<ChaoIntroductions.Candidate>(),new[]{first,drums},allowStart:false);
            if(musicGroup.Active || pet.History.Events.Any(e=>e.Source=="music-ensemble"))throw new Exception("Missing performer did not cancel cleanly");

            await Fixture(true);for(int i=0;i<3;i++)Prefer(actors[i],actors[(i+1)%3]);
            var beach=new ToyCar{IsBeachBall=true};world.AddChild(beach);int[][] ballCounts=actors.Select(_=>new int[3]).ToArray();
            for(int seed=0;seed<500;seed++)for(int i=0;i<3;i++)
            {
                ClearLeases();actors[i].Pet.Fun=5;actors[i].Pet.SocialSelection=new(new Random(seed+2011));
                beach.BeginPlay(actors[i].Actor,actors[i].Pet);ballGame.Advance(ChaoThought.Minimum,pet,actors,actors,new[]{beach});
                int selected=Array.FindIndex(actors,c=>c.Id==ballGame.PartnerId);if(selected<0 || selected==i)throw new Exception("Ball partner identity/eligibility failed");
                ballCounts[i][selected]++;ballGame.Cancel();
            }
            for(int i=0;i<3;i++)if(ballCounts[i][(i+1)%3]<=ballCounts[i][(i+2)%3])throw new Exception("Native ball choices ignored actor-specific experience");
            if(actors.Any(c=>c.Pet.History.Events.Any(e=>e.Source=="shared-ball")))throw new Exception("Cancelled invitations recorded completed games");
            GD.Print("PARTNER_BALL_COUNTS: "+JsonSerializer.Serialize(ballCounts));
            // Complete twice, once with main inviter and once with an actual resident inviter.
            for(int inviterIndex=0;inviterIndex<2;inviterIndex++)
            {
                await Fixture(true);ClearLeases();var inviter=actors[inviterIndex];var invited=actors[(inviterIndex+1)%3];
                actors[(inviterIndex+2)%3].Actor.Position=new(35,0,25);
                beach=new(){IsBeachBall=true};world.AddChild(beach);beach.BeginPlay(inviter.Actor,inviter.Pet);
                // Establish the invitation while both initial candidates are genuinely idle.
                // Otherwise their independent coordinator may legitimately begin an atomic
                // activity during consideration, leaving no eligible receiver.
                ballGame.Advance(ChaoThought.Minimum,pet,BallParticipants(),SocialCandidates(true),new[]{beach},PauseForSocial);
                if(ballGame.PartnerId!=invited.Id)throw new Exception("Idle ball receiver was not eligible");
                bool finalNeed=false;string favorites=inviter.Pet.FavoriteFruit+inviter.Pet.FavoriteToy+inviter.Pet.FavoriteInstrument;var innate=inviter.Pet.Personality!.Copy();
                string ballRelationships=JsonSerializer.Serialize(pet.SocialLinks);float[] finalNeedDelta=new float[2];
                for(int n=0;n<4000;n++)
                {
                    pet.Tick(.02f,false);
                    float needA=inviter.Pet.SocialNeed,needB=invited.Pet.SocialNeed;int pass=ballGame.CompletedPasses;
                    ballGame.Advance(.02f,pet,BallParticipants(),SocialCandidates(true),new[]{beach},PauseForSocial);
                    if(ballGame.CompletedPasses==10 && pass==9){finalNeed=Math.Abs(inviter.Pet.SocialNeed-Math.Max(0,needA-8))<.001 && Math.Abs(invited.Pet.SocialNeed-Math.Max(0,needB-8))<.001;finalNeedDelta=new[]{inviter.Pet.SocialNeed-needA,invited.Pet.SocialNeed-needB};}
                    if(!ballGame.Active && n>2)break;
                    UpdateEggs(.02f);MoveFriend(.02f);if(n%180==0)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                }
                if(!finalNeed || ballGame.Active || ballGame.CompletedPasses!=10 ||
                    inviter.Pet.History.Events.Single(e=>e.Source=="shared-ball").Participants.Single()!=invited.Id ||
                    invited.Pet.History.Events.Single(e=>e.Source=="shared-ball").Participants.Single()!=inviter.Id ||
                    !inviter.Pet.Personality.Equals(innate) || favorites!=inviter.Pet.FavoriteFruit+inviter.Pet.FavoriteToy+inviter.Pet.FavoriteInstrument)
                    throw new Exception("Actual ten-pass game failed: inviter="+inviter.Id+", stage="+ballGame.Stage+", passes="+ballGame.CompletedPasses+", need="+finalNeed);
                if(ballRelationships!=JsonSerializer.Serialize(pet.SocialLinks))throw new Exception("Ball added relationship rewards");
                if(HabitCredits(inviter.Pet,ChaoActivity.Socialize)!=1 || HabitCredits(invited.Pet,ChaoActivity.Socialize)!=1 ||
                    HabitCredits(inviter.Pet,ChaoActivity.Play)!=0 || HabitCredits(invited.Pet,ChaoActivity.Play)!=0)throw new Exception("Shared ball must teach Socialize, never Play or ten credits");
                completed.Add(new{activity="Ball",initiator=inviter.Id,partner=invited.Id,passes=ballGame.CompletedPasses,weights=inviter.Pet.SocialSelection.LastCandidates,
                    inviterMemories=inviter.Pet.History.Events.Where(e=>e.Source=="shared-ball").ToArray(),peerMemories=invited.Pet.History.Events.Where(e=>e.Source=="shared-ball").ToArray(),
                    relationshipsBefore=ballRelationships,relationshipsAfter=JsonSerializer.Serialize(pet.SocialLinks),finalNeedDelta});
            }
            Save();var loaded=SaveStore.Load(savePath,out _);
            if(loaded.GardenEggs.Count!=2 || !loaded.GardenEggs[0].Chao!.History.Events.Any(e=>e.Source=="shared-ball") || loaded.GardenEggs[0].Id!="partner-b")throw new Exception("Partner memory save/load lost identity or experience");
            var restored=ExpeditionRoster.Available(loaded).ToDictionary(c=>c.Id,c=>c.State);
            if(ChaoSocialPartnerScorer.Evaluate(loaded,"partner-b",restored["partner-b"],"partner-c").MemoryContribution<=0)throw new Exception("Restored experience did not affect subsequent selection");
            GD.Print("PARTNER_BALL_OK: real main/resident ten-pass games, stable reciprocal memory, exactly existing pass rewards, save/load");
            await Fixture(true);ClearLeases();actors[1].Pet.Energy=30;
            beach=new(){IsBeachBall=true};world.AddChild(beach);beach.BeginPlay(friend,pet);
            ballGame.Advance(ChaoThought.Minimum,pet,actors,new[]{actors[1]},new[]{beach});
            for(int n=0;n<1000 && ballGame.Active;n++)ballGame.Advance(.05f,pet,actors,new[]{actors[1]},new[]{beach});
            if(ballGame.Active || actors.Any(c=>c.Pet.History.Events.Any(e=>e.Source=="shared-ball" || e.Source=="ball-invitation")))throw new Exception("Tired refusal created false shared/negative partner memory");
            beach.BeginPlay(friend,pet);ballGame.Advance(ChaoThought.Minimum,pet,actors,new[]{actors[2]},new[]{beach});ballGame.Advance(71,pet,actors,actors,new[]{beach});
            if(ballGame.Active || actors.Any(c=>c.Pet.History.Events.Any(e=>e.Source=="shared-ball")))throw new Exception("Timed-out ball game credited shared completion");
            await Fixture(true);ClearLeases();AdapterFor(actors[1].Actor).ReserveSocial();Prefer(actors[0],actors[1]);
            // Reset introductions only; the favored atomic participant must remain protected.
            foreach(var l in pet.SocialLinks)l.Introduced=false;
            if(!StartSocialActivity(friend) || pet.SocialSelection.LastChosenPartner!="partner-c" || AdapterFor(actors[1].Actor).Coordinator.CanJoinSocial)
                throw new Exception("Preferred protected partner was stolen");introductions.Cancel();
            foreach(var l in pet.SocialLinks)l.Introduced=true;ClearLeases();
            foreach(var t in world.GetChildren().OfType<ToyCar>())t.Free();
            if(SocialActivityTargets(friend)!=0)throw new Exception("No-repeatable-activity fixture invented social availability");
            MainActivity.Select(true);if(MainActivity.Coordinator.SelectedActivity==ChaoActivity.Socialize)throw new Exception("No eligible social activity did not fall back");
            GD.Print("PARTNER_SAFETY_OK: reserved actor protected, no repeatable social activity remains unavailable");
            if(OS.GetCmdlineUserArgs().Contains("--activity-habits-check"))await CheckHabitExecutors(restored["partner-b"]);
            File.WriteAllText(Path.Combine(directory,"partner-check.json"),JsonSerializer.Serialize(new{passed=true,introCounts,musicCounts,ballCounts,completed,
                cancelledChoices=new{introductions=3000,music=1000,ball=1500},tiredRefusal=true,ballTimeout=true,missingPerformer=true,forcedEnsembleCleanup=true,
                mainAndResident=true,editorRebind=true,saveLoad=true,noDuplicateNeeds=true}));
            GD.Print("PARTNER_SCENE_OK");GetTree().Quit();
        }
        catch(Exception error){GD.PushError("PARTNER_SCENE_FAILED: "+error);GetTree().Quit(1);}
    }
}
