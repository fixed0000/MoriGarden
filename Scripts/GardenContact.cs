using Godot;

namespace Mori;

// Soft contacts keep small garden residents movable instead of making them immovable walls.
internal static class GardenContact
{
    public static void Resolve(CharacterBody3D player,Vector3 previous,Vector3 position,float radius,float height,System.Action<Vector3> push)
    {
        if (player.Position.Y>position.Y+height || player.Position.Y+1.7f<position.Y) return;
        var offset=position-player.Position; offset.Y=0;
        float distance=offset.Length(), clearance=.30f+radius;
        if (distance>=clearance) return;
        var travel=player.Position-previous; travel.Y=0;
        var normal=distance>.001f ? offset/distance : travel.LengthSquared()>.001f ? travel.Normalized() : player.Basis.Z;
        float overlap=clearance-distance+.002f;
        // The receiver moves first; its own collision query prevents pushing it through scenery.
        push(normal*overlap);
    }

    public static void Move(CharacterBody3D body,Vector3 travel)
    {
        var hit=body.MoveAndCollide(travel);
        if (hit!=null) body.MoveAndCollide(hit.GetRemainder().Slide(hit.GetNormal()));
    }

    public static void SeparatePlayer(CharacterBody3D player,Vector3 position,float radius,float height)
    {
        if (player.Position.Y>position.Y+height || player.Position.Y+1.7f<position.Y) return;
        var offset=player.Position-position; offset.Y=0;
        float distance=offset.Length(),overlap=.30f+radius-distance;
        if (overlap<=.001f) return;
        var direction=distance>.001f ? offset/distance : -player.Basis.Z;
        Move(player,direction*(overlap+.002f));
    }
}
