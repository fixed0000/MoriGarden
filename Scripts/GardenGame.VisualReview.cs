using Godot;
using System.Linq;

namespace Mori;

public partial class GardenGame
{
    private bool meadowReview;
    private int reviewFrame;
    private void ReviewMeadow()
    {
        var views=new (string Name,Vector2 At,float Yaw,float Distance,float Pitch)[] {
            ("clearing",new(0,18),.12f,7,.20f),
            ("woodland",new(-20,5),.40f,6.5f,.24f),
            ("hills",new(18,-9),-.6f,7,.23f),
            ("path",new(-6,5),-.25f,6.5f,.20f),
            ("boundary",new(21,14),-1.15f,7,.26f),
            ("overlook",new(-21,-11),.65f,15,.53f)
        };
        int index=reviewFrame/20;
        if(index>=views.Length) { GD.Print($"MEADOW_REVIEW_OK: draw calls={Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)}, primitives={Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame)}"); GetTree().Quit(); return; }
        var view=views[index];
        player.Position=new(view.At.X,ExpeditionArt.Height(view.At.X,view.At.Y)+.01f,view.At.Y);
        player.Rotation=new(0,view.Yaw+Mathf.Pi,0);
        verticalSpeed=0;
        cameraRig.Yaw=view.Yaw; cameraRig.Distance=view.Distance; cameraRig.Pitch=view.Pitch;
        cameraRig.Follow(player.Position,0,true);
        if(view.Name=="overlook") { camera.GlobalPosition=new(-10,17,-24); camera.LookAt(new Vector3(1,0,6)); }
        if(reviewFrame%20==19)
        {
            string version=OS.GetCmdlineUserArgs().Contains("--review-before") ? "before" : "after";
            GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://artifacts/meadow-{version}-{view.Name}.png"));
        }
        reviewFrame++;
    }
}
