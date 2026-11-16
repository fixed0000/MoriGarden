using System;
using System.Linq;
using System.Numerics;

namespace Mori;

public readonly record struct SportJoint(Vector3 Angles,Vector3 Offset)
{
    public static SportJoint Blend(SportJoint a,SportJoint b,float t)=>new(Vector3.Lerp(a.Angles,b.Angles,t),Vector3.Lerp(a.Offset,b.Offset,t));
}
public readonly record struct SportPose(Vector3 Torso,Vector3 Root,Vector3 Head,SportJoint LeftArm,SportJoint RightArm,SportJoint LeftFoot,SportJoint RightFoot,bool Grounded=true)
{
    public static SportPose Blend(SportPose a,SportPose b,float t)=>new(Vector3.Lerp(a.Torso,b.Torso,t),Vector3.Lerp(a.Root,b.Root,t),Vector3.Lerp(a.Head,b.Head,t),SportJoint.Blend(a.LeftArm,b.LeftArm,t),SportJoint.Blend(a.RightArm,b.RightArm,t),SportJoint.Blend(a.LeftFoot,b.LeftFoot,t),SportJoint.Blend(a.RightFoot,b.RightFoot,t),t<.5f ? a.Grounded : b.Grounded);
    public bool Finite=>new[]{Torso,Root,Head,LeftArm.Angles,LeftArm.Offset,RightArm.Angles,RightArm.Offset,LeftFoot.Angles,LeftFoot.Offset,RightFoot.Angles,RightFoot.Offset}.All(v=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z));
}
public readonly record struct SportKey(float Phase,SportPose Pose);
public sealed record SportMotion(string Id,float Seconds,bool Loop,double SourceStart,double SourceEnd,string Source,SportKey[] Keys,string Status="Approximate",string Notes="Depth and joint angles estimated from monocular footage")
{
    public SportPose At(float seconds)
    {
        if(!float.IsFinite(seconds))seconds=0;
        float t=Loop ? (Math.Max(0,seconds)%Seconds)/Seconds : Math.Clamp(seconds/Seconds,0,1);
        int i=0;while(i+1<Keys.Length && Keys[i+1].Phase<t)i++;
        if(i+1==Keys.Length)return Keys[^1].Pose;
        var a=Keys[i];var b=Keys[i+1];float blend=Math.Clamp((t-a.Phase)/Math.Max(.00001f,b.Phase-a.Phase),0,1);
        return SportPose.Blend(a.Pose,b.Pose,blend);
    }
}

