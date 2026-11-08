using Godot;

namespace Mori;

internal sealed class ChaoMotion
{
    private float speed, vertical, peak, recovery;
    private Vector3 airDirection;
    private bool airborne, flying;
    private bool balconyTrip, nextBalcony=true, climbing, balconyJump;
    private int cliffLeg=-1;
    private float cliffTime;
    public bool Sprinting { get; private set; }
    public bool Airborne => airborne;
    public bool Flying => airborne && flying;
    public float GroundSpeed { get; private set; }
    public float BlockedTime { get; private set; }

    public void Reset()
    {
        speed=vertical=recovery=0;
        airborne=flying=Sprinting=false;
        climbing=balconyJump=false;
        GroundSpeed=BlockedTime=0;
        cliffLeg=-1;
    }

    public void Tumble() { recovery=.64f; speed=0; }

    public Vector3? CliffTarget(Vector3 at,float dt,bool begin)
    {
        if (begin && cliffLeg<0)
        {
            cliffLeg=0; balconyTrip=nextBalcony; nextBalcony=!nextBalcony;
            cliffTime=balconyTrip ? 80 : 34;
        }
        if (cliffLeg<0) return null;
        cliffTime-=dt;
        if (balconyTrip) return BalconyTarget(at);
        Vector3 target=cliffLeg switch
        {
            0=>new(1.6f,0,-9.4f),
            5=>new(-6.2f,0,-13.1f),
            _=>GardenArt.Ledges[cliffLeg-1].Center
        };
        var delta=target-at; delta.Y=0;
        if (delta.Length()<.46f) cliffLeg++;
        if (cliffTime<=0 || cliffLeg>5) { cliffLeg=-1; return null; }
        return target;
    }

    private Vector3? BalconyTarget(Vector3 at)
    {
        var stairs=GardenArt.BalconySteps;
        int rail=stairs.Length+2;
        Vector3 target=cliffLeg switch
        {
            0=>stairs[0]+new Vector3(-1.35f,-stairs[0].Y,0),
            _ when cliffLeg<=stairs.Length=>stairs[cliffLeg-1],
            _ when cliffLeg==rail-1=>GardenArt.Balcony,
            _ when cliffLeg==rail=>GardenArt.BalconyRail,
            _=>GardenArt.BalconyRail+Vector3.Back*3.8f
        };
        var offset=target-at; offset.Y=0;
        if (!airborne && offset.Length()<.39f && (cliffLeg==0 || at.Y>=target.Y-.16f))
        {
            cliffLeg++;
            if (cliffLeg==rail+1) balconyJump=true;
        }
        if (cliffTime>0 && cliffLeg<=rail+1 && (cliffLeg<=rail || offset.Length()>.39f)) return target;
        // Keep the takeoff target until the Chao leaves the railing.
        if (balconyJump) return GardenArt.BalconyRail+Vector3.Back*3.8f;
        cliffLeg=-1;
        return null;
    }

    public bool Recover(Node3D art,float dt)
    {
        if (recovery<=0) return false;
        recovery=Mathf.Max(0,recovery-dt);
        float flat=Mathf.SmoothStep(0,1,recovery/.38f);
        art.RotationDegrees=new(0,0,flat*85);
        art.Position=Vector3.Up*(flat*.22f);
        return true;
    }

    public bool Step(CharacterBody3D body,Vector3 direction,PetState pet,bool running,bool water,float dt,
        bool ledges,out bool hardLanding)
    {
        var start=body.Position;
        Advance(body,direction,pet,running,water,dt,ledges,out hardLanding);
        var travel=body.Position-start; travel.Y=0;
        GroundSpeed=travel.Length()/Mathf.Max(dt,.001f);
        BlockedTime=direction.LengthSquared()>.16f && GroundSpeed<.13f && !airborne ? BlockedTime+dt : 0;
        Sprinting=Sprinting && GroundSpeed>=3.15f;
        if (GroundSpeed>.075f)
            body.Rotation=new(0,Mathf.LerpAngle(body.Rotation.Y,Mathf.Atan2(travel.X,travel.Z),1-Mathf.Exp(-9*dt)),0);
        return GroundSpeed>.075f;
    }

