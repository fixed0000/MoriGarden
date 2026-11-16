using System;
using System.Collections.Generic;
using Godot;

namespace Mori;

internal static class ExpeditionArt
{
    private static readonly Dictionary<string,Action<Node3D>> Builders=new() { [WorldCatalog.Meadow]=WindfallMeadow.Build,[WorldCatalog.Greenhouse]=GreenhouseArt.Build };
    public static void Build(Node3D root,string id)
    {
        if(!Builders.TryGetValue(id,out var builder)) throw new ArgumentException($"No scenery registered for {id}");
        root.SetMeta("expedition_area",id);builder(root);
    }
    internal static string Area(Node3D root)=>root.HasMeta("expedition_area") ? root.GetMeta("expedition_area").AsString() : WorldCatalog.Meadow;
    internal static Rect2 Bounds(string area)=>area==WorldCatalog.Greenhouse ? new(-18,-21,36,45) : new(-35.5f,-26.5f,71,53);
    internal static float Height(float x,float z,string? area=null)=>area==WorldCatalog.Greenhouse ? GreenhouseArt.Height(x,z) : WindfallMeadow.Height(x,z);
    internal static Vector2[] Landmarks(string area)=>area==WorldCatalog.Greenhouse ? GreenhouseArt.Landmarks : WindfallMeadow.ObservationSpots;
    internal static NaturalWaterBody Water(string area)=>area==WorldCatalog.Greenhouse ? GreenhouseArt.Water : WindfallMeadow.Water;
    internal static Vector3 RestorePosition(Vector3 saved,CharacterBody3D player,string? area=null)
    {
        var bounds=Bounds(area ?? WorldCatalog.Meadow);
        var at=new Vector3(Mathf.Clamp(saved.X,bounds.Position.X+.5f,bounds.End.X-.5f),saved.Y,Mathf.Clamp(saved.Z,bounds.Position.Y+.5f,bounds.End.Y-.5f));
        var shape=new CapsuleShape3D { Radius=.31f,Height=1.7f };
        for(int ring=0;ring<7;ring++) for(int i=0;i<(ring==0 ? 1 : 12);i++)
        {
            float angle=i*Mathf.Tau/12;
            var candidate=at+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*ring*.6f;
            candidate.Y=Mathf.Max(Mathf.Clamp(saved.Y,-.24f,area==WorldCatalog.Greenhouse ? 5 : 18),Height(candidate.X,candidate.Z,area)+.025f);
            var query=new PhysicsShapeQueryParameters3D { Shape=shape,CollisionMask=1,Transform=new(Basis.Identity,candidate+Vector3.Up*.85f) };
            if(player.GetWorld3D().DirectSpaceState.IntersectShape(query,1).Count==0)return candidate;
        }
        return new(0,.025f,area==WorldCatalog.Greenhouse ? 17 : 23);
    }
}
