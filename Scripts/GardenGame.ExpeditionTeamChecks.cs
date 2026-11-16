using System;
using System.Linq;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private void CheckExpeditionTeam()
    {
        var owner=new PetState { Bond=80,Run=160,Curiosity=95,Initiative=850,Energy=90 };
        for(int i=0;i<2;i++)owner.GardenEggs.Add(new() { Id="team-test-"+i,Hatched=true,Color=i==0 ? "Black" : "Yellow",
            X=5+i,Y=0,Z=4,Chao=new PetState { Name="Companion "+i,Bond=i==0 ? 0 : 70,Run=140,Energy=85,Sociability=90,Curiosity=-80,Initiative=10 } });
        var homes=owner.GardenEggs.Select(e=>(e.X,e.Y,e.Z)).ToArray();
        foreach(var existing in expeditionTeam!.Members) { world.RemoveChild(existing.Body);existing.Body.QueueFree(); }
        expeditionTeam=null;
        for(int size=1;size<=3;size++)
        {
            owner.Expedition.LastTeamIds=ExpeditionRoster.Available(owner).Take(size).Select(e=>e.Id).ToList();
            var team=new ExpeditionTeam(world,owner,new(0,0,18));
            if(team.Members.Count!=size || team.Members.Select(m=>m.Pet.Id).Distinct().Count()!=size ||
                !ReferenceEquals(team.Members[0].Pet.State,owner))throw new Exception("Team identity/reference binding failed");
            foreach(var boundary in new[]{new Vector3(35,0,0),new Vector3(13,0,3),new Vector3(-34,0,-24)})
            {
                team.Reposition(boundary);
                for(int a=0;a<size;a++)for(int b=a+1;b<size;b++)
                    if(ExpeditionCompanion.Flat(team.Members[a].Body.Position,team.Members[b].Body.Position)<.7f)throw new Exception("Restored team overlaps near a boundary or pond");
                if(team.Members.Any(m=>WindfallMeadow.Water.Contains(m.Body.Position)))throw new Exception("Team restored inside the pond");
            }
            team.Reposition(new(0,0,18));
            var from=team.Members[0].Body.Position;
            team.Command("Follow",new(0,0,12));
            for(int t=0;t<600;t++)team.Step(1f/60,new(0,0,12),camera);
            if(team.Members[0].Body.Position.DistanceTo(from)<1 || team.Members.Any(m=>!m.Body.Position.IsFinite() || m.Body.Position.Y<-.1f || m.Recoveries!=0))
                throw new Exception($"Team {size} failed ordinary movement: {team.Members[0].Body.Position}, from {from}");
            for(int a=0;a<size;a++)for(int b=a+1;b<size;b++)
                if(ExpeditionCompanion.Flat(team.Members[a].Body.Position,team.Members[b].Body.Position)<.335f)throw new Exception("Companions overlap");
            team.Command("Wait",new(0,0,12));
            for(int t=0;t<1500;t++)team.Step(1f/60,new(0,0,-9),camera);
            if(team.Members.Any(m=>ExpeditionCompanion.Flat(m.Body.Position,new(0,0,12))>7))throw new Exception("Wait failed to hold an area");
            team.Command("Call",new(0,0,9));
            if(size>1 && team.Members[1].Intent==ExpeditionIntent.Follow)throw new Exception("Low bond was ignored by Call");
            team.Command("Follow",new(0,0,9));
            if(team.Order!=ExpeditionOrder.Follow)throw new Exception("Follow did not resume travel");
            var first=team.Members[0]; first.Pet.State.Energy=3;
            first.SetIntent(ExpeditionIntent.Sleep,first.Body.Position,30);
            for(int t=0;t<600;t++)team.Step(1f/60,new(0,0,9),camera);
            if(!first.Sleeping || first.Pet.State.Energy<=3)throw new Exception("Expedition sleep conflicts with decisions or fails to restore energy");
            team.Command("Follow",new(0,0,9));
            for(int t=0;t<200;t++)team.Step(1f/60,new(0,0,9),camera);
            if(first.Sleeping)throw new Exception("Sleeping companion could not rise for Follow");
            if(size>=2)
            {
                var other=team.Members[1];var relation=owner.SocialLink(first.Pet.Id,other.Pet.Id,true)!;relation.Met=true;relation.Score=60;
                // Exercise the walk variant with rested friends; tired friends correctly choose shared rest.
                first.Pet.State.Energy=other.Pet.State.Energy=80;
                for(int attempt=0;attempt<64 && first.Partner!=other;attempt++)
                {
                    first.SetIntent(ExpeditionIntent.Observe,first.Body.Position,0);other.SetIntent(ExpeditionIntent.Observe,other.Body.Position,12);
                    team.Decide(first,new(0,0,9));
                }
                if(first.Partner!=other)throw new Exception("Autonomous social choice never selected an eligible friend");
                var socialStart=$"start {first.Body.Position} → {first.Goal}; other {other.Body.Position} → {other.Goal}";
                for(int t=0;t<120;t++)team.Step(1f/60,new(0,0,9),camera);
                if(first.Partner!=other || other.Partner!=first)throw new Exception("Social pair did not retain its shared action");
                bool walked=false;
                string history="";
                for(int step=0;step<3600 && !walked;step++) {
                    team.Step(1f/60,new(0,0,9),camera);walked=first.WalkedTogether;
                    if(step%300==0)history+=$"\n{step}: {first.Intent} / {other.Intent}, partners {first.Partner?.Pet.Id}/{other.Partner?.Pet.Id}, contact {first.SocialContact}/{other.SocialContact}, gap {ExpeditionCompanion.Flat(first.Body.Position,first.Goal)}/{ExpeditionCompanion.Flat(other.Body.Position,other.Goal)}";
                }
                if(!walked)throw new Exception($"Friends never transitioned from looking to a short shared walk: {size}, {socialStart}; {first.Intent}, {first.Remaining}, {first.Body.Position}, {first.RouteDebug}; other {other.Intent}, {other.Remaining}, {other.Body.Position}, {other.RouteDebug}; {history}");
                first.Partner=other.Partner=null;first.Pet.State.Energy=other.Pet.State.Energy=20;
                for(int attempt=0;attempt<64 && first.Partner!=other;attempt++)
                {
                    first.SetIntent(ExpeditionIntent.Observe,first.Body.Position,0);other.SetIntent(ExpeditionIntent.Observe,other.Body.Position,12);
                    team.Decide(first,new(0,0,9));
                }
                if(first.Partner!=other || first.Intent!=ExpeditionIntent.Rest || other.Intent!=ExpeditionIntent.Rest ||
                    ExpeditionCompanion.Flat(first.Goal,other.Goal)<.7f)throw new Exception("Tired friends did not choose separate shared-rest spaces");
            }
            if(size==1)
            {
                var nav=new ExpeditionNavigation(world);first.Pet.State.Run=990;
                foreach(var destination in new[]{new Vector3(21,0,4),new Vector3(-23,0,-13),new Vector3(24,0,-12)})
                {
                    var target=nav.Nearest(destination);first.SetIntent(ExpeditionIntent.Explore,target,120);
                    for(int step=0;step<3600 && ExpeditionCompanion.Flat(first.Body.Position,target)>.65f;step++)
                    {
                        first.Step(1f/60,nav,camera,player.Position);
                        if(WindfallMeadow.Water.Contains(first.Body.Position))throw new Exception("Companion route entered deep pond water");
                    }
                    if(ExpeditionCompanion.Flat(first.Body.Position,target)>1.2f || first.Recoveries!=0)
                        throw new Exception($"Companion failed forest / hill / water-bank traversal: {first.Body.Position} → {target}, {first.RouteDebug}");
                }
            }
            if(size==2)
            {
                var changed=new PetState { Name="Edited companion",Energy=40,Run=160 };
                owner.GardenEggs.First(e=>e.Id=="team-test-0").Chao=changed;
                var at=team.Members[1].Body.Position;
                team.RefreshStates(owner);
                if(!ReferenceEquals(team.Members[1].Pet.State,changed) || team.Members[1].Body.Position!=at)
                    throw new Exception("Editor replacement detached a companion or moved it");
                var editedOwner=new PetState { GardenEggs=owner.GardenEggs,Expedition=owner.Expedition,SocialLinks=owner.SocialLinks };
                team.RefreshStates(editedOwner);
                if(!ReferenceEquals(team.Members[0].Pet.State,editedOwner))throw new Exception("Editor replacement detached the main companion");
            }
            foreach(var member in team.Members) { world.RemoveChild(member.Body);member.Body.QueueFree(); }
        }
        if(owner.GardenEggs.Where((egg,i)=>(egg.X,egg.Y,egg.Z)!=homes[i]).Any())throw new Exception("Expedition changed home coordinates");
        var navigation=new ExpeditionNavigation(world);
        foreach(var goal in new[]{new Vector3(-23,0,-13),new Vector3(24,0,-12),new Vector3(21,0,4)})
        {
            var path=navigation.Path(new(0,0,18),goal);
            if(path.Length<3 || path.Any(p=>!navigation.Safe(p)))throw new Exception("Hill, forest or pond-bank route is missing or unsafe");
        }
        RestoreExpeditionTeam();
        if(expeditionTeam==null || expeditionTeam.Members.Any(m=>!ReferenceEquals(m.Pet.State,ExpeditionRoster.Available(pet).First(e=>e.Id==m.Pet.Id).State)))
            throw new Exception("World transition restored detached companion states");
        GD.Print("EXPEDITION_TEAM_OK: 1/2/3, live identities, motion, separation, wait/call/follow, sleep, social pairs, dry hill/forest routes, homes");
    }
}
