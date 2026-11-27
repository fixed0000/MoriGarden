using System.Collections.Generic;
using Godot;

namespace Mori;

public partial class ImportedChao
{
    private bool sportMotionDriven;
    private Vector3 sportBaseScale;
    private readonly Dictionary<string,(Node3D Node,Transform3D Home)> sportJoints=new();
    internal static void PrepareSportRig(ImportedChao art)
    {
        art.RestoreIdleGestureOffsets(); // Capture/use the authored rig, never a decorative Idle offset.
        if(art.sportJoints.Count>0)return;art.sportBaseScale=art.Scale;
        foreach(string name in new[]{"PaddleL","PaddleR","FootL","FootR","HeadPivot"})
            if(art.FindChild(name,true,false) is Node3D joint)art.sportJoints[name]=(joint,joint.Transform);
    }
    internal static SportPose ApplySportMotion(ImportedChao art,SportMotion clip,float seconds,bool mirror=false)
    {
        PrepareSportRig(art);art.sportMotionDriven=true;art.alignSoles=false;StandForAction(art);
        var pose=clip.At(seconds);float sign=mirror ? -1 : 1;
        art.Basis=Basis.FromEuler(new Vector3(pose.Torso.X,pose.Torso.Y*sign,pose.Torso.Z*sign)*Mathf.Pi/180).Scaled(art.sportBaseScale);
        art.Position=new(pose.Root.X*.16f*sign,pose.Root.Y*.16f+.008f,pose.Root.Z*.16f);
        if(art.softBody!=null){art.softBody.Rotation=Vector3.Zero;art.softBody.Scale=art.development;}
        void Joint(string name,SportJoint value,bool mirrored)
        {
            if(!art.sportJoints.TryGetValue(name,out var joint))return;
            var angles=value.Angles;var offset=value.Offset;
            if(mirrored){angles.Y=-angles.Y;angles.Z=-angles.Z;offset.X=-offset.X;}
            var basis=Basis.FromEuler(new Vector3(angles.X,angles.Y,angles.Z)*Mathf.Pi/180);
            var pivot=joint.Home.Origin+Vector3.Up*(name.StartsWith("Paddle") ? .062f : .040f);
            joint.Node.Transform=new(basis*joint.Home.Basis,pivot+basis*(joint.Home.Origin-pivot)+new Vector3(offset.X,offset.Y,offset.Z)*.16f);
        }
        Joint("PaddleL",mirror ? pose.RightArm : pose.LeftArm,mirror);Joint("PaddleR",mirror ? pose.LeftArm : pose.RightArm,mirror);
        Joint("FootL",mirror ? pose.RightFoot : pose.LeftFoot,mirror);Joint("FootR",mirror ? pose.LeftFoot : pose.RightFoot,mirror);
        if(art.sportJoints.TryGetValue("HeadPivot",out var head))head.Node.Transform=new(Basis.FromEuler(new Vector3(pose.Head.X,pose.Head.Y*sign,pose.Head.Z*sign)*Mathf.Pi/180)*head.Home.Basis,head.Home.Origin);
        // The source's created eye design/animal parts are not a universal face.
        // Keep the supplied model's neutral eyes; don't invent hidden expressions.
        art.eyesClosed=art.eyesHappy=art.eyesHalf=art.eyesSquint=false;art.sleepWeight=art.happyWeight=art.squintWeight=art.lidWeight=art.BlinkAmount=0;art.Expression=ChaoExpression.Neutral;art.RefreshEyes();
        foreach(var extension in art.instrumentArms.Values)extension.Visible=false;
        if(pose.Grounded)SupportStadiumFloor(art);
        return pose;
    }
}
