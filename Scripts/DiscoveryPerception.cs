using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mori;

internal sealed class DiscoveryPerception
{
    private readonly Node3D world;
    private readonly Dictionary<Vector2I,List<DiscoveryInterest>> cells=new();
    internal IReadOnlyList<DiscoveryInterest> Interests { get; }
    internal int VisibilityQueries { get; private set; }
    internal float SearchRadius { get; }
    internal DiscoveryPerception(Node3D world)
    {
        this.world=world;
        var found=new List<DiscoveryInterest>();
        void Visit(Node node) {foreach(var child in node.GetChildren()){if(child is DiscoveryInterest interest)found.Add(interest);Visit(child);}}
        Visit(world);
        if(found.Any(i=>!DiscoveryMemory.ValidId(i.Definition.Id)) || found.Select(i=>i.Definition.Id).Distinct().Count()!=found.Count)
            throw new ArgumentException("Discovery objects require unique stable IDs.");
        if(found.Any(i=>!float.IsFinite(i.Definition.Radius) || i.Definition.Radius<=0 || i.Definition.Radius>64 || !float.IsFinite(i.Definition.Appeal) || i.Definition.Appeal<0))
            throw new ArgumentException("Discovery objects require a finite radius (0–64 m) and nonnegative appeal.");
        SearchRadius=found.Count==0 ? 0 : found.Max(i=>i.Definition.Radius)*1.2f;
        Interests=found;Reindex();
    }
    private static Vector2I Cell(Vector3 point)=>new(Mathf.FloorToInt(point.X/8),Mathf.FloorToInt(point.Z/8));
    internal void Reindex()
    {
        cells.Clear();
        foreach(var item in Interests.Where(i=>GodotObject.IsInstanceValid(i) && i.IsInsideTree()))
        {
            var key=Cell(item.GlobalPosition);if(!cells.TryGetValue(key,out var list))cells[key]=list=new();list.Add(item);
        }
    }
    internal IEnumerable<DiscoveryInterest> Nearby(Vector3 point,float radius)
    {
        var cell=Cell(point);int reach=Mathf.CeilToInt(radius/8);
        for(int x=-reach;x<=reach;x++)for(int z=-reach;z<=reach;z++)
            if(cells.TryGetValue(cell+new Vector2I(x,z),out var list))foreach(var item in list)
                if(GodotObject.IsInstanceValid(item) && item.IsInsideTree() && ExpeditionCompanion.Flat(point,item.GlobalPosition)<=radius)yield return item;
    }
    internal bool Visible(ExpeditionCompanion pet,DiscoveryInterest item,bool fieldOfView=true)
    {
        var delta=item.LookPoint-(pet.Body.GlobalPosition+Vector3.Up*.3f);
        var flat=new Vector3(delta.X,0,delta.Z);
        if(fieldOfView && flat.Length()>1.3f && pet.Body.GlobalBasis.Z.Dot(flat.Normalized())<-.15f)return false;
        VisibilityQueries++;
        var hit=world.GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(pet.Body.GlobalPosition+Vector3.Up*.3f,item.LookPoint,1));
        var own=item.VisibilityRoot ?? item;
        return hit.Count==0 || hit["collider"].AsGodotObject() is Node node && (own==node || own.IsAncestorOf(node));
    }
}
