using System;
using System.IO;
using System.Linq;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private bool saveLoaded;
    private void ShowLoadFailure(Exception error)
    {
        saveLoaded=false;
        SetProcess(false);SetPhysicsProcess(false);SetProcessInput(false);SetProcessUnhandledInput(false);SetProcessUnhandledKeyInput(false);
        GetWindow().Title="MoriGarden — Save protected";
        var layer=new CanvasLayer();AddChild(layer);
        var background=new ColorRect{Color=new("17242b")};layer.AddChild(background);background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var message=new Label{Text="SAVE PROTECTED\n\n"+error.Message+"\n\nNo garden was replaced. Autosave is disabled.\nKeep the original files and recover a verified copy.\n\n"+savePath,Position=new(95,100),Size=new(1080,430),AutowrapMode=TextServer.AutowrapMode.WordSmart};layer.AddChild(message);message.AddThemeFontSizeOverride("font_size",22);
        var close=new Button{Text="Exit safely",Position=new(95,590),Size=new(250,55)};layer.AddChild(close);close.Pressed+=()=>GetTree().Quit();
        GD.PushError("SAVE_PROTECTED: "+error.Message);
    }
    private void ResolveRecoveryDirectory()
    {
        string directory=OS.GetEnvironment("MORI_SAVE_DIRECTORY");
        if(OS.GetCmdlineUserArgs().Contains("--recovery-check") && string.IsNullOrWhiteSpace(directory))throw new SaveLoadException("Recovery checks require a separate copied save directory.");
        if(string.IsNullOrWhiteSpace(directory))return;
        if(testMode || !Path.IsPathFullyQualified(directory))throw new SaveLoadException("A recovery folder must be an absolute path and cannot be combined with a generated test profile.");
        savePath=Path.Combine(Path.GetFullPath(directory),"pet.json");
        if(!File.Exists(savePath) && !File.Exists(savePath+".bak"))throw new SaveLoadException("No existing garden was found in the selected recovery folder.");
    }
}