public static class SportMotions
{
    private const string Primary="https://www.youtube.com/watch?v=mNN-Mwmah0k";
    private static SportJoint Joint(float pitch,float y=0,float z=0,float roll=0)=>new(new(pitch,0,roll),new(0,y,z));
    // Authored silhouettes from the privately retained frame sheets. Coordinates
    // are torso widths, angles degrees. No source animation data is imported.
    private static SportPose Run(float left,float right,float liftLeft,float liftRight,float footLeft,float footRight,float bob,float pitch,bool fast)=>
        new(new(pitch,0,0),new(0,bob,0),new(-pitch*.45f,0,0),Joint(-left,roll:-9),Joint(-right,roll:9),Joint(footLeft,liftLeft,left*(fast ? .011f : .013f)),Joint(footRight,liftRight,right*(fast ? .011f : .013f)),bob<.035f);
    private static SportKey[] RunKeys(bool fast)
    {
        float amplitude=fast ? 43 : 30;
        // Contact -> compression -> passing/flight -> opposite contact. The
        // passing feet stay above the floor rather than sweeping through it.
        return new[]{
            new SportKey(0,Run(amplitude,-amplitude,0,.17f,-18,26,0,fast ? 9 : 5,fast)),
            new SportKey(.125f,Run(amplitude*.4f,-amplitude*.4f,.05f,.24f,-5,35,.055f,fast ? 11 : 7,fast)),
            new SportKey(.25f,Run(0,0,.20f,.20f,27,-5,.11f,fast ? 9 : 5,fast)),
            new SportKey(.375f,Run(-amplitude*.65f,amplitude*.65f,.23f,.055f,35,-12,.06f,fast ? 7 : 4,fast)),
            new SportKey(.5f,Run(-amplitude,amplitude,.17f,0,26,-18,0,fast ? 9 : 5,fast)),
            new SportKey(.625f,Run(-amplitude*.4f,amplitude*.4f,.24f,.05f,35,-5,.055f,fast ? 11 : 7,fast)),
            new SportKey(.75f,Run(0,0,.20f,.20f,-5,27,.11f,fast ? 9 : 5,fast)),
            new SportKey(.875f,Run(amplitude*.65f,-amplitude*.65f,.055f,.23f,-12,35,.06f,fast ? 7 : 4,fast)),
            new SportKey(1,Run(amplitude,-amplitude,0,.17f,-18,26,0,fast ? 9 : 5,fast))};
    }
    private static SportPose Stance(float turn,float bob)=>new(new(3,turn,0),new(0,bob,0),new(-2,-turn*.25f,0),Joint(-25,roll:-12),Joint(-20,roll:12),Joint(-4,z:.08f),Joint(4,z:-.08f));
    private static SportPose Strike(float turn,float arm,float step,float dip)=>new(new(arm< -80 ? 18 : 5,turn,0),new(0,dip,0),new(-3,-turn*.3f,0),Joint(-25,roll:-12),Joint(arm,roll:12),Joint(-4,z:step),Joint(7,z:-.10f));
    public static readonly SportMotion[] All={
        new("race_run_normal",.70f,true,778.50,779.20,Primary,RunKeys(false),Notes:"Blue NPC cycle; hands alternate below the head. Back-side depth partly occluded; timing estimate ±0.08 s."),
        new("race_run_fast",.48f,true,779.80,780.28,Primary,RunKeys(true),Notes:"Selected Dark Chao, alternate feet and arms; animal parts excluded from retarget. Timing estimate ±0.05 s."),
        new("karate_idle",1.0f,true,968.50,969.50,Primary,new[]{new SportKey(0,Stance(-4,0)),new SportKey(.25f,Stance(0,-.018f)),new SportKey(.5f,Stance(4,0)),new SportKey(.75f,Stance(0,-.018f)),new SportKey(1,Stance(-4,0))},Notes:"Low paws, staggered feet; body/head depth estimated. Native facial design and animal parts are not copied."),
        new("karate_approach",.80f,true,971.0,971.80,Primary,new[]{new SportKey(0,Stance(-4,0)),new SportKey(.2f,Stance(-4,.04f) with{LeftFoot=Joint(-22,.16f,.22f),RightFoot=Joint(12,0,-.12f)}),new SportKey(.45f,Stance(2,.015f) with{LeftFoot=Joint(0,0,.14f),RightFoot=Joint(-16,.09f,-.02f)}),new SportKey(.7f,Stance(4,.03f) with{LeftFoot=Joint(8,0,-.06f),RightFoot=Joint(-18,.13f,.16f)}),new SportKey(1,Stance(-4,0))},Notes:"Short two-foot advance; displacement remains owned by combat simulation."),
        new("karate_punch",1.20f,false,972.55,973.75,Primary,new[]{new SportKey(0,Stance(0,0)),new SportKey(.18f,Strike(-14,-5,-.02f,-.02f)),new SportKey(.33f,Strike(-25,15,-.04f,-.045f)),new SportKey(KarateContact.ImpactSwing,Strike(18,-94,.16f,-.015f)),new SportKey(.57f,Strike(14,-82,.14f,0)),new SportKey(.72f,Strike(6,-42,.10f,0)),new SportKey(.9f,Stance(0,0)),new SportKey(1,Stance(0,0))},Notes:"Observed forward paw strike; contact is phase-aligned to the existing rule. Hidden elbow/depth unresolved; not claimed as exact 3D capture.")
    };
    public static SportMotion? Find(string id)=>All.FirstOrDefault(c=>c.Id==id);
}
