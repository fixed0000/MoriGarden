using Godot;
using System.Linq;

namespace Mori;

// These are behavioral destinations only; no signs or markers are spawned.
internal sealed class ChaoLeisure
{
    internal static readonly Vector3[][] Routes={
        new[]{new Vector3(12,0,12),new Vector3(29,0,12),new Vector3(46,0,12),new Vector3(64,0,12)},
        new[]{new Vector3(-15,0,14),new Vector3(-25,0,14),new Vector3(-35,0,14),new Vector3(-45,0,14)},
        new[]{new Vector3(36,0,5),new Vector3(36,0,17),new Vector3(36,0,29),new Vector3(36,0,41)},
        new[]{new Vector3(-10,0,25),new Vector3(-22,0,30),new Vector3(-34,0,35),new Vector3(-46,0,40)}
    };
    internal static readonly Vector3[] SkySpots={new(0,0,12),new(11,0,10),new(-2,0,4),new(14,0,-1)};
    private float delay=4+GD.Randf()*5, pause, elapsed, stuck, gait, previousDistance=float.MaxValue;
    private int route=-1, waypoint;
    private bool sky;
    private Vector3 goal;
    private int[]? allowedRoutes;
    private Vector3[]? allowedSpots;
    private readonly ChaoWander steer=new();
    private readonly ChaoRising rising=new();
    public bool Active => route>=0 || sky || rising.Active;
    internal bool CanPauseForSocial => !sky && !rising.Active;
    internal bool WatchingSky => sky && pause>0;
    internal ActivityInterruptibility Interruptibility=>rising.Active || sky && pause>0 ? ActivityInterruptibility.Atomic : pause>0 ? ActivityInterruptibility.ShortSafe : ActivityInterruptibility.LongOptional;
    internal string LastFailureReason { get; private set; }="";
    internal bool Reconsider(CharacterBody3D actor)
    {delay=0;LastFailureReason="";allowedRoutes=ReachableRoutes(actor);allowedSpots=SkySpots.Where(p=>Supported(actor,p)).ToArray();return allowedRoutes.Length+allowedSpots.Length>0;}
    private static bool Supported(CharacterBody3D actor,Vector3 at)
    {
        var hit=actor.GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(at+Vector3.Up*.6f,at-Vector3.Up*.3f,1));
        return hit.Count>0 && ((Vector3)hit["normal"]).Y>.7f && Mathf.Abs(((Vector3)hit["position"]).Y-at.Y)<.13f;
    }
    private static int[] ReachableRoutes(CharacterBody3D actor)=>Enumerable.Range(0,Routes.Length).Where(i=>Routes[i].All(p=>Supported(actor,p))).ToArray();
    internal static int AvailableTargets(CharacterBody3D actor)=>ReachableRoutes(actor).Length+SkySpots.Count(p=>Supported(actor,p));
    public void Cancel(Node3D art)
    {
        if (Active) { ImportedChao.MovePose(art,0,false,false,false,false,false,false); ImportedChao.CloseEyes(art,false); }
        rising.Cancel(); route=-1; sky=false; pause=elapsed=stuck=0; delay=7;
    }
    internal void BeginSky(Vector3 target) { sky=true; route=-1; goal=target; pause=elapsed=stuck=0; }
    internal void BeginRoute(int index) { route=index%Routes.Length; sky=false; waypoint=0; goal=Routes[route][0]; pause=elapsed=stuck=0; }

    public bool Step(CharacterBody3D actor,Node3D art,PetState state,ChaoMotion motion,float dt,bool canStart)
    {
        if (rising.Active)
        {
            rising.Step(art,dt);
            if (!rising.Active) { Cancel(art); ImportedChao.PauseAfterAction(art); }
            return true;
        }
        if (!canStart || motion.IsSwimmingAt(actor.Position))
        {
            if (Active) Cancel(art);
            return false;
        }
        if (!Active)
        {
            delay-=dt;
            if (delay>0 || actor.Position.Y>.15f || motion.IsSwimmingAt(actor.Position)) return false;
            var routes=allowedRoutes ?? Enumerable.Range(0,Routes.Length).ToArray();
            var spots=allowedSpots ?? SkySpots;
            if(routes.Length==0 && spots.Length==0)return false;
            if (spots.Length>0 && (routes.Length==0 || GD.Randf()<.32f))
            {
                Vector3 favorite=spots[0];float score=-1;
                foreach(var spot in spots)
                {
                    float weight=GD.Randf()*(1+state.History.PlaceBias(state.History.LastArea,new(spot.X,spot.Y,spot.Z)));
                    if(weight>score){favorite=spot;score=weight;}
                }
                BeginSky(favorite);
            }
            else BeginRoute(routes[(int)(GD.Randi()%(uint)routes.Length)]);
            previousDistance=float.MaxValue;
        }
        elapsed+=dt;
        if (elapsed>300) { LastFailureReason="Leisure route timed out";Cancel(art); return false; }
        if (pause>0)
        {
            pause-=dt;
            if (sky)
            {
                float lying=24-pause;
                ImportedChao.CloudPose(art,lying,Mathf.Min(lying/1.3f,1));
            }
            else ImportedChao.MovePose(art,0,false,false,false,false,false,false);
            if (pause<=0)
            {
                if (sky) { rising.Begin(art); return true; }
                if (++waypoint>=Routes[route].Length) { Cancel(art); ImportedChao.PauseAfterAction(art); return true; }
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
        if (stuck>2.5f) { LastFailureReason="Leisure route blocked";Cancel(art); delay=1.5f; return false; }
        var heading=steer.Steer(actor,delta,dt);
        heading=ChaoMotion.AvoidNeighbors(actor,heading);
        bool moved=motion.Step(actor,heading,state,false,false,dt,false,out _);
        gait+=dt*Mathf.Clamp(motion.GroundSpeed*9,4,19);
        ImportedChao.MovePose(art,gait,moved,false,false,false,motion.Airborne,motion.Flying);
        return true;
    }
}
