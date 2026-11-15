using System;
using Godot;

namespace Mori;

// A water body's extent and depth come from the same triangle field as its solid bed.
internal sealed record NaturalWaterBody(string Id,Rect2 Bounds,float Level,Func<float,float,float> Relief,bool Sea=false,Func<Vector2,bool>? Area=null)
{
    internal float Bed(float x,float z)
    {
        float a=Mathf.Floor(x),b=Mathf.Floor(z),u=x-a,v=z-b;
        float h00=Relief(a,b),h10=Relief(a+1,b),h01=Relief(a,b+1),h11=Relief(a+1,b+1);
        return u+v<=1 ? h00+(h10-h00)*u+(h01-h00)*v : h11+(h01-h11)*(1-u)+(h10-h11)*(1-v);
    }
    internal float SignedVertexDepth(Vector2 p)=>Area==null || Area(p) ? Level-Relief(p.X,p.Y) : -1;
    internal float Depth(Vector2 p)=>Bounds.HasPoint(p) && (Area==null || Area(p)) ? Mathf.Max(0,Level-Bed(p.X,p.Y)) : 0;
    internal bool Contains(Vector3 p)=>p.Y<Level+.06f && Depth(new(p.X,p.Z))>.004f;
}
