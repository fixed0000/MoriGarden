using System;
using System.Collections.Generic;
using Godot;

namespace Mori;

public partial class ImportedChao
{
    private PetState? personality;
    internal ChaoPoseKind PoseKind { get; private set; }
    internal ChaoExpression Expression { get; private set; }
    internal float BlinkAmount { get; private set; }
    internal float PresentationClock=>presentationClock;
    private Random microRandom=new(Guid.NewGuid().GetHashCode());
    private float presentationClock,blinkAt,blinkAge=-1,nextIdleLook,idleYaw,idlePitch;
    private float happyWeight,sleepWeight,squintWeight,lidWeight,sadWeight,curiousWeight,fearWeight,surpriseWeight,delightWeight;
    private ChaoExpression requestedExpression;
    private float expressionUntil,expressionIntensity=1,visualIntensity;
    private ChaoPoseKind expressionPose;
    private bool blinkSuppressed;
    private ChaoExpressionDecision? expressionDecision;
    internal float HappyWeight=>happyWeight;
    internal float BlinkDeadline=>blinkAt;
    internal PetState? PresentationState=>personality;
    internal ChaoExpressionDiagnostic ExpressionDiagnostics
    {
        get
        {
            var mood=personality?.Mood.Snapshot;var d=expressionDecision;
            return new(mood?.DominantEmotion,mood?.EmotionIntensity ?? 0,mood?.Valence ?? 0,mood?.Arousal ?? 0,
                Expression.ToString(),PoseKind.ToString(),d?.Source.ToString() ?? "Neutral",d?.PersonalityMultiplier ?? 1,
                d?.VisualIntensity ?? 0,visualIntensity,BlinkAmount,d?.ProtectedAnimation ?? !ChaoExpressionLayer.IsFree(PoseKind),d?.SuppressionReason ?? "Not yet presented",IdleGestureDiagnostics,VocalDiagnostics);
        }
    }
    private Vector3? attention;
    private int attentionPriority;
    private float attentionUntil;
    private Quaternion previousHead;
    private bool headInitialized,poseInitialized,heldExternally;
    private ChaoPoseKind previousPose;
    private readonly Dictionary<Node3D,Transform3D> blendedLimbs=new();
    private readonly Dictionary<Node3D,Vector3> eyeScales=new();
    private readonly Dictionary<Node3D,StandardMaterial3D> lineMaterials=new();
    private float individualPhase,individualPace=1;
    private long poseSerial,lastPresentationPose;
    private ChaoExpression previousExpression;
    private float emotionSince;
    private Vector3 softRest;
    private bool softRestKnown;
    private bool presentationPoseWritten;
    internal static void BindPresentation(Node3D art,PetState state,bool held=false,string? id=null)
    {
        if(art is not ImportedChao model)return;
        if(model.personality==null)
        {
            model.blinkAge=-1;model.BlinkAmount=0;
            if(id!=null){int seed=17;foreach(char letter in id)seed=unchecked(seed*31+letter);model.microRandom=new(seed);}
            model.individualPhase=(float)model.microRandom.NextDouble()*Mathf.Tau;model.individualPace=.96f+(float)model.microRandom.NextDouble()*.08f;model.blinkAt=ChaoExpressiveness.BlinkDelay(model.microRandom,state);
        }
        if(model.idleGestures==null && !string.IsNullOrWhiteSpace(id))model.idleGestures=new(id);
        if(!string.IsNullOrWhiteSpace(id))model.BindVocal(id);
        model.personality=state;model.heldExternally=held;
    }
    internal static void Pose(Node3D art,ChaoPoseKind kind){if(art is ImportedChao model){model.RestoreIdleGestureOffsets();if(kind!=ChaoPoseKind.Idle){model.idleGestures?.Cancel(model.presentationClock,"Protected "+kind);model.idleOffsets=default;}model.PoseKind=kind;model.poseSerial++;}}
    internal static void Attend(Node3D art,Vector3 point,int priority=50,float seconds=.25f)
    {
        if(art is not ImportedChao model || !point.IsFinite())return;
        if(model.presentationClock<model.attentionUntil && priority<model.attentionPriority)return;
        model.attention=point;model.attentionPriority=priority;model.attentionUntil=model.presentationClock+seconds;
    }
    internal static void Express(Node3D art,ChaoExpression expression,float seconds=.3f,float intensity=1)
    {
        if(art is not ImportedChao model)return;
        model.expressionPose=model.PoseKind;model.requestedExpression=expression;model.expressionUntil=model.presentationClock+seconds;model.expressionIntensity=Mathf.Clamp(intensity,0,1);
    }
    private static float Ease(float value,float target,float speed,float dt)=>Mathf.Lerp(value,target,1-Mathf.Exp(-speed*dt));
    private void UpdatePresentation(float dt,Node3D actor)
    {
        presentationClock+=dt;
        presentationPoseWritten=poseSerial!=lastPresentationPose;
        expressionDecision=ChaoExpressionLayer.Evaluate(PoseKind,eyesClosed,eyesSquint,eyesHalf,eyesHappy,heldExternally,
            personality?.Mood.Snapshot,personality?.Personality,presentationClock<expressionUntil ? requestedExpression : null,expressionIntensity,expressionPose);
        Expression=expressionDecision.Expression;
        if(Expression!=previousExpression){emotionSince=presentationClock;previousExpression=Expression;}
        float speed=personality==null ? 7 : ChaoExpressiveness.Response(personality);
        visualIntensity=Ease(visualIntensity,expressionDecision.VisualIntensity,speed,dt);
        happyWeight=Ease(happyWeight,Expression is ChaoExpression.Happy or ChaoExpression.Delighted ? expressionDecision.VisualIntensity : 0,speed,dt);
        sadWeight=Ease(sadWeight,Expression==ChaoExpression.Sad ? expressionDecision.VisualIntensity : 0,speed,dt);
        curiousWeight=Ease(curiousWeight,Expression==ChaoExpression.Curious ? expressionDecision.VisualIntensity : 0,speed,dt);
        fearWeight=Ease(fearWeight,Expression==ChaoExpression.Afraid ? expressionDecision.VisualIntensity : 0,speed,dt);
        surpriseWeight=Ease(surpriseWeight,Expression==ChaoExpression.Surprised ? expressionDecision.VisualIntensity : 0,speed,dt);
        delightWeight=Ease(delightWeight,Expression==ChaoExpression.Delighted ? expressionDecision.VisualIntensity : 0,speed,dt);
        sleepWeight=eyesClosed ? 1 : Ease(sleepWeight,0,10,dt);
        squintWeight=Ease(squintWeight,eyesSquint ? 1 : 0,10,dt);
        float lidsTarget=Expression is ChaoExpression.Annoyed or ChaoExpression.Sleepy ? .5f : Expression==ChaoExpression.Sad ? .25f : Expression==ChaoExpression.Focused ? .22f : 0;
        lidWeight=Ease(lidWeight,lidsTarget*expressionDecision.VisualIntensity,7,dt);
        bool suppress=eyesClosed || eyesSquint;
        if(suppress)blinkAge=-1;
        else if(blinkSuppressed)blinkAt=presentationClock+ChaoExpressiveness.BlinkDelay(microRandom,personality);
        blinkSuppressed=suppress;
        if(presentationClock>=blinkAt && blinkAge<0 && !suppress && happyWeight<.6f)blinkAge=0;
        BlinkAmount=0;
        if(blinkAge>=0)
        {
            blinkAge+=dt;BlinkAmount=Mathf.Clamp(Mathf.Sin(Mathf.Clamp(blinkAge/.19f,0,1)*Mathf.Pi),0,1);
            if(blinkAge>=.19f){blinkAge=-1;blinkAt=presentationClock+ChaoExpressiveness.BlinkDelay(microRandom,personality);}
        }
        UpdateIdleGestures();
        RefreshEyes();
        UpdateHead(dt,actor);
        BlendLimbs(dt);
        // Body gestures only decorate free upright poses; grips, swimming and lying remain authoritative.
        if(softBody!=null)
        {
            if(!softRestKnown){softRest=softBody.Position;softRestKnown=true;}
            softBody.Position=softRest;
        }
        if(!heldExternally && PoseKind==ChaoPoseKind.Idle && personality!=null && poseSerial!=lastPresentationPose)
        {
            float joy=happyWeight*.5f+delightWeight*.9f;
            float age=presentationClock-emotionSince;
            if(softBody!=null)
            {
                float tilt=.09f*fearWeight+.11f*sadWeight+.035f*curiousWeight-.08f*surpriseWeight*Mathf.Exp(-age*4);
                softBody.Rotation+=new Vector3(tilt,0,0);
                if(Expression==ChaoExpression.Delighted && age<1.2f){softBody.Position+=Vector3.Up*Mathf.Abs(Mathf.Sin(age*Mathf.Tau/1.2f))*.025f*delightWeight;alignSoles=false;}
            }
            foreach(var (name,side) in new[]{("PaddleL",-1),("PaddleR",1)})
                if(FindChild(name,true,false) is Node3D paw)paw.Rotation+=new Vector3(-joy*.16f-surpriseWeight*.2f*Mathf.Exp(-age*3),0,side*(joy*.14f+(-.07f*fearWeight)));
        }
        lastPresentationPose=poseSerial;
    }
    private void UpdateHead(float dt,Node3D actor)
    {
        if(FindChild("HeadPivot",true,false) is not Node3D head)return;
        var desired=head.Quaternion;
        bool free=PoseKind is ChaoPoseKind.Idle or ChaoPoseKind.Walk or ChaoPoseKind.Interaction or ChaoPoseKind.Grip;
        if(free && presentationClock<attentionUntil && attention is {} target)
        {
            var local=actor.ToLocal(target)-Vector3.Up*.27f;
            float tilt=ChaoExpressionLayer.IsFree(PoseKind) && !heldExternally ? -.06f*curiousWeight : 0;
            desired=Quaternion.FromEuler(new(Mathf.Clamp(-Mathf.Atan2(local.Y,new Vector2(local.X,local.Z).Length()),-.4f,.35f),Mathf.Clamp(Mathf.Atan2(local.X,local.Z),-.55f,.55f),tilt));
        }
        else if(PoseKind==ChaoPoseKind.Idle && !heldExternally)
        {
            if(presentationClock>nextIdleLook)
            {
                nextIdleLook=presentationClock+2.5f+(float)microRandom.NextDouble()*4;
                idleYaw=((float)microRandom.NextDouble()-.5f)*.42f;idlePitch=-.06f+(float)microRandom.NextDouble()*.09f;
            }
            float lively=1+.1f*happyWeight;
            desired=Quaternion.FromEuler(new(idlePitch+(.1f*sadWeight),idleYaw*lively,-.06f*curiousWeight));
        }
        else if(PoseKind==ChaoPoseKind.Walk && !heldExternally)
            desired=Quaternion.FromEuler(new(.1f*sadWeight,0,-.06f*curiousWeight));
        // Preserve the deliberately inverted swimming/crawling neck and the lying/rising choreography.
        bool compatible=free || PoseKind==ChaoPoseKind.Thought;
        if(!headInitialized || !compatible || previousPose is ChaoPoseKind.Sleep or ChaoPoseKind.Rest or ChaoPoseKind.Rise or ChaoPoseKind.Water or ChaoPoseKind.Crawl)
            previousHead=desired;
        else previousHead=previousHead.Normalized().Slerp(desired.Normalized(),1-Mathf.Exp(-(personality==null ? 7 : ChaoExpressiveness.Response(personality))*dt)).Normalized();
        head.Quaternion=previousHead;headInitialized=true;ApplyIdleHead(head);
    }
    private void BlendLimbs(float dt)
    {
        bool upright=PoseKind is ChaoPoseKind.Idle or ChaoPoseKind.Walk or ChaoPoseKind.Thought or ChaoPoseKind.Interaction;
        bool wasUpright=previousPose is ChaoPoseKind.Idle or ChaoPoseKind.Walk or ChaoPoseKind.Thought or ChaoPoseKind.Interaction;
        foreach(string name in new[]{"PaddleL","PaddleR","FootL","FootR"})
        {
            if(FindChild(name,true,false) is not Node3D limb)continue;
            var target=limb.Transform;
            if(poseInitialized && upright && wasUpright && !heldExternally && blendedLimbs.TryGetValue(limb,out var previous))
                limb.Transform=previous.InterpolateWith(target,1-Mathf.Exp(-(name.StartsWith("Foot") ? 26 : 18)*dt));
            blendedLimbs[limb]=limb.Transform;
        }
        previousPose=PoseKind;poseInitialized=true;
    }
    private void ApplyEyeSurfaces()
    {
        float closed=eyesClosed ? 1 : Mathf.Max(sleepWeight,Mathf.SmoothStep(.75f,1,BlinkAmount));
        bool protectedPose=!ChaoExpressionLayer.IsFree(PoseKind) || heldExternally;
        float smile=protectedPose && Expression is not (ChaoExpression.Happy or ChaoExpression.Delighted) ? 0 : happyWeight;
        float sad=protectedPose && Expression!=ChaoExpression.Sad ? 0 : sadWeight;
        float curious=protectedPose && Expression!=ChaoExpression.Curious ? 0 : curiousWeight;
        float squint=eyesSquint ? 1 : squintWeight;
        float covered=Mathf.Max(BlinkAmount,lidWeight),line=Mathf.Max(smile,Mathf.Max(closed,squint));
        foreach(var (mesh,lid) in lids)
        {
            float curiosity=ToLocal(mesh.GlobalPosition).X<0 ? .12f*curious : 0;
            float opening=1-Mathf.Max(covered,curiosity);
            mesh.MaterialOverride=opening<.999f || line>.001f ? lid.Half : lid.Original;
            lid.Half.SetShaderParameter("openness",opening);lid.Half.SetShaderParameter("opacity",1-line);
            if(PoseKind!=ChaoPoseKind.Distress)lid.Half.SetShaderParameter("slant",(ToLocal(mesh.GlobalPosition).X<0 ? -.18f : .18f)*sad);
        }
        foreach(var (eye,shapes) in eyeShapes)
        {
            eye.Visible=line<.995f;
            if(!eyeScales.TryGetValue(eye,out var rest))eyeScales[eye]=rest=eye.Scale;
            eye.Scale=rest*new Vector3(1,1+(.07f*Mathf.Max(fearWeight,surpriseWeight)),1);
            Line(shapes.Happy,smile*(1-Mathf.Max(closed,squint)));Line(shapes.Sleep,closed);Line(shapes.Squint,squint*(1-closed));
        }
    }
    private void Line(Node3D root,float amount)
    {
        root.Visible=amount>.005f;
        foreach(var node in root.FindChildren("*","MeshInstance3D",true,false))
            if(node is MeshInstance3D mesh)
            {
                if(!lineMaterials.TryGetValue(mesh,out var material))
                {
                    material=(StandardMaterial3D)(mesh.GetActiveMaterial(0)?.Duplicate() ?? new StandardMaterial3D());
                    material.Transparency=BaseMaterial3D.TransparencyEnum.Alpha;mesh.MaterialOverride=material;lineMaterials[mesh]=material;
                }
                var color=material.AlbedoColor;color.A=amount;material.AlbedoColor=color;
            }
    }
    private void ConformStanceFeet()
    {
        if(!presentationPoseWritten || PoseKind is not (ChaoPoseKind.Idle or ChaoPoseKind.Walk or ChaoPoseKind.Thought or ChaoPoseKind.Interaction))return;
        foreach(var (foot,rest) in feet)
        {
            if(foot.Position.Y-rest.Y>.025f)continue; // Leave the swing foot free to clear the ground.
            var at=foot.GlobalPosition;
            var hit=GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(at+Vector3.Up*.18f,at-Vector3.Up*.25f,1));
            if(hit.Count==0)continue;var normal=(Vector3)hit["normal"];
            if(normal.Y<.72f || normal.Y>.9999f)continue;
            var parent=(Node3D)foot.GetParent();var localNormal=(parent.GlobalBasis.Inverse()*normal).Normalized();
            var tilt=Quaternion.Identity.Slerp(new Quaternion(Vector3.Up,localNormal),.65f);
            foot.Quaternion=(tilt*foot.Quaternion).Normalized();
            float minimum=float.MaxValue;
            foreach(var (mesh,vertices) in soleVertices)
                if(mesh==foot || foot.IsAncestorOf(mesh))foreach(var vertex in vertices)minimum=Mathf.Min(minimum,(mesh.GlobalTransform*vertex).Y);
            if(minimum<float.MaxValue)foot.Position+=parent.GlobalBasis.Inverse()*(Vector3.Up*Mathf.Clamp(((Vector3)hit["position"]).Y+.003f-minimum,-.035f,.035f));
        }
    }
}
