using System.Linq;
using Godot;

namespace Mori;

public partial class WateringCan : Node3D
{
    private enum Stage { Approach, PickUp, Think, Pond, Dip, ThinkAgain, Tree, Pour, Away, SetDown }
    private static readonly Vector3 Home=new(2.2f,0,1.4f);
    private static readonly Vector3 PondSide=(GardenArt.SwimExit-GardenArt.Pond).Normalized();
    private static readonly Vector3 PondBank=GardenArt.Pond+PondSide*(GardenArt.PondRadius+.38f);
    private static readonly Vector3 OuterBank=GardenArt.Pond+PondSide*(GardenArt.PondRadius+1.48f);
    private CharacterBody3D? owner;
    private Node3D? ownerArt;
    private Stage stage;
    private readonly ChaoWander detour=new();
    private float delay=80+GD.Randf()*60;
    private float elapsed, phase, lease, tripTime;
    private Vector3 tree, treeStand, putAway, lowerFrom;
    private Vector3[] treeRoute=System.Array.Empty<Vector3>();
    private int treeWaypoint;
    private Node3D water = null!;
    private CpuParticles3D drops = null!;
    private static readonly Vector3 Spout=new(0,.143f,.255f);

    public override void _Ready()
    {
        Position=Home;
        var green=new Color("708e90");
        GardenArt.Shape(this,new CylinderMesh { TopRadius=.090f,BottomRadius=.115f,Height=.139f,RadialSegments=12 },
            new(0,.071f,0),Vector3.One,green);
        GardenArt.Shape(this,new TorusMesh { InnerRadius=.071f,OuterRadius=.098f,Rings=12,RingSegments=8 },
            new(0,.145f,0),Vector3.One,green);
        var handle=GardenArt.Shape(this,new TorusMesh { InnerRadius=.063f,OuterRadius=.082f,Rings=12,RingSegments=8 },
            new(0,.12f,-.11f),Vector3.One,green);
        handle.RotationDegrees=new(0,0,90);
        var from=new Vector3(0,.063f,.076f);
        var spout=GardenArt.Shape(this,new CylinderMesh { TopRadius=.014f,BottomRadius=.031f,Height=from.DistanceTo(Spout),RadialSegments=9 },
            (from+Spout)*.5f,Vector3.One,green);
        spout.Quaternion=new Quaternion(Vector3.Up,(Spout-from).Normalized());
        GardenArt.Ball(this,Spout,new(.076f,.055f,.034f),new("84999a"));
        water=GardenArt.Cylinder(this,new(0,.153f,0),.071f,.006f,new("65b2cc"));
        water.Name="WaterInside";
        water.Hide();
        drops=new CpuParticles3D
        {
            Name="WateringDrops", Position=Spout, Emitting=false, LocalCoords=false, Amount=32, Lifetime=.48f, Spread=17,
            Direction=Vector3.Down, Gravity=Vector3.Down*7, InitialVelocityMin=.25f, InitialVelocityMax=.85f,
            Mesh=new SphereMesh { Radius=.011f,Height=.022f,RadialSegments=5,Rings=3,
                Material=GardenArt.Material(new Color("90c9d7")) }
        };
        AddChild(drops);
    }

    internal bool InUse => owner!=null;
    internal void PushAside(Vector3 travel)
    {
        if (InUse) return;
        var space=GetWorld3D().DirectSpaceState;
        var query=new PhysicsShapeQueryParameters3D { Shape=new SphereShape3D { Radius=.13f },
            Transform=new Transform3D(Basis.Identity,GlobalPosition+Vector3.Up*.15f),Motion=travel,CollisionMask=1 };
        var fraction=space.CastMotion(query);
        GlobalPosition+=travel*fraction[0];
    }

    internal static bool Qualified(PetState pet) => pet.Intelligence>=140 && pet.Kindness>=50;

    public void Advance(float dt)
    {
        if (owner==null) { delay=Mathf.Max(0,delay-dt); return; }
        lease-=dt;
        tripTime-=dt;
        if (lease<=0 || tripTime<=0 || !IsInstanceValid(owner)) Finish();
    }

