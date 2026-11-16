using Godot;

namespace Mori;

public partial class GardenGame
{
    private GreenhouseMission? SiteMission=>zone==GardenZone.Expedition && travel.ExpeditionArea==WorldCatalog.Greenhouse ? world.GetNodeOrNull<GreenhouseMission>("GreenhouseMission") : null;
    private void ConfigureSiteMission()
    {
        if(SiteMission is not {} mission)return;
        mission.SaveRequested=Save;mission.Message=text=>Notify(text,6);mission.RefreshOwner(pet);
    }
    private void AdvanceSiteMission(float dt)
    { if(SiteMission is {} mission){mission.RefreshOwner(pet);mission.Advance(dt,player,expeditionTeam);} }
    private void MissionPlayerPose()
    {
        if(SiteMission?.PlayerAction.Target is not {} element)return;
        float blend=SiteMission.PlayerAction.Blend;
        avatarWalk?.Crouch(blend*.7f);avatarWalk?.UpperBody(.25f*blend,0);
        avatarWalk?.ReachWithHands(element.Contact,element.GlobalBasis.X,blend,.15f);
        playerArt.Position=new(0,-.28f*blend,0);
    }
}
