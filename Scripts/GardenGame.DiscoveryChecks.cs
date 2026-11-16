using System;
using System.Linq;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private void CheckDiscoveries()
    {
        var original=expeditionTeam!;
        foreach(var member in original.Members){world.RemoveChild(member.Body);member.Body.QueueFree();}expeditionTeam=null;
        var interests=world.GetChildren().OfType<DiscoveryInterest>().ToArray();
        var flower=interests.First(i=>i.Definition.Kind==DiscoveryKind.Plant);
        var available=interests.Select(i=>i.Available).ToArray();foreach(var item in interests)item.Available=item==flower;
        ExpeditionTeam? team=null;
        PetState owner=new();
        var observer=flower.Position+new Vector3(3,0,4);
        void Advance(int ticks,Vector3? at=null) {for(int i=0;i<ticks;i++)team!.Step(1f/60,at ?? observer,camera);}
        void Prepare()
        {
            team!.Discoveries.CancelAll("fixture reset");
            foreach(var m in team.Members){m.Partner=null;m.SetIntent(ExpeditionIntent.Observe,m.Body.Position,100);m.Pet.State.Energy=90;}
            Advance(250);
            foreach(var m in team.Members){m.SetIntent(ExpeditionIntent.Observe,m.Body.Position,100);owner.Expedition.Memory(m.Pet.Id).Objects.Clear();}
        }
        try
        {
            for(int count=1;count<=3;count++)
            {
                if(team!=null)foreach(var m in team.Members){world.RemoveChild(m.Body);m.Body.QueueFree();}
                owner=new PetState { Curiosity=100,Initiative=900,Run=200,Bond=100,Sociability=100 };owner.GardenEggs.Clear();
                for(int i=1;i<count;i++)owner.GardenEggs.Add(new() { Id="discovery-peer-"+i,Hatched=true,Color=i==1 ? "Black" : "Yellow",
                    Chao=new() { Run=180,Curiosity=90,Initiative=850,Sociability=100,Energy=90,Bond=90 } });
                owner.Expedition.LastTeamIds=ExpeditionRoster.Available(owner).Select(p=>p.Id).ToList();
                foreach(var entry in owner.GardenEggs){var relation=owner.SocialLink("main",entry.Id,true)!;relation.Met=true;relation.Score=50;}
                team=new(world,owner,observer);team.Discoveries.AutoScan=false;
                var nav=new ExpeditionNavigation(world);
                for(int i=0;i<count;i++)
                {
                    var m=team.Members[i];float a=i*Mathf.Tau/3;
                    m.Body.Position=nav.Nearest(flower.Position+new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*2.6f);
                    m.Body.Rotation=new(0,Mathf.Atan2(flower.Position.X-m.Body.Position.X,flower.Position.Z-m.Body.Position.Z),0);
                    m.SetIntent(ExpeditionIntent.Observe,m.Body.Position,100);
                    if(!team.Discoveries.Perception.Visible(m,flower))throw new Exception("Visible flower was not perceived");
                    if(!team.Discoveries.Start(m,flower,new(DiscoveryChoice.Approach,4,i==0,false),true))throw new Exception("A free discovery slot was not reserved");
                }
                if(team.Discoveries.Reservations.Count!=count || team.Discoveries.Sessions.Select(s=>s.Spot).Distinct().Count()!=count)
                    throw new Exception("Shared discovery reservations overlap");
                bool called=false,acknowledged=false;
                for(int step=0;step<3000;step++)
                {
                    var first=team.Discoveries.Sessions.FirstOrDefault(s=>s.Pet==team.Members[0]);
                    if(first?.Phase==DiscoveryPhase.AwaitPlayer)called=true;
                    team.Step(1f/60,called ? flower.Position+Vector3.Back*2 : observer,camera);
                    if(team.Discoveries.Sessions.Any(s=>s.Phase==DiscoveryPhase.Acknowledge))acknowledged=true;
                }
                if(!called || !acknowledged || team.Members.Any(m=>owner.Expedition.Memory(m.Pet.Id).Find(flower.Definition.Id)?.Visits!=1 || m.Recoveries!=0))
                    throw new Exception($"Discovery sequence / attention / identity failed for {count}: {string.Join(';',team.Discoveries.Events)}");
                if(team.Discoveries.Reservations.Count!=0)throw new Exception("Finished discoveries retained interaction slots");
                if(team.Members.Any(m=>!m.Pet.State.History.Events.Any(e=>e.Source==flower.Definition.Id && e.Kind==ChaoMemoryKind.IndependentStudy)) ||
                    !team.Members[0].Pet.State.History.Events.Any(e=>e.Source==flower.Definition.Id && e.Kind==ChaoMemoryKind.PlayerDiscovery))
                    throw new Exception("Actual research or player acknowledgement did not enter long-term memory");
                var historyCopy=SaveStore.Snapshot(owner);
                if(historyCopy.History.Events.Count!=owner.History.Events.Count || historyCopy.GardenEggs.Any(e=>e.Chao!.History.Events.Count==0))throw new Exception("Expedition memories did not survive a save snapshot");
                if(count>1 && !owner.Expedition.Discoveries.Values.Any(m=>m.SharedRewards.Count>0))throw new Exception("Shared experience did not use existing directed relationships");
                var subject=team.Members[0];
                if(team.Discoveries.Start(subject,flower,new(DiscoveryChoice.Approach,4,false,false),false))throw new Exception("Recent study restarted immediately");
                Prepare();
                foreach(string command in new[]{"Wait","Follow","Call"})
                {
                    subject.Pet.State.Bond=100;
                    if(!team.Discoveries.Start(subject,flower,new(DiscoveryChoice.Approach,4,false,false),true))throw new Exception("Command-cancellation setup failed");
                    team.Command(command,observer);
                    if(team.Discoveries.Sessions.Count!=0 || team.Discoveries.Reservations.Count!=0 || subject.Intent==ExpeditionIntent.Investigate)
                        throw new Exception(command+" failed to cancel research");
                    Prepare();
                }
                team.Discoveries.Start(subject,flower,new(DiscoveryChoice.Approach,4,false,false),true);
                Advance(1,observer+Vector3.Right*30);
                if(team.Discoveries.Sessions.Count!=0)throw new Exception("An exploring Chao did not return when the player left");
                Prepare();team.Discoveries.Start(subject,flower,new(DiscoveryChoice.Approach,4,false,false),true);
                subject.Pet.State.Energy=5;Advance(1);
                if(team.Discoveries.Sessions.Count!=0 || subject.Intent!=ExpeditionIntent.Sleep)throw new Exception("Fatigue failed to preempt study");
                Prepare();team.Discoveries.Start(subject,flower,new(DiscoveryChoice.Approach,4,false,false),true);flower.Available=false;Advance(1);flower.Available=true;
                if(team.Discoveries.Sessions.Count!=0 || team.Discoveries.Reservations.Count!=0)throw new Exception("Unavailable discovery retained its owner");
                Prepare();
                var temporary=new DiscoveryInterest { Definition=flower.Definition with { Id="test/disappearing" },Position=flower.Position };world.AddChild(temporary);
                team.Discoveries.Start(subject,temporary,new(DiscoveryChoice.Approach,4,false,false),true);temporary.Free();Advance(1);
                if(team.Discoveries.Sessions.Count!=0 || team.Discoveries.Reservations.Count!=0)throw new Exception("Destroyed objects left a dangling research session");
                Prepare();
                var gated=new DiscoveryInterest { Definition=flower.Definition with { Id="test/gated",RequiredFlag="test-unlock" },Position=flower.Position };world.AddChild(gated);
                if(team.Discoveries.Start(subject,gated,new(DiscoveryChoice.Approach,4,false,false),true))throw new Exception("A locked interest ignored its availability flag");
                owner.Expedition.AreaFlags[WorldCatalog.Meadow]=new() { "test-unlock" };
                if(!team.Discoveries.Start(subject,gated,new(DiscoveryChoice.Approach,4,false,false),true))throw new Exception("An unlocked interest remained inaccessible");
                team.Discoveries.CancelAll("fixture reset");gated.Free();
                Prepare();team.Discoveries.Start(subject,flower,new(DiscoveryChoice.Approach,4,false,false),true);
                subject.SetIntent(ExpeditionIntent.Rest,subject.Body.Position,20);Advance(1);
                if(team.Discoveries.Sessions.Count!=0 || subject.Intent!=ExpeditionIntent.Rest)throw new Exception("Research overwrote a higher priority action");
                Prepare();team.Discoveries.Start(subject,flower,new(DiscoveryChoice.Approach,4,false,false),true);
                subject.Pet.State.Bond=0;team.Command("Call",observer);
                if(subject.Intent==ExpeditionIntent.Follow || team.Discoveries.Sessions.Count!=0)throw new Exception("Cancelling research bypassed a low-bond response");
                subject.Pet.State.Bond=100;
                Prepare();subject.Pet.State.Run=0;ImportedChao.ApplyDevelopment(subject.Art,subject.Pet.State);
                ImportedChao.MovePose(subject.Art,.5f,true,false,false,false,false,false);
                team.Discoveries.Start(subject,flower,new(DiscoveryChoice.Approach,4,false,false),true);Advance(10);
                if(!subject.Transitioning || team.Discoveries.Sessions.First().PhaseTime>.05f)throw new Exception("A crawling researcher skipped its existing get-up animation");
                subject.Pet.State.Run=200;
                Prepare();
                subject.Body.Position=nav.Nearest(flower.Position+Vector3.Back*3);subject.SetIntent(ExpeditionIntent.Observe,subject.Body.Position,100);
                team.Discoveries.Start(subject,flower,new(DiscoveryChoice.Approach,4,false,false),true);
                var enclosure=new StaticBody3D { Position=subject.Body.Position };world.AddChild(enclosure);
                foreach(var axis in new[]{Vector3.Right,Vector3.Left,Vector3.Back,Vector3.Forward})
                    enclosure.AddChild(new CollisionShape3D { Position=axis*.6f+Vector3.Up*.6f,Shape=new BoxShape3D { Size=axis.X!=0 ? new(.12f,1.5f,1.4f) : new Vector3(1.4f,1.5f,.12f) } });
                Advance(1500);world.RemoveChild(enclosure);enclosure.QueueFree();
                if(team.Discoveries.Sessions.Count!=0 || !team.Discoveries.Events.Any(e=>e.Action=="cancelled: blocked"))throw new Exception("An unreachable discovery did not cancel safely");
                Prepare();
                // Visibility must reject both a rearward object and a physically occluded one.
                subject.Body.Position=nav.Nearest(flower.Position+Vector3.Back*3);
                subject.Body.Rotation=new(0,0,0);
                if(team.Discoveries.Perception.Visible(subject,flower))throw new Exception("Rearward objects bypassed the field of view");
                subject.Body.Rotation=new(0,Mathf.Pi,0);
                var wall=new StaticBody3D { Position=(subject.Body.Position+flower.Position)*.5f+Vector3.Up*.7f };
                wall.AddChild(new CollisionShape3D { Shape=new BoxShape3D { Size=new(3,2,.45f) } });world.AddChild(wall);
                team.Discoveries.Scan(subject);
                if(owner.Expedition.Memory(subject.Pet.Id).Find(flower.Definition.Id)!=null || team.Discoveries.Perception.Visible(subject,flower))
                    throw new Exception("An occluded object leaked into Chao knowledge");
                world.RemoveChild(wall);wall.QueueFree();
                int rays=team.Discoveries.Perception.VisibilityQueries;team.Discoveries.AutoScan=true;Advance(1200);
                if(team.Discoveries.Perception.VisibilityQueries-rays>70)throw new Exception("Perception exceeded its periodic visibility budget");
                if(count>1)
                {
                    team.Discoveries.AutoScan=false;Prepare();
                    for(int i=0;i<count;i++)
                    {
                        var m=team.Members[i];m.Body.Position=nav.Nearest(flower.Position+(i==0 ? Vector3.Back : i==1 ? Vector3.Right : Vector3.Left)*2.4f);
                        m.SetIntent(ExpeditionIntent.Observe,m.Body.Position,100);
                    }
                    var recipient=team.Members[1];owner.Expedition.Memory(recipient.Pet.Id).Notice(flower.Definition.Id,owner.Expedition.ExplorationSeconds);
                    int startEvent=team.Discoveries.Events.Count;
                    bool invitedSetup=team.Discoveries.Start(subject,flower,new(DiscoveryChoice.Approach,4,false,true),true);Advance(2400);
                    if(!team.Discoveries.Events.Skip(startEvent).Any(e=>e.Action is "invited peer" or "declined invitation"))throw new Exception($"A sociable explorer never invited an eligible peer: {count}, started {invitedSetup}, peer busy {recipient.Busy}, ready {recipient.ReadyForDiscovery}, last invite {owner.Expedition.Memory(recipient.Pet.Id).LastInvitation}, time {owner.Expedition.ExplorationSeconds}; {string.Join(';',team.Discoveries.Events.TakeLast(14))}");
                    Prepare();owner.Expedition.ExplorationSeconds+=91;
                    team.Discoveries.Start(subject,flower,new(DiscoveryChoice.Approach,4,true,true),true);Advance(540);
                    team.Discoveries.Start(recipient,flower,new(DiscoveryChoice.Observe,4,false,false),true);Advance(2400);
                    if(team.Discoveries.Events.Any(e=>e.Pet==recipient.Pet.Id && e.Action=="cancelled: joining peer") &&
                        owner.Expedition.Memory(recipient.Pet.Id).Find(flower.Definition.Id)?.Visits!=1)
                        throw new Exception("Joining a peer discarded the replacement research session");
                    Prepare();var relation=owner.SocialLink(subject.Pet.Id,recipient.Pet.Id)!;relation.Score=-50;relation.ReverseScore=-50;
                    if(count==3){var third=owner.SocialLink(subject.Pet.Id,team.Members[2].Pet.Id)!;third.Score=-50;third.ReverseScore=-50;}
                    team.Discoveries.Start(subject,flower,new(DiscoveryChoice.Approach,4,false,true),true);
                    if(team.Discoveries.Invite(team.Discoveries.Sessions.First()))throw new Exception("An enemy accepted a friendly discovery invitation");
                }
                if(team.Members.Select(m=>m.Pet.Id).Distinct().Count()!=count || !ReferenceEquals(team.Members[0].Pet.State,owner))throw new Exception("Research duplicated a pet or detached its live state");
            }
            GD.Print("DISCOVERY_OK: 1/2/3, physical research, shared slots/experience, attention/acknowledgement, commands, fatigue, distance, availability, FOV, occlusion, ray budget, live IDs");
        }
        finally
        {
            if(team!=null)foreach(var m in team.Members){world.RemoveChild(m.Body);m.Body.QueueFree();}
            for(int i=0;i<interests.Length;i++)interests[i].Available=available[i];
            RestoreExpeditionTeam();
        }
    }
}
