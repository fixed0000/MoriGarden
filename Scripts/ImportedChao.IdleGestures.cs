using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mori;

public partial class ImportedChao
{
    private ChaoIdleGestureController? idleGestures;
    private IdleGestureOffsets idleOffsets;
    private bool idleGesturesPaused;
    internal float IdleGestureWeight { get; set; }=1; // Native zero-layer comparison; never saved/editor-editable.
    private readonly Dictionary<Node3D,(Transform3D Base,Transform3D Applied,Vector3 Scale)> idleTransforms=new();
    internal IdleGestureDiagnostic? IdleGestureDiagnostics=>idleGestures?.Diagnostic;
    internal static void PauseIdleGestures(Node3D art,bool value){if(art is ImportedChao model)model.idleGesturesPaused=value;}
    internal void RestoreIdleGestureOffsets()
    {
        foreach(var (node,pose) in idleTransforms)
            // A real executor may already have authored this limb. Never overwrite it.
            if(GodotObject.IsInstanceValid(node) && node.Transform.IsEqualApprox(pose.Applied)){node.Transform=pose.Base;node.Scale=pose.Scale;}
        idleTransforms.Clear();
    }
    private void RememberIdleTransform(Node3D node,Transform3D baseline,Vector3 scale)
    {idleTransforms[node]=(baseline,node.Transform,scale);}
    private void ApplyIdleRotation(Node3D node,Vector3 rotation)
    {
        if(rotation.LengthSquared()<1e-14f)return;
        var baseline=node.Transform;var scale=node.Scale;node.Transform=new(baseline.Basis*new Basis(Quaternion.FromEuler(rotation)),baseline.Origin);RememberIdleTransform(node,baseline,scale);
    }
    private void UpdateIdleGestures()
    {
        if(idleGestures==null){idleOffsets=default;return;}
        bool protectedActivity=personality?.GardenActivity?.SelectedActivity is ChaoActivity.Eat or ChaoActivity.Play or ChaoActivity.Sleep or ChaoActivity.Socialize;
        bool special=personality?.Mood.Sleeping==true || protectedActivity || expressionDecision?.Source is ChaoExpressionSource.Action or ChaoExpressionSource.Forced ||
            Expression==ChaoExpression.Delighted && presentationClock-emotionSince<1.2f;
        bool physical=afterAction>0 || discardTime>0 || sportMotionDriven || lastVelocity.LengthSquared()>.0025f || Mathf.Abs(Rotation.X)>.12f || Mathf.Abs(Rotation.Z)>.12f;
        idleOffsets=idleGestures.Update(presentationClock,new(PoseKind,heldExternally,special,physical,
            presentationClock<attentionUntil && attention!=null,idleGesturesPaused),personality?.Personality,personality?.Mood.Snapshot);
    }
    // Called inside the existing UpdateHead after its authoritative base interpolation.
    private void ApplyIdleHead(Node3D head)
    {
        if(heldExternally || IdleGestureWeight<=0 || PoseKind!=ChaoPoseKind.Idle || presentationClock<attentionUntil && attention!=null)return;
        float yaw=idleOffsets.HeadYaw*IdleGestureWeight,pitch=idleOffsets.HeadPitch*IdleGestureWeight;
        if(Mathf.Abs(yaw)+Mathf.Abs(pitch)<1e-8f)return;
        var baseline=head.Transform;var scale=head.Scale;var angle=head.Rotation;
        head.Rotation=new(Mathf.Clamp(angle.X+pitch,-.4f,.35f),Mathf.Clamp(angle.Y+yaw,-.55f,.55f),angle.Z);RememberIdleTransform(head,baseline,scale);
    }
    // Last visual write of the physics frame, after the existing sole alignment.
    private void ApplyIdleGestureVisuals()
    {
        if(heldExternally || IdleGestureWeight<=0 || PoseKind!=ChaoPoseKind.Idle)return;
        float weight=Mathf.Clamp(IdleGestureWeight,0,1);
        if(FindChild("PaddleL",true,false) is Node3D left)ApplyIdleRotation(left,new(idleOffsets.LeftPaw*weight,0,idleOffsets.Solitude*weight));
        if(FindChild("PaddleR",true,false) is Node3D right)ApplyIdleRotation(right,new(idleOffsets.RightPaw*weight,0,-idleOffsets.Solitude*weight));
        if(softBody!=null && Mathf.Abs(idleOffsets.Sway)>1e-8f)
        {
            var anchored=feet.Keys.Where(GodotObject.IsInstanceValid).ToDictionary(f=>f,f=>f.GlobalTransform);
            ApplyIdleRotation(softBody,new(0,0,idleOffsets.Sway*weight));
            foreach(var (foot,world) in anchored){var baseline=foot.Transform;var scale=foot.Scale;foot.GlobalTransform=world;RememberIdleTransform(foot,baseline,scale);}
        }
    }
}
