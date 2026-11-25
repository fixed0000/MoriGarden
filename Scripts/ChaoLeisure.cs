using Godot;
using System.Linq;

namespace Mori;

// These are behavioral destinations only; no signs or markers are spawned.
internal sealed class ChaoLeisure
{
    internal static readonly Vector3[][] Routes=LeisureDestinationCatalog.All.Where(d=>d.Type==LeisureDestinationType.Route).Select(d=>d.Points.Select(p=>new Vector3(p.X,p.Y,p.Z)).ToArray()).ToArray();
    internal static readonly Vector3[] SkySpots=LeisureDestinationCatalog.All.Where(d=>d.Type==LeisureDestinationType.Sky).Select(d=>new Vector3(d.End.X,d.End.Y,d.End.Z)).ToArray();
    private System.Random destinationRandom;
    private readonly bool injectedRandom;
    private string? actorIdentity,currentId,recentRoute,recentSky;
    private string area="",startArea="",outcome="None";
    private PetState? owner;
    private System.Collections.Generic.IReadOnlyList<LeisurePlaceScore> scores=System.Array.Empty<LeisurePlaceScore>();
    internal ChaoLeisure(System.Random? random=null){destinationRandom=random ?? new System.Random();injectedRandom=random!=null;}
    internal string? CurrentDestinationId=>currentId;
    internal void BindContext(PetState state,string areaId,string stableId)
    {
        if(area!=areaId)scores=System.Array.Empty<LeisurePlaceScore>();
        owner=state;area=areaId;
        if(actorIdentity==null){actorIdentity=stableId;if(!injectedRandom){int seed=1951;foreach(char c in stableId)seed=unchecked(seed*31+c);destinationRandom=new System.Random(seed);}}
        Publish();
    }
    private void Publish()
    {if(owner!=null)owner.LeisureSelection=new(area,currentId,recentRoute,recentSky,outcome,scores);}
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
    {delay=0;LastFailureReason="";return InspectTargets(actor)>0;}
    internal int InspectTargets(CharacterBody3D actor)
    {
        allowedRoutes=ReachableRoutes(actor);allowedSpots=SkySpots.Where(p=>Supported(actor,p)).ToArray();
        if(owner!=null){scores=ChaoLeisurePreferenceScorer.Candidates(owner.History,area,LeisureDestinationCatalog.All.Where(d=>d.Type==LeisureDestinationType.Route ? allowedRoutes.Contains(d.Index) : allowedSpots.Contains(new Vector3(d.End.X,d.End.Y,d.End.Z))).Select(d=>d.Id),recentRoute,recentSky);Publish();}
        return allowedRoutes.Length+allowedSpots.Length;
    }
    private static bool Supported(CharacterBody3D actor,Vector3 at)
    {
        var hit=actor.GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(at+Vector3.Up*.6f,at-Vector3.Up*.3f,1));
        return hit.Count>0 && ((Vector3)hit["normal"]).Y>.7f && Mathf.Abs(((Vector3)hit["position"]).Y-at.Y)<.13f;
    }
    private static int[] ReachableRoutes(CharacterBody3D actor)=>Enumerable.Range(0,Routes.Length).Where(i=>Routes[i].All(p=>Supported(actor,p))).ToArray();
    internal static int AvailableTargets(CharacterBody3D actor)=>ReachableRoutes(actor).Length+SkySpots.Count(p=>Supported(actor,p));
    public void Cancel(Node3D art,string reason="Interrupted")
    {
        bool active=Active;Cleanup(art);
        if(active)outcome=LastFailureReason.Length>0 ? "Failed: "+LastFailureReason : reason;
        Publish();
    }
    private void Complete(CharacterBody3D actor,Node3D art,PetState state)
    {
        if(!Active)return;
        if(!Supported(actor,goal)){LastFailureReason="Leisure destination unavailable";Cancel(art);return;}
        bool valid=currentId!=null && startArea==area && area==LeisureDestinationCatalog.Area;
        if(valid)
        {
            ChaoLeisurePreferenceScorer.RecordSuccess(state.History,area,currentId!);
            if(sky)recentSky=currentId;else recentRoute=currentId;
        }
        Cleanup(art);outcome=valid ? "Succeeded" : "Interrupted: no garden context";Publish();
    }
    private void Cleanup(Node3D art)
    {
        if (Active) { ImportedChao.MovePose(art,0,false,false,false,false,false,false); ImportedChao.CloseEyes(art,false); }
        rising.Cancel(); route=-1; sky=false; currentId=null;pause=elapsed=stuck=0; delay=7;
    }
    internal void BeginSky(Vector3 target) { sky=true; route=-1; goal=target; pause=elapsed=stuck=0;
        currentId=LeisureDestinationCatalog.All.FirstOrDefault(d=>d.Type==LeisureDestinationType.Sky && new Vector3(d.End.X,d.End.Y,d.End.Z)==target)?.Id;startArea=area;LastFailureReason="";Publish(); }
    internal void BeginRoute(int index) { route=index%Routes.Length; sky=false; waypoint=0; goal=Routes[route][0]; pause=elapsed=stuck=0;
        currentId=LeisureDestinationCatalog.All.First(d=>d.Type==LeisureDestinationType.Route && d.Index==route).Id;startArea=area;LastFailureReason="";Publish(); }

    public bool Step(CharacterBody3D actor,Node3D art,PetState state,ChaoMotion motion,float dt,bool canStart)
    {
        if ((!canStart || motion.IsSwimmingAt(actor.Position) || motion.Airborne || motion.Recovering || owner!=null && area!=LeisureDestinationCatalog.Area) && Active)
        { Cancel(art);return false; }
        if (rising.Active)
        {
            rising.Step(art,dt);
            if (!rising.Active) { Complete(actor,art,state); ImportedChao.PauseAfterAction(art); }
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
            var routes=allowedRoutes ?? ReachableRoutes(actor);
            var spots=allowedSpots ?? SkySpots.Where(p=>Supported(actor,p)).ToArray();
            var available=LeisureDestinationCatalog.All.Where(d=>d.Type==LeisureDestinationType.Route ? routes.Contains(d.Index) : spots.Contains(new Vector3(d.End.X,d.End.Y,d.End.Z))).Select(d=>d.Id);
            scores=ChaoLeisurePreferenceScorer.Candidates(state.History,area,available,recentRoute,recentSky);
            var choice=ChaoLeisurePreferenceScorer.Choose(scores,destinationRandom);Publish();
            if(choice==null)return false;
            var destination=LeisureDestinationCatalog.Find(choice.Id)!;
            if(destination.Type==LeisureDestinationType.Sky)BeginSky(SkySpots[destination.Index]);else BeginRoute(destination.Index);
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
                if (++waypoint>=Routes[route].Length) { Complete(actor,art,state); ImportedChao.PauseAfterAction(art); return true; }
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
