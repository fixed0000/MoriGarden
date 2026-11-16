using Godot;

namespace Mori;

public partial class ImportedChao
{
    private bool racingWet;
    private float racingWaterTransition;
    internal static void RacingPose(ImportedChao art,RaceEntrant racer,RaceSurface surface,float time,float dt)
    {
        art.sportMotionDriven=false;
        if(racer.Action==RaceAction.Move && racer.Traversal.Phase==RaceObstaclePhase.Run && !racer.Traversal.OwnsMovement && racer.Fatigue>0)
        {
            var clip=SportMotions.Find(racer.Pet.Run>=300 ? "race_run_fast" : "race_run_normal")!;
            ApplySportMotion(art,clip,racer.Traversal.Travelled/(racer.Pet.Run>=300 ? 3.2f : 1.8f));
            art.GetNode<EmotionOrb>("MoodOrb").SetIdea(racer.Reaction=="cheer" ? "!" : null,racer.Reaction=="cheer");return;
        }
        var previousRotation=art.Quaternion;var previousPosition=art.Position;
        var traversal=racer.Traversal;var stage=traversal.Phase;
        bool moving=racer.Action==RaceAction.Move && !traversal.OwnsMovement;
        bool water=surface==RaceSurface.Swim,air=surface==RaceSurface.Fly;
        if(water!=art.racingWet){art.racingWet=water;art.racingWaterTransition=.22f;}
        art.GetNode<EmotionOrb>("MoodOrb").SetIdea(racer.Reaction=="cheer" ? "!" : null,racer.Reaction=="cheer");
        float gait=traversal.Travelled*13;
        MovePose(art,gait,moving,water,racer.Pet.Swim>=100,racer.BoostRemaining>0 || racer.Speed>3.5f,air,air);
        if(stage==RaceObstaclePhase.Climb)RaceClimb(art,traversal.Travelled*14);
        if(stage==RaceObstaclePhase.FailedFlight){art.alignSoles=false;art.Rotation=new(.45f,0,Mathf.Sin(time*7)*.06f);Express(art,ChaoExpression.Surprised);}
        if(stage==RaceObstaclePhase.Prepare || racer.Action==RaceAction.Ready)
        {
            StandForAction(art);art.Rotation=new(.18f,0,0);art.Position=Vector3.Down*.018f;Express(art,ChaoExpression.Focused);
            foreach(string side in new[]{"L","R"})if(art.FindChild("Paddle"+side,true,false) is Node3D paw)paw.Rotation=new(.4f,0,side=="L" ? -.18f : .18f);
        }
        if(stage==RaceObstaclePhase.Takeoff){art.alignSoles=false;art.Rotation=new(-.18f+.5f*traversal.Progress,0,0);foreach(string side in new[]{"L","R"})if(art.FindChild("Foot"+side,true,false) is Node3D foot)foot.Rotation=new(-.8f*Mathf.Sin(traversal.Progress*Mathf.Pi),0,0);}
        if(stage==RaceObstaclePhase.Mount)
        {
            RaceClimb(art,gait);float pull=Mathf.Sin(traversal.Progress*Mathf.Pi);art.Rotation=new(pull*.65f,0,0);art.Position+=Vector3.Up*pull*.025f;
        }
        if(stage==RaceObstaclePhase.Land && !water){StandForAction(art);float settle=Mathf.Sin(traversal.Progress*Mathf.Pi);art.Rotation=new(settle*.3f,0,0);art.Position=Vector3.Down*settle*.025f;}
        if(racer.Action is RaceAction.Fall or RaceAction.Prone)
        {
            art.alignSoles=false;float blend=Mathf.SmoothStep(0,.35f,racer.TripAge);art.Rotation=new(Mathf.Pi/2*blend,0,0);art.Position=Vector3.Up*Mathf.Lerp(.008f,.12f,blend);Pose(art,ChaoPoseKind.Distress);Express(art,ChaoExpression.Surprised);
        }
        else if(racer.Action==RaceAction.Rise)StandUpPose(art,Quaternion.FromEuler(new(Mathf.Pi/2,0,0)),Vector3.Up*.12f,(racer.TripAge-1.2f)/.6f);
        else if(racer.Fatigue<=0 && moving && !water && !air && stage==RaceObstaclePhase.Run){art.Rotation=new(.24f,0,.025f*Mathf.Sin(time));Express(art,ChaoExpression.Sleepy);}
        else if(racer.Reaction=="cheer")Express(art,ChaoExpression.Delighted);
        if(racer.Action==RaceAction.Finish){Pose(art,ChaoPoseKind.Idle);Express(art,racer.Place==1 ? ChaoExpression.Delighted : ChaoExpression.Sad);}
        if(art.racingWaterTransition>0){art.racingWaterTransition=Mathf.Max(0,art.racingWaterTransition-dt);float blend=1-Mathf.Exp(-18*dt);art.Quaternion=previousRotation.Slerp(art.Quaternion,blend);art.Position=previousPosition.Lerp(art.Position,blend);}
    }
    private static void RaceClimb(ImportedChao art,float phase)
    {
        ClimbPose(art,phase/11);art.alignSoles=false;art.Rotation=Vector3.Zero;
        var actor=(Node3D)art.GetParent();var at=actor.GlobalPosition;var forward=actor.GlobalBasis.Z;var right=actor.GlobalBasis.X;
        float stroke=Mathf.Sin(phase);
        Vector3 Grip(float side,float height)
        {
            var from=at+right*side+Vector3.Up*height;
            using var query=PhysicsRayQueryParameters3D.Create(from-forward*.05f,from+forward*.45f,1);
            var hit=actor.GetWorld3D().DirectSpaceState.IntersectRay(query);
            return hit.Count>0 && Mathf.Abs(((Vector3)hit["normal"]).Y)<.7f ? (Vector3)hit["position"]-forward*.012f : from+forward*.17f;
        }
        InstrumentHands(art,Grip(-.09f,.25f+stroke*.065f),Grip(.09f,.25f-stroke*.065f));
        foreach(var (side,sign) in new[]{("L",-1),("R",1)})if(art.FindChild("Foot"+side,true,false) is Node3D foot)
        {foot.Rotation=new(-.55f,0,0);foot.GlobalPosition=Grip(sign*.065f,.045f-sign*stroke*.035f);}
    }
}
