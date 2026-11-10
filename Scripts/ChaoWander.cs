using Godot;

namespace Mori;

internal sealed class ChaoWander
{
    private Vector3 detour;
    private float detourTime;

    public static Vector3 PickGoal(CharacterBody3D actor,Vector3 center,float radius)
    {
        var best=actor.Position;
        float reach=0;
        for (int i=0;i<12;i++)
        {
            var heading=Vector3.Back.Rotated(Vector3.Up,GD.Randf()*Mathf.Tau);
            var candidate=center+heading*Mathf.Lerp(radius*.38f,radius,GD.Randf());
            candidate.Y=actor.Position.Y;
            if (GardenArt.IsPondWater(candidate) && !GardenArt.IsPondWater(actor.Position)) continue;
            var direction=candidate-actor.Position;
            float distance=direction.Length();
            if (distance<=reach || Blocked(actor,direction.LimitLength(Mathf.Min(1.15f,distance)))) continue;
            best=candidate;
            reach=distance;
        }
        return best;
    }

    public Vector3 Steer(CharacterBody3D actor,Vector3 toward,float dt)
    {
        if (toward.LengthSquared()<.05f) return toward;
        detourTime=Mathf.Max(0,detourTime-dt);
        if (detourTime>0 && !Blocked(actor,detour*.62f)) return detour;
        var heading=toward.Normalized();
        if (!Blocked(actor,heading*Mathf.Min(.75f,toward.Length())))
        {
            detourTime=0;
            return toward;
        }
        float first=heading.Dot(actor.Basis.X)<0 ? 1 : -1;
        foreach (float angle in new[]{first*.82f,-first*.82f,first*1.45f,-first*1.45f,first*2.28f,-first*2.28f})
        {
            var candidate=heading.Rotated(Vector3.Up,angle);
            if (Blocked(actor,candidate*.78f)) continue;
            detour=candidate;
            detourTime=.88f;
            return detour;
        }
        return Vector3.Zero;
    }

    private static bool Blocked(CharacterBody3D actor,Vector3 travel)
    {
        var at=actor.GlobalTransform;
        at.Origin+=Vector3.Up*.065f;
        var hit=new KinematicCollision3D();
        return actor.TestMove(at,travel,hit) && hit.GetNormal().Y<.58f;
    }
}
