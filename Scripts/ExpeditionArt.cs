using System;
using System.Collections.Generic;
using Godot;

namespace Mori;

internal static class ExpeditionArt
{
    private static readonly Dictionary<string,Action<Node3D>> Builders=new() { [WorldCatalog.Meadow]=WindfallMeadow.Build };
    public static void Build(Node3D root,string id)
    {
        if(!Builders.TryGetValue(id,out var builder)) throw new ArgumentException($"No scenery registered for {id}");
        builder(root);
    }
    internal static float Height(float x,float z)=>WindfallMeadow.Height(x,z);
    internal static Vector3 RestorePosition(Vector3 saved,CharacterBody3D player)
    {
        var at=new Vector3(Mathf.Clamp(saved.X,-35,35),saved.Y,Mathf.Clamp(saved.Z,-27,27));
        var shape=new CapsuleShape3D { Radius=.31f,Height=1.7f };
        for(int ring=0;ring<7;ring++) for(int i=0;i<(ring==0 ? 1 : 12);i++)
        {
            float angle=i*Mathf.Tau/12;
            var candidate=at+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*ring*.6f;
            candidate.Y=Mathf.Max(Mathf.Clamp(saved.Y,-.24f,18),Height(candidate.X,candidate.Z)+.025f);
            var query=new PhysicsShapeQueryParameters3D { Shape=shape,CollisionMask=1,Transform=new(Basis.Identity,candidate+Vector3.Up*.85f) };
            if(player.GetWorld3D().DirectSpaceState.IntersectShape(query,1).Count==0)return candidate;
        }
        return new(0,.025f,23);
    }
}
