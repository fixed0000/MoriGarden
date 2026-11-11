using System.Collections.Generic;
using Godot;

namespace Mori;

internal sealed class AvatarWalk
{
    private readonly Skeleton3D skeleton;
    private readonly Dictionary<string,int> joints = new();
    private float phase, amplitude, jumpArms, runBlend, stepSpeed;
    private readonly int leftHand, rightHand;
    internal float AirPose=>jumpArms;

    private AvatarWalk(Skeleton3D skeleton)
    {
        this.skeleton=skeleton;
        leftHand=skeleton.FindBone("hand.L"); rightHand=skeleton.FindBone("hand.R");
        foreach (string name in new[]{"spine.001","toe.L","toe.R"})
        {
            int index=skeleton.FindBone(name);
            if (index>=0) joints[name]=index;
        }
        foreach(string side in new[] {"_L","_R"})
            foreach(string name in new[] {"UpperArm","LowerArm"})
            {
                int i=skeleton.FindBone(name+side);
                if(i>=0) joints[name+side]=i;
            }
        foreach (string side in new[] { "_L","_R" })
            foreach (string joint in new[] { "UpperLeg","LowerLeg","Foot" })
            {
                string name=joint+side;
                int index=skeleton.FindBone(name);
                if (index>=0) joints[name]=index;
            }
    }

    public static AvatarWalk? FromScene(Node root)
    {
        if (root is Skeleton3D skeleton)
        {
            var walk=new AvatarWalk(skeleton);
            return walk.joints.ContainsKey("UpperLeg_L") && walk.joints.ContainsKey("Foot_R") ? walk : null;
        }
        foreach (var child in root.GetChildren())
            if (FromScene(child) is AvatarWalk walk) return walk;
        return null;
    }

    public void Step(float dt,float speed,bool grounded,bool carrying,float verticalSpeed)
    {
        stepSpeed=Mathf.Lerp(stepSpeed,speed,1-Mathf.Exp(-11*dt));
        amplitude=Mathf.MoveToward(amplitude,grounded ? Mathf.Clamp(stepSpeed/2.4f,0,1) : 0,dt*5);
        jumpArms=Mathf.MoveToward(jumpArms,grounded ? 0 : 1,dt*7);
        runBlend=Mathf.MoveToward(runBlend,Mathf.SmoothStep(3.6f,5.4f,stepSpeed),dt*4);
        // One gait cycle advances by its actual two-foot stride distance.
        float cycleStride=Mathf.Lerp(.38f,.60f,runBlend)*amplitude*(carrying ? .76f : 1);
        if (grounded && speed>.025f && cycleStride>.005f) phase+=Mathf.Tau*(speed*dt)/(2*cycleStride/.58f);
        float swing=Mathf.Sin(phase)*amplitude*(carrying ? .70f : 1);
        for (int side=-1;side<=1;side+=2)
        {
            string suffix=side<0 ? "_L" : "_R";
            if (grounded)
            {
                float cycle=Mathf.PosMod(phase/Mathf.Tau+(side>0 ? .5f : 0),1);
                float strideLength=Mathf.Lerp(.38f,.60f,runBlend)*amplitude*(carrying ? .76f : 1);
                float swingTime=Mathf.Clamp((cycle-.58f)/.42f,0,1);
                float lift=Mathf.Sin(swingTime*Mathf.Pi);
                float travel=cycle<.58f ? 1-2*cycle/.58f : Mathf.Lerp(-1,1,Mathf.SmoothStep(0,1,swingTime));
                float roll=cycle<.58f ? .16f*Mathf.Pow(cycle/.58f,3) :
                    .16f*(1-swingTime)+Mathf.Sin(swingTime*Mathf.Tau)*Mathf.Lerp(.18f,.33f,runBlend);
                StepLeg(suffix,side<0 ? "toe.L" : "toe.R",
                    new Vector3(0,lift*Mathf.Lerp(.10f,.39f,runBlend)*amplitude,travel*strideLength),
                    roll*amplitude);
                continue;
            }
            float stride=swing*side;
            float hip = -.32f-(side>0 ? .18f : 0)+stride*.08f;
            float flex = verticalSpeed>0 ? .69f : .43f;
            AimRotation("UpperLeg"+suffix,new Quaternion(Vector3.Right,hip));
            AimRotation("LowerLeg"+suffix,new Quaternion(Vector3.Right,hip+flex));
            AimRotation("Foot"+suffix,new Quaternion(Vector3.Right,.10f));
        }
    }

    public void Arms(bool holding,float reach,float stroke,float shake=0)
    {
        for(int side=-1;side<=1;side+=2)
        {
            string suffix=side<0 ? "_L" : "_R";
            string upper="UpperArm"+suffix, lower="LowerArm"+suffix;
            if (!joints.TryGetValue(upper,out var joint)) continue;
            float outward=Mathf.Sign(skeleton.GetBoneGlobalRest(joint).Origin.X);
            if (outward==0) outward=-side;
            float action = reach*(side>0 ? 1 : .25f);
            float swing = Mathf.Sin(phase)*amplitude*Mathf.Lerp(.22f,.57f,runBlend)*side;
            float elbowSwing=amplitude*Mathf.Lerp(.23f,.85f,runBlend)+swing*.53f;
            Vector3 shoulder = new(outward*(holding ? .17f : .29f)+shake*.29f,-1,holding ? .71f : swing+action*(.85f+stroke*.19f));
            Vector3 elbow = new((holding ? -outward*.34f : outward*.16f)+shake*.39f,
                holding ? -.29f : Mathf.Lerp(-.96f,-.55f,runBlend),holding ? 1 : swing+elbowSwing+action*(1.05f+stroke*.23f));
            if (!holding)
            {
                shoulder=shoulder.Lerp(new(outward*.72f,.62f,.52f),jumpArms);
                elbow=elbow.Lerp(new(outward*.48f,.84f,.61f),jumpArms);
            }
            Aim(upper,shoulder);
            Aim(lower,elbow);
        }
    }

