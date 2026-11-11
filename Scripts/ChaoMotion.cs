using System.Linq;
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
    private float lockedHeading, bankTime, waterTime, shoreRest;
    private Vector3 bankTangent, deepWaterTarget;
    private bool shoreExit, driftingToIsland;
    private static readonly Vector3 Outlet=(GardenArt.SwimExit-GardenArt.Pond).Normalized();
    private static readonly Vector3 OutletApproach=GardenArt.Pond+Outlet*(GardenArt.PondRadius-2.65f);
    private static readonly Vector3 Island=new(-13.2f,0,-10.8f);
    public bool Recovering => recovery>0;
    public bool OnCliffRoute => cliffLeg>=0;
    public bool LeavingPond => shoreExit;
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
        lockedHeading=bankTime=waterTime=shoreRest=0;
        shoreExit=false;
        GroundSpeed=BlockedTime=0;
        cliffLeg=-1;
    }

    public void StopCliffRoute() { cliffLeg=-1; balconyJump=rampApproach=false; }

    public void ClearBlocked() => BlockedTime=0;

    public void Tumble() { recovery=5.40f; speed=0; }

    public void LeapFromBed(CharacterBody3D actor,PetState chao)
    {
        var direction=actor.Position-ChaoRest.Bed;
        direction.Y=0;
        Launch(actor,direction.LengthSquared()>.01f ? direction.Normalized() : Vector3.Back,
            chao.Fly>=120 && GD.Randf()<.65f,3.4f);
        vertical=4.3f;
    }

    public void GlideFromPerch(CharacterBody3D actor,Vector3 heading,PetState chao)
    {
        bool glide=chao.Fly>=120;
        Launch(actor,heading,glide,glide ? 2.6f+chao.Fly*.0035f : 2.3f);
        vertical=glide ? .65f : 2.35f;
        lockedHeading=.48f;
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
        float flat=Mathf.SmoothStep(0,1,Mathf.Clamp((recovery-2)/.40f,0,1));
        recovery=Mathf.Max(0,recovery-dt);
        ImportedChao.StandUpPose(art,Quaternion.FromEuler(new(Mathf.Pi/2,0,0)),Vector3.Up*.16f,1-flat);
        return true;
    }

    public bool IsSwimmingAt(Vector3 at) => !airborne && GardenArt.IsPondWater(at);

    public bool Step(CharacterBody3D body,Vector3 direction,PetState pet,bool running,bool water,float dt,
        bool ledges,out bool hardLanding,bool directIsland=false,bool leavingIsland=false,bool allowIslandDrift=false)
    {
        var start=body.Position;
        Advance(body,direction,pet,running,water,dt,ledges,out hardLanding,directIsland,leavingIsland,allowIslandDrift);
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
        bool ledges,out bool hardLanding,bool directIsland,bool leavingIsland,bool allowIslandDrift)
    {
        hardLanding=false;
        bool struggling=water && pet.Swim<100;
        shoreRest=Mathf.Max(0,shoreRest-dt);
        var positionOnMap=new Vector3(body.Position.X,0,body.Position.Z);
        if (water && !airborne)
        {
            if (waterTime<=0)
            {
                var fromCenter=positionOnMap-GardenArt.Pond;
                deepWaterTarget=GardenArt.Pond+(fromCenter.X>1 ? new Vector3(-2,0,6.2f) : new Vector3(5.5f,0,3.8f));
                driftingToIsland=allowIslandDrift && positionOnMap.DistanceTo(Island)<7.8f && GD.Randf()<.22f;
                if (driftingToIsland) deepWaterTarget=Island+Vector3.Back*.65f;
                bankTime=0;
            }
            waterTime+=dt;
            if (leavingIsland || (!directIsland && (struggling ? waterTime>=24 || (waterTime>=15 && positionOnMap.DistanceTo(deepWaterTarget)<.45f)
                : cliffLeg<0 && !GardenArt.IsPondWater(positionOnMap+direction))))
                shoreExit=true;
            if (shoreExit)
            {
                var radial=positionOnMap-GardenArt.Pond;
                bool atOutlet=radial.Normalized().Dot(Outlet)>.997f && radial.Dot(Outlet)>GardenArt.PondRadius-3.5f;
                var target=atOutlet ? GardenArt.SwimExit : OutletApproach;
                direction=PondDirection(positionOnMap,target);
            }
            else if (struggling && !directIsland)
            {
                var course=driftingToIsland ? deepWaterTarget-positionOnMap : PondDirection(positionOnMap,deepWaterTarget);
                direction=course.Rotated(Vector3.Up,Mathf.Sin(waterTime*1.23f)*(driftingToIsland ? .20f : .48f));
            }
        }
        else if (!water)
        {
            waterTime=bankTime=0;
            driftingToIsland=false;
            if (shoreExit)
            {
                direction=GardenArt.SwimExit-positionOnMap;
                if (direction.Length()<.29f)
                {
                    shoreExit=false;
                    shoreRest=6;
                }

            }
            else if (shoreRest>0 && (positionOnMap-GardenArt.SwimExit).Length()<2.3f &&
                !direction.IsZeroApprox() && direction.Dot(Outlet)<0)
                direction=Outlet*1.7f;
        }
        float distance=direction.Length();
        float stopDistance=rampApproach ? .065f : .16f;
        Vector3 forward=direction.Normalized();
        float pace=water ? (struggling ? .30f+pet.Swim*.001f : .65f+pet.Swim*.0019f)
            : (running ? 2.2f : .95f)+pet.Run*.0042f;
        speed=Mathf.MoveToward(speed,distance>stopDistance ? pace : 0,dt*(2.6f+pet.Run*.004f));
        if (struggling) speed=Mathf.Min(speed,pace);
        Sprinting=!water && pet.Run>=650 && speed>=3.15f && distance>.32f;
        float jumpSpeed=pet.Power>=100 && cliffLeg>=0 ? 10.6f : 6.9f;
        bool jumpFromWater=water && !shoreExit && !struggling && !airborne && ledges && cliffLeg>0 &&
            climbGoalY-body.Position.Y>.42f && distance is >.18f and <2.1f;
        if (water && !airborne && !jumpFromWater)
        {
            bankTime=Mathf.Max(0,bankTime-dt);
            var travel=forward*Mathf.Min(distance,speed*dt);
            bool crossingOutlet=shoreExit && (positionOnMap-GardenArt.Pond).Normalized().Dot(Outlet)>.997f;
            if (!crossingOutlet && bankTime>0 && forward.Dot(bankTangent)<.65f)
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
                    float remainingDistance=bank.GetRemainder().Length();
                    var offset=(tangent+normal*.28f).Normalized()*remainingDistance;
                    if (body.MoveAndCollide(offset,testOnly:true)!=null)
                        offset=(-tangent+normal*.28f).Normalized()*remainingDistance;
                    body.MoveAndCollide(offset);
                    bankTangent=offset.Normalized();
                    bankTime=.48f;
                }
            }
            return;
        }

        if (shoreExit && !water && !airborne && (positionOnMap-GardenArt.Pond).Length()<GardenArt.PondRadius+.95f &&
            (positionOnMap-GardenArt.Pond).Normalized().Dot(Outlet)>.990f)
        {
            if (body.Position.Y<0) body.MoveAndCollide(Vector3.Up*Mathf.Min(-body.Position.Y,dt*4));
            body.MoveAndCollide(forward*Mathf.Min(distance,speed*dt));
            return;
        }

        bool supported=jumpFromWater || (body.MoveAndCollide(Vector3.Down*.07f,testOnly:true) is { } floor &&
            floor.GetNormal().Y>.52f);
        var bedOffset=body.Position-ChaoRest.Bed; bedOffset.Y=0;
        if (!airborne && supported && ledges && body.Position.Y is >.24f and <.53f &&
            bedOffset.LengthSquared()<1.45f*1.45f && (bedOffset+direction).LengthSquared()>1.7f*1.7f)
            LeapFromBed(body,pet);
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
            else
            {
                if (support.GetTravel().Y<-.003f) body.MoveAndCollide(Vector3.Down*.07f);
                // The grass mesh is flat. Keep collider soles on the same plane across triangle seams.
                if (support.GetNormal().Y>.999f && Mathf.Abs(support.GetPosition().Y)<.001f &&
                    body.GetChildren().OfType<CollisionShape3D>().FirstOrDefault() is {} shape)
                {
                    float half=shape.Shape is CapsuleShape3D capsule ? capsule.Height*.5f : shape.Shape is SphereShape3D sphere ? sphere.Radius : 0;
                    if (half>0) body.Position=new(body.Position.X,half-shape.Position.Y+.001f,body.Position.Z);
                }
            }
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
        if (flying)
        {
            float skill=Mathf.Clamp(pet.Fly/990f,0,1);
            vertical=Mathf.Max(-Mathf.Lerp(1.65f,.57f,skill),vertical);
            float glideSpeed=Mathf.Lerp(2.45f,5.9f,skill);
            airDirection=airDirection.Normalized()*Mathf.MoveToward(airDirection.Length(),glideSpeed,dt*3.3f);
        }
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

    private static Vector3 PondDirection(Vector3 at,Vector3 target)
    {
        var direct=target-at;
        if (direct.LengthSquared()<.02f) return direct;
        var fromIsland=at-Island;
        float along=Mathf.Clamp(-fromIsland.Dot(direct)/direct.LengthSquared(),0,1);
        const float clearance=3.60f;
        if ((fromIsland+direct*along).LengthSquared()>clearance*clearance) return direct;
        // The southern side of the island is open water, away from the waterfall rocks.
        Vector3 waypoint=Island+new Vector3(at.X<Island.X ? -2.6f : 2.8f,0,4.0f);
        if (at.Z>Island.Z+3.4f && Mathf.Abs(at.X-Island.X)<3.4f)
            waypoint=Island+Vector3.Back*5.2f;
        return waypoint-at;
    }

    private static bool HasLedge(CharacterBody3D body,Vector3 direction,float height)
    {
        Vector3 probe=body.Position+direction*.55f;
        var query=PhysicsRayQueryParameters3D.Create(probe+Vector3.Up*height,probe+Vector3.Up*.18f,1);
        var hit=body.GetWorld3D().DirectSpaceState.IntersectRay(query);
        return hit.Count>0 && ((Vector3)hit["normal"]).Y>.52f &&
            hit["collider"].AsGodotObject() is GodotObject surface && !surface.HasMeta("unclimbable");
    }

    private static bool HasDropAhead(CharacterBody3D body,Vector3 direction,float drop)
    {
        Vector3 ahead=body.Position+direction*.31f;
        var ray=PhysicsRayQueryParameters3D.Create(ahead+Vector3.Up*.10f,ahead-Vector3.Up*drop,1);
        return body.GetWorld3D().DirectSpaceState.IntersectRay(ray).Count==0;
    }
}
