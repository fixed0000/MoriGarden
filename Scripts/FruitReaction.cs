using Godot;

namespace Mori;

internal sealed class FruitReaction
{
    private enum Stage { Idle, Confused, Alert, Chase, Beg, Waiting, Upset }
    private Stage stage;
    private float remaining, cooldown, gait;
    private Vector3 lastCarrier;
    public bool IsConfused => stage==Stage.Confused;
    public bool AnticipatesFruit => stage is Stage.Alert or Stage.Chase or Stage.Beg or Stage.Waiting;
    public bool CanAcceptFruit(PetState chao) =>
        stage!=Stage.Alert && (stage!=Stage.Idle || cooldown>0 || !WantsFruit(chao));

    private static bool WantsFruit(PetState chao) =>
        chao.Fullness<=32 || (chao.Glutton && chao.Fullness<97);

    public void Confuse()
    {
        bool eager=AnticipatesFruit;
        stage=eager ? Stage.Waiting : Stage.Confused;
        remaining=eager ? 5 : 1.35f;
    }

    public void Interrupt(Node3D art)
    {
        if (stage!=Stage.Idle) ImportedChao.EndThink(art);
        if (stage==Stage.Upset && art.FindChild("MoodOrb",true,false) is EmotionOrb orb) orb.SetUpset(false);
        ImportedChao.StopCrying(art);
        stage=Stage.Idle;
        cooldown=0;
    }

    public bool Step(CharacterBody3D actor,Node3D art,PetState chao,ChaoMotion motion,
        Vector3? fruitCarrier,float dt)
    {
        var orb=art.FindChild("MoodOrb",true,false) as EmotionOrb;
        if (fruitCarrier==null) cooldown=0;
        else cooldown=Mathf.Max(0,cooldown-dt);
        if (stage==Stage.Idle)
        {
            if (cooldown>0 || fruitCarrier is not Vector3 carrier || actor.Position.DistanceTo(carrier)>6 ||
                !WantsFruit(chao)) return false;
            var eyes=actor.Position+Vector3.Up*.27f;
            var ray=PhysicsRayQueryParameters3D.Create(eyes,carrier+Vector3.Up*.8f,1);
            if (actor.GetWorld3D().DirectSpaceState.IntersectRay(ray).Count>0) return false;
            stage=Stage.Alert;
            remaining=.65f;
            lastCarrier=carrier;
        }
        remaining-=dt;
        if (stage==Stage.Confused)
        {
            ImportedChao.ThinkPose(art,remaining);
            if (remaining<=0) Interrupt(art);
            return true;
        }
        if (stage==Stage.Waiting)
        {
            ImportedChao.ThinkPose(art,remaining);
            if (remaining<=0) Disappoint(chao,art);
            return true;
        }
        if (stage==Stage.Upset)
        {
            orb?.SetUpset(true);
            if (chao.Crybaby) ImportedChao.CryPose(art,remaining);
            if (remaining<=0) { stage=Stage.Idle; cooldown=20; ImportedChao.StopCrying(art); }
            return true;
        }
        if (fruitCarrier is not Vector3 target)
        {
            Confuse();
            return true;
        }
        Vector3 direction=target-actor.Position; direction.Y=0;
        var previous=lastCarrier-actor.Position; previous.Y=0;
        lastCarrier=target;
        if ((stage is Stage.Alert or Stage.Chase) && direction.Length()>1.5f &&
            direction.Length()-previous.Length()>1.6f*dt)
        {
            Confuse();
            return true;
        }
        if (stage==Stage.Alert)
        {
            if (direction.LengthSquared()>.01f) actor.Rotation=new(0,Mathf.Atan2(direction.X,direction.Z),0);
            orb?.SetAlert(true);
            ImportedChao.ReceivePose(art,.45f);
            if (remaining<=0) { orb?.SetAlert(false); stage=Stage.Chase; remaining=8; }
        }
        else if (stage==Stage.Chase)
        {
            if (direction.Length()<1.27f) { stage=Stage.Beg; remaining=3.2f; }
            else if (remaining<=0 || direction.Length()>6) Confuse();
            else
            {
                bool water=!motion.Airborne && GardenArt.IsPondWater(actor.Position);
                if (water) actor.Position=new(actor.Position.X,Mathf.MoveToward(actor.Position.Y,0,dt*4),actor.Position.Z);
                var route=motion.RouteOffUpperPlatform(actor.Position,target)-actor.Position;
                route.Y=0;
                bool moving=motion.Step(actor,route,chao,true,water,dt,true,out _);
                gait+=dt*15;
                ImportedChao.MovePose(art,gait,moving,water,chao.Swim>=100,motion.Sprinting,motion.Airborne,motion.Flying);
            }
        }
        else if (stage==Stage.Beg)
        {
            if (direction.LengthSquared()>.01f) actor.Rotation=new(0,Mathf.Atan2(direction.X,direction.Z),0);
            ImportedChao.ReceivePose(art,.68f+.24f*Mathf.Sin(remaining*11));
            if (remaining<=0 || direction.Length()>2.2f) Confuse();
        }
        return true;
    }

    private void Disappoint(PetState chao,Node3D art)
    {
        ImportedChao.EndThink(art);
        stage=Stage.Upset; remaining=4.5f;
        chao.Joy=Mathf.Max(0,chao.Joy-4);
        chao.Bond=Mathf.Max(0,chao.Bond-1);
    }
}
