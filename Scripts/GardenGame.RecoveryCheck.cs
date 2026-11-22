using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private int recoveryFrames;
    private bool recoveryCapturing;
    private bool CheckRecoveryScene()
    {
        if(++recoveryFrames<45)return false;
        if(!recoveryCapturing){recoveryCapturing=true;CaptureRecoveryScene();}
        return true;
    }
    private async void CaptureRecoveryScene()
    {
        try
        {
            string baseline=OS.GetEnvironment("MORI_RECOVERY_BASELINE");
            if(string.IsNullOrEmpty(baseline))throw new Exception("Missing independent recovery baseline");
            var expected=SaveStore.LoadSlot(baseline) ?? throw new Exception("Unreadable recovery baseline");
            string[] ids=expected.GardenEggs.Select(e=>e.Id).ToArray();
            if(pet.Name!=expected.Name || pet.Form!=expected.Form || !friend.Visible || zone!=GardenZone.Garden || petZone!=GardenZone.Garden || !friendArt.IsVisibleInTree())throw new Exception("Recovered main Chao is not visible in its saved garden");
            if(!ids.SequenceEqual(pet.GardenEggs.Select(e=>e.Id)) || !ids.Order().SequenceEqual(world.GetChildren().OfType<GardenEgg>().Select(e=>e.Data.Id).Order()))throw new Exception("Recovered scene lost or duplicated another Chao");
            if(pet.AgeSeconds<expected.AgeSeconds || pet.AgeSeconds-expected.AgeSeconds>10 || pet.SocialLinks.Count!=expected.SocialLinks.Count)throw new Exception("Recovered development/relationships changed unexpectedly");
            camera.GlobalPosition=friend.GlobalPosition+new Vector3(.72f,.62f,.70f);camera.LookAt(friend.GlobalPosition+Vector3.Up*.18f);
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            string directory=Path.GetDirectoryName(savePath)!;
            GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory,"recovered-mori.png"));
            var roster=ExpeditionRoster.Available(pet).ToDictionary(e=>e.Id,e=>e.State);editor!.Poll(roster,false);
            Save();var again=SaveStore.LoadSlot(savePath) ?? throw new Exception("Recovery copy could not be saved");
            if(!ids.SequenceEqual(again.GardenEggs.Select(e=>e.Id)) || again.Name!=expected.Name)throw new Exception("Saving the recovered scene changed the roster");
            File.WriteAllText(Path.Combine(directory,"scene-check.json"),JsonSerializer.Serialize(new{passed=true,name=pet.Name,id="main",form=pet.Form,residents=ids.Length+1,visible=friend.Visible,editorSeesMain=roster.ContainsKey("main"),roundTripIdsPreserved=true}));
            GD.Print("RECOVERY_SCENE_OK: existing main and all residents visible, saved form/age/relationships, editor and round-trip identities checked");GetTree().Quit();
        }
        catch(Exception error){GD.PushError("RECOVERY_SCENE_FAILED: "+error);GetTree().Quit(1);}
    }
}
