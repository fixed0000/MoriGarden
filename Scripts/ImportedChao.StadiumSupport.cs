using System.Collections.Generic;
using Godot;

namespace Mori;

public partial class ImportedChao
{
    private readonly Dictionary<MeshInstance3D,Vector3[]> stadiumVertices=new();
    // Fallen athletes use the actual deformed mesh rather than a fixed sleeping
    // height. This also preserves floor contact through the getting-up pose.
    internal static void SupportStadiumFloor(ImportedChao art)
    {
        var actor=(Node3D)art.GetParent();
        using var query=PhysicsRayQueryParameters3D.Create(actor.GlobalPosition+Vector3.Up*.3f,actor.GlobalPosition-Vector3.Up*.6f,1);
        var hit=actor.GetWorld3D().DirectSpaceState.IntersectRay(query);
        if(hit.Count==0 || ((Vector3)hit["normal"]).Y<.7f)return;
        float bottom=float.MaxValue;
        foreach(var node in art.GetChild(0).FindChildren("*","MeshInstance3D",true,false))
        {
            if(node is not MeshInstance3D mesh || mesh.Mesh==null || !mesh.IsVisibleInTree())continue;
            if(!art.stadiumVertices.TryGetValue(mesh,out var vertices))
            {
                var points=new List<Vector3>();for(int i=0;i<mesh.Mesh.GetSurfaceCount();i++)points.AddRange(mesh.Mesh.SurfaceGetArrays(i)[(int)Mesh.ArrayType.Vertex].AsVector3Array());
                art.stadiumVertices[mesh]=vertices=points.ToArray();
            }
            foreach(var vertex in vertices)bottom=Mathf.Min(bottom,(mesh.GlobalTransform*vertex).Y);
        }
        if(bottom<float.MaxValue)art.GlobalPosition+=Vector3.Up*Mathf.Clamp(((Vector3)hit["position"]).Y+.003f-bottom,-.15f,.15f);
    }
}
