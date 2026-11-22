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
    private float happyWeight,sleepWeight,squintWeight,lidWeight;
    private ChaoExpression requestedExpression;
    private float expressionUntil,expressionIntensity=1;
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
            if(id!=null){int seed=17;foreach(char letter in id)seed=unchecked(seed*31+letter);model.microRandom=new(seed);}
            model.individualPhase=(float)model.microRandom.NextDouble()*Mathf.Tau;model.individualPace=.96f+(float)model.microRandom.NextDouble()*.08f;model.blinkAt=ChaoExpressiveness.BlinkDelay(model.microRandom,state);
        }
        model.personality=state;model.heldExternally=held;
    }
    internal static void Pose(Node3D art,ChaoPoseKind kind){if(art is ImportedChao model){model.PoseKind=kind;model.poseSerial++;}}
    internal static void Attend(Node3D art,Vector3 point,int priority=50,float seconds=.25f)
    {
        if(art is not ImportedChao model || !point.IsFinite())return;
        if(model.presentationClock<model.attentionUntil && priority<model.attentionPriority)return;
        model.attention=point;model.attentionPriority=priority;model.attentionUntil=model.presentationClock+seconds;
    }
    internal static void Express(Node3D art,ChaoExpression expression,float seconds=.3f,float intensity=1)
    {
        if(art is not ImportedChao model)return;
        model.requestedExpression=expression;model.expressionUntil=model.presentationClock+seconds;model.expressionIntensity=Mathf.Clamp(intensity,.3f,1);
    }
    private static float Ease(float value,float target,float speed,float dt)=>Mathf.Lerp(value,target,1-Mathf.Exp(-speed*dt));
    private void UpdatePresentation(float dt,Node3D actor)
    {
        presentationClock+=dt;
        presentationPoseWritten=poseSerial!=lastPresentationPose;
        var request=presentationClock<expressionUntil ? requestedExpression : PoseKind==ChaoPoseKind.Thought ? ChaoExpression.Curious : PoseKind==ChaoPoseKind.Interaction || PoseKind==ChaoPoseKind.Grip ? ChaoExpression.Focused : ChaoExpression.Neutral;
        if(presentationClock>=expressionUntil && request==ChaoExpression.Neutral && PoseKind==ChaoPoseKind.Idle && personality!=null)
        {
            var mood=personality.Mood.Snapshot;request=ChaoExpressiveness.FromMood(mood);
            if(request!=ChaoExpression.Neutral)expressionIntensity=mood.EmotionIntensity;
        }
        Expression=ChaoExpressiveness.Resolve(eyesClosed && PoseKind is ChaoPoseKind.Sleep or ChaoPoseKind.Rest,
            eyesSquint && PoseKind==ChaoPoseKind.Water,eyesHalf || eyesSquint && PoseKind==ChaoPoseKind.Distress,eyesHappy,personality?.Energy ?? 90,request);
        if(Expression!=previousExpression){emotionSince=presentationClock;previousExpression=Expression;}
        if(Expression is ChaoExpression.Annoyed or ChaoExpression.Afraid)expressionIntensity=1;
        float speed=personality==null ? 7 : ChaoExpressiveness.Response(personality);
        happyWeight=Ease(happyWeight,Expression is ChaoExpression.Happy or ChaoExpression.Delighted ? 1 : 0,speed,dt);
        sleepWeight=Ease(sleepWeight,eyesClosed ? 1 : 0,10,dt);
        squintWeight=Ease(squintWeight,eyesSquint ? 1 : 0,10,dt);
        float lidsTarget=Expression is ChaoExpression.Annoyed or ChaoExpression.Sleepy ? .5f : Expression==ChaoExpression.Sad ? .25f : Expression==ChaoExpression.Focused ? .22f : 0;
        lidWeight=Ease(lidWeight,lidsTarget,7,dt);
        if(presentationClock>=blinkAt && blinkAge<0 && !eyesClosed && !eyesSquint && happyWeight<.6f)blinkAge=0;
        BlinkAmount=0;
        if(blinkAge>=0)
        {
            blinkAge+=dt;BlinkAmount=Mathf.Sin(Mathf.Clamp(blinkAge/.19f,0,1)*Mathf.Pi);
            if(blinkAge>=.19f){blinkAge=-1;blinkAt=presentationClock+ChaoExpressiveness.BlinkDelay(microRandom,personality);}
        }
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
            float amount=ChaoExpressiveness.Intensity(personality)*expressionIntensity;
            float joy=happyWeight*(Expression==ChaoExpression.Delighted ? 1.4f : .5f);
            float age=presentationClock-emotionSince;
            if(softBody!=null)
            {
                float tilt=Expression==ChaoExpression.Afraid ? .09f : Expression==ChaoExpression.Sad ? .11f : Expression==ChaoExpression.Curious ? .035f : Expression==ChaoExpression.Surprised ? -.08f*Mathf.Exp(-age*4) : 0;
                softBody.Rotation+=new Vector3(tilt*amount,0,0);
                if(Expression==ChaoExpression.Delighted && age<1.2f){softBody.Position+=Vector3.Up*Mathf.Abs(Mathf.Sin(age*Mathf.Tau/1.2f))*.025f*amount;alignSoles=false;}
            }
            foreach(var (name,side) in new[]{("PaddleL",-1),("PaddleR",1)})
                if(FindChild(name,true,false) is Node3D paw)paw.Rotation+=new Vector3(-joy*.16f-(Expression==ChaoExpression.Surprised ? .2f*Mathf.Exp(-age*3) : 0),0,side*(joy*.14f+(Expression==ChaoExpression.Afraid ? -.07f : 0)));
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
            float tilt=Expression==ChaoExpression.Curious ? -.035f-.055f*((personality?.Curiosity ?? 0)+100)/200f : 0;
            desired=Quaternion.FromEuler(new(Mathf.Clamp(-Mathf.Atan2(local.Y,new Vector2(local.X,local.Z).Length())+(Expression==ChaoExpression.Sad ? .1f : 0),-.4f,.35f),Mathf.Clamp(Mathf.Atan2(local.X,local.Z),-.55f,.55f),tilt));
        }
        else if(PoseKind==ChaoPoseKind.Idle && !heldExternally)
        {
            if(presentationClock>nextIdleLook)
            {
                nextIdleLook=presentationClock+2.5f+(float)microRandom.NextDouble()*4;
                idleYaw=((float)microRandom.NextDouble()-.5f)*.42f;idlePitch=-.06f+(float)microRandom.NextDouble()*.09f;
            }
            desired=Quaternion.FromEuler(new(idlePitch,idleYaw,0));
        }
        // Preserve the deliberately inverted swimming/crawling neck and the lying/rising choreography.
        bool compatible=free || PoseKind==ChaoPoseKind.Thought;
        if(!headInitialized || !compatible || previousPose is ChaoPoseKind.Sleep or ChaoPoseKind.Rest or ChaoPoseKind.Rise or ChaoPoseKind.Water or ChaoPoseKind.Crawl)
            previousHead=desired;
        else previousHead=previousHead.Normalized().Slerp(desired.Normalized(),1-Mathf.Exp(-(personality==null ? 7 : ChaoExpressiveness.Response(personality))*dt)).Normalized();
        head.Quaternion=previousHead;headInitialized=true;
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
        float closed=Mathf.Max(sleepWeight,Mathf.SmoothStep(.75f,1,BlinkAmount));
        float covered=Mathf.Max(BlinkAmount,lidWeight),line=Mathf.Max(happyWeight,Mathf.Max(closed,squintWeight));
        foreach(var (mesh,lid) in lids)
        {
            float curiosity=Expression==ChaoExpression.Curious && ToLocal(mesh.GlobalPosition).X<0 ? .12f : 0;
            float opening=1-Mathf.Max(covered,curiosity);
            mesh.MaterialOverride=opening<.999f || line>.001f ? lid.Half : lid.Original;
            lid.Half.SetShaderParameter("openness",opening);lid.Half.SetShaderParameter("opacity",1-line);
            if(PoseKind!=ChaoPoseKind.Distress)lid.Half.SetShaderParameter("slant",Expression==ChaoExpression.Sad ? (ToLocal(mesh.GlobalPosition).X<0 ? -.18f : .18f) : 0f);
        }
        foreach(var (eye,shapes) in eyeShapes)
        {
            eye.Visible=line<.995f;
            if(!eyeScales.TryGetValue(eye,out var rest))eyeScales[eye]=rest=eye.Scale;
            eye.Scale=rest*new Vector3(1,1+(Expression is ChaoExpression.Surprised or ChaoExpression.Afraid ? .07f : 0),1);
            Line(shapes.Happy,happyWeight*(1-Mathf.Max(closed,squintWeight)));Line(shapes.Sleep,closed);Line(shapes.Squint,squintWeight*(1-closed));
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
