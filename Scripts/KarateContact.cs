using System;
using System.Numerics;

namespace Mori;

// Shared by the rules and the limb targets. A miss keeps its finite reach;
// it must never stretch a paw all the way to a distant opponent.
public static class KarateContact
{
    public const float ImpactSwing=.46f,Reach=.33f,TargetRadius=.115f;
    public static Vector2 Tip(Vector2 origin,Vector2 facing,int kind)
    {
        var side=new Vector2(facing.Y,-facing.X);
        return origin+facing*Reach+side*(kind==0 ? -.025f : .025f);
    }
    public static bool Touches(KarateFighter attacker,KarateFighter defender)
    {
        var delta=defender.Position-attacker.Position;
        return delta.LengthSquared()>.000001f && Vector2.Dot(attacker.Facing,Vector2.Normalize(delta))>.75f
            && Vector2.Distance(Tip(attacker.Position,attacker.Facing,attacker.AttackKind),defender.Position)<=TargetRadius;
    }
}
