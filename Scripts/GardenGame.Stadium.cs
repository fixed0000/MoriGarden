using Godot;

namespace Mori;

public partial class GardenGame
{
    private StadiumSession? stadium;
    private bool racing=>stadium?.InCompetition==true;
    private bool StadiumBlocking=>stadium?.Blocking==true;
    private void ConfigureStadium()
    {
        if(zone!=GardenZone.Stadium)return;
        stadium=new(this,world,()=>pet,Save,text=>Notify(text,7),visible=>{friend.Visible=visible && petZone==zone;});
    }
    private void StartRace(){if(zone==GardenZone.Stadium){carrying=false;stadium?.Open();}}
    private bool CanCheer()=>stadium?.CanCheer==true;
    private void Cheer()=>stadium?.Cheer();
    private void UpdateRace(float dt)=>stadium?.Step(dt,camera);
    private void FinishRace(int reward,bool cancelled=false)=>stadium?.Leave();
}
