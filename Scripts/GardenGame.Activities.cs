using System.Linq;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private GardenActivityAdapter? mainActivity;
    private GardenActivityClaims activityClaims=new();
    private readonly ChaoCasualSocial casualSocial=new();
    private int socialStartAttempts,casualStarts,firstIntroductionStarts,musicJoinStarts;
    internal bool GardenSocialUses(CharacterBody3D actor)=>introductions.Uses(actor) || ballGame.Uses(actor) || musicGroup.Uses(actor) || casualSocial.Uses(actor);
    internal void CancelCasualSocial(CharacterBody3D actor,string reason)=>casualSocial.Cancel(actor,reason);
    private bool CasualReady(ChaoIntroductions.Candidate c,bool continuing=false,bool initiator=false)
    {
        if(zone!=GardenZone.Garden || !SocialCandidates(true,false,continuing).Any(p=>p.Actor==c.Actor && p.Art==c.Art))return false;
        var coordinator=AdapterFor(c.Actor).Coordinator;
        return continuing || coordinator.CanJoinSocial || initiator && coordinator.SelectedActivity==ChaoActivity.Socialize && coordinator.CurrentExecutor=="pending start";
    }
    private System.Collections.Generic.Dictionary<string,CasualSocialDiagnostic> CasualSocialDiagnostics=>
        pet.GardenEggs.Where(e=>e.Hatched && e.Chao!=null).Select(e=>e.Id).Prepend("main").ToDictionary(id=>id,id=>casualSocial.Diagnostic(id));
    internal bool GardenWateringUses(CharacterBody3D actor)=>CanInGarden?.IsUsing(actor)==true;
    private bool GardenActivitiesEnabled=>zone is GardenZone.Garden or GardenZone.CoastalCove;
    private void RebindGardenExecutors(string id,PetState updated)
    {
        if(!GardenActivitiesEnabled)return;
        CharacterBody3D? actor=id=="main" ? friend : world.GetChildren().OfType<GardenEgg>().FirstOrDefault(e=>e.Data.Id==id);
        if(actor==null)return;Node3D art=actor is GardenEgg egg ? egg.Art : friendArt;
        foreach(var toy in world.GetChildren().OfType<ToyCar>())toy.RebindPlayer(actor,updated,art);
        ballGame.RebindPlayer(actor,updated,art);musicGroup.RebindPlayer(actor,updated,art);
        casualSocial.RebindPlayer(actor,updated,art);
    }
    private GardenActivityAdapter MainActivity=>mainActivity ??=new(friend,()=>friendArt,moriInterests,moriRest,moriLeisure,chaoMotion,"main");
    private GardenRhythmContext EditorRhythmContext=>new(pet.GardenSeconds,travel.AreaId(zone),"main",GardenActivitiesEnabled);
    internal GardenActivityContext ActivityContext=>new(world,zone==GardenZone.Garden,travel.AreaId(zone),FruitSlots(),world.GetChildren().OfType<ToyCar>(),activityClaims,SocialActivityTargets,StartSocialActivity,pet.GardenSeconds,GardenActivitiesEnabled);
    private GardenActivityAdapter AdapterFor(CharacterBody3D actor)=>actor is GardenEgg egg ? egg.ActivityAdapter : MainActivity;
    private bool SocialLeaseable(ChaoIntroductions.Candidate c)=>AdapterFor(c.Actor).Coordinator.CanJoinSocial;
    private int SocialActivityTargets(CharacterBody3D actor)
    {
        if(zone!=GardenZone.Garden)return 0;
        var raw=SocialCandidates(true,false);var self=raw.FirstOrDefault(c=>c.Actor==actor);if(self==null)return 0;
        var peers=raw.Where(c=>c.Actor!=actor && SocialLeaseable(c)).ToArray();
        int count=peers.Count(c=>introductions.CanStart(pet,self,c));
        count+=peers.Count(c=>casualSocial.CanStart(pet,self,c,CasualReady(self,initiator:true),CasualReady(c)));
        var props=world.GetChildren().OfType<ToyCar>().Where(t=>activityClaims.Free(t,actor)).ToArray();
        count+=musicGroup.JoinTargetCount(pet,self,BallParticipants(),props);
        if(self.Pet.Sociability>50 && props.Any(t=>t.IsBeachBall && t.Available(actor,self.Pet) && AdapterFor(actor).CanUseTarget(t)))
            count+=peers.Count(c=>c.Pet.Fun<100 && c.Actor.Position.DistanceTo(actor.Position)<5 &&
                pet.SocialLink(self.Id,c.Id) is {Met:true} link && link.ScoreFrom(self.Id)>=0);
        return count;
    }
    private bool StartSocialActivity(CharacterBody3D actor)
    {
        socialStartAttempts++;
        var raw=SocialCandidates(true,false);var self=raw.FirstOrDefault(c=>c.Actor==actor);if(self==null)return false;
        var eligible=raw.Where(c=>c.Actor!=actor && SocialLeaseable(c) && introductions.CanStart(pet,self,c)).ToList();
        while(eligible.Count>0)
        {
            string? chosen=self.Pet.SocialSelection.Select(pet,self.Id,self.Pet,eligible.Select(c=>new SocialPartnerCandidate(c.Id,c.Actor.Position.DistanceTo(actor.Position))),"Introduction",false);
            var peer=eligible.FirstOrDefault(c=>c.Id==chosen);if(peer==null)break;
            eligible.Remove(peer);
            if(SocialLeaseable(peer) && introductions.TryStart(pet,self,peer))
            {PauseForSocial(self);PauseForSocial(peer);firstIntroductionStarts++;return true;}
        }
        var props=world.GetChildren().OfType<ToyCar>().Where(t=>activityClaims.Free(t,actor)).ToArray();
        if(musicGroup.TryJoin(pet,BallParticipants(),new[]{self},props)){PauseForSocial(self);musicJoinStarts++;return true;}
        eligible=raw.Where(c=>c.Actor!=actor && casualSocial.CanStart(pet,self,c,CasualReady(self,initiator:true),CasualReady(c))).ToList();
        if(eligible.Count>0)
        {
            string? chosen=self.Pet.SocialSelection.Select(pet,self.Id,self.Pet,eligible.Select(c=>new SocialPartnerCandidate(c.Id,c.Actor.Position.DistanceTo(actor.Position))),"CasualGreeting");
            var peer=eligible.FirstOrDefault(c=>c.Id==chosen);
            if(peer!=null && casualSocial.TryStart(pet,self,peer,CasualReady(self,initiator:true),CasualReady(peer)))
            {
                try{PauseForSocial(self);PauseForSocial(peer);casualStarts++;return true;}
                catch{casualSocial.Cancel(reason:"Reservation failed");AdapterFor(self.Actor).Coordinator.Suspend("Reservation failed");AdapterFor(peer.Actor).Coordinator.Suspend("Reservation failed");return false;}
            }
        }
        return false;
    }
}
