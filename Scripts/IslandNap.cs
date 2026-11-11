using Godot;

namespace Mori;

internal sealed class IslandNap
{
    private enum ReturnMode { None, Stones, WaterEntry, Swimming }
    private const float NapDuration=34;
    private static readonly Vector3 IslandCenter=new(-13.2f,0,-10.8f);
    private static readonly Vector3 WaterEntry=new(-10.15f,0,-8.55f);
    private ReturnMode returning;
    private float islandWalk, nextStroll;
    private Vector3 strollTarget;
    private bool exploredIsland;
    private static readonly Vector3[] NapPlaces={new(-12.73f,.32f,-10.36f),new(-13.67f,.32f,-10.36f),
        new(-12.73f,.32f,-11.24f),new(-13.67f,.32f,-11.24f)};
    public System.Collections.Generic.Dictionary<int,IslandNap> Spaces { get; set; }=new();
    private int slot=-1;
    internal Vector3 NapSpot => NapPlaces[slot<0 ? 0 : slot];
    private readonly ChaoSettling settling=new();
    private float landingAge=-1;
    private Vector3 landingAt;
    private bool returnLanding;
    private float cooldown=34, approach, remaining, phase, hopTime;
    private int stone=-1;
    private bool useStones;
    private Vector3 hopFrom;
    private readonly ChaoWander walker=new();
    internal bool IsSettling=>settling.Active;
    public bool IsNapping => remaining>0;
    public bool IsVisiting => settling.Active || landingAge>=0 || approach>0 || stone>=0 || IsNapping || returning!=ReturnMode.None || islandWalk>0;

    public void Cancel(Node3D art)
    {
        if (IsNapping)
        {
            ImportedChao.CloseEyes(art,false);
            if (art.FindChild("HeadPivot",true,false) is Node3D head) head.Rotation=Vector3.Zero;
        }
        if (remaining>0 || approach>0) cooldown=62;
        ReleaseSpot(); settling.Cancel(); landingAge=-1;
        approach=remaining=hopTime=islandWalk=0;
        returning=ReturnMode.None;
        stone=-1;
    }

