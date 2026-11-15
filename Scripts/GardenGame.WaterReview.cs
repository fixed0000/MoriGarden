using Godot;
using System.Linq;

namespace Mori;

public partial class GardenGame
{
    private void ReviewWaters()
    {
        var views=new (GardenZone Zone,string Name,Vector3 At,float Yaw,float Distance,float Pitch)[]{
            (GardenZone.CoastalCove,"coast-beach",new(2,0,8),1.2f,7,.22f),
            (GardenZone.CoastalCove,"coast-shore",new(-5,0,1),1.5f,6,.25f),
            (GardenZone.CoastalCove,"coast-shallows",new(-12,0,2),1.15f,4.5f,.30f),
            (GardenZone.CoastalCove,"coast-palms",new(12,0,11),.6f,7,.20f),
            (GardenZone.CoastalCove,"coast-rocks",new(-2,0,-13),1.1f,6.5f,.25f),
            (GardenZone.CoastalCove,"coast-lookout",new(18,0,3),1.5f,6.5f,.28f),
            (GardenZone.Expedition,"meadow-pond",new(8,0,8),-2.1f,7,.25f),
            (GardenZone.Expedition,"meadow-bank",new(14,0,7),0,4.5f,.35f),
            (GardenZone.Expedition,"meadow-surface",new(13,0,3),.2f,3.8f,.42f),
            (GardenZone.Expedition,"meadow-crossing",new(18,0,3),1.5f,5,.3f),
            (GardenZone.Expedition,"meadow-player",new(10,0,8),0,6.5f,.22f)
        };
        int index=reviewFrame/18;
        if(index>=views.Length) { GD.Print("WATER_REVIEW_OK"); GetTree().Quit(); return; }
        var view=views[index];
        if(reviewFrame==0 || zone!=view.Zone) { carrying=false; SwitchZone(view.Zone); }
        var at=view.At;
        if(view.Name=="coast-shallows" && !OS.GetCmdlineUserArgs().Contains("--review-before"))at=new(CoastalCove.Shore(3)-3,0,3);
        var hit=GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(at+Vector3.Up*12,at-Vector3.Up*6,1));
        if(hit.Count>0) at.Y=((Vector3)hit["position"]).Y;
        player.Position=at+Vector3.Up*.012f; verticalSpeed=0;
        player.Rotation=new(0,view.Yaw+Mathf.Pi,0);
        cameraRig.Yaw=view.Yaw; cameraRig.Distance=view.Distance; cameraRig.Pitch=view.Pitch; cameraRig.Follow(player.Position,0,true);
        if(reviewFrame%18==17)
        {
            string version=OS.GetCmdlineUserArgs().Contains("--review-before") ? "before" : "after";
            GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://artifacts/water-{version}-{view.Name}.png"));
            if(view.Name is "coast-lookout" or "meadow-player") GD.Print($"WATER_VIEW {view.Name}: draws={Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)}, primitives={Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame)}");
        }
        reviewFrame++;
    }
}
