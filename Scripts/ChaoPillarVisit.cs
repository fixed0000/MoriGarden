using System.Linq;
using Godot;

namespace Mori;

internal sealed class ChaoPillarVisit
{
    private enum Stage { Idle, Approach, Climb, Glide }
    private Stage stage;
    private Vector3 top, face;
    private float delay=28+GD.Randf()*24, elapsed, gait;
    private Vector3 climbStart;
    public bool Active => stage!=Stage.Idle;

    public void Cancel() { stage=Stage.Idle; delay=28; }

    public bool Step(CharacterBody3D actor,Node3D art,PetState pet,ChaoMotion motion,float dt,bool available)
    {
        if (stage==Stage.Idle)
        {
            delay=Mathf.Max(0,delay-dt);
            if (!available || delay>0 || pet.Power<150 || motion.Airborne) return false;
            top=GardenLandscape.SouthPillars.OrderBy(p=>actor.Position.DistanceSquaredTo(p)).First();
            if (actor.Position.DistanceTo(top)>38) return false;
            face=actor.Position-top; face.Y=0;
            face=face.LengthSquared()<.1f ? Vector3.Forward : face.Normalized();
            stage=Stage.Approach;
            elapsed=0;
        }
        elapsed+=dt;
        switch (stage)
        {
            case Stage.Approach:
                Vector3 basePosition=new(top.X,0,top.Z);
                Vector3 offset=basePosition+face*.94f-actor.Position;
                offset.Y=0;
                if (offset.Length()<.27f && !motion.Airborne && !motion.IsSwimmingAt(actor.Position))
                {
                    climbStart=basePosition+face*.86f;
                    stage=Stage.Climb;
                    elapsed=0;
                    return true;
                }
                if (elapsed>45 || motion.BlockedTime>2)
                {
                    Cancel();
                    return false;
                }
                Move(actor,art,pet,motion,offset,dt);
                return true;
            case Stage.Climb:
                float duration=top.Y/Mathf.Lerp(.72f,1.45f,Mathf.Clamp(pet.Power/990f,0,1));
                if (elapsed<duration)
                    actor.Position=climbStart+Vector3.Up*(top.Y*elapsed/duration);
                else
                    actor.Position=(top+face*.86f).Lerp(top,Mathf.SmoothStep(0,1,Mathf.Clamp((elapsed-duration)/.45f,0,1)));
                actor.Rotation=new(0,Mathf.Atan2(-face.X,-face.Z),0);
                ImportedChao.ClimbPose(art,elapsed);
                if (elapsed>=duration+1.1f)
                {
                    motion.GlideFromPerch(actor,face,pet);
                    stage=Stage.Glide;
                }
                return true;
            case Stage.Glide:
                Move(actor,art,pet,motion,face*7,dt);
                if (!motion.Airborne) Cancel();
                return true;
            default:
                return false;
        }
    }

    private void Move(CharacterBody3D actor,Node3D art,PetState pet,ChaoMotion motion,Vector3 direction,float dt)
    {
        bool water=motion.IsSwimmingAt(actor.Position);
        bool moving=motion.Step(actor,direction,pet,false,water,dt,true,out _);
        gait+=dt*Mathf.Clamp(motion.GroundSpeed*9,4,19);
        ImportedChao.MovePose(art,gait,moving,water,pet.Swim>=100,motion.Sprinting,motion.Airborne,motion.Flying);
    }
}
