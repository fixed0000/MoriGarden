using Godot;

namespace Mori;

public partial class ImportedChao
{
    internal static void MeditationPose(ImportedChao art,float time,bool eyesClosed=true)
    {
        MovePose(art,0,false,false,false,false,false,false);art.alignSoles=false;StandForAction(art);Pose(art,ChaoPoseKind.Rest);CloseEyes(art,eyesClosed);
        art.Position=new(0,-.025f+Mathf.Sin(time*2)*.002f,0);art.Rotation=new(-.06f,0,0);
        var actor=(Node3D)art.GetParent();var at=actor.GlobalPosition;
        foreach(string side in new[]{"L","R"})if(art.FindChild("Foot"+side,true,false) is Node3D foot){foot.Rotation=new(-.9f,0,side=="L" ? -.12f : .12f);foot.Position+=new Vector3(0,.025f,.07f);}
        InstrumentHands(art,at+actor.GlobalBasis.Z*.12f-actor.GlobalBasis.X*.085f+Vector3.Up*.12f,at+actor.GlobalBasis.Z*.12f+actor.GlobalBasis.X*.085f+Vector3.Up*.12f);
    }
    internal static void KaratePose(Node3D art,KarateFighter fighter,Vector3 target,bool finished,bool won,float endAge=0)
    {
        MovePose(art,fighter.Travelled*13,fighter.Action==KarateAction.Approach,false,false,false,false,false);
        if(art is ImportedChao model)model.alignSoles=false;
        art.GetNodeOrNull<EmotionOrb>("MoodOrb")?.SetIdea(fighter.CheerReaction>0 ? "!" : null,fighter.CheerReaction>0);
        StandForAction(art);Pose(art,ChaoPoseKind.Grip);Attend(art,target+Vector3.Up*.25f,90);
        if(finished && endAge>.12f)
        {
            if(won){Pose(art,ChaoPoseKind.Idle);Express(art,ChaoExpression.Delighted,1);art.Position+=Vector3.Up*(Mathf.Abs(Mathf.Sin(endAge*4))*.07f);var winner=(Node3D)art.GetParent();var up=winner.GlobalPosition+Vector3.Up*.34f+winner.GlobalBasis.Z*.02f;InstrumentHands(art,up-winner.GlobalBasis.X*.17f,up+winner.GlobalBasis.X*.17f);art.GetNodeOrNull<EmotionOrb>("MoodOrb")?.SetIdea("!",true);}
            else {LieDown(art,Mathf.Min(1,(endAge-.12f)/.45f));CloseEyes(art,false);Pose(art,ChaoPoseKind.Rest);Express(art,ChaoExpression.Sad,1);}
            return;
        }
        var actor=(Node3D)art.GetParent();var at=actor.GlobalPosition;var forward=actor.GlobalBasis.Z;
        Vector3 left=at+forward*.13f-actor.GlobalBasis.X*.085f+Vector3.Up*.23f,right=at+forward*.13f+actor.GlobalBasis.X*.085f+Vector3.Up*.23f;
        bool contactFrame=fighter.StrikeAge<.12f;
        if(!contactFrame && fighter.Action==KarateAction.Meditate && art is ImportedChao sitting){MeditationPose(sitting,fighter.ActionTime);return;}
        if(!contactFrame && fighter.Action==KarateAction.Knockdown){LieDown(art,Mathf.Min(fighter.ActionTime/.35f,1));CloseEyes(art,false);Express(art,ChaoExpression.Sad);return;}
        if(!contactFrame && fighter.Action==KarateAction.Rise){StandUpPose(art,Quaternion.FromEuler(new(-Mathf.Pi/2,0,0)),Vector3.Up*.1f,fighter.ActionTime/1.2f);return;}
        if(fighter.Action==KarateAction.Attack || contactFrame)
        {
            float swing=contactFrame ? KarateContact.ImpactSwing : fighter.Swing;int kind=contactFrame ? fighter.StrikeKind : fighter.AttackKind;float reach=Mathf.SmoothStep(.16f,.46f,swing)*(1-Mathf.SmoothStep(.55f,.85f,swing));
            bool kick=kind is 2 or 4;
            var origin=contactFrame ? fighter.StrikeOrigin : fighter.Position;var facing=contactFrame ? fighter.StrikeFacing : fighter.Facing;
            var tip=KarateContact.Tip(origin,facing,kind);var stage=(Node3D)actor.GetParent();
            var contact=stage.ToGlobal(StadiumArt.Ring+new Vector3(tip.X,.03f+(kick ? .13f : .20f),tip.Y));
            float turn=kind==4 ? Mathf.Tau*Mathf.SmoothStep(0,.44f,swing) : kind==3 ? Mathf.Sin(swing*Mathf.Pi)*.35f : 0;
            art.Rotation=new(.08f*reach,turn,(kind==0 ? 1 : -1)*.06f*reach);
            if(kick && art.FindChild("FootR",true,false) is Node3D foot){foot.Rotation=new(-reach*1.2f,0,0);foot.GlobalPosition=foot.GlobalPosition.Lerp(contact,reach);}
            else if(kind==0)left=left.Lerp(contact,reach);else right=right.Lerp(contact,reach);
            Express(art,fighter.Struck && fighter.StrikeMissed ? ChaoExpression.Surprised : ChaoExpression.Focused);
        }
        else if(fighter.Action==KarateAction.Guard){left.Y+=.04f;right.Y+=.04f;Express(art,ChaoExpression.Focused);}
        else if(fighter.Action==KarateAction.Dodge){float lean=Mathf.Sin(fighter.ActionTime/.7f*Mathf.Pi);art.Rotation=new(0,0,lean*.32f);art.Position+=actor.GlobalBasis.X*lean*.035f;Express(art,ChaoExpression.Curious);}
        else if(fighter.Action==KarateAction.Hit){art.Rotation=new(-.24f*Mathf.Sin(fighter.ActionTime/.28f*Mathf.Pi),0,0);Express(art,ChaoExpression.Surprised);}
        InstrumentHands(art,left,right);
    }
}
