using System;
using Godot;

namespace Mori;

internal sealed class WorldTravel
{
    public string ExpeditionArea { get; set; }=WorldCatalog.Meadow;
    public WorldRoute? Pending { get; private set; }
    private bool armed=true;
    public string AreaId(GardenZone zone) => zone switch {
        GardenZone.Garden=>"garden",GardenZone.Lobby=>"plaza",GardenZone.Stadium=>"stadium",
        GardenZone.CoastalCove=>"coastal-cove",_=>ExpeditionArea };
    public static Vector3 Point(WorldPoint p) => new(p.X,p.Y,p.Z);
    public static GardenZone Zone(string id) => id switch {
        "garden"=>GardenZone.Garden,"plaza"=>GardenZone.Lobby,"stadium"=>GardenZone.Stadium,
        "coastal-cove"=>GardenZone.CoastalCove,_=>GardenZone.Expedition };
    public WorldRoute? Detect(GardenZone zone,Vector3 at,ExpeditionProgress progress)
    {
        WorldRoute? found=null;
        foreach(var route in WorldCatalog.Default.Exits(AreaId(zone),progress))
        {
            var delta=at-Point(route.Entrance);
            if (Mathf.Abs(delta.Y)<1 && new Vector2(delta.X,delta.Z).Length()<route.Radius) { found=route; break; }
        }
        if (!armed) { if(found==null)armed=true; return null; }
        if(found!=null) { armed=false; Pending=found; }
        return found;
    }
    public void Arrived() { Pending=null; armed=false; }
    public string RestoreArea(string? id,ExpeditionProgress progress) =>
        id!=null && WorldCatalog.Default.IsExpedition(id) && WorldCatalog.Default.IsOpen(id,progress) ? id : WorldCatalog.Meadow;
    public void Build(Node3D world,GardenZone zone,PetState save)
    {
        WaterArt.CurrentExpeditionArea=ExpeditionArea;WaterArt.SiteWater=null;
        if (zone==GardenZone.Expedition) ExpeditionArt.Build(world,ExpeditionArea);
        else ZoneArt.Build(world,zone,save.GardenTrees);
        if(zone==GardenZone.Garden)StadiumArt.PrizeToys(world,save);
        foreach(var route in WorldCatalog.Default.Exits(AreaId(zone),save.Expedition))
            if (WorldCatalog.Default.IsExpedition(route.To) || zone==GardenZone.Expedition)
                ZoneArt.Portal(world,Point(route.Entrance),WorldCatalog.Default.Areas[route.To].Title,new("849282"));
        if(zone==GardenZone.Expedition && ExpeditionArea==WorldCatalog.Greenhouse)
            world.GetNode<GreenhouseMission>("GreenhouseMission").Bind(save);
        if(zone==GardenZone.Expedition)WildlifeHabitats.Build(world,save,ExpeditionArea);
    }
    public string Title(GardenZone zone) => WorldCatalog.Default.Areas[AreaId(zone)].Title;
}