    public void UpperBody(float pitch,float roll) =>
        AimRotation("spine.001",Quaternion.FromEuler(new Vector3(pitch,0,roll)));

    private void StepLeg(string side,string toe,Vector3 offset,float roll)
    {
        string thigh="UpperLeg"+side, shin="LowerLeg"+side, foot="Foot"+side;
        if (!joints.TryGetValue(thigh,out int hip) || !joints.TryGetValue(shin,out int knee) ||
            !joints.TryGetValue(foot,out int ankle)) return;
        var hipAt=skeleton.GetBoneGlobalPose(hip).Origin;
        var hipRest=skeleton.GetBoneGlobalRest(hip).Origin;
        var kneeRest=skeleton.GetBoneGlobalRest(knee).Origin;
        var ankleRest=skeleton.GetBoneGlobalRest(ankle).Origin;
        var upperRest=kneeRest-hipRest;
        var lowerRest=ankleRest-kneeRest;
        float upper=upperRest.Length(), lower=lowerRest.Length();
        Vector3 target=ankleRest+offset-hipAt;
        if (upper<.005f || lower<.005f || target.LengthSquared()<.0001f) return;
        Vector3 along=target.Normalized();
        float length=Mathf.Clamp(target.Length(),Mathf.Abs(upper-lower)+.002f,upper+lower-.002f);
        float reach=(upper*upper+length*length-lower*lower)/(2*length);
        Vector3 kneeForward=Vector3.Back.Slide(along).Normalized();
        Vector3 upperAim=along*reach+kneeForward*Mathf.Sqrt(Mathf.Max(0,upper*upper-reach*reach));
        var lowerAim=along*length-upperAim;
        AimRotation(thigh,new Quaternion(upperRest.Normalized(),upperAim.Normalized()));
        AimRotation(shin,new Quaternion(lowerRest.Normalized(),lowerAim.Normalized()));
        AimRotation(foot,new Quaternion(Vector3.Right,roll));
        AimRotation(toe,new Quaternion(Vector3.Right,roll*.35f));
    }

    public void Crouch(float amount)
    {
        foreach (string side in new[]{"_L","_R"})
        {
            AimRotation("UpperLeg"+side,new Quaternion(Vector3.Right,-.83f*amount));
            AimRotation("LowerLeg"+side,new Quaternion(Vector3.Right,.65f*amount));
            AimRotation("Foot"+side,Quaternion.Identity);
        }
    }

    public void ReachWithHands(Vector3 contact,Vector3 across,float blend,float spacing)
    {
        for (int side=-1;side<=1;side+=2)
        {
            string suffix=side<0 ? "_L" : "_R";
            string upper="UpperArm"+suffix, lower="LowerArm"+suffix;
            int hand=side<0 ? leftHand : rightHand;
            if (hand<0 || !joints.TryGetValue(upper,out int shoulder) ||
                !joints.TryGetValue(lower,out int elbow)) continue;
            Vector3 start=skeleton.GetBoneGlobalPose(shoulder).Origin;
            Vector3 currentElbow=skeleton.GetBoneGlobalPose(elbow).Origin;
            Vector3 currentHand=skeleton.GetBoneGlobalPose(hand).Origin;
            float width=Mathf.Sign(skeleton.GetBoneGlobalRest(shoulder).Origin.X);
            Vector3 desired=skeleton.ToLocal(contact+across*width*spacing)-start;
            float a=start.DistanceTo(currentElbow), b=currentElbow.DistanceTo(currentHand);
            if (a<.001f || b<.001f || desired.LengthSquared()<.00001f) continue;
            float distance=Mathf.Clamp(desired.Length(),Mathf.Abs(a-b)+.001f,a+b-.001f);
            Vector3 along=desired.Normalized();
            Vector3 bend=Vector3.Down.Slide(along).Normalized();
            float joint=(a*a+distance*distance-b*b)/(2*distance);
            Vector3 upperAim=along*joint+bend*Mathf.Sqrt(Mathf.Max(0,a*a-joint*joint));
            Vector3 lowerAim=along*distance-upperAim;
            Aim(upper,(currentElbow-start).Lerp(upperAim,blend));
            Aim(lower,(currentHand-currentElbow).Lerp(lowerAim,blend));
        }
    }

    public Vector3? Grip()
    {
        if (leftHand<0 || rightHand<0) return null;
        Vector3 at=(skeleton.GetBoneGlobalPose(leftHand).Origin+skeleton.GetBoneGlobalPose(rightHand).Origin)*.5f;
        return skeleton.ToGlobal(at);
    }

    private void Aim(string name,Vector3 direction)
    {
        if (!joints.TryGetValue(name,out var joint)) return;
        var rest=skeleton.GetBoneGlobalRest(joint).Basis.Orthonormalized();
        AimRotation(name,new Quaternion(rest.Y.Normalized(),direction.Normalized()));
    }

    private void AimRotation(string name,Quaternion turn)
    {
        if (!joints.TryGetValue(name,out var joint)) return;
        var rest=skeleton.GetBoneGlobalRest(joint).Basis.Orthonormalized();
        var desired=new Basis(turn)*rest;
        int parent=skeleton.GetBoneParent(joint);
        if(parent>=0) desired=skeleton.GetBoneGlobalPose(parent).Basis.Orthonormalized().Inverse()*desired;
        skeleton.SetBonePoseRotation(joint,desired.GetRotationQuaternion());
    }
}
