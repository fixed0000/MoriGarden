using Godot;

namespace Mori;

internal static class ThrowMotion
{
    public static bool Step(CharacterBody3D body, ref Vector3 velocity, float dt,
        System.Action<KinematicCollision3D>? onImpact = null)
    {
        velocity += Vector3.Down*18*dt;
        var hit = body.MoveAndCollide(velocity*dt);
        if (hit == null) return false;
        onImpact?.Invoke(hit);
        Vector3 normal = hit.GetNormal();
        if (normal.Y > .6f && velocity.Y > -2)
        {
            velocity = Vector3.Zero;
            return true;
        }
        velocity = velocity.Bounce(normal) * (normal.Y > .6f ? .25f : .46f);
        return false;
    }
    public static bool StepItem(CharacterBody3D body,ref Vector3 velocity,float dt,ref int bounces)
    {
        velocity+=Vector3.Down*18*dt;
        var hit=body.MoveAndCollide(velocity*dt);
        if (hit==null) return false;
        var normal=hit.GetNormal();
        if (normal.Y>.6f)
        {
            if (bounces>=2) { velocity=Vector3.Zero; return true; }
            float rebound=bounces==0 ? Mathf.Clamp(-velocity.Y*.32f,1.25f,2) : Mathf.Clamp(-velocity.Y*.28f,.48f,.70f);
            bounces++;
            velocity=new Vector3(velocity.X*.58f,rebound,velocity.Z*.58f);
        }
        else velocity=velocity.Bounce(normal)*.38f;
        return false;
    }

}
