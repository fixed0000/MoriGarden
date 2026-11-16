using Godot;

namespace Mori;

public partial class ImportedChao
{
    internal static void KaratePose(Node3D art,KarateFighter fighter,Vector3 target,bool finished,bool won,float endAge=0)
    {
        MovePose(art,fighter.ActionTime*12,fighter.Action==KarateAction.Approach,false,false,false,false,false);
        if(art is ImportedChao model)model.alignSoles=false;
        StandForAction(art);Pose(art,ChaoPoseKind.Grip);Attend(art,target+Vector3.Up*.25f,90);
        if(finished && endAge>.12f)
        {
            if(won){Pose(art,ChaoPoseKind.Idle);Express(art,ChaoExpression.Delighted,1);art.Position+=Vector3.Up*(Mathf.Abs(Mathf.Sin(endAge*4))*.045f);art.GetNodeOrNull<EmotionOrb>("MoodOrb")?.SetIdea("!",true);}
            else {LieDown(art,Mathf.Min(1,endAge/.45f));CloseEyes(art,false);Pose(art,ChaoPoseKind.Rest);Express(art,ChaoExpression.Sad,1);}
            return;
        }
        var actor=(Node3D)art.GetParent();var forward=actor.GlobalBasis.Z;
        Vector3 left=actor.Position+forward*.13f-actor.GlobalBasis.X*.085f+Vector3.Up*.22f,right=actor.Position+forward*.13f+actor.GlobalBasis.X*.085f+Vector3.Up*.22f;
        if(fighter.Action==KarateAction.Knockdown){LieDown(art,Mathf.Min(fighter.ActionTime/.35f,1));CloseEyes(art,false);Express(art,ChaoExpression.Sad);return;}
        if(fighter.Action==KarateAction.Rise){StandUpPose(art,Quaternion.FromEuler(new(-Mathf.Pi/2,0,0)),Vector3.Up*.1f,fighter.ActionTime/1.3f);return;}
        if(fighter.Action==KarateAction.Attack)
        {
            float reach=Mathf.SmoothStep(.20f,.42f,fighter.ActionTime)*(1-Mathf.SmoothStep(.52f,.82f,fighter.ActionTime));
            var towards=target-actor.Position; towards.Y=0;
            var contact=target-towards.Normalized()*.10f+Vector3.Up*(fighter.AttackKind==2 ? .13f : .2f);
            if(fighter.AttackKind==0)left=left.Lerp(contact,reach);else if(fighter.AttackKind==1)right=right.Lerp(contact,reach);
            if(fighter.AttackKind==2 && art.FindChild("FootR",true,false) is Node3D foot){foot.Rotation=new(-reach*1.2f,0,0);foot.GlobalPosition=foot.GlobalPosition.Lerp(contact,reach);}
            art.Rotation=new(.10f*reach,0,(fighter.AttackKind==0 ? 1 : -1)*.08f*reach);Express(art,ChaoExpression.Focused);
        }
        else if(fighter.Action==KarateAction.Guard){left.Y+=.04f;right.Y+=.04f;left+=forward*.015f;right+=forward*.015f;Express(art,ChaoExpression.Focused);}
        else if(fighter.Action==KarateAction.Dodge){art.Rotation=new(0,0,Mathf.Sin(fighter.ActionTime/.65f*Mathf.Pi)*.23f);Express(art,ChaoExpression.Curious);}
        else if(fighter.Action==KarateAction.Hit){art.Rotation=new(-.2f*Mathf.Sin(fighter.ActionTime/.33f*Mathf.Pi),0,0);Express(art,ChaoExpression.Surprised);}
        else if(fighter.Action==KarateAction.Recover){left.Y-=.05f;right.Y-=.05f;Express(art,ChaoExpression.Sleepy);}
        InstrumentHands(art,left,right);
    }
}
