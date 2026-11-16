using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mori;

internal sealed class DiscoveryReservations
{
    private readonly Dictionary<string,(string Object,Vector3 Point)> slots=new();
    internal int Count=>slots.Count;
    internal Vector3? Reserve(DiscoveryInterest item,ExpeditionCompanion pet,ExpeditionNavigation nav,IEnumerable<ExpeditionCompanion> team)
    {
        var delta=pet.Body.Position-item.GlobalPosition;delta.Y=0;
        float angle=Mathf.Atan2(delta.X,delta.Z);
        for(int attempt=0;attempt<16;attempt++)
        {
            float a=angle+attempt*.85f,radius=.85f+attempt/8*.75f;
            var point=nav.Nearest(item.GlobalPosition+new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*radius);
            if(point.DistanceTo(item.GlobalPosition)>3 || slots.Values.Any(s=>ExpeditionCompanion.Flat(s.Point,point)<.9f) ||
                team.Any(m=>m!=pet && ExpeditionCompanion.Flat(m.Body.Position,point)<.65f) || nav.Path(pet.Body.Position,point).Length==0)continue;
            slots[pet.Pet.Id]=(item.Definition.Id,point);return point;
        }
        return null;
    }
    internal void Release(string petId)=>slots.Remove(petId);
}
