using Godot;

namespace Mori;

internal sealed class IslandNap
{
    private static readonly Vector3 Spot=new(-12.63f,.32f,-10.39f);
    private float cooldown=42, approach, remaining, phase;
    public bool IsNapping => remaining>0;

    public void Cancel(Node3D art)
    {
        if (IsNapping)
        {
            ImportedChao.CloseEyes(art,false);
            if (art.FindChild("HeadPivot",true,false) is Node3D head) head.Rotation=Vector3.Zero;
        }
        if (remaining>0 || approach>0) cooldown=62;
        approach=remaining=0;
    }

    public bool Step(CharacterBody3D actor,Node3D art,PetState chao,ChaoMotion motion,float dt)
    {
        if (remaining>0)
        {
            ImportedChao.IslandNapPose(art);
            if (remaining<=dt) Cancel(art);
            else remaining-=dt;
            return true;
        }
        Vector3 direction=Spot-actor.Position;
        direction.Y=0;
        if (approach<=0)
        {
            cooldown=Mathf.Max(0,cooldown-dt);
            if (cooldown>0 || chao.Energy>=94 || direction.Length()>14) return false;
            approach=24;
        }
        approach-=dt;
        if (direction.Length()<.29f && actor.Position.Y>.24f)
        {
            approach=0; remaining=10;
            ImportedChao.IslandNapPose(art);
            return true;
        }
        if (approach<=0 || motion.BlockedTime>2.7f)
        {
            approach=0; cooldown=55;
            return false;
        }
        var pond=actor.Position-GardenArt.Pond; pond.Y=0;
        bool water=direction.Length()>3.15f && pond.Length()<GardenArt.PondRadius && !motion.Airborne;
        if (water)
            actor.Position=new(actor.Position.X,Mathf.MoveToward(actor.Position.Y,0,dt),actor.Position.Z);
        var route=motion.RouteOffUpperPlatform(actor.Position,Spot)-actor.Position;
        route.Y=0;
        bool moving=motion.Step(actor,route,chao,false,water,dt,true,out _);
        if (moving) phase+=dt*9;
        ImportedChao.MovePose(art,phase,moving,water,chao.Swim>=100,false,motion.Airborne,false);
        return true;
    }
}
