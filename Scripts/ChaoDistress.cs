using Godot;

namespace Mori;

internal sealed class ChaoDistress
{
    private bool bounced, wokeWhileHeld;
    private float lying, reacting, wakeUpset, sulking, waterUpset;

    public bool Upset => bounced || lying>0 || reacting>0 || wakeUpset>0 || sulking>0 || waterUpset>0;
    public bool Busy => lying>0 || reacting>0 || sulking>0;

    public void Reset()
    {
        bounced=wokeWhileHeld=false;
        lying=reacting=wakeUpset=sulking=waterUpset=0;
    }

    public void Tick(float dt)
    {
        wakeUpset=Mathf.Max(0,wakeUpset-dt);
        waterUpset=Mathf.Max(0,waterUpset-dt);
    }

    public void PickUp(Node3D art,bool wasSleeping)
    {
        lying=reacting=0;
        ImportedChao.StopCrying(art);
        if (wasSleeping) wakeUpset=60;
        wokeWhileHeld |= wasSleeping || sulking>0;
        sulking=0;
    }

    public void PutDown()
    {
        if (!wokeWhileHeld) return;
        wokeWhileHeld=false;
        sulking=Mathf.Lerp(30,60,GD.Randf());
    }

    public void BeginThrow()
    {
        bounced=wokeWhileHeld=false;
        lying=reacting=sulking=0;
    }

    public bool ThrowStep(CharacterBody3D body,Node3D art,ref Vector3 velocity,float dt,bool hasPond)
    {
        velocity+=Vector3.Down*18*dt;
        art.Rotation=new(.45f,0,Mathf.Sin(velocity.Y)*.22f);
        Vector3 travel=velocity*dt;
        if (hasPond && travel.Y<0 && body.Position.Y+travel.Y<=.025f)
        {
            float fraction=Mathf.Clamp((.025f-body.Position.Y)/travel.Y,0,1);
            Vector3 entry=travel*fraction;
            if (GardenArt.IsPondWater(body.Position+entry) &&
                body.MoveAndCollide(entry,testOnly:true)==null)
            {
                body.MoveAndCollide(entry);
                velocity=Vector3.Zero;
                bounced=false;
                waterUpset=5;
                return true;
            }
        }
        var hit=body.MoveAndCollide(travel);
        if (hit==null) return false;
        var normal=hit.GetNormal();
        if (!bounced)
        {
            bounced=true;
            (art.FindChild("MoodOrb",true,false) as EmotionOrb)?.SetDistressed(true);
            velocity=velocity.Bounce(normal)*.31f;
            velocity.Y=Mathf.Clamp(velocity.Y,2.15f,3.5f);
            return false;
        }
        if (normal.Y<.55f)
        {
            velocity=velocity.Slide(normal);
            return false;
        }
        velocity=Vector3.Zero;
        bounced=false;
        lying=3;
        FaceDown(art);
        return true;
    }

    private static void FaceDown(Node3D art)
    {
        ImportedChao.Gait(art,0,false,false);
        art.RotationDegrees=new(90,0,0);
        art.Position=Vector3.Up*.16f;
    }

    public bool HoldPose(Node3D art,PetState chao,float dt)
    {
        if (lying>0)
        {
            FaceDown(art);
            lying=Mathf.Max(0,lying-dt);
            if (lying==0) reacting=2.4f;
            return true;
        }
        if (reacting>0)
        {
            if (reacting>2)
            {
                float flat=Mathf.SmoothStep(0,1,(reacting-2)/.4f);
                ImportedChao.StandUpPose(art,Quaternion.FromEuler(new(Mathf.Pi/2,0,0)),Vector3.Up*.16f,1-flat);
                reacting=Mathf.Max(2,reacting-dt);
                return true;
            }
            art.Position=Vector3.Up*.034f;
            if (chao.Aggressiveness>=25)
            {
                ImportedChao.ReceivePose(art,.64f);
                art.Rotation=new(.24f,Mathf.Sin(reacting*19)*.15f,0);
            }
            else ImportedChao.CryPose(art,reacting);
            reacting=Mathf.Max(0,reacting-dt);
            if (reacting==0) ImportedChao.StopCrying(art);
            return true;
        }
        if (sulking<=0) return false;
        sulking=Mathf.Max(0,sulking-dt);
        ImportedChao.MovePose(art,0,false,false,false,false,false,false);
        return true;
    }
}
