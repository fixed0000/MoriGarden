using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mori;

internal partial class ExpeditionTeamUi : CanvasLayer
{
    internal bool Choosing=>picker?.Visible==true;
    private Control? picker;
    private PanelContainer hud=null!;
    private Label status=null!, count=null!;
    private Button depart=null!;
    private readonly List<string> selected=new();
    private readonly List<(Button Button,ExpeditionPet Pet)> cards=new();
    private Action<List<string>>? accept;
    private Action? cancel;
    internal Action<string>? Order;
    public override void _Ready()
    {
        Layer=8;
        hud=new PanelContainer { OffsetLeft=-355,OffsetTop=-195,OffsetRight=-18,OffsetBottom=-68 };
        AddChild(hud);hud.SetAnchorsPreset(Control.LayoutPreset.BottomRight,true); Style(hud);
        var box=new VBoxContainer();hud.AddChild(box);
        box.AddChild(new Label { Text="EXPEDITION TEAM",HorizontalAlignment=HorizontalAlignment.Center });
        status=new Label { HorizontalAlignment=HorizontalAlignment.Center };box.AddChild(status);
        var row=new HBoxContainer();box.AddChild(row);
        foreach(var (label,command) in new[]{("Q · Call","Call"),("T · Wait","Wait"),("Y · Follow","Follow")})
        { var button=new Button { Text=label,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill };row.AddChild(button);button.Pressed+=()=>Order?.Invoke(command); }
        hud.Visible=false;
    }
    private static void Style(PanelContainer panel)
    {
        var style=new StyleBoxFlat { BgColor=new(.045f,.085f,.12f,.87f),CornerRadiusTopLeft=12,CornerRadiusTopRight=12,
            CornerRadiusBottomLeft=12,CornerRadiusBottomRight=12,ContentMarginLeft=18,ContentMarginRight=18,ContentMarginTop=14,ContentMarginBottom=14 };
        panel.AddThemeStyleboxOverride("panel",style);
    }
    internal void Show(PetState owner,Action<List<string>> chosen,Action dismissed)
    {
        Close();accept=chosen;cancel=dismissed;selected.Clear();selected.AddRange(ExpeditionRoster.Resolve(owner,owner.Expedition.LastTeamIds));
        var available=ExpeditionRoster.Available(owner);if(selected.Count==0 && available.Count==1)selected.Add(available[0].Id);
        picker=new Control();AddChild(picker);picker.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var shade=new ColorRect { Color=new(0,0,0,.45f) };picker.AddChild(shade);shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var center=new CenterContainer();picker.AddChild(center);center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var panel=new PanelContainer { CustomMinimumSize=new(650,450) };center.AddChild(panel);Style(panel);
        var box=new VBoxContainer();panel.AddChild(box);
        box.AddChild(new Label { Text="WINDFALL MEADOW · CHOOSE YOUR TEAM",HorizontalAlignment=HorizontalAlignment.Center });
        box.AddChild(new Label { Text="Choose 1–3 Chao. Their home gardens stay unchanged.\nArrow keys / Tab: navigate · Enter / Space: select · Esc: cancel" });
        var scroll=new ScrollContainer { CustomMinimumSize=new(610,295),SizeFlagsVertical=Control.SizeFlags.ExpandFill };box.AddChild(scroll);
        var rows=new VBoxContainer { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill };scroll.AddChild(rows);cards.Clear();
        foreach(var pet in available)
        {
            var s=pet.State;
            var button=new Button { Alignment=HorizontalAlignment.Left,CustomMinimumSize=new(590,65),ToggleMode=true,
                ButtonPressed=selected.Contains(pet.Id),Text=$"{s.Name} · {pet.Color} · {s.LifeStage}\nSwim {s.Swim/10}   Fly {s.Fly/10}   Run {s.Run/10}   Power {s.Power/10}   Stamina {s.Stamina/10}" };
            rows.AddChild(button);cards.Add((button,pet));
            button.Pressed+=()=>{if(selected.Contains(pet.Id))selected.Remove(pet.Id);else if(selected.Count<3)selected.Add(pet.Id);Refresh();};
        }
        count=new Label();box.AddChild(count);var actions=new HBoxContainer();box.AddChild(actions);
        var back=new Button { Text="Cancel",SizeFlagsHorizontal=Control.SizeFlags.ExpandFill };actions.AddChild(back);back.Pressed+=Cancel;
        depart=new Button { Text="Set out",SizeFlagsHorizontal=Control.SizeFlags.ExpandFill };actions.AddChild(depart);depart.Pressed+=Confirm;
        Refresh();if(cards.Count>0)cards[0].Button.GrabFocus();else back.GrabFocus();
    }
    private void Refresh()
    {
        foreach(var (button,pet) in cards)
        {
            button.SetPressedNoSignal(selected.Contains(pet.Id));
            var s=pet.State;
            button.Text=$"{(selected.Contains(pet.Id) ? "[x]" : "[ ]")} {s.Name} · {pet.Color} · {s.LifeStage}\nSwim {s.Swim/10}   Fly {s.Fly/10}   Run {s.Run/10}   Power {s.Power/10}   Stamina {s.Stamina/10}";
        }
        count.Text=cards.Count==0 ? "No active, hatched Chao are available." : $"Selected: {selected.Count}/3";
        depart.Disabled=selected.Count==0;
    }
    internal void Confirm() { if(!Choosing || selected.Count==0)return;var ids=selected.ToList();var action=accept;Close();action?.Invoke(ids); }
    internal void Cancel() {var action=cancel;Close();action?.Invoke();}
    internal void Close() { if(picker!=null){RemoveChild(picker);picker.QueueFree();picker=null;}accept=null;cancel=null;cards.Clear(); }
    internal void Update(ExpeditionTeam? team,bool visible)
    {
        hud.Visible=team!=null && visible;if(team==null)return;
        status.Text=string.Join(" · ",team.Members.Select(m=>m.Pet.State.Name))+"\n"+team.Message;
    }
}
