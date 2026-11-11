using Godot;

namespace Mori;

public partial class ToyCar : CharacterBody3D
{
    private static readonly Vector3 Home=new(10,0,5);
    private CharacterBody3D? player;
    private PetState? playChao;
    private Node3D? playArt;
    private float playing, cooldown, phraseOffset;
    private System.Collections.Generic.HashSet<ToyCar>? ensemble;
    private Vector3 placementFrom, placementTo, placementRotation;
    private Vector3 playOrigin, playForward, velocity;
    private bool held, inFlight;
    public bool IsRattle { get; init; }
    public bool IsFlute { get; init; }
    public bool IsElectricGuitar { get; init; }
    public bool IsSynthesizer { get; init; }
    public bool IsBeachBall { get; init; }
    public bool IsInstrument => IsFlute || IsElectricGuitar || IsSynthesizer;
    internal bool IsGroundToy => !IsInstrument && !IsRattle && !IsBeachBall;
    internal CharacterBody3D? PlayActor => player;
    internal PetState? PlayState => playChao;
    internal void FinishBall()
    {
        if (player==null) return;
        if (IsInstanceValid(player) && Position.Y>player.Position.Y+.04f) Position=PlacementPoint(player);
        EndPlay();
    }
    public string ItemName => IsBeachBall ? "beach ball" : IsElectricGuitar ? "electric guitar" : IsSynthesizer ? "synthesizer" : IsFlute ? "flute" : IsRattle ? "rattle" : "toy car";
    private int lastShake, bounces;
    internal int BounceCount=>bounces;
    private Node3D? strap;
    public float PlayDuration => IsInstrument ? 60 : 4;
    internal float PlayingSeconds => playing;
    internal bool Performing => player!=null && playing<PlayDuration;
    internal bool CanJoin => IsInstrument && Performing && playing<50;
    internal void StartWith(ToyCar leader)
    {
        phraseOffset=leader.playing%1.20f; lastShake=-1;
        ensemble=leader.ensemble ?? new System.Collections.Generic.HashSet<ToyCar> { leader };
        leader.ensemble=ensemble; ensemble.Add(this);
    }
    private void FinishEnsemble()
    {
        var group=ensemble;
        if (group==null) return;
        foreach(var member in group) member.ensemble=null;
        foreach(var member in group)
        {
            if (!IsInstanceValid(member) || member==this || member.player==null) continue;
            member.placementFrom=member.Position; member.placementRotation=member.Rotation;
            member.placementTo=member.PlacementPoint(member.player);
            member.playing=member.PlayDuration;
            member.GetNodeOrNull<AudioStreamPlayer3D>("ChaoCue")?.Stop();
        }
    }
    public bool CanPickUp => !held && !inFlight;
    internal bool PlayedBy(CharacterBody3D actor) => player==actor;
    internal bool IsFavorite(PetState chao) => IsInstrument ? chao.FavoriteInstrument==(IsFlute ? "Flute" : IsElectricGuitar ? "Electric Guitar" : "Synthesizer") : IsBeachBall ? chao.PreferredToy=="Beach Ball" :
        chao.PreferredToy==(IsRattle ? "Rattle" : "Toy Car");
    internal void CancelPlay(CharacterBody3D actor)
    {
        if (!PlayedBy(actor)) return;
        Position=PlacementPoint(actor);
        Rotation=new(0,actor.Rotation.Y,0);
        EndPlay();
    }