    private void Advance(CharacterBody3D body,Vector3 direction,PetState pet,bool running,bool water,float dt,
        bool ledges,out bool hardLanding)
    {
        hardLanding=false;
        float distance=direction.Length();
        Vector3 forward=direction.Normalized();
        float pace=water ? (pet.Swim<100 ? .82f : 1.20f+pet.Swim*.0018f)
            : (running ? 2.2f : .95f)+pet.Run*.0042f;
        speed=Mathf.MoveToward(speed,distance>.16f ? pace : 0,dt*(2.6f+pet.Run*.004f));
        Sprinting=!water && pet.Run>=650 && speed>=3.15f && distance>.32f;
        if (water && !airborne)
        {
            body.MoveAndCollide(forward*Mathf.Min(distance,speed*dt));
            return;
        }

        bool supported=body.MoveAndCollide(Vector3.Down*.07f,testOnly:true) is { } floor &&
            floor.GetNormal().Y>.52f;
        if (!airborne && supported && balconyJump)
        {
            balconyJump=false; climbing=false; airborne=true;
            flying=pet.Fly>=100; peak=body.Position.Y; vertical=2.8f;
            airDirection=Vector3.Back*2.2f;
        }
        if (!airborne && supported && distance>.16f)
        {
            var hit=body.MoveAndCollide(forward*Mathf.Min(distance,speed*dt));
            if (hit!=null)
            {
                if (ledges && hit.GetCollider() is GodotObject stone && stone.HasMeta("stone") &&
                    HasLedge(body,forward))
                {
                    airborne=climbing=true; flying=false; peak=body.Position.Y;
                    vertical=6.9f; airDirection=forward*Mathf.Clamp(speed,2.0f,3.2f);
                }
                else body.MoveAndCollide(hit.GetRemainder().Slide(hit.GetNormal()));
            }
        }
        if (!airborne && body.MoveAndCollide(Vector3.Down*.07f,testOnly:true)==null)
        {
            airborne=true; peak=body.Position.Y;
            flying=ledges && pet.Fly>=100 && Mathf.PosMod(body.Position.X*11+body.Position.Z*7,5)>1.6f;
            vertical=flying ? 2.8f : 0;
            airDirection=forward*Mathf.Clamp(speed,1.2f,4.2f);
        }
        if (!airborne) return;
        peak=Mathf.Max(peak,body.Position.Y);
        vertical-=dt*(flying && vertical<0 ? 4.0f : 18f);
        if (flying) vertical=Mathf.Max(-1.65f,vertical);
        var impact=body.MoveAndCollide(Vector3.Up*vertical*dt);
        if (impact!=null)
        {
            if (vertical<0 && impact.GetNormal().Y>.52f)
            {
                hardLanding=!flying && peak-body.Position.Y>1.12f;
                airborne=flying=climbing=false; vertical=0;
                if (hardLanding) Tumble();
            }
            else if (vertical>0) vertical=0;
        }
        if (airborne)
        {
            Vector3 travel=airDirection*dt;
            if (climbing)
                travel=travel.LimitLength(Mathf.Max(0,direction.Dot(airDirection.Normalized())));
            body.MoveAndCollide(travel);
        }
    }

    private static bool HasLedge(CharacterBody3D body,Vector3 direction)
    {
        Vector3 probe=body.Position+direction*.55f;
        var query=PhysicsRayQueryParameters3D.Create(probe+Vector3.Up*1.24f,probe+Vector3.Up*.18f,1);
        var hit=body.GetWorld3D().DirectSpaceState.IntersectRay(query);
        return hit.Count>0 && ((Vector3)hit["normal"]).Y>.52f;
    }
}
