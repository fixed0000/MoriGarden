using System;
using System.Linq;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private OptionButton? needsDebugPicker;
    private RichTextLabel? needsDebugText;
    private string needsDebugId="main";
    private float needsDebugClock;
    private void BuildNeedsDebug()
    {
        if(!OS.GetCmdlineUserArgs().Contains("--needs-debug"))return;
        var layer=new CanvasLayer{Layer=90};AddChild(layer);
        var panel=new PanelContainer{Position=new(840,130),CustomMinimumSize=new(420,560)};layer.AddChild(panel);
        var box=new VBoxContainer();panel.AddChild(box);
        box.AddChild(new Label{Text="CHAO STATE — DEVELOPER VIEW"});
        needsDebugPicker=new OptionButton();box.AddChild(needsDebugPicker);
        needsDebugPicker.ItemSelected+=i=>{needsDebugId=needsDebugPicker.GetItemMetadata((int)i).AsString();RefreshNeedsDebug();};
        needsDebugText=new RichTextLabel{CustomMinimumSize=new(410,470),ScrollActive=true,SelectionEnabled=true};box.AddChild(needsDebugText);
        var hide=new Button{Text="Hide diagnostic view"};box.AddChild(hide);hide.Pressed+=()=>panel.Hide();
        RefreshNeedsDebug();
    }
    private void UpdateNeedsDebug(float dt)
    {
        if(needsDebugPicker==null)return;
        needsDebugClock-=dt;if(needsDebugClock>0)return;needsDebugClock=.5f;RefreshNeedsDebug();
    }
    private void RefreshNeedsDebug()
    {
        if(needsDebugPicker==null || needsDebugText==null)return;
        var roster=ExpeditionRoster.Available(pet).ToArray();
        needsDebugPicker.Clear();
        foreach(var item in roster)
        {needsDebugPicker.AddItem(item.State.Name);needsDebugPicker.SetItemMetadata(needsDebugPicker.ItemCount-1,item.Id);}
        int selected=Array.FindIndex(roster,p=>p.Id==needsDebugId);
        if(selected<0)selected=0;
        if(roster.Length==0){needsDebugText.Text="No active Chao.";return;}
        needsDebugPicker.Select(selected);needsDebugId=roster[selected].Id;
        needsDebugText.Text=ChaoStateDiagnostics.Describe(roster[selected].State);
    }
}