    public override void _Ready()
    {
        Position=IsFlute ? new Vector3(3.1f,0,1.7f) : IsRattle ? Home+new Vector3(-3,0,.5f) : Home;
        if (IsElectricGuitar) Position=new(4.2f,0,1.8f);
        if (IsSynthesizer) Position=new(5.4f,0,1.8f);
        if (IsBeachBall) Position=new(6.5f,0,3.2f);
        CollisionLayer=0;
        AddChild(new CollisionShape3D { Position=Vector3.Up*.055f,
            Shape=new BoxShape3D { Size=new(.15f,.10f,.27f) } });
        if (IsBeachBall)
        {
            // Six curved panels on a spherical beach ball, rather than stacked shapes.
            var mesh=new SurfaceTool(); mesh.Begin(Mesh.PrimitiveType.Triangles);
            Color[] colors={new("f5df65"),new("f3f1df"),new("64bdd8"),new("f3f1df"),new("e57569"),new("f3f1df")};
            Vector3 Point(float lat,float lon) => new Vector3(Mathf.Sin(lat)*Mathf.Cos(lon),Mathf.Cos(lat),Mathf.Sin(lat)*Mathf.Sin(lon))*.115f+Vector3.Up*.115f;
            for (int panel=0;panel<6;panel++) for (int y=0;y<10;y++) for (int x=0;x<5;x++)
            {
                float a=y*Mathf.Pi/10,b=(y+1)*Mathf.Pi/10,c=(panel+x/5f)*Mathf.Tau/6,d=(panel+(x+1)/5f)*Mathf.Tau/6;
                foreach (var point in new[]{Point(a,c),Point(b,c),Point(b,d),Point(a,c),Point(b,d),Point(a,d)})
                { mesh.SetColor(colors[panel]); mesh.AddVertex(point); }
            }
            mesh.GenerateNormals();
            AddChild(new MeshInstance3D { Mesh=mesh.Commit(),MaterialOverride=new StandardMaterial3D { VertexColorUseAsAlbedo=true,CullMode=BaseMaterial3D.CullModeEnum.Disabled,Roughness=.55f } });
            return;
        }
        if (IsElectricGuitar)
        {
            GardenArt.Ball(this,new(-.025f,.05f,-.03f),new(.18f,.065f,.21f),new("a94052"));
            GardenArt.Ball(this,new(.035f,.05f,.04f),new(.13f,.06f,.13f),new("b84c58"));
            GardenArt.Shape(this,new BoxMesh(),new(0,.055f,.185f),new(.045f,.025f,.28f),new("aa835b"));
            GardenArt.Shape(this,new BoxMesh(),new(0,.055f,.34f),new(.067f,.032f,.07f),new("d0ad75"));
            for (int i=0;i<6;i++) GardenArt.Shape(this,new BoxMesh(),new(-.015f+i*.006f,.072f,.15f),new(.0015f,.002f,.35f),new("dad4c1"));
            for (int i=0;i<7;i++) GardenArt.Shape(this,new BoxMesh(),new(0,.073f,.07f+i*.032f),new(.046f,.002f,.002f),new("a8a7a0"));
            return;
        }
        if (IsSynthesizer)
        {
            GardenArt.Shape(this,new BoxMesh(),new(0,.06f,0),new(.36f,.07f,.18f),new("394555"));
            for (int i=0;i<12;i++)
            {
                GardenArt.Shape(this,new BoxMesh(),new(-.154f+i*.028f,.10f,.028f),new(.025f,.014f,.11f),new("ebece1"));
                if (i%7 is not 2 and not 6) GardenArt.Shape(this,new BoxMesh(),new(-.14f+i*.028f,.113f,.003f),new(.015f,.018f,.065f),new("20252c"));
            }
            for (int i=0;i<4;i++) GardenArt.Ball(this,new(-.12f+i*.035f,.104f,-.061f),new(.013f,.009f,.013f),new("75bdac"));
            strap=new Node3D { Name="ShoulderStrap",Visible=false };
            AddChild(strap);
            var path=new[]{new Vector3(-.155f,.065f,.055f),new Vector3(-.115f,.13f,.20f),new Vector3(.105f,.13f,.20f),new Vector3(.155f,.065f,.055f)};
            for(int i=0;i<path.Length-1;i++)
            {
                var axis=path[i+1]-path[i];
                var ribbon=GardenArt.Shape(strap,new BoxMesh(),(path[i+1]+path[i])*.5f,new(.021f,axis.Length(),.008f),new("9f675e"));
                ribbon.Quaternion=new Quaternion(Vector3.Up,axis.Normalized());
            }
            return;
        }
        if (IsRattle)
        {
            GardenArt.Ball(this,new(0,.069f,.065f),new(.14f,.14f,.14f),new("dfa648"));
            GardenArt.Ball(this,new(0,.038f,-.055f),new(.058f,.067f,.20f),new("70a9b2"));
            return;
        }
        if (IsFlute)
        {
            var tube=GardenArt.Cylinder(this,new(0,.039f,0),.023f,.31f,new("d5bd81"));
            tube.RotationDegrees=new(0,0,90);
            for (int i=0;i<5;i++)
                GardenArt.Ball(this,new(-.066f+i*.034f,.060f,0),new(.013f,.003f,.013f),new("665540"));
            return;
        }
        GardenArt.Shape(this,new BoxMesh(),new(0,.062f,0),new(.14f,.071f,.26f),new("b35b42"));
        GardenArt.Shape(this,new BoxMesh(),new(0,.111f,-.028f),new(.116f,.05f,.107f),new("a1b8ba"));
        for (int side=-1;side<=1;side+=2)
            for (int axle=-1;axle<=1;axle+=2)
                GardenArt.Cylinder(this,new(side*.071f,.039f,axle*.077f),.039f,.027f,
                    new("33383a")).RotationDegrees=new(0,0,90);
    }

