using Godot;

namespace Mori;

// These are behavioral destinations only; no signs or markers are spawned.
internal sealed class ChaoLeisure
{
    internal static readonly Vector3[][] Routes={
        new[]{new Vector3(12,0,12),new Vector3(29,0,12),new Vector3(46,0,12),new Vector3(63,0,12),new Vector3(80,0,12)},
        new[]{new Vector3(-15,0,14),new Vector3(-33,0,14),new Vector3(-51,0,14),new Vector3(-69,0,14),new Vector3(-85,0,14)},
        new[]{new Vector3(25,0,27),new Vector3(25,0,35),new Vector3(25,0,43),new Vector3(25,0,51),new Vector3(25,0,59)},
        new[]{new Vector3(-49,0,27),new Vector3(-61,0,32),new Vector3(-73,0,37),new Vector3(-85,0,42)}
    };
    internal static readonly Vector3[] SkySpots={new(0,0,12),new(11,0,10),new(-2,0,4),new(14,0,-1)};
    private float delay=4+GD.Randf()*5, pause, elapsed, stuck, gait, previousDistance=float.MaxValue;
    private int route=-1, waypoint;
    private bool sky;
    private Vector3 goal;
    private readonly ChaoWander steer=new();
    public bool Active => route>=0 || sky;
    internal bool WatchingSky => sky && pause>0;
    public void Cancel(Node3D art)
    {
        if (Active) { ImportedChao.MovePose(art,0,false,false,false,false,false,false); ImportedChao.CloseEyes(art,false); }
        route=-1; sky=false; pause=elapsed=stuck=0; delay=7;
    }
    internal void BeginSky(Vector3 target) { sky=true; route=-1; goal=target; pause=elapsed=stuck=0; }
    internal void BeginRoute(int index) { route=index%Routes.Length; sky=false; waypoint=0; goal=Routes[route][0]; pause=elapsed=stuck=0; }

    public bool Step(CharacterBody3D actor,Node3D art,PetState state,ChaoMotion motion,float dt,bool canStart)
    {
        if (!canStart || motion.IsSwimmingAt(actor.Position))
        {
            if (Active) Cancel(art);
            return false;
        }
        if (!Active)
        {
            delay-=dt;
            if (delay>0 || actor.Position.Y>.15f || motion.IsSwimmingAt(actor.Position)) return false;
            if (GD.Randf()<.32f) BeginSky(SkySpots[(int)(GD.Randi()%(uint)SkySpots.Length)]);
            else BeginRoute((int)(GD.Randi()%(uint)Routes.Length));
            previousDistance=float.MaxValue;
        }
        elapsed+=dt;
        if (elapsed>300) { Cancel(art); return false; }
        if (pause>0)
        {
            pause-=dt;
            if (sky)
            {
                float lying=24-pause;
                ImportedChao.CloudPose(art,lying,Mathf.Min(lying/1.3f,pause/1.3f));
            }
            else ImportedChao.MovePose(art,0,false,false,false,false,false,false);
            if (pause<=0)
            {
                if (sky || ++waypoint>=Routes[route].Length) { Cancel(art); return true; }
                goal=Routes[route][waypoint]; previousDistance=float.MaxValue;
            }
            return true;
        }
        var delta=goal-actor.Position; delta.Y=0;
        float distance=delta.Length();
        if (distance<.24f)
        {
            pause=sky ? 24 : 1.3f+GD.Randf()*1.4f;
            return true;
        }
        if (distance>previousDistance-.003f) stuck+=dt; else stuck=0;
        previousDistance=distance;
        if (stuck>2.5f) { Cancel(art); delay=1.5f; return false; }
        var heading=steer.Steer(actor,delta,dt);
        heading=ChaoMotion.AvoidNeighbors(actor,heading);
        bool moved=motion.Step(actor,heading,state,false,false,dt,false,out _);
        if (heading.LengthSquared()>.01f)
            actor.Rotation=new(0,Mathf.LerpAngle(actor.Rotation.Y,Mathf.Atan2(heading.X,heading.Z),1-Mathf.Exp(-8*dt)),0);
        gait+=dt*Mathf.Clamp(motion.GroundSpeed*9,4,19);
        ImportedChao.MovePose(art,gait,moved,false,false,false,motion.Airborne,motion.Flying);
        return true;
    }
}
