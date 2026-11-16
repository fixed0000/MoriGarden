using Godot;

namespace Mori;

public partial class ImportedChao
{
    private bool racingWet;
    private float racingWaterTransition;
    internal static void RacingPose(ImportedChao art,RaceEntrant racer,RaceSurface surface,float time,float dt)
    {
        var previousRotation=art.Quaternion;var previousPosition=art.Position;
        bool moving=racer.Action==RaceAction.Move;
        bool water=surface==RaceSurface.Swim,air=surface==RaceSurface.Fly;
        if(water!=art.racingWet){art.racingWet=water;art.racingWaterTransition=.35f;}
        art.GetNode<EmotionOrb>("MoodOrb").SetIdea(racer.Reaction=="cheer" ? "!" : null,racer.Reaction=="cheer");
        MovePose(art,racer.Distance*13,moving,water,racer.Pet.Swim>=100,racer.BoostRemaining>0 || racer.Speed>3.5f,air,air);
        if(surface==RaceSurface.Power){ClimbPose(art,time);art.alignSoles=false;}
        if(racer.Action==RaceAction.Ready)
        {
            StandForAction(art);art.Rotation=new(.12f,0,0);Express(art,ChaoExpression.Focused);
            foreach(string side in new[]{"L","R"})if(art.FindChild("Paddle"+side,true,false) is Node3D paw)paw.Rotation=new(.4f,0,side=="L" ? -.18f : .18f);
        }
        if(racer.Action is RaceAction.Fall or RaceAction.Prone)
        {
            art.alignSoles=false;float blend=Mathf.SmoothStep(0,.35f,racer.TripAge);art.Rotation=new(Mathf.Pi/2*blend,0,0);art.Position=Vector3.Up*Mathf.Lerp(.008f,.12f,blend);Pose(art,ChaoPoseKind.Distress);Express(art,ChaoExpression.Surprised);
        }
        else if(racer.Action==RaceAction.Rise)StandUpPose(art,Quaternion.FromEuler(new(Mathf.Pi/2,0,0)),Vector3.Up*.12f,(racer.TripAge-1.2f)/.6f);
        else if(racer.Fatigue<=0 && moving){art.Rotation=new(.24f,0,.025f*Mathf.Sin(time));Express(art,ChaoExpression.Sleepy);}
        else if(racer.Reaction=="cheer"){Express(art,ChaoExpression.Delighted);art.GetNode<EmotionOrb>("MoodOrb").SetIdea("!",true);}
        if(racer.Action==RaceAction.Finish){Pose(art,ChaoPoseKind.Idle);Express(art,racer.Place==1 ? ChaoExpression.Delighted : ChaoExpression.Sad);}
        if(art.racingWaterTransition>0){art.racingWaterTransition=Mathf.Max(0,art.racingWaterTransition-dt);float blend=1-Mathf.Exp(-14*dt);art.Quaternion=previousRotation.Slerp(art.Quaternion,blend);art.Position=previousPosition.Lerp(art.Position,blend);}
    }
}