    internal bool Step(CharacterBody3D actor,Node3D art,PetState pet,ChaoMotion motion,float dt,bool available)
    {
        if (owner==null)
        {
            if (!available || delay>0 || motion.Airborne || !Qualified(pet) ||
                GardenArt.IsPondWater(actor.Position) || actor.Position.DistanceTo(Position)>12)
                return false;
            var trees=GetParent().GetChildren().OfType<FruitTree>()
                .Where(candidate=>!candidate.IsFeeder && !GardenArt.IsPondWater(candidate.Position)).ToArray();
            if (trees.Length==0) return false;
            tree=trees[GD.RandRange(0,trees.Length-1)].Position;
            var approach=PondBank-tree; approach.Y=0;
            approach=approach.Normalized();
            treeStand=ClearShore(tree+approach*1.30f);
            putAway=ClearShore(treeStand+approach.Rotated(Vector3.Up,1.23f)*1.45f);
            treeRoute=MakeTreeRoute(tree,treeStand);
            treeWaypoint=0;
            owner=actor;
            ownerArt=art;
            stage=Stage.Approach;
            tripTime=155;
        }
        if (owner!=actor) return false;
        if (!Qualified(pet)) { Finish(); return false; }
        lease=.45f;
        elapsed+=dt;
        switch (stage)
        {
            case Stage.Approach:
                if (WalkTo(actor,art,pet,motion,Position,dt))
                {
                    Face(actor,Position);
                    lowerFrom=Position;
                    Next(Stage.PickUp);
                }
                break;
            case Stage.PickUp:
                float lift=Smooth((elapsed-.23f)/.34f);
                Bend(art,Mathf.Sin(Smooth(elapsed/.57f)*Mathf.Pi)*.62f);
                Position=lowerFrom.Lerp(actor.Position+actor.Basis.Z*.16f+Vector3.Up*.14f,lift);
                Rotation=new(0,actor.Rotation.Y,0);
                if (elapsed>=.57f) Next(Stage.Think);
                break;
            case Stage.Think:
            case Stage.ThinkAgain:
                ImportedChao.ThinkPose(art,elapsed);
                Hold(actor,0,0);
                float thought=ChaoThought.WithIntelligence(pet);
                if (elapsed>=thought)
                {
                    ImportedChao.EndThink(art);
                    Next(stage==Stage.Think ? Stage.Pond : Stage.Tree);
                }
                break;
            case Stage.Pond:
                if (CarryTo(actor,art,pet,motion,PondBank,dt))
                {
                    Face(actor,GardenArt.Pond);
                    Next(Stage.Dip);
                }
                break;
            case Stage.Dip:
                float dip=elapsed<1 ? Smooth(elapsed) : 1-Smooth((elapsed-1.35f)/.65f);
                Bend(art,dip*.72f);
                Hold(actor,dip*1.02f,dip);
                if (elapsed>=1) water.Show();
                if (elapsed>=2) Next(Stage.ThinkAgain);
                break;
            case Stage.Tree:
                if (CarryTo(actor,art,pet,motion,treeRoute[treeWaypoint],dt,followWaypoints:true) &&
                    ++treeWaypoint>=treeRoute.Length)
                {
                    Face(actor,tree);
                    Next(Stage.Pour);
                }
                break;
            case Stage.Pour:
                float pour=Smooth(elapsed/.55f)*(1-Smooth((elapsed-2.65f)/.55f));
                Bend(art,pour*.29f);
                Hold(actor,pour*.98f,0);
                drops.Direction=GlobalBasis.Inverse()*Vector3.Down;
                drops.Emitting=elapsed is >.45f and <2.7f;
                if (elapsed>=2.7f) water.Hide();
                if (elapsed>=3.2f) Next(Stage.Away);
                break;
            case Stage.Away:
                if (CarryTo(actor,art,pet,motion,putAway,dt))
                {
                    lowerFrom=Position;
                    Next(Stage.SetDown);
                }
                break;
            case Stage.SetDown:
                float lower=Smooth(elapsed/.62f);
                Bend(art,Mathf.Sin(lower*Mathf.Pi)*.56f);
                Position=lowerFrom.Lerp(Floor(actor.Position+actor.Basis.Z*.52f),lower);
                Rotation=new(0,actor.Rotation.Y,0);
                if (elapsed>=.62f) Finish();
                break;
        }
        return true;
    }

