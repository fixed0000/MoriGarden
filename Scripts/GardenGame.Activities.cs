using System.Linq;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private GardenActivityAdapter? mainActivity;
    private GardenActivityClaims activityClaims=new();
    internal bool GardenSocialUses(CharacterBody3D actor)=>introductions.Uses(actor) || ballGame.Uses(actor) || musicGroup.Uses(actor);
    internal bool GardenWateringUses(CharacterBody3D actor)=>CanInGarden?.IsUsing(actor)==true;
    private bool GardenActivitiesEnabled=>zone is GardenZone.Garden or GardenZone.CoastalCove;
    private void RebindGardenExecutors(string id,PetState updated)
    {
        if(!GardenActivitiesEnabled)return;
        CharacterBody3D? actor=id=="main" ? friend : world.GetChildren().OfType<GardenEgg>().FirstOrDefault(e=>e.Data.Id==id);
        if(actor==null)return;Node3D art=actor is GardenEgg egg ? egg.Art : friendArt;
        foreach(var toy in world.GetChildren().OfType<ToyCar>())toy.RebindPlayer(actor,updated,art);
        ballGame.RebindPlayer(actor,updated,art);musicGroup.RebindPlayer(actor,updated,art);
    }
    private GardenActivityAdapter MainActivity=>mainActivity ??=new(friend,()=>friendArt,moriInterests,moriRest,moriLeisure,chaoMotion,"main");
    internal GardenActivityContext ActivityContext=>new(world,zone==GardenZone.Garden,travel.AreaId(zone),FruitSlots(),world.GetChildren().OfType<ToyCar>(),activityClaims,SocialActivityTargets,StartSocialActivity);
    private GardenActivityAdapter AdapterFor(CharacterBody3D actor)=>actor is GardenEgg egg ? egg.ActivityAdapter : MainActivity;
    private bool SocialLeaseable(ChaoIntroductions.Candidate c)=>AdapterFor(c.Actor).Coordinator.CanJoinSocial;
    private int SocialActivityTargets(CharacterBody3D actor)
    {
        if(zone!=GardenZone.Garden)return 0;
        var raw=SocialCandidates(true,false);var self=raw.FirstOrDefault(c=>c.Actor==actor);if(self==null)return 0;
        var peers=raw.Where(c=>c.Actor!=actor && SocialLeaseable(c)).ToArray();
        int count=peers.Count(c=>introductions.CanStart(pet,self,c));
        var props=world.GetChildren().OfType<ToyCar>().Where(t=>activityClaims.Free(t,actor)).ToArray();
        count+=musicGroup.JoinTargetCount(pet,self,BallParticipants(),props);
        if(self.Pet.Sociability>50 && props.Any(t=>t.IsBeachBall && t.Available(actor,self.Pet) && AdapterFor(actor).CanUseTarget(t)))
            count+=peers.Count(c=>c.Pet.Fun<100 && c.Actor.Position.DistanceTo(actor.Position)<5 &&
                pet.SocialLink(self.Id,c.Id) is {Met:true} link && link.ScoreFrom(self.Id)>=0);
        return count;
    }
    private bool StartSocialActivity(CharacterBody3D actor)
    {
        var raw=SocialCandidates(true,false);var self=raw.FirstOrDefault(c=>c.Actor==actor);if(self==null)return false;
        var eligible=raw.Where(c=>c.Actor!=actor && SocialLeaseable(c) && introductions.CanStart(pet,self,c)).ToList();
        while(eligible.Count>0)
        {
            string? chosen=self.Pet.SocialSelection.Select(pet,self.Id,self.Pet,eligible.Select(c=>new SocialPartnerCandidate(c.Id,c.Actor.Position.DistanceTo(actor.Position))),"Introduction",false);
            var peer=eligible.FirstOrDefault(c=>c.Id==chosen);if(peer==null)break;
            eligible.Remove(peer);
            if(SocialLeaseable(peer) && introductions.TryStart(pet,self,peer))
            {PauseForSocial(self);PauseForSocial(peer);return true;}
        }
        var props=world.GetChildren().OfType<ToyCar>().Where(t=>activityClaims.Free(t,actor)).ToArray();
        if(musicGroup.TryJoin(pet,BallParticipants(),new[]{self},props)){PauseForSocial(self);return true;}
        return false;
    }
}
