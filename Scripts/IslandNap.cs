using Godot;

namespace Mori;

internal sealed class IslandNap
{
    private static readonly Vector3 Spot=new(-12.63f,.32f,-10.39f);
    private float cooldown=34, approach, remaining, phase, hopTime;
    private int stone=-1;
    private bool useStones;
    private Vector3 hopFrom;
    private readonly ChaoWander walker=new();
    public bool IsNapping => remaining>0;
    public bool IsVisiting => approach>0 || stone>=0 || IsNapping;

    public void Cancel(Node3D art)
    {
        if (IsNapping)
        {
            ImportedChao.CloseEyes(art,false);
            if (art.FindChild("HeadPivot",true,false) is Node3D head) head.Rotation=Vector3.Zero;
        }
        if (remaining>0 || approach>0) cooldown=62;
        approach=remaining=hopTime=0;
        stone=-1;
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
            if (cooldown>0 || chao.Energy>=99 || direction.Length()>42) return false;
            approach=70;
            useStones=!motion.IsSwimmingAt(actor.Position);
            stone=-1;
        }
        approach-=dt;
        if (direction.Length()<.29f && actor.Position.Y>.24f)
        {
            approach=0; remaining=10;
            ImportedChao.IslandNapPose(art);
            return true;
        }
        if (stone>=0) return Cross(actor,art,dt);
        if (approach<=0 || motion.BlockedTime>2.7f)
        {
            approach=0; cooldown=55;
            return false;
        }
        bool water=motion.IsSwimmingAt(actor.Position);
        var goal=useStones ? BankRoute(actor.Position) : Spot;
        if (useStones && goal==GardenLandscape.IslandBank && new Vector2(actor.Position.X-goal.X,actor.Position.Z-goal.Z).Length()<.29f)
        {
            stone=0; hopFrom=actor.Position; hopTime=0;
            motion.Reset();
            return Cross(actor,art,dt);
        }
        var route=motion.RouteOffUpperPlatform(actor.Position,goal)-actor.Position;
        route.Y=0;
        if (useStones && !water && !motion.Airborne) route=walker.Steer(actor,route,dt);
        bool moving=motion.Step(actor,route,chao,false,water,dt,true,out _);
        if (moving || water) phase+=dt*(water && chao.Swim<100 ? 5.2f : 9);
        ImportedChao.MovePose(art,phase,moving,water,chao.Swim>=100,false,motion.Airborne,motion.Flying);
        return true;
    }
    private bool Cross(CharacterBody3D actor,Node3D art,float dt)
    {
        var steps=GardenLandscape.IslandSteps;
        Vector3 target=stone<steps.Length ? steps[stone] : GardenLandscape.IslandLanding;
        float duration=.59f;
        hopTime=Mathf.Min(duration,hopTime+dt);
        float t=hopTime/duration;
        var heading=target-hopFrom;
        actor.Rotation=new(0,Mathf.Atan2(heading.X,heading.Z),0);
        actor.Position=hopFrom.Lerp(target,t)+Vector3.Up*(Mathf.Sin(t*Mathf.Pi)*.62f);
        ImportedChao.MovePose(art,0,false,false,false,false,true,false);
        if (t>=1)
        {
            hopFrom=target; hopTime=0; stone++;
            if (stone>steps.Length) { stone=-1; useStones=false; }
        }
        return true;
    }

    private static Vector3 BankRoute(Vector3 at)
    {
        var radial=at-GardenArt.Pond; radial.Y=0;
        var end=GardenLandscape.IslandBank-GardenArt.Pond;
        var delta=end-radial;
        if (delta.LengthSquared()<.1f) return GardenLandscape.IslandBank;
        float fraction=Mathf.Clamp(-radial.Dot(delta)/delta.LengthSquared(),0,1);
        if ((radial+delta*fraction).Length()>GardenArt.PondRadius+.82f)
            return GardenLandscape.IslandBank;
        // Take the grass on the south side, clear of the waterfall.
        float angle=Mathf.Atan2(radial.Z,radial.X);
        var waypoint=Vector3.Right.Rotated(Vector3.Up,-(angle+.30f));
        return GardenArt.Pond+waypoint*(GardenArt.PondRadius+2.60f);
    }

}