    public bool IsUsing(CharacterBody3D actor) => owner==actor;

    public void Cancel(CharacterBody3D actor)
    {
        if (owner==actor) Finish();
    }

    private void Finish()
    {
        if (IsInstanceValid(ownerArt)) { ImportedChao.EndThink(ownerArt!); ImportedChao.PauseAfterAction(ownerArt!); }
        if (IsInstanceValid(owner) && stage!=Stage.Approach && stage!=Stage.SetDown)
            Position=Floor(owner!.Position+owner.Basis.Z*.55f);
        Rotation=new(0,Rotation.Y,0);
        owner=null;
        ownerArt=null;
        water.Hide();
        drops.Emitting=false;
        delay=130+GD.Randf()*115;
    }

    private void Next(Stage next) { stage=next; elapsed=0; }
    private static float Smooth(float t) => Mathf.SmoothStep(0,1,Mathf.Clamp(t,0,1));

    private bool WalkTo(CharacterBody3D actor,Node3D art,PetState pet,ChaoMotion motion,Vector3 destination,float dt,bool followWaypoints=false)
    {
        bool waterborne=motion.IsSwimmingAt(actor.Position);
        var goal=motion.RouteOffUpperPlatform(actor.Position,destination);
        if (goal==destination && !waterborne && !followWaypoints) goal=DryRoute(actor.Position,destination);
        var offset=goal-actor.Position; offset.Y=0;
        if (goal==destination && offset.Length()<.29f && !motion.Airborne && !waterborne) return true;
        bool moving;
        var radial=actor.Position-GardenArt.Pond; radial.Y=0;
        if (!motion.Airborne && !waterborne && (destination==PondBank || goal==OuterBank) &&
            radial.Length()<GardenArt.PondRadius+2.5f && radial.Normalized().Dot(PondSide)>.995f)
        {
            // Cross the shore's terrain seam at water level along the cleared approach.
            if (actor.Position.Y<0) actor.MoveAndCollide(Vector3.Up*Mathf.Min(-actor.Position.Y,3*dt));
            moving=actor.MoveAndCollide(offset.LimitLength(.92f*dt))==null;
        }
        else
        {
            if (!waterborne && !motion.Airborne) offset=detour.Steer(actor,offset,dt);
            var before=actor.Position;
            moving=motion.Step(actor,offset,pet,false,waterborne,dt,true,out _);
            var moved=actor.Position-before; moved.Y=0;
            if (!waterborne && !motion.Airborne && offset.LengthSquared()>.08f &&
                moved.LengthSquared()<.000006f && actor.Position.Y is >-.065f and <.025f)
            {
                var raised=actor.GlobalTransform;
                var lift=Vector3.Up*(.027f-actor.Position.Y);
                raised.Origin+=lift;
                var across=offset.LimitLength(.9f*dt);
                if (!actor.TestMove(raised,across) && actor.MoveAndCollide(lift)==null)
                    moving=actor.MoveAndCollide(across)==null;
            }
        }
        phase+=dt*9;
        ImportedChao.MovePose(art,phase,moving,motion.IsSwimmingAt(actor.Position),pet.Swim>=100,
            false,motion.Airborne,motion.Flying);
        return false;
    }

    private static Vector3[] MakeTreeRoute(Vector3 tree,Vector3 stand)
    {
        if (tree.X<-20)
            return new[]{OuterBank,new Vector3(-2,0,3),new Vector3(-7.7f,0,6),new Vector3(-16,0,5.3f),
                new Vector3(-22.5f,0,.8f),stand};
        if (tree.X<-10)
            return new[]{OuterBank,new Vector3(-2,0,3),new Vector3(-7.7f,0,6),stand};
        if (tree.X<0)
            return new[]{OuterBank,new Vector3(-2,0,1.8f),stand};
        if (tree.Z<-10)
            return new[]{OuterBank,new Vector3(3,0,-.6f),new Vector3(5,0,-5.5f),new Vector3(5.7f,0,-10.8f),stand};
        if (tree.Z<0)
            return new[]{OuterBank,new Vector3(4.2f,0,-.6f),stand};
        return new[]{OuterBank,new Vector3(4.4f,0,.9f),stand};
    }

