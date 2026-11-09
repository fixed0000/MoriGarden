using Godot;

namespace Mori;

internal sealed class ChaoMotion
{
    private float speed, vertical, peak, recovery;
    private Vector3 airDirection;
    private bool airborne, flying;
    private bool walking;
    private bool balconyTrip, nextBalcony=true, climbing, balconyJump;
    private int cliffLeg=-1;
    private float cliffTime;
    private float climbGoalY;
    private bool clearLedgeBeforeMoving;
    private bool airborneJump, rebounded, landingTumble, rampApproach;
    private float lockedHeading, bankTime, waterTime;
    private Vector3 bankTangent;
    public bool Sprinting { get; private set; }
    public bool Airborne => airborne;
    public bool Flying => airborne && flying;
    public float GroundSpeed { get; private set; }
    public float BlockedTime { get; private set; }

    public void Reset()
    {
        speed=vertical=recovery=0;
        airborne=flying=Sprinting=walking=false;
        climbing=balconyJump=false;
        airborneJump=rebounded=landingTumble=rampApproach=false;
        lockedHeading=bankTime=waterTime=0;
        GroundSpeed=BlockedTime=0;
        cliffLeg=-1;
    }

    public void Tumble() { recovery=.64f; speed=0; }

    public void LeapFromBed(CharacterBody3D actor,PetState chao)
    {
        if (chao.Fly<120 || GD.Randf()>.65f) return;
        var direction=actor.Position-ChaoRest.Bed;
        direction.Y=0;
        Launch(actor,direction.LengthSquared()>.01f ? direction.Normalized() : Vector3.Back,
            GD.Randf()<.65f,2.2f);
    }

