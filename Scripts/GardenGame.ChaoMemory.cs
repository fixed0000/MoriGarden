using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private Panel memoriesPanel=null!;
    private OptionButton memoryPetPicker=null!;
    private RichTextLabel memoryText=null!;
    private bool memoriesOpen;
    private readonly List<ExpeditionPet> memoryRoster=new();
    private readonly Dictionary<Node3D,float> quietRest=new();
    private void BuildMemoriesUi(Control ui)
    {
        var open=new Button { Text="Memories",Position=new(43,492),Size=new(215,32) };details.AddChild(open);open.Pressed+=OpenMemories;
        memoriesPanel=Panel(ui,new(330,135,620,465),new Color(.97f,.95f,.89f,.94f));memoriesPanel.MouseFilter=Control.MouseFilterEnum.Stop;memoriesPanel.Hide();
        Text(memoriesPanel,"CHAO MEMORIES",23,new(22,16,470,35),GardenArt.Ink);
        var close=new Button {Text="Close",Position=new(500,18),Size=new(96,32)};memoriesPanel.AddChild(close);close.Pressed+=()=>{memoriesOpen=false;memoriesPanel.Hide();};
        memoryPetPicker=new OptionButton {Position=new(22,63),Size=new(570,38)};memoriesPanel.AddChild(memoryPetPicker);memoryPetPicker.ItemSelected+=_=>RefreshMemories();
        memoryText=new RichTextLabel {Position=new(24,116),Size=new(568,326),BbcodeEnabled=false,ScrollActive=true};
        memoryText.AddThemeColorOverride("default_color",GardenArt.Ink);memoryText.AddThemeFontSizeOverride("normal_font_size",16);memoriesPanel.AddChild(memoryText);
    }
    private void OpenMemories()
    {
        memoryRoster.Clear();memoryRoster.AddRange(ExpeditionRoster.Available(pet));memoryPetPicker.Clear();
        foreach(var member in memoryRoster)memoryPetPicker.AddItem(member.State.Name);
        int selected=heldEgg?.Chao is {} held ? memoryRoster.FindIndex(m=>ReferenceEquals(m.State,held)) : 0;
        if(memoryRoster.Count>0)memoryPetPicker.Select(Math.Max(0,selected));
        memoriesOpen=true;memoriesPanel.Show();RefreshMemories();memoryPetPicker.GrabFocus();
    }
    private void RefreshMemories()
    {
        if(memoryPetPicker.Selected<0 || memoryPetPicker.Selected>=memoryRoster.Count){memoryText.Text="Memories will grow through life together.";return;}
        var selected=memoryRoster[memoryPetPicker.Selected];var history=selected.State.History;
        var friendNames=pet.SocialLinks.Where(link=>link.Met && (link.First==selected.Id || link.Second==selected.Id) && link.ScoreFrom(selected.Id)>=25)
            .OrderByDescending(link=>link.ScoreFrom(selected.Id)).Select(link=>memoryRoster.FirstOrDefault(m=>m.Id==(link.First==selected.Id ? link.Second : link.First))?.State.Name).Where(n=>n!=null).Take(3).ToArray();
        var lines=new List<string>();
        if(friendNames.Length>0)lines.Add("Close friends: "+string.Join(", ",friendNames)+".\n");
        var favorite=history.Events.Where(e=>e.Place!=null && e.Valence>.2f).OrderByDescending(e=>e.Valence*e.Influence(history.Seconds)).FirstOrDefault();
        if(favorite!=null)lines.Add("Favorite places: "+ChaoExperience.AreaTitle(favorite.Area)+".\n");
        lines.Add("Important moments\n");
        var important=history.Events.OrderByDescending(e=>e.Importance*.6f+e.Influence(history.Seconds)*.4f).Take(5).ToArray();
        lines.AddRange(important.Select(e=>"• "+e.Description));
        var recent=history.Events.Except(important).OrderByDescending(e=>e.LastAt).Take(4).ToArray();
        if(recent.Length>0){lines.Add("\nRecent experiences\n");lines.AddRange(recent.Select(e=>"• "+e.Description));}
        else if(important.Length==0)lines.Add("Your Chao will remember meaningful moments as you spend time together.");
        memoryText.Text=string.Join("\n",lines);
    }
    private Dictionary<string,ChaoExpressionDiagnostic> ExpressionDiagnostics
    {
        get
        {
            var result=new Dictionary<string,ChaoExpressionDiagnostic>(StringComparer.Ordinal);
            void Add(string id,Node3D art){if(art is ImportedChao model)result[id]=model.ExpressionDiagnostics;}
            if(expeditionTeam!=null){foreach(var member in expeditionTeam.Members)Add(member.Pet.Id,member.Art);}
            else
            {
                if(friend.Visible)Add("main",friendArt);
                foreach(var egg in world.GetChildren().OfType<GardenEgg>())if(egg.Chao!=null)Add(egg.Data.Id,egg.Art);
            }
            return result;
        }
    }
    private void PauseIdleGesturePresentations(bool value)
    {
        if(expeditionTeam!=null){foreach(var member in expeditionTeam.Members)ImportedChao.PauseIdleGestures(member.Art,value);}
        else
        {
            if(friendArt!=null)ImportedChao.PauseIdleGestures(friendArt,value);
            if(world!=null)foreach(var egg in world.GetChildren().OfType<GardenEgg>())ImportedChao.PauseIdleGestures(egg.Art,value);
        }
    }
    private void AdvanceChaoMemories(float dt)
    {
        string area=travel.AreaId(zone),title=ChaoExperience.AreaTitle(area);
        void Bind(PetState state,Node3D art,CharacterBody3D actor,bool held,string id)
        {
            if(!state.CanAct)return;
            state.History.Visit(area,title,expeditionTeam?.Members.Where(m=>m.Pet.State!=state).Select(m=>m.Pet.Id).ToArray());
            ImportedChao.BindPresentation(art,state,held,id);
            if(art is ImportedChao model && model.PoseKind is ChaoPoseKind.Rest or ChaoPoseKind.Sleep)
            {
                quietRest[art]=quietRest.GetValueOrDefault(art)+dt;
                if(quietRest[art]>20)state.History.Record(ChaoMemoryKind.Pleasant,"quiet-rest",area,$"Enjoyed a quiet rest in {title}.",.6f,.5f,place:new(actor.Position.X,actor.Position.Y,actor.Position.Z));
            }
            else quietRest[art]=0;
            if(!held && player.Position.DistanceTo(actor.Position)<2)ImportedChao.Attend(art,player.Position+Vector3.Up,20);
            if(held)ImportedChao.Attend(art,player.Position+Vector3.Up*1.4f,90);
        }
        if(expeditionTeam!=null)
        {foreach(var member in expeditionTeam.Members)Bind(member.Pet.State,member.Art,member.Body,false,member.Pet.Id);}
        else
        {
            if(friend.Visible)Bind(pet,friendArt,friend,carrying,"main");
            foreach(var egg in world.GetChildren().OfType<GardenEgg>())if(egg.Chao is {} state)Bind(state,egg.Art,egg,egg.IsCarried,egg.Data.Id);
        }
        foreach(var node in quietRest.Keys.Where(n=>!GodotObject.IsInstanceValid(n) || !n.IsInsideTree()).ToArray())quietRest.Remove(node);
    }
}