    private Vector3 DryRoute(Vector3 start,Vector3 goal)
    {
        var radial=start-GardenArt.Pond; radial.Y=0;
        bool atOutlet=radial.Normalized().Dot(PondSide)>.9975f;
        bool atOuterBank=radial.Length()>GardenArt.PondRadius+1.18f;
        Vector3 next;
        if (goal==PondBank && atOutlet)
            next=goal;
        else if (goal!=PondBank && atOutlet && !atOuterBank)
            next=OuterBank;
        else
            next=Around(start,goal==PondBank ? OuterBank : goal,GardenArt.Pond,GardenArt.PondRadius+1.12f,1.03f);
        next=Around(start,next,ChaoRest.Bed,1.91f,.50f);
        next=Around(start,next,new Vector3(-5,0,4),1.40f,.46f);
        foreach (var trunk in GetParent().GetChildren().OfType<FruitTree>().Where(tree=>!tree.IsFeeder).Select(tree=>tree.Position))
            next=Around(start,next,trunk,.87f,.38f);
        return next;
    }

    private static Vector3 ClearShore(Vector3 point)
    {
        var radial=point-GardenArt.Pond; radial.Y=0;
        float clearance=GardenArt.PondRadius+1.23f;
        return radial.Length()<clearance ? GardenArt.Pond+radial.Normalized()*clearance : point;
    }

    private static Vector3 Around(Vector3 start,Vector3 goal,Vector3 center,float clearance,float padding)
    {
        var from=start-center; from.Y=0;
        var to=goal-center; to.Y=0;
        var segment=to-from;
        if (segment.LengthSquared()<.09f || to.Length()<clearance) return goal;
        var closest=from+segment*Mathf.Clamp(-from.Dot(segment)/segment.LengthSquared(),0,1);
        if (closest.LengthSquared()>clearance*clearance) return goal;
        var radial=from.LengthSquared()>.01f ? from.Normalized() : to.Normalized();
        float turn=radial.Cross(to).Y>=0 ? 1 : -1;
        return center+radial.Rotated(Vector3.Up,turn*.31f)*(clearance+padding);
    }

    private bool CarryTo(CharacterBody3D actor,Node3D art,PetState pet,ChaoMotion motion,Vector3 goal,float dt,bool followWaypoints=false)
    {
        bool arrived=WalkTo(actor,art,pet,motion,goal,dt,followWaypoints);
        ImportedChao.ReceivePose(art,.86f);
        Hold(actor,0,0);
        return arrived;
    }

    private void Hold(CharacterBody3D actor,float tip,float dip)
    {
        var forward=actor.Basis.Z;
        var at=actor.Position+forward*(.16f+dip*.28f)+Vector3.Up*(.14f-dip*.17f);
        Position=at;
        Rotation=new(tip,actor.Rotation.Y,0);
    }

    private static void Bend(Node3D art,float lean)
    {
        ImportedChao.ReceivePose(art,.92f);
        art.Rotation=new(lean,0,0);
        art.Position=Vector3.Zero;
    }

    private static void Face(Node3D actor,Vector3 at)
    {
        var offset=at-actor.Position;
        actor.Rotation=new(0,Mathf.Atan2(offset.X,offset.Z),0);
    }

    private Vector3 Floor(Vector3 near)
    {
        var ray=PhysicsRayQueryParameters3D.Create(near+Vector3.Up*.55f,near+Vector3.Down*.72f,1);
        var hit=GetWorld3D().DirectSpaceState.IntersectRay(ray);
        return hit.Count>0 ? (Vector3)hit["position"] : near;
    }
}
