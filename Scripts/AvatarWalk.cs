using System.Collections.Generic;
using Godot;

namespace Mori;

internal sealed class AvatarWalk
{
    private readonly Skeleton3D skeleton;
    private readonly Dictionary<string,int> joints = new();
    private float phase, amplitude, jumpArms;
    private readonly int leftHand, rightHand;

    private AvatarWalk(Skeleton3D skeleton)
    {
        this.skeleton=skeleton;
        leftHand=skeleton.FindBone("hand.L"); rightHand=skeleton.FindBone("hand.R");
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
        amplitude=Mathf.MoveToward(amplitude,grounded ? Mathf.Clamp(speed/2.4f,0,1) : 0,dt*6);
        jumpArms=Mathf.MoveToward(jumpArms,grounded ? 0 : 1,dt*7);
        phase+=dt*(4+speed*2.15f);
        float swing=Mathf.Sin(phase)*amplitude*(carrying ? .70f : 1);
        for (int side=-1;side<=1;side+=2)
        {
            string suffix=side<0 ? "_L" : "_R";
            float stride=swing*side;
            float hip = grounded ? stride*.30f : -.32f-(side>0 ? .18f : 0);
            // Knees flex toward the back of the model in the common sagittal plane.
            float flex = grounded ? Mathf.Max(0,-stride)*.40f : verticalSpeed>0 ? .69f : .43f;
            AimRotation("UpperLeg"+suffix,new Quaternion(Vector3.Right,hip));
            AimRotation("LowerLeg"+suffix,new Quaternion(Vector3.Right,hip+flex));
            AimRotation("Foot"+suffix,new Quaternion(Vector3.Right,grounded ? -stride*.10f : .10f));
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
            float swing = Mathf.Sin(phase)*amplitude*.16f*side;
            Vector3 shoulder = new(outward*(holding ? .17f : .29f)+shake*.29f,-1,holding ? .71f : swing+action*(.85f+stroke*.19f));
            Vector3 elbow = new((holding ? -outward*.34f : outward*.16f)+shake*.39f,holding ? -.29f : -.96f,holding ? 1 : swing+action*(1.05f+stroke*.23f));
            if (!holding)
            {
                shoulder=shoulder.Lerp(new(outward*.72f,.62f,.52f),jumpArms);
                elbow=elbow.Lerp(new(outward*.48f,.84f,.61f),jumpArms);
            }
            Aim(upper,shoulder);
            Aim(lower,elbow);
        }
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

    public float GroundOffset()
    {
        if (!joints.TryGetValue("Foot_L",out int left) || !joints.TryGetValue("Foot_R",out int right)) return 0;
        float rest=Mathf.Min(skeleton.GetBoneGlobalRest(left).Origin.Y,skeleton.GetBoneGlobalRest(right).Origin.Y);
        float posed=Mathf.Min(skeleton.GetBoneGlobalPose(left).Origin.Y,skeleton.GetBoneGlobalPose(right).Origin.Y);
        return Mathf.Clamp((rest-posed)*skeleton.GlobalBasis.Scale.Y,-.095f,.055f);
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