    public bool Step(CharacterBody3D actor,Node3D art,PetState chao,ChaoMotion motion,float dt)
    {
        if (landingAge>=0)
        {
            landingAge+=dt;
            float rebound=landingAge<.26f ? Mathf.Sin(landingAge/.26f*Mathf.Pi)*.065f : 0;
            actor.Position=new(actor.Position.X,landingAt.Y+rebound,actor.Position.Z);
            ImportedChao.MovePose(art,0,false,false,false,false,landingAge<.26f,false);
            if (art.FindChild("HeadPivot",true,false) is Node3D head) head.Rotation=new(.18f,Mathf.Sin(landingAge*2)*.22f,0);
            if (landingAge>=2.26f) { landingAge=-1; if (returnLanding) FinishReturn(); }
            return true;
        }
        if (settling.Active)
        {
            if (chao.Energy>=100) { settling.Cancel(); ReleaseSpot(); return false; }
            if (settling.Step(actor,art,chao.Manners,dt)) remaining=NapDuration;
            return true;
        }
        if (remaining>0)
        {
            ImportedChao.SleepPose(art,NapDuration-remaining,chao.Manners);
            remaining=Mathf.Max(0,remaining-dt);
            if (remaining<=0)
            {
                ImportedChao.CloseEyes(art,false);
                ReleaseSpot();
                BeginReturn(GD.Randf()<(chao.Swim<100 ? .68f : .42f));
            }
            return true;
        }
        if (returning!=ReturnMode.None) return Return(actor,art,chao,motion,dt);
        if (approach<=0 && OnIsland(actor.Position) && !exploredIsland)
        {
            exploredIsland=true;
            islandWalk=8+GD.Randf()*7;
            nextStroll=0;
        }
        else if (!OnIsland(actor.Position) && islandWalk<=0) exploredIsland=false;
        if (islandWalk>0)
        {
            islandWalk=Mathf.Max(0,islandWalk-dt);
            nextStroll-=dt;
            if (nextStroll<=0)
            {
                var offset=Vector3.Back.Rotated(Vector3.Up,GD.Randf()*Mathf.Tau)*.77f;
                strollTarget=IslandCenter+offset;
                nextStroll=2.5f;
            }
            Walk(actor,art,chao,motion,strollTarget,dt);
            if (islandWalk<=0)
            {
                if (chao.Energy<91 && ReserveSpot(actor)) { approach=15; useStones=false; }
                else BeginReturn(GD.Randf()<.5f);
            }
            return true;
        }
        Vector3 direction=NapSpot-actor.Position;
        direction.Y=0;
        if (approach<=0)
        {
            cooldown=Mathf.Max(0,cooldown-dt);
            if (cooldown>0 || chao.Energy>=99 || direction.Length()>90 || !ReserveSpot(actor)) return false;
            direction=NapSpot-actor.Position;
            approach=70;
            useStones=!motion.IsSwimmingAt(actor.Position);
            stone=-1;
        }
        approach-=dt;
        if (direction.Length()<.24f && actor.Position.Y>.24f)
        {
            if (direction.Length()>.035f)
            {
                actor.MoveAndCollide(direction.LimitLength(dt*.38f));
                ImportedChao.MovePose(art,phase+=dt*5,true,false,false,false,false,false);
                return true;
            }
            approach=0;
            exploredIsland=true;
            if (chao.Energy>=100) { ReleaseSpot(); return false; }
            var support=actor.GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(
                actor.Position+Vector3.Up*.4f,actor.Position-Vector3.Up*.5f,1));
            if (support.Count>0) actor.Position=new(actor.Position.X,((Vector3)support["position"]).Y,actor.Position.Z);
            settling.Begin(actor);
            return true;
        }
        if (stone>=0) return Cross(actor,art,dt);
        if (approach<=0 || motion.BlockedTime>2.7f)
        {
            approach=0; cooldown=55; ReleaseSpot();
            return false;
        }
        bool water=motion.IsSwimmingAt(actor.Position);
        var goal=useStones ? BankRoute(actor.Position) : NapSpot;
        if (useStones && goal==GardenLandscape.IslandBank && new Vector2(actor.Position.X-goal.X,actor.Position.Z-goal.Z).Length()<.29f)
        {
            stone=0; hopFrom=actor.Position; hopTime=0;
            motion.Reset();
            return Cross(actor,art,dt);
        }
        var route=motion.RouteOffUpperPlatform(actor.Position,goal)-actor.Position;
        route.Y=0;
        if (!water && !motion.Airborne) route=walker.Steer(actor,route,dt);
        bool moving=motion.Step(actor,route,chao,false,water,dt,true,out _,directIsland:!useStones);
        if (moving || water) phase+=dt*(water && chao.Swim<100 ? 5.2f : 9);
        ImportedChao.MovePose(art,phase,moving,water,chao.Swim>=100,false,motion.Airborne,motion.Flying);
        return true;
    }
    private bool Cross(CharacterBody3D actor,Node3D art,float dt)
    {
        var steps=GardenLandscape.IslandSteps;
        bool reverse=returning==ReturnMode.Stones;
        Vector3 target=reverse ? (stone<steps.Length ? steps[stone] : GardenLandscape.IslandBank) :
            (stone<steps.Length ? steps[stone] : GardenLandscape.IslandLanding);
        float duration=.59f;
        hopTime=Mathf.Min(duration,hopTime+dt);
        float t=hopTime/duration;
        var heading=target-hopFrom;
        actor.Rotation=new(0,Mathf.Atan2(heading.X,heading.Z),0);
        actor.Position=hopFrom.Lerp(target,t)+Vector3.Up*(Mathf.Sin(t*Mathf.Pi)*.62f);
        ImportedChao.MovePose(art,0,false,false,false,false,true,false);
        if (t>=1)
        {
            hopFrom=target; hopTime=0;
            if (reverse)
            {
                if (stone==steps.Length) { landingAge=0; landingAt=target; returnLanding=true; stone=-1; }
                else stone=stone==0 ? steps.Length : stone-1;
            }
            else if (++stone>steps.Length) { stone=-1; useStones=false; landingAge=0; landingAt=target; returnLanding=false; }
        }
        return true;
    }

    internal void BeginReturn(bool viaStones)
    {
        returning=viaStones ? ReturnMode.Stones : ReturnMode.WaterEntry;
        stone=-1;
        hopTime=0;
    }

    private bool Return(CharacterBody3D actor,Node3D art,PetState chao,ChaoMotion motion,float dt)
    {
        if (returning==ReturnMode.Stones)
        {
            if (stone>=0) return Cross(actor,art,dt);
            if (Flat(actor.Position-GardenLandscape.IslandLanding).Length()<.28f)
            {
                stone=GardenLandscape.IslandSteps.Length-1;
                hopFrom=actor.Position;
                motion.Reset();
                return Cross(actor,art,dt);
            }
            Walk(actor,art,chao,motion,GardenLandscape.IslandLanding,dt);
            return true;
        }
        bool wet=motion.IsSwimmingAt(actor.Position);
        if (returning==ReturnMode.WaterEntry && wet) returning=ReturnMode.Swimming;
        if (returning==ReturnMode.Swimming && !wet && !motion.LeavingPond &&
            Flat(actor.Position-GardenArt.Pond).Length()>GardenArt.PondRadius+.55f)
        {
            FinishReturn();
            return false;
        }
        Walk(actor,art,chao,motion,returning==ReturnMode.WaterEntry ? WaterEntry : GardenArt.SwimExit,dt);
        return true;
    }

    private void Walk(CharacterBody3D actor,Node3D art,PetState chao,ChaoMotion motion,Vector3 at,float dt)
    {
        var direction=Flat(at-actor.Position);
        bool wet=motion.IsSwimmingAt(actor.Position);
        bool moving=motion.Step(actor,direction,chao,false,wet,dt,true,out _,
            leavingIsland:returning==ReturnMode.Swimming);
        phase+=dt*(wet && chao.Swim<100 ? 5.2f : 9);
        ImportedChao.MovePose(art,phase,moving,motion.IsSwimmingAt(actor.Position),chao.Swim>=100,
            false,motion.Airborne,motion.Flying);
    }

    private void FinishReturn()
    {
        returning=ReturnMode.None;
        stone=-1;
        cooldown=62;
    }

    private bool ReserveSpot(CharacterBody3D actor)
    {
        if (slot>=0) return true;
        float nearest=float.MaxValue;
        for(int i=0;i<NapPlaces.Length;i++)
        {
            if (Spaces.ContainsKey(i)) continue;
            bool occupied=false;
            foreach(var node in actor.GetTree().GetNodesInGroup("GardenChao"))
                if (node is Node3D other && other!=actor && other.Visible && Flat(other.GlobalPosition-NapPlaces[i]).Length()<.56f &&
                    Mathf.Abs(other.GlobalPosition.Y-NapPlaces[i].Y)<.35f) { occupied=true; break; }
            float distance=actor.Position.DistanceSquaredTo(NapPlaces[i]);
            if (occupied || distance>=nearest) continue;
            nearest=distance; slot=i;
        }
        if (slot<0) return false;
        Spaces[slot]=this; return true;
    }
    private void ReleaseSpot()
    {
        if (slot>=0 && Spaces.TryGetValue(slot,out var owner) && owner==this) Spaces.Remove(slot);
        slot=-1;
    }

    private static Vector3 Flat(Vector3 vector) => new(vector.X,0,vector.Z);
    private static bool OnIsland(Vector3 position) =>
        position.Y>.20f && Flat(position-IslandCenter).LengthSquared()<2.25f;

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
