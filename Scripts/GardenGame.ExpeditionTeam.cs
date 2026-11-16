using System.Linq;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private ExpeditionTeam? expeditionTeam;
    private ExpeditionTeamUi teamUi=null!;
    private void BuildTeamUi()
    {
        teamUi=new ExpeditionTeamUi();AddChild(teamUi);
        teamUi.Order=command=>{if(zone==GardenZone.Expedition && !paused && transition<0)expeditionTeam?.Command(command,player.Position);};
    }
    private void ChooseExpeditionTeam()
    {
        teamUi.Show(pet,ids=>
        {
            pet.Expedition.LastTeamIds=ExpeditionRoster.Resolve(pet,ids);
            if(pet.Expedition.LastTeamIds.Count==0) { travel.Arrived();return; }
            BeginTransition(GardenZone.Expedition);
        },()=>{travel.Arrived();portalCooldown=1;Notify("Expedition cancelled.");});
    }
    private void RestoreExpeditionTeam()
    {
        expeditionTeam=null;
        if(zone!=GardenZone.Expedition)return;
        pet.Expedition.LastTeamIds=ExpeditionRoster.Resolve(pet,pet.Expedition.LastTeamIds);
        // Stage-one saves did not have a party. Choose one existing eligible pet safely.
        if(pet.Expedition.LastTeamIds.Count==0 && ExpeditionRoster.Available(pet).FirstOrDefault() is {} first)
            pet.Expedition.LastTeamIds.Add(first.Id);
        friend.Visible=false;
        expeditionTeam=new(world,pet,player.Position);
    }
}