    public void Advance(float dt)
    {
        cooldown=Mathf.Max(0,cooldown-dt);
        if (inFlight)
        {
            inFlight=!ThrowMotion.StepItem(this,ref velocity,dt,ref bounces);
            return;
        }
        if (player==null || IsBeachBall) return;
        if (!IsInstanceValid(player) || player.Position.DistanceTo(playOrigin)>1)
        {
            EndPlay();
            return;
        }
        float before=playing;
        if (playing<PlayDuration && playChao!=null) playChao.Fun=Mathf.Min(100,playChao.Fun+(IsInstrument ? .65f : 9)*Mathf.Min(dt,PlayDuration-playing));
        playing+=dt;
        if (playing<PlayDuration) return;
        if (before<PlayDuration)
        {
            FinishEnsemble();
            GetNodeOrNull<AudioStreamPlayer3D>("ChaoCue")?.Stop();
            placementFrom=Position;
            placementRotation=Rotation;
            placementTo=PlacementPoint(player);
        }
        float progress=Mathf.SmoothStep(0,1,Mathf.Clamp((playing-PlayDuration)/.45f,0,1));
        Position=placementFrom.Lerp(placementTo,progress);
        Rotation=placementRotation.Lerp(new(0,player.Rotation.Y,0),progress);
        if (playing>=PlayDuration+.45f) EndPlay();
    }

    private Vector3 PlacementPoint(CharacterBody3D actor)
    {
        var space=GetWorld3D().DirectSpaceState;
        var origin=actor.Position+Vector3.Up*.21f;
        foreach (float angle in new[]{0f,.65f,-.65f})
        {
            var ahead=origin+playForward.Rotated(Vector3.Up,angle)*.78f;
            if (space.IntersectRay(PhysicsRayQueryParameters3D.Create(origin,ahead,1)).Count>0) continue;
            var floor=space.IntersectRay(PhysicsRayQueryParameters3D.Create(ahead,ahead+Vector3.Down*.8f,1));
            if (floor.Count>0) return (Vector3)floor["position"]+Vector3.Up*.009f;
        }
        return actor.Position+playForward*.64f;
    }

    private void EndPlay()
    {
        FinishEnsemble();
        GetNodeOrNull<AudioStreamPlayer3D>("ChaoCue")?.Stop();
        if (IsInstanceValid(playArt)) { playArt!.GetNodeOrNull<EmotionOrb>("MoodOrb")?.SetIdea(null,false); ImportedChao.PauseAfterAction(playArt); }
        if (strap!=null) strap.Hide();
        player=null;
        playChao=null;
        playArt=null;
        held=false;
        cooldown=18;
    }

    public bool TakeFromPlay()
    {
        if (playChao==null) return false;
        playChao.ChangeRelationship(-.55f);
        EndPlay();
        return true;
    }

    public void Hold(Vector3 at)
    {
        if (strap!=null) strap.Hide();
        held=true;
        Position=at;
    }

    public void Toss(Vector3 impulse)
    {
        held=false;
        inFlight=true;
        velocity=impulse; bounces=0;
        if (strap!=null) strap.Hide();
    }

    public void HoldByChao(CharacterBody3D actor,Node3D art)
    {
        if (IsRattle) { RattleGrip(actor,art,0); return; }
        var front=Vector3.Back.Rotated(Vector3.Up,actor.Rotation.Y);
        Hold(actor.Position+front*(IsSynthesizer ? .22f : .19f)+Vector3.Up*(IsSynthesizer ? .075f : .16f));
        Rotation=new(0,actor.Rotation.Y+(IsSynthesizer ? Mathf.Pi : 0),0);
        if (IsSynthesizer)
            ImportedChao.InstrumentHands(art,GlobalPosition-actor.Basis.X*.13f+Vector3.Up*.055f,
                GlobalPosition+actor.Basis.X*.13f+Vector3.Up*.055f);
        else if (IsFlute)
            ImportedChao.InstrumentHands(art,ToGlobal(new(-.065f,.039f,0)),ToGlobal(new(.085f,.039f,0)));
    }

    private void RattleGrip(CharacterBody3D actor,Node3D art,float phase)
    {
        var grip=actor.Position+actor.Basis.Z*.22f+Vector3.Up*.14f;
        held=player==null; inFlight=false;
        Rotation=new(-Mathf.Pi/2+Mathf.Sin(phase)*.33f,actor.Rotation.Y,0);
        Position=grip-Basis*new Vector3(0,.038f,-.095f);
        ImportedChao.InstrumentHands(art,ToGlobal(new(-.028f,.038f,-.115f)),ToGlobal(new(.028f,.038f,-.075f)));
    }

    internal void PushAside(Vector3 travel)
    {
        if (!CanPickUp || player!=null && !IsGroundToy) return;
        var before=Position;
        GardenContact.Move(this,travel);
        MoveAndCollide(Vector3.Down*.55f);
        var shift=Position-before;
        if (player!=null)
        {
            playOrigin+=shift;
            if (playing>=PlayDuration) { placementFrom+=shift; placementTo+=shift; }
        }
    }

