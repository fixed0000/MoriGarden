using System;
using System.Linq;
using Godot;

namespace Mori;

internal partial class GreenhouseMission : Node3D
{
    internal PetState SaveData { get; private set; }=null!;
    internal CompactMissionState Progress=>GreenhouseRules.State(SaveData);
    internal PuzzleElement[] Elements { get; private set; }=Array.Empty<PuzzleElement>();
    internal PuzzleMovable Counterweight { get; private set; }=null!;
    internal PlayerPuzzleAction PlayerAction { get; }=new();
    internal Action SaveRequested { get; set; }=()=>{};
    internal Action<string> Message { get; set; }=_=>{};
    internal int PlayerActions { get; private set; }
    internal int ChaoActions { get; private set; }
    internal bool PlateDown=>Elements.First(e=>e.Id==GreenhouseRules.Plate).Pressed;
    private GreenhouseVisuals visual=null!;
    private float reactionDelay;
    private bool changed;
    internal void RefreshOwner(PetState owner)=>SaveData=owner;
    internal void BuildElements()
    {
        PuzzleElement Make(string id,string title,PuzzleElementKind kind,Vector3 at)
        {
            var element=new PuzzleElement { Id=id,Title=title,Kind=kind,Position=at,
                State=()=>Progress.Element(id),Locked=()=>SaveData!=null && Progress.Completed };
            AddChild(element);element.Build();element.Changed=OnActivated;return element;
        }
        Elements=new[]{Make(GreenhouseRules.Intake,"INTAKE\nOpen the water supply",PuzzleElementKind.Switch,new(-7.7f,0,7)),
            Make(GreenhouseRules.Diverter,"DISTRIBUTOR\n0: Closed   1: Side bed   2: Central bed",PuzzleElementKind.Rotary,new(7,0,-1)),
            Make(GreenhouseRules.Plate,"FILTER LIFT\nWeight lifts the grate",PuzzleElementKind.Plate,new(6,0,-7)),
            Make("reset","MAINTENANCE\nReset unfinished mechanisms",PuzzleElementKind.Reset,new(4,0,17))};
        Counterweight=new PuzzleMovable { Name="Counterweight",Initial=new(9,.01f,-5),Position=new(9,.01f,-5),PlacementBounds=new(-16,-19,32,42),FloorHeight=GreenhouseArt.Height,State=()=>Progress.Element(GreenhouseRules.Crate) };AddChild(Counterweight);
        visual=new(GetParent<Node3D>());
        GreenhouseArt.Plaque(GetParent<Node3D>(),new(4.3f,1.05f,-4.3f),"THE CENTRAL BED\nSupply → Distributor → Weighted filter",3.4f);
    }
    internal void Bind(PetState owner)
    {
        SaveData=owner;Progress.Validate();Counterweight.Restore();
        WaterArt.SiteWater=new("ActiveGreenhouseCanals",new(-18,-21,36,45),-.045f,GreenhouseArt.Height,Area:p=>visual.Wet(p,Progress,PlateDown));
        foreach(var element in Elements)element.Visual(.016f,Progress.Revision);
        visual.Update(Progress,PlateDown,0,true);
    }
    private void OnActivated(PuzzleElement element,ExpeditionCompanion? chao)
    {
        if(element.Kind==PuzzleElementKind.Reset)
        {
            if(!Progress.ResetUnfinished())return;
            Counterweight.Restore();PlayerAction.Cancel();Message("Mechanisms reset. Your Chao and inventory are unchanged.");
        }
        else Progress.Revision=Math.Min(1000000,Progress.Revision+1);
        if(chao==null)PlayerActions++;else ChaoActions++;
        changed=true;SaveRequested();
    }
    internal PuzzleElement? Nearby(CharacterBody3D player)=>Elements.Where(e=>e.Accessible(player)).OrderBy(e=>e.GlobalPosition.DistanceSquaredTo(player.GlobalPosition)).FirstOrDefault();
    internal bool Interact(CharacterBody3D player)
    {
        if(Nearby(player) is not {} element)return false;
        if(element.Kind==PuzzleElementKind.Plate){Message("A steady weight lifts the filter. Stand here or push the counterweight onto the plate.");return true;}
        return PlayerAction.Begin(element,player);
    }
    internal bool Ask(CharacterBody3D player,ExpeditionTeam team)
    {
        var element=Nearby(player);if(element==null || element.Kind==PuzzleElementKind.Reset)return false;
        foreach(var pet in team.Members.Where(m=>(!m.Busy || m.Intent==ExpeditionIntent.Investigate) && ExpeditionCompanion.Flat(m.Body.Position,player.Position)<7).OrderBy(m=>m.Body.Position.DistanceSquaredTo(player.Position)))
            if(team.Discoveries.Request(pet,element.Interest))return true;
        Message("Your Chao need time or rest. You can operate this mechanism yourself.");return true;
    }
    internal string Hint(CharacterBody3D player,ExpeditionTeam? team)
    {
        if(PlayerAction.Target is {} active)return $"Operating {active.Title.Split('\n')[0]} · Move away to cancel";
        bool helper=team?.Members.Any(m=>(!m.Busy || m.Intent==ExpeditionIntent.Investigate) && !m.Sleeping && m.Pet.State.Energy>=15 && m.Pet.State.Joy>=10 && m.Pet.State.RelationshipWith()>=-50 && ExpeditionCompanion.Flat(m.Body.Position,player.Position)<7)==true;
        return Nearby(player) is {} element ? "E: "+(element.Kind==PuzzleElementKind.Plate ? "Examine filter plate" : element.Kind==PuzzleElementKind.Reset ? "Reset unfinished mechanisms" : "Operate "+element.Title.Split('\n')[0])+ (element.Kind==PuzzleElementKind.Reset || !helper ? "" : "   F: Ask a Chao to help") : "";
    }
    internal void Advance(float dt,CharacterBody3D player,ExpeditionTeam? team)
    {
        if(SaveData==null)return;
        PlayerAction.Step(dt,player);reactionDelay=Mathf.Max(0,reactionDelay-dt);
        var plate=Elements.First(e=>e.Id==GreenhouseRules.Plate);
        bool OnPlate(Vector3 at)=>Mathf.Abs(at.Y-plate.Position.Y)<.5f && ExpeditionCompanion.Flat(at,plate.Position)<1.2f;
        plate.Pressed=OnPlate(player.Position) || OnPlate(Counterweight.Position) || team?.Members.Any(m=>OnPlate(m.Body.Position))==true;
        bool completed=GreenhouseRules.Advance(SaveData,plate.Pressed,dt);
        if(completed){Progress.Revision++;changed=true;Message("Greenhouse restored! The Moonblossoms are growing again.");}
        bool reward=GreenhouseRules.ClaimReward(SaveData);
        if(reward)Message("Greenhouse restored · Moonblossom Seed added to your bag.");
        if(completed || reward)SaveRequested();
        foreach(var element in Elements)element.Visual(dt,Progress.Revision);
        visual.Update(Progress,plate.Pressed,dt);
        if(changed && reactionDelay<=0 && team!=null)
        {
            var awake=team.Members.Where(m=>!m.Sleeping && ExpeditionCompanion.Flat(m.Body.Position,player.Position)<12).ToArray();
            foreach(var pet in awake)
                pet.Art.GetNodeOrNull<EmotionOrb>("MoodOrb")?.Celebrate();
            if(awake.Length>0)ChaoSounds.Play(awake[0].Art,"voice");changed=false;reactionDelay=6;
        }
    }
}
