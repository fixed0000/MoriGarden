using System.Collections.Generic;
using Godot;

namespace Mori;

internal static class ChaoContacts
{
    internal readonly record struct Member(CharacterBody3D Body,bool Fixed=false,float Radius=.17f,Vector3 Offset=default);
    public static void Resolve(IReadOnlyList<Member> members)
    {
        for(int pass=0;pass<12;pass++)
        {
            bool overlap=false;
            for(int i=0;i<members.Count;i++) for(int j=i+1;j<members.Count;j++)
            {
                var a=members[i]; var b=members[j];
                if (!GodotObject.IsInstanceValid(a.Body) || !GodotObject.IsInstanceValid(b.Body) ||
                    Mathf.Abs(a.Body.GlobalPosition.Y-b.Body.GlobalPosition.Y)>.38f) continue;
                var delta=b.Body.GlobalPosition+b.Offset-a.Body.GlobalPosition-a.Offset; delta.Y=0;
                float distance=delta.Length(),needed=a.Radius+b.Radius;
                if (distance>=needed-.0005f) continue;
                overlap=true;
                var normal=distance>.001f ? delta/distance : ((i+j)%2==0 ? Vector3.Right : Vector3.Back);
                float depth=needed-distance+.001f;
                float share=a.Fixed ? 0 : b.Fixed ? 1 : .5f;
                GardenContact.Move(a.Body,-normal*depth*share);
                GardenContact.Move(b.Body,normal*depth*(1-share));
                var after=b.Body.GlobalPosition+b.Offset-a.Body.GlobalPosition-a.Offset; after.Y=0;
                float remaining=needed-after.Length();
                if (remaining>.001f)
                {
                    if (!a.Fixed) GardenContact.Move(a.Body,-normal*remaining);
                    if (!b.Fixed) GardenContact.Move(b.Body,normal*remaining);
                }
            }
            if (!overlap) break;
        }
    }
}