    public bool BeginPlay(CharacterBody3D actor,PetState chao)
    {
        if (GardenArt.IsPondWater(actor.Position) || (!IsRattle && !IsInstrument && !IsBeachBall && !chao.EnjoysToyCars) || chao.Fun>=100) return false;
        player=actor; playChao=chao; playing=phraseOffset=0; held=inFlight=false;
        lastShake=-1;
        playForward=Vector3.Back.Rotated(Vector3.Up,actor.Rotation.Y);
        playOrigin=actor.Position+playForward*.30f;
        chao.Fun=Mathf.Min(100,chao.Fun+.5f);
        Position=playOrigin;
        return true;
    }

    public bool PlayPose(CharacterBody3D actor,Node3D art)
    {
        if (player!=actor) return false;
        playArt=art;
        if (IsBeachBall)
        {
            ImportedChao.ReceivePose(art,.80f);
            Position=actor.Position+playForward*.19f+Vector3.Up*.07f;
            return true;
        }
        if (playing>=PlayDuration)
        {
            ImportedChao.ReceivePose(art,.55f);
            art.Rotation=new(.33f,0,0);
            return true;
        }
        float wobble=Mathf.Sin(playing*7);
        if (IsElectricGuitar || IsSynthesizer)
        {
            Position=actor.Position+playForward*(IsElectricGuitar ? .15f : .22f)+Vector3.Up*(IsSynthesizer ? .065f : .085f);
            Rotation=new(IsElectricGuitar ? -.55f : -.25f,actor.Rotation.Y+(IsSynthesizer ? Mathf.Pi : 0),IsElectricGuitar ? -.48f : 0);
            if (strap!=null) strap.Show();
            ImportedChao.InstrumentPose(art,playing,IsElectricGuitar);
            if (IsSynthesizer)
                ImportedChao.InstrumentHands(art,GlobalPosition-actor.Basis.X*.09f+Vector3.Up*.103f,
                    GlobalPosition+actor.Basis.X*.09f+Vector3.Up*(.103f+.009f*Mathf.Sin(playing*13)));
            else
                ImportedChao.InstrumentHands(art,ToGlobal(new(0,.075f,.20f)),ToGlobal(new(0,.075f+.012f*Mathf.Sin(playing*27),.0f)));
            int phrase=(int)((playing+phraseOffset)/1.20f);
            if (phrase!=lastShake) { ChaoSounds.Play(this,IsElectricGuitar ? "guitar" : "synth",(playing+phraseOffset)%1.20f); lastShake=phrase; }
            return true;
        }
        if (IsFlute)
        {
            Position=actor.Position+playForward*.16f+Vector3.Up*(.148f+.004f*wobble);
            Rotation=new(0,actor.Rotation.Y,.06f*wobble);
            ImportedChao.MovePose(art,0,false,false,false,false,false,false);
            ImportedChao.InstrumentHands(art,ToGlobal(new(-.065f,.039f,0)),ToGlobal(new(.085f,.039f,0)));
            int phrase=(int)(playing/1.24f);
            if (phrase!=lastShake) { ChaoSounds.Play(this,"flute"); lastShake=phrase; }
            return true;
        }
        if (IsRattle)
        {
            ImportedChao.MovePose(art,0,false,false,false,false,false,false);
            RattleGrip(actor,art,playing*12);
            int shake=(int)(playing*4);
            if (shake!=lastShake) { ChaoSounds.Play(this,"rattle"); lastShake=shake; }
            return true;
        }
        Position=playOrigin+playForward*(.14f*(1+wobble));
        Rotation=new(0,actor.Rotation.Y,0);
        ImportedChao.EatPose(art,playing);
        art.Rotation=new(.35f+.11f*wobble,0,0);
        return true;
    }

    public bool Use(CharacterBody3D actor,Node3D art,PetState chao,out Vector3 target)
    {
        target=Position-Vector3.Back*.40f;
        if (!Available(actor,chao) || actor.Position.DistanceTo(target)>.25f) return false;
        actor.Rotation=Vector3.Zero;
        return BeginPlay(actor,chao) && PlayPose(actor,art);
    }

    public bool Available(CharacterBody3D actor,PetState chao) =>
        !GardenArt.IsPondWater(actor.Position) && (IsRattle || IsInstrument || IsBeachBall || chao.EnjoysToyCars) && chao.Fun<(IsFavorite(chao) ? 92 : 72) &&
        cooldown<=0 && player==null && CanPickUp && actor.Position.DistanceTo(Position)<(IsFavorite(chao) ? 12 : 7);
}