    private void Launch(CharacterBody3D actor,Vector3 heading,bool glide,float pace)
    {
        airborne=true; climbing=false; peak=actor.Position.Y;
        flying=glide;
        airborneJump=true; rebounded=landingTumble=false;
        vertical=glide ? 3.4f : 2.35f;
        airDirection=heading*pace;
    }

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
        rampApproach=false;
        Vector3 target=cliffLeg switch
        {
            0=>new(1.6f,0,-9.4f),
            5=>new(-6.2f,0,-13.1f),
            _=>GardenArt.Ledges[cliffLeg-1].Center+Vector3.Up*GardenArt.Ledges[cliffLeg-1].Size.Y*.5f
        };
        climbGoalY=target.Y;
        var delta=target-at; delta.Y=0;
        if (delta.Length()<.46f) cliffLeg++;
        if (cliffTime<=0 || cliffLeg>5) { cliffLeg=-1; return null; }
        return target;
    }

    private Vector3? BalconyTarget(Vector3 at)
    {
        var ledges=GardenArt.Ledges;
        int rail=ledges.Length+4;
        rampApproach=cliffLeg>=rail-3 && cliffLeg<=rail-2;
        Vector3 target=cliffLeg switch
        {
            0=>new(1.6f,0,-9.4f),
            _ when cliffLeg<=ledges.Length=>ledges[cliffLeg-1].Center+
                Vector3.Up*ledges[cliffLeg-1].Size.Y*.5f,
            _ when cliffLeg==rail-3=>GardenArt.BalconyRampStart,
            _ when cliffLeg==rail-2=>GardenArt.BalconyRampEnd+Vector3.Left*.21f,
            _ when cliffLeg==rail-1=>GardenArt.Balcony,
            _ when cliffLeg==rail=>GardenArt.BalconyRail,
            _=>GardenArt.BalconyRail+Vector3.Back*3.8f
        };
        var offset=target-at; offset.Y=0;
        climbGoalY=target.Y;
        float arrivalDistance=cliffLeg==rail-2 ? .13f : .39f;
        float arrivalHeight=cliffLeg==rail-2 ? .055f : .16f;
        if (!airborne && offset.Length()<arrivalDistance && (cliffLeg==0 || at.Y>=target.Y-arrivalHeight))
        {
            cliffLeg++;
            if (cliffLeg==rail+1) balconyJump=true;
        }
        if (cliffTime>0 && cliffLeg<=rail+1 && (cliffLeg<=rail || offset.Length()>.39f)) return target;
        // Keep the takeoff target until the Chao leaves the railing.
        if (balconyJump) return GardenArt.BalconyRail+Vector3.Back*3.8f;
        cliffLeg=-1; rampApproach=false;
        return null;
    }

    public Vector3 RouteOffUpperPlatform(Vector3 at,Vector3 target)
    {
        if (cliffLeg>=0 || !OnUpperPlatform(at)) return target;
        var exit=GardenArt.WaterfallExit;
        if (at.DistanceSquaredTo(exit)>.16f) return exit;
        return exit+WaterfallHeading(at)*2;
    }

    private static bool OnUpperPlatform(Vector3 at) => at.Y is >5.15f and <5.78f &&
        at.X is >-10.10f and <-6.95f && at.Z is >-21.72f and <-18.26f;

    private static Vector3 WaterfallHeading(Vector3 at)
    {
        var direction=new Vector3(-11,at.Y,-16.64f)-at;
        return direction.Normalized();
    }

    public bool Recover(Node3D art,float dt)
    {
        if (recovery<=0) return false;
        recovery=Mathf.Max(0,recovery-dt);
        float flat=Mathf.SmoothStep(0,1,recovery/.38f);
        art.RotationDegrees=new(flat*85,0,0);
        art.Position=Vector3.Up*(flat*.22f);
        return true;
    }

    public bool IsSwimmingAt(Vector3 at) => !airborne && GardenArt.IsPondWater(at);

    public bool Step(CharacterBody3D body,Vector3 direction,PetState pet,bool running,bool water,float dt,
        bool ledges,out bool hardLanding)
    {
        var start=body.Position;
        Advance(body,direction,pet,running,water,dt,ledges,out hardLanding);
        if (water && !airborne && body.Position.Y<0 && GardenArt.IsPondWater(body.Position))
            body.MoveAndCollide(Vector3.Up*Mathf.Min(-body.Position.Y,dt*3.2f));
        var travel=body.Position-start; travel.Y=0;
        float actualSpeed=travel.Length()/Mathf.Max(dt,.001f);
        GroundSpeed=Mathf.Lerp(GroundSpeed,actualSpeed,1-Mathf.Exp(-15*dt));
        walking=direction.LengthSquared()>.0256f && GroundSpeed>(walking ? .085f : .19f);
        BlockedTime=direction.LengthSquared()>.16f && GroundSpeed<.13f && !airborne ? BlockedTime+dt : 0;
        Sprinting=Sprinting && GroundSpeed>=3.15f;
        var facing=airborne ? airDirection : travel;
        if ((!airborne && walking && actualSpeed>.12f) || (flying && facing.LengthSquared()>.00001f))
            body.Rotation=new(0,Mathf.LerpAngle(body.Rotation.Y,Mathf.Atan2(facing.X,facing.Z),1-Mathf.Exp(-10*dt)),0);
        return walking;
    }

    public static Vector3 AvoidNeighbors(CharacterBody3D actor,Vector3 intent)
    {
        float length=intent.Length();
        if (length<.30f) return intent;
        Vector3 heading=intent/length;
        foreach (var node in actor.GetTree().GetNodesInGroup("GardenChao"))
        {
            if (node is not CharacterBody3D other || other==actor || !other.IsVisibleInTree() ||
                Mathf.Abs(other.Position.Y-actor.Position.Y)>.35f) continue;
            Vector3 delta=other.Position-actor.Position;
            delta.Y=0;
            float ahead=heading.Dot(delta);
            if (ahead<=.03f || ahead>.60f) continue;
            Vector3 side=heading.Cross(Vector3.Up);
            float across=side.Dot(delta);
            if (Mathf.Abs(across)>.31f) continue;
            // Keep the chosen side relative to the original destination, not the previous frame's turn.
            return (heading+side*(across>.025f ? -.9f : .9f)).Normalized()*length;
        }
        return intent;
    }

    private void Advance(CharacterBody3D body,Vector3 direction,PetState pet,bool running,bool water,float dt,
        bool ledges,out bool hardLanding)
    {
        hardLanding=false;
        bool struggling=water && pet.Swim<100;
        waterTime=water ? waterTime+dt : 0;
        if (water && !airborne && (cliffLeg<0 || struggling))
        {
            if (struggling) direction=GardenArt.SwimExit-body.Position;
            var fromPond=body.Position-GardenArt.Pond;
            fromPond.Y=0;
            if (!GardenArt.IsPondWater(new Vector3(body.Position.X+direction.X,0,body.Position.Z+direction.Z)))
            {
                var towardExit=GardenArt.SwimExit-GardenArt.Pond;
                var outlet=towardExit.Normalized();
                if (fromPond.Length()>GardenArt.PondRadius-3.2f && fromPond.Normalized().Dot(outlet)<.994f)
                {
                    // Swim around the shallow ring before crossing the gap between its rocks.
                    var radial=fromPond.Normalized();
                    float side=Mathf.Sign(radial.Cross(outlet).Y);
                    var waypoint=GardenArt.Pond+radial.Rotated(Vector3.Up,side*.26f)*(GardenArt.PondRadius-1.35f);
                    direction=waypoint-body.Position;
                }
                else direction=GardenArt.SwimExit-body.Position;
                direction.Y=0;
                if (struggling && fromPond.Normalized().Dot(outlet)<.986f)
                    direction=direction.Rotated(Vector3.Up,Mathf.Sin(waterTime*1.15f)*.37f);
            }
        }
        float distance=direction.Length();
        float stopDistance=rampApproach ? .065f : .16f;
        Vector3 forward=direction.Normalized();
        float pace=water ? (struggling ? .30f+pet.Swim*.001f : .65f+pet.Swim*.0019f)
            : (running ? 2.2f : .95f)+pet.Run*.0042f;
        speed=Mathf.MoveToward(speed,distance>stopDistance ? pace : 0,dt*(2.6f+pet.Run*.004f));
        Sprinting=!water && pet.Run>=650 && speed>=3.15f && distance>.32f;
        float jumpSpeed=pet.Fly>0 && cliffLeg>=0 ? 10.6f : 6.9f;
        bool jumpFromWater=water && !struggling && !airborne && ledges && cliffLeg>0 &&
            climbGoalY-body.Position.Y>.42f && distance is >.18f and <2.1f;
        if (water && !airborne && !jumpFromWater)
        {
            bankTime=Mathf.Max(0,bankTime-dt);
            var travel=forward*Mathf.Min(distance,speed*dt);
            if (bankTime>0 && forward.Dot(bankTangent)<.65f)
                travel=travel.Lerp(bankTangent*travel.Length(),.68f);
            var bank=body.MoveAndCollide(travel);
            if (bank!=null)
            {
                var surface=bank.GetNormal();
                var remainder=bank.GetRemainder();
                if (surface.Y>.55f && surface.Y<.995f)
                    body.MoveAndCollide(remainder.Slide(surface));
                else
                {
                    var normal=new Vector3(surface.X,0,surface.Z).Normalized();
                    var slide=remainder.Slide(normal);
                    var tangent=normal.Cross(Vector3.Up);
                    if (bankTime>0 ? tangent.Dot(bankTangent)<0 : tangent.Dot(slide)<0) tangent=-tangent;
                    var offset=(tangent+normal*.28f).Normalized()*travel.Length();
                    if (body.MoveAndCollide(offset,testOnly:true)!=null)
                        offset=(-tangent+normal*.28f).Normalized()*travel.Length();
                    body.MoveAndCollide(offset);
                    bankTangent=offset.Normalized();
                    bankTime=.48f;
                }
            }
            return;
        }

        bool supported=jumpFromWater || (body.MoveAndCollide(Vector3.Down*.07f,testOnly:true) is { } floor &&
            floor.GetNormal().Y>.52f);
        if (!airborne && supported && balconyJump)
        {
            balconyJump=false;
            Launch(body,Vector3.Back,pet.Fly>=100,2.2f);
            lockedHeading=.35f;
        }
        if (!airborne && supported && !rampApproach && (jumpFromWater || (balconyTrip && cliffLeg>0 && jumpSpeed>7)) &&
            climbGoalY-body.Position.Y>.40f && distance>.18f &&
            distance<(climbGoalY>7 ? 3.9f : 2.85f))
        {
            airborne=climbing=true; flying=false; peak=body.Position.Y;
            airborneJump=true; rebounded=landingTumble=false;
            vertical=jumpSpeed; airDirection=forward*4.6f;
            clearLedgeBeforeMoving=true;
        }
        if (!airborne && supported && ledges && !rampApproach && distance>.23f &&
            body.Position.Y>(pet.Fly>=120 ? .31f : .62f) && (cliffLeg<0 || climbGoalY<body.Position.Y+.21f) &&
            HasDropAhead(body,forward,pet.Fly>=120 ? .29f : .53f))
        {
            bool upper=OnUpperPlatform(body.Position);
            Launch(body,upper ? WaterfallHeading(body.Position) : forward,pet.Fly>=120 && GD.Randf()<.68f,
                Mathf.Clamp(speed,1.55f,3.75f));
            lockedHeading=upper ? .55f : 0;
        }
        if (!airborne && supported && distance>stopDistance)
        {
            var hit=body.MoveAndCollide(forward*Mathf.Min(distance,speed*dt));
            if (hit!=null)
            {
                if (ledges && !rampApproach && hit.GetCollider() is GodotObject stone && stone.HasMeta("stone") && !stone.HasMeta("unclimbable") &&
                    HasLedge(body,forward,jumpSpeed>7 ? 2.8f : 1.24f))
                {
                    airborne=climbing=true; flying=false; peak=body.Position.Y;
                    airborneJump=true; rebounded=landingTumble=false;
                    clearLedgeBeforeMoving=false;
                    vertical=jumpSpeed; airDirection=forward*Mathf.Clamp(speed,2.0f,3.2f);
                }
                else body.MoveAndCollide(hit.GetRemainder().Slide(hit.GetNormal()));
            }
        }
        if (!airborne)
        {
            var support=body.MoveAndCollide(Vector3.Down*.07f,testOnly:true);
            if (support==null || support.GetNormal().Y<=.52f)
            {
                airborne=true; peak=body.Position.Y; flying=false; vertical=0;
                airborneJump=rebounded=landingTumble=false;
                airDirection=forward*Mathf.Clamp(speed,1.2f,4.2f);
            }
            else if (support.GetTravel().Y<-.003f)
                body.MoveAndCollide(Vector3.Down*.07f);
        }
        if (!airborne) return;
        peak=Mathf.Max(peak,body.Position.Y);
        vertical-=dt*(flying && vertical<0 ? 4.0f : 18f);
        var position=body.Position;
        if (ledges && vertical<=0 && !climbing && GardenArt.IsPondWater(position) &&
            position.Y+vertical*dt<=.025f)
        {
            body.MoveAndCollide(Vector3.Up*Mathf.Min(0,.025f-position.Y));
            airborne=flying=false;
            vertical=lockedHeading=0;
            return;
        }
        if (flying) vertical=Mathf.Max(-1.65f,vertical);
        var impact=body.MoveAndCollide(Vector3.Up*vertical*dt);
        if (impact!=null)
        {
            if (vertical<0 && impact.GetNormal().Y>.52f)
            {
                bool hard=!flying && peak-body.Position.Y>1.12f;
                if (airborneJump && !rebounded)
                {
                    rebounded=true; landingTumble=hard;
                    flying=false; vertical=1.55f;
                    airDirection*=.25f;
                }
                else
                {
                    hardLanding=landingTumble || (!rebounded && hard);
                    airborne=flying=climbing=false; vertical=0; lockedHeading=0;
                    airborneJump=rebounded=landingTumble=false;
                    if (hardLanding) Tumble();
                }
            }
            else if (vertical>0) vertical=0;
            else body.MoveAndCollide(impact.GetRemainder().Slide(impact.GetNormal()));
        }
        if (airborne)
        {
            lockedHeading=Mathf.Max(0,lockedHeading-dt);
            if (flying && !climbing && !rebounded && lockedHeading<=0 && distance>.23f)
                airDirection=airDirection.Lerp(forward*Mathf.Max(1.5f,airDirection.Length()),1-Mathf.Exp(-5*dt));
            if (climbing && clearLedgeBeforeMoving && vertical>0 && body.Position.Y<climbGoalY+.08f) return;
            Vector3 travel=airDirection*dt;
            if (climbing)
                travel=travel.LimitLength(Mathf.Max(0,direction.Dot(airDirection.Normalized())));
            body.MoveAndCollide(travel);
        }
    }

    private static bool HasLedge(CharacterBody3D body,Vector3 direction,float height)
    {
        Vector3 probe=body.Position+direction*.55f;
        var query=PhysicsRayQueryParameters3D.Create(probe+Vector3.Up*height,probe+Vector3.Up*.18f,1);
        var hit=body.GetWorld3D().DirectSpaceState.IntersectRay(query);
        return hit.Count>0 && ((Vector3)hit["normal"]).Y>.52f;
    }

    private static bool HasDropAhead(CharacterBody3D body,Vector3 direction,float drop)
    {
        Vector3 ahead=body.Position+direction*.31f;
        var ray=PhysicsRayQueryParameters3D.Create(ahead+Vector3.Up*.10f,ahead-Vector3.Up*drop,1);
        return body.GetWorld3D().DirectSpaceState.IntersectRay(ray).Count==0;
    }
}
