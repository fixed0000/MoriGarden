using System;
using System.Linq;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private Panel sportsCard=null!;
    private OptionButton sportsPetPicker=null!;
    private RichTextLabel sportsRecords=null!;
    private string[] sportsPetIds=Array.Empty<string>();
    private void BuildSportsCard(Control ui)
    {
        var button=new Button{Text="Competitions",Position=new(43,532),Size=new(215,32)};details.AddChild(button);
        sportsCard=Panel(ui,new(330,135,620,465),new Color(.97f,.95f,.89f,.94f));sportsCard.Hide();
        Text(sportsCard,"SPORTING ACHIEVEMENTS",23,new(22,16,470,35),GardenArt.Ink);
        var close=new Button{Text="Close",Position=new(500,18),Size=new(96,32)};sportsCard.AddChild(close);close.Pressed+=sportsCard.Hide;
        sportsPetPicker=new OptionButton{Position=new(22,63),Size=new(570,38)};sportsCard.AddChild(sportsPetPicker);sportsPetPicker.ItemSelected+=_=>RefreshSportsCard();
        sportsRecords=new RichTextLabel{Position=new(24,116),Size=new(568,326),ScrollActive=true};sportsRecords.AddThemeColorOverride("default_color",GardenArt.Ink);sportsRecords.AddThemeFontSizeOverride("normal_font_size",16);sportsCard.AddChild(sportsRecords);
        button.Pressed+=()=>{var roster=ExpeditionRoster.Available(pet);sportsPetIds=roster.Select(p=>p.Id).ToArray();sportsPetPicker.Clear();foreach(var member in roster)sportsPetPicker.AddItem(member.State.Name);int selected=heldEgg?.Chao is {} held ? roster.ToList().FindIndex(p=>ReferenceEquals(p.State,held)) : 0;if(roster.Count>0)sportsPetPicker.Select(Math.Max(0,selected));RefreshSportsCard();sportsCard.Show();sportsPetPicker.GrabFocus();};
    }
    private void RefreshSportsCard()
    {
        if(sportsPetPicker.Selected<0 || sportsPetPicker.Selected>=sportsPetIds.Length)return;
        var selected=ExpeditionRoster.Available(pet).FirstOrDefault(p=>p.Id==sportsPetIds[sportsPetPicker.Selected]);if(selected==null)return;
        var progress=selected.State.Athletics;
        sportsRecords.Text=$"{selected.State.Name} · Medals {progress.Medals.Count} · Cups {progress.Cups.Count}\n\n"+
            string.Join("\n\n",progress.Results.Select(p=>{var e=SportsCatalog.Find(p.Key);return (e?.Title ?? "Past competition")+$"\nEntries {p.Value.Entries} · Wins {p.Value.Wins} · Best place {(p.Value.BestPlace<99 ? p.Value.BestPlace.ToString() : "—")}"+(p.Value.BestSeconds>0 ? $" · {p.Value.BestSeconds:0.00}s" : "")+(progress.Cups.Contains(p.Key) ? "\nCup won" : "");}))+
            (progress.Results.Count==0 ? "Visit Chao Stadium to begin a sporting journey." : "")+
            "\n\nUnlocked competitions\n"+string.Join(" · ",SportsCatalog.Events.Where(e=>SportsCatalog.Open(e,selected.State)).Select(e=>e.Title));
    }
}
