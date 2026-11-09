using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mori;

public partial class GardenGame : Node3D
{
    private PetState pet = new();
    private ChaoEditorBridge? editor;
    private float editorClock;
    private CharacterBody3D player = null!;
    private CharacterBody3D friend = null!;
    private Node3D friendArt = null!;
    private AvatarWalk? avatarWalk;
    private CpuParticles3D playerSplashes = null!, petSplashes = null!;
    private float chaoGait;
    private readonly ChaoMotion chaoMotion=new();
    private bool tossedMori;
    private MeshInstance3D playerWake = null!;
    private bool showStats, showBag;
    private float pettingTime, treeReachTime;
    private FruitTree? shakingTree;
    private const float TreeActionDuration=FruitTree.ShakeDuration+.94f;
    private Panel hintPanel = null!;
    private Control bagPanel = null!;
    private Label bagText = null!;
    private ProgressBar readinessBar = null!;
    private Node3D? importedAvatar;
    private float offerTime, eatTime, thinkTime;
    private readonly FruitMeal fruitMeal=new();
    private bool rejectFruit, rejectToy, welcomeFruit;
    private AudioStreamPlayer3D mainSwimSound = null!, wadeSound = null!, impactSound = null!;
    private CpuParticles3D impactSpray = null!;
    private WateringCan? CanInGarden => world.GetNodeOrNull<WateringCan>("WateringCan");
    private float wadeStride;
    private float mainSwimBeat;
    private float offerLean;
    private const float HandoffDuration=.72f;
    private float pickupTime;
    private bool pickingItem;
    private Action? releaseObject;
    private Vector3 pickupStart;
    private const float PickupDuration = .58f;
    private MeshInstance3D wake = null!;
    private float callTime, swimTime;
    private int activity;
    private bool swimmingTrip, leavingWater;
    private Node3D playerArt = null!;
    private Camera3D camera = null!;
    private ThirdPersonCamera cameraRig = null!;
    private Node3D world = null!;
    private GardenZone zone = GardenZone.Garden, destination;
    private Label zoneLabel = null!;
    private ColorRect fade = null!;
    private Control details = null!;
    private float transition = -1, portalCooldown = 2, verticalSpeed;
    private bool zoneSwapped, carrying;
    private GardenZone petZone = GardenZone.Garden;
    private Vector3 petZonePosition = new(1.8f,0,3);
    private Label stats = null!, formLabel = null!, hint = null!, toast = null!, raceLabel = null!, pauseLabel = null!;
    private TrainingAnimal? heldAnimal;
    private GardenEgg? heldEgg, recentHatch, machineResident;
    private float recentCardTime, groundHold;
    private Label3D mainName = null!;
    private bool machineOpen, machineOpening;
    private PetState? machinePet;
    private Control machinePanel = null!;
    private Label machineTitle = null!;
    private LineEdit renameInput = null!;
    private Button renameApply = null!;
    private float mainEggShake, upsetTime;
    private bool mainEggCracked;
    private bool shopOpen, pausedByFocus;
    private readonly GardenCourtship courtship=new();
    private int shopColor;
    private const int HeartFruitPrice=2;
    private static int ShopChoices=>GardenEgg.Colors.Length+1;
    private bool HeartFruitSelected=>shopColor==GardenEgg.Colors.Length;
    private Control shopPanel = null!;
    private Label shopText = null!;
    private FruitTree? heldFruit;
    private ToyCar? heldToy;
    private readonly Dictionary<Vector3,(float Growth,Vector3? GroundPosition,int Bites)[]> fruitGrowth=new();
    private GardenEgg? offeredResident;
    private readonly ChaoRest moriRest=new();
    private readonly IslandNap moriIslandNap=new();
    private readonly ChaoDistress moriDistress=new();
    private readonly ChaoVocal moriVoice=new();
    private readonly Dictionary<int,ChaoRest> bedSpaces=new();
    private readonly FruitReaction moriFruitReaction=new();
    private GardenSky gardenSky=null!;
    private Vector3? FruitCarrier => heldFruit!=null && offerTime<=0 && eatTime<=0 && !selfEating
        ? player.Position : null;
    private Vector3 selfFoodStart;
    private bool selfEating;
    private float machineLoad;
    private Vector3 machineTossStart;
    private bool machineThrowStarted;
    private Vector3 throwVelocity;
    private bool moriInFlight;
    private float releasePose;
    private bool throwingPose, loweringMori;
    private Vector3 setDownFrom, setDownTo;
    private const float SetDownDuration = .46f;
    private AudioStreamPlayer music = null!;
    private AudioStreamPlayer machineChime = null!;
    private Button renameButton = null!;
    private Node3D? rival;
    private float moriProgress, rivalProgress, cheerTime, cheerCooldown;
    private static readonly Vector3[] Track = { new(0,0,5),new(-6,0,4),new(-7,0,-2),new(-4,0,-6),new(3,0,-6),new(7,0,-2),new(6,0,4),new(0,0,5) };
    private static readonly string[] Sections = { "Run", "Swim", "Swim", "Fly", "Power", "Power", "Run" };
    private readonly Vector3 kitchen = new(-5,0,4), bed = new(5,0,4);
    private float time, saveTimer, petCooldown, dashTime, dashCooldown, messageTime, raceTime, wanderTime;
    private bool sleeping, following, paused, racing, testMode;
    private float napRemaining;
    private int frame;
    private Vector3 wanderTarget;
    private string savePath = "";

    public override void _Ready()
    {
        GetWindow().Title = "Mori: Garden Adventure";
        var loop = AudioStreamWav.LoadFromFile(ProjectSettings.GlobalizePath("res://Audio/garden-loop.wav"));
        if (loop != null)
        {
            loop.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            loop.LoopEnd = loop.GetLength() > 0 ? Mathf.RoundToInt((float)loop.GetLength()*loop.MixRate) : 0;
            music = new AudioStreamPlayer { Stream=loop, VolumeDb=-18, Autoplay=true };
            AddChild(music);
        }
        testMode = OS.GetCmdlineUserArgs().Contains("--smoke-test");
        savePath = ProjectSettings.GlobalizePath(testMode ? "user://smoke/pet.json" : "user://pet.json");
        string loadWarning = "";
        pet = testMode ? new PetState() : SaveStore.Load(savePath, out loadWarning);
        if (!testMode) editor=new ChaoEditorBridge(System.IO.Path.GetDirectoryName(savePath)!);
        RenderingServer.SetDefaultClearColor(new Color("849da7"));
        var environment = new WorldEnvironment { Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("849da7"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = Colors.White, AmbientLightEnergy = .4f,
            TonemapMode = Godot.Environment.ToneMapper.Linear
        }};
        AddChild(environment);
        gardenSky=new GardenSky(environment.Environment);
        AddChild(gardenSky);
        moriRest.Spaces=bedSpaces;
        world = new Node3D(); AddChild(world); ZoneArt.Build(world, zone);
        PopulateEggs();
        player = new CharacterBody3D { Position = new(0,0,5), Rotation = new(0,Mathf.Pi,0), CollisionLayer = 2 };
        PhysicsInterpolationMode=PhysicsInterpolationModeEnum.Off;
        player.PhysicsInterpolationMode=PhysicsInterpolationModeEnum.On;
        player.AddChild(new CollisionShape3D { Position = new(0,.85f,0),
            Shape = new CapsuleShape3D { Radius=.30f, Height=1.70f } });
        AddChild(player);
        player.AddToGroup("garden_player");
        playerArt = new Node3D();
        player.AddChild(playerArt);
        LoadAvatar();
        playerWake = GardenArt.Shape(player,new TorusMesh { InnerRadius=.48f,OuterRadius=.52f },
            new(0,.116f,0),Vector3.One,new("92bac2"));
        playerWake.Hide();
        playerSplashes = CreateSplashes(player);
        impactSpray=CreateSplashes(player);
        impactSpray.Name="WaterImpactSpray";
        impactSpray.OneShot=true;
        impactSpray.Explosiveness=.95f;
        impactSpray.Amount=82;
        impactSpray.Lifetime=.73f;
        impactSpray.Spread=83;
        impactSpray.InitialVelocityMin=2.2f;
        impactSpray.InitialVelocityMax=5.2f;
        wadeSound=SwimAudio.Attach(player);
        impactSound=SwimAudio.Attach(player,impact:true);
        friend = new CharacterBody3D { Position = new(1.8f,0,3), CollisionLayer = 0 };
        friend.AddToGroup("GardenChao");
        friend.AddChild(new CollisionShape3D { Position = new(0,.235f,0),
            Shape = new CapsuleShape3D { Radius=.14f, Height=.41f } });
        AddChild(friend);
        wanderTarget=friend.Position+Vector3.Left*2.4f;
        wanderTime=4;
        wake = GardenArt.Shape(friend,new TorusMesh { InnerRadius=.14f,OuterRadius=.17f },new(0,.017f,0),Vector3.One,new("92bac2"));
        wake.Hide();
        petSplashes = CreateSplashes(friend);
        friendArt = BuildLifeArt();
        mainSwimSound=SwimAudio.Attach(friend);
        mainName=GardenArt.Sign(friend,pet.Name,new(0,.64f,0),Colors.White);
        cameraRig = new ThirdPersonCamera(); AddChild(cameraRig);
        camera = cameraRig.View; cameraRig.Follow(player.Position, 0, true);
        BuildUi();
        GetTree().AutoAcceptQuit = false;
        Notify(pet.IsEgg ? "Pick up the egg with E. Hold X to rock it, then set it down." : "Welcome back to the garden.", 7);
        if (loadWarning.Length > 0) Notify(loadWarning, 12);
        UpdateUi();
    }

    public override void _Process(double delta)
    {
        float dt = Math.Min((float)delta, .1f);
        if (testMode && ++frame == 12)
        {
            try { RunSmokeChecks(); }
            catch (Exception ex) { GD.PushError("SMOKE_FAILED: " + ex); GetTree().Quit(1); return; }
        }
        if (testMode && frame == 90)
        {
            GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://artifacts/garden.png"));
            GD.Print("SMOKE_OK: scene, interactions, evolution, race, save; screenshot captured");
            GetTree().Quit();
        }
        if (testMode && frame == 45) { SwitchZone(GardenZone.Lobby); SetPreviewPosition(); }
        if (testMode && frame == 55)
        {
            GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://artifacts/lobby.png"));
            SwitchZone(GardenZone.Stadium); SetPreviewPosition();
        }
        if (testMode && frame == 65)
        {
            GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://artifacts/stadium.png"));
            SwitchZone(GardenZone.Garden); SetPreviewPosition();
        }
        editorClock+=dt;
        if (editor!=null && editorClock>=.45f)
        {
            editorClock=0;
            try
            {
                var roster=new Dictionary<string,PetState> { ["main"]=pet };
                foreach (var egg in pet.GardenEggs)
                    if (egg.Hatched && egg.Chao!=null) roster.TryAdd(egg.Id,egg.Chao);
                var edited=editor.Poll(roster,!racing && transition<0 && offerTime<=0 && thinkTime<=0 && eatTime<=0 && !moriInFlight);
                if (edited!=null)
                {
                    if (editor.EditedTarget!="main")
                    {
                        var entry=pet.GardenEggs.Find(egg=>egg.Id==editor.EditedTarget);
                        if (entry!=null)
                        {
                            bool changedArt=entry.Chao?.Form!=edited.Form || entry.Chao?.LifeStage!=edited.LifeStage;
                            entry.Chao=edited;
                            if (changedArt)
                                world.GetChildren().OfType<GardenEgg>().FirstOrDefault(egg=>egg.Data==entry)?.RefreshArt();
                            if (machineResident?.Data==entry) machinePet=edited;
                        }
                    }
                    else
                    {
                        bool changedArt=pet.Form!=edited.Form || pet.LifeStage!=edited.LifeStage;
                        pet=edited;
                        if (machinePet!=null && machineResident==null) machinePet=pet;
                        if (changedArt)
                        {
                            friendArt.QueueFree();
                            friendArt=BuildLifeArt();
                        }
                    }
                    Save();
                    UpdateUi();
                }
            }
            catch (System.IO.IOException ex) { GD.PushWarning(ex.Message); }
        }
        if (paused) return;
        if (transition >= 0) { AdvanceTransition(dt); return; }
        if (zone==GardenZone.Garden)
        {
            pet.GardenSeconds+=dt;
            if (friend.Visible && !pet.IsEgg) pet.AgeSeconds+=dt;
            foreach (var egg in pet.GardenEggs)
                if (egg.Hatched && egg.Chao is { IsEgg:false } chao) chao.AgeSeconds+=dt;
        }
        portalCooldown = Math.Max(0, portalCooldown - dt);
        time += dt; saveTimer += dt; messageTime -= dt;
        gardenSky.Advance((float)pet.GardenSeconds,zone==GardenZone.Garden);
        petCooldown = Math.Max(0, petCooldown - dt);
        dashCooldown = Math.Max(0, dashCooldown - dt);
        dashTime = Math.Max(0, dashTime - dt);
        if (friend.Visible && pet.CanAct) pet.Tick(dt,sleeping || moriIslandNap.IsNapping);
        if (Held(Key.J)) cameraRig.Yaw += dt * 1.8f;
        if (Held(Key.L)) cameraRig.Yaw -= dt * 1.8f;
        if (Held(Key.I)) cameraRig.Pitch = Math.Min(.7f, cameraRig.Pitch + dt);
        if (Held(Key.K)) cameraRig.Pitch = Math.Max(-.08f, cameraRig.Pitch - dt);
        cameraRig.Follow(racing ? friend.Position : player.Position, dt);
        CheckPortals();
        UpdateTrainingAnimals(dt);
        if (racing) UpdateRace(dt);
        if (friend.Visible && zone == GardenZone.Garden && !carrying && !pet.IsEgg && !courtship.IsBlooming(friend) && pet.AdvanceLife(dt))
            OnLifeChanged();
        if (saveTimer >= 20) Save();
        UpdateUi();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (paused || transition >= 0) return;
        if (!racing && !shopOpen && !machineOpen && !machineOpening) MovePlayer((float)delta);
        world.GetNodeOrNull<ToyCar>("ToyCar")?.Advance((float)delta);
        world.GetNodeOrNull<ToyCar>("Rattle")?.Advance((float)delta);
        world.GetNodeOrNull<ToyCar>("Flute")?.Advance((float)delta);
        CanInGarden?.Advance((float)delta);
        UpdatePickupPose((float)delta);
        UpdateEggs((float)delta);
        UpdateOffering((float)delta);
        MoveFriend((float)delta);
        if (zone==GardenZone.Garden) courtship.Advance((float)delta,pet.GardenEggs.Count<12,LayGardenEgg);
        UpdateMachineLoad((float)delta);
        foreach (var tree in world.GetChildren().OfType<FruitTree>())
            foreach(var slot in tree.Slots) slot.UpdateFruit((float)delta);
    }

    private Node3D BuildLifeArt() => pet.CanAct
        ? GardenArt.Creature(friend,false,pet.Form) : GardenArt.LifeShell(friend,pet.IsEgg);

    private static string FormName(string form) => form switch
    {
        "tide" => "Tide", "swift" => "Swift", "stone" => "Stone",
        "comet" => "Comet", "bloom" => "Blossom", _ => "Sprout"
    };

    private void OnLifeChanged()
    {
        friendArt.QueueFree();
        friendArt = BuildLifeArt();
        sleeping = false;
        swimmingTrip = false;
        callTime = 0;
        wanderTime = 0;
        if (pet.InCocoon)
        {
            friend.Position = KeepInGarden(friend.Position);
            wake.Hide();
            Notify("Mori formed a cocoon. Give him a moment to grow.",8);
        }
        else if (pet.Form == "seed")
        {
            recentHatch=null; recentCardTime=8;
            Notify($"The egg hatched! Meet {pet.Name}.",8);
        }
        else Notify($"Mori emerged as {FormName(pet.Form)}!",8);
        Save();
    }

    private static bool Held(Key key) => Input.IsPhysicalKeyPressed(key);
    private static Vector2 MovementAxes() => new(
        (Held(Key.D) || Held(Key.Right) ? 1 : 0) - (Held(Key.A) || Held(Key.Left) ? 1 : 0),
        (Held(Key.S) || Held(Key.Down) ? 1 : 0) - (Held(Key.W) || Held(Key.Up) ? 1 : 0));

    private Vector3 HandPosition()
    {
        Vector3 forward=Vector3.Back.Rotated(Vector3.Up,player.Rotation.Y);
        return avatarWalk?.Grip() is Vector3 grip ? grip+forward*.075f+Vector3.Up*.025f :
            player.Position+new Vector3(0,.86f,.69f).Rotated(Vector3.Up,player.Rotation.Y);
    }

    private Vector3 ThrowImpulse()
    {
        Vector3 direction = cameraRig.RelativeDirection(MovementAxes());
        return direction*6.2f+Vector3.Up*4.6f;
    }

    private void LoadAvatar()
    {
        const string path = "res://Assets/player-city.glb";
        var document = new GltfDocument();
        var state = new GltfState();
        if (document.AppendFromFile(ProjectSettings.GlobalizePath(path),state) != Error.Ok)
        {
            GD.PushError("The new player model could not be loaded.");
            return;
        }
        var scene = document.GenerateScene(state) as Node3D;
        if (scene == null) return;
        foreach (var child in playerArt.GetChildren().OfType<Node3D>()) child.Hide();
        importedAvatar = new Node3D { Name = "ImportedAvatar" };
        playerArt.AddChild(importedAvatar);
        importedAvatar.AddChild(scene);
        avatarWalk = AvatarWalk.FromScene(scene);
        // The avatar's bind-pose soles are 8.6 cm above its authored origin.
        importedAvatar.Position=Vector3.Down*.086f;
    }

    private void MovePlayer(float dt)
    {
        Vector3 before = player.Position;
        Vector2 axes = MovementAxes();
        Vector3 direction = cameraRig.RelativeDirection(axes);
        // Скорость — первое удобное место для твоих экспериментов с кодом.
        float speed = dashTime > 0 ? 10 : Held(Key.Shift) ? 5.5f : 3.7f;
        if (carrying) speed *= .7f;
        bool inWater=zone==GardenZone.Garden && GardenArt.IsPondWater(player.Position);
        playerWake.Visible = inWater && player.Position.Y<.16f;
        playerWake.Scale = Vector3.One*(1+.12f*Mathf.Sin(time*7));
        playerWake.Position = new(0,.017f-player.Position.Y,0);
        if (inWater) speed *= .63f;
        if (machineLoad>0 || pickingItem || (carrying && pickupTime<PickupDuration) || (releasePose>0 && !throwingPose) || pettingTime>0 || treeReachTime>0) speed = 0;
        Vector3 target = KeepInGarden(player.Position + direction*speed*dt,allowWater:true);
        Vector3 horizontal = new(target.X-player.Position.X,0,target.Z-player.Position.Z);
        var wall = horizontal.LengthSquared()>.000001f ? player.MoveAndCollide(horizontal) : null;
        if (wall!=null)
        {
            var normal=wall.GetNormal();
            bool noStep=wall.GetCollider() is GodotObject body && body.HasMeta("no_step");
            if (noStep) normal=new Vector3(normal.X,0,normal.Z).Normalized();
            if (noStep || normal.Y>=.6f || !TryStepUp(wall.GetRemainder()))
                player.MoveAndCollide(wall.GetRemainder().Slide(normal));
        }

        float fallingSpeed=verticalSpeed;

        const float floorSnap=.085f;
        var support = verticalSpeed<=0 ? player.MoveAndCollide(Vector3.Down*floorSnap,testOnly:true) : null;
        bool grounded = support!=null && support.GetNormal().Y>.58f;
        if (grounded && verticalSpeed==0)
        {
            if (support!.GetTravel().LengthSquared()>.000016f) player.MoveAndCollide(Vector3.Down*floorSnap);
        }
        else
        {
            verticalSpeed -= 18*dt;
            var impact=player.MoveAndCollide(Vector3.Up*verticalSpeed*dt);
            if (impact!=null)
            {
                grounded=verticalSpeed<=0 && impact.GetNormal().Y>.58f;
                verticalSpeed=0;
            }
        }
        if (direction.LengthSquared() > 0 && speed>0)
            player.Rotation = new(0, Mathf.LerpAngle(player.Rotation.Y, Mathf.Atan2(direction.X, direction.Z), 1-Mathf.Exp(-15*dt)), 0);
        float speedNow = FlatDistance(before,player.Position)/Math.Max(dt,.001f);
        if (grounded) groundHold=.065f;
        else groundHold=verticalSpeed>0 ? 0 : Math.Max(0,groundHold-dt);
        avatarWalk?.Step(dt,speedNow,grounded || groundHold>0,carrying,verticalSpeed);
        playerArt.Position=new(0,Mathf.Lerp(playerArt.Position.Y,-.47f*offerLean,1-Mathf.Exp(-12*dt)),0);
        bool wetNow=zone==GardenZone.Garden && GardenArt.IsPondWater(player.Position);
        bool wading=wetNow && grounded && speedNow>.2f;
        SetSplashes(playerSplashes,wading,player.Position.Y);
        SwimAudio.Wade(wadeSound,wading ? FlatDistance(before,player.Position) : 0,wetNow && grounded,ref wadeStride);
        if (wetNow && before.Y>.025f && player.Position.Y<=.025f && fallingSpeed<-5.8f)
        {
            var drift=player.Position-before; drift.Y=0;
            impactSpray.Position=new(0,.025f-player.Position.Y,0);
            impactSpray.Direction=player.GlobalBasis.Inverse()*(Vector3.Up+drift.Normalized()*.48f).Normalized();
            impactSpray.Restart();
            impactSpray.Emitting=true;
            impactSound.Play();
        }

    }

    private static CpuParticles3D CreateSplashes(Node3D actor)
    {
        var drops=new CpuParticles3D
        {
            Emitting=false, LocalCoords=false, Amount=36, Lifetime=.38f,
            EmissionShape=CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents=new(.13f,.01f,.16f),
            Direction=Vector3.Up, Spread=59, Gravity=new(0,-6,0),
            InitialVelocityMin=.42f, InitialVelocityMax=1.42f,
            Mesh=new SphereMesh { Radius=.023f, Height=.046f, RadialSegments=6,Rings=3,
                Material=GardenArt.Material(new Color("a8ccd2")) }
        };
        actor.AddChild(drops);
        return drops;
    }

    private static void SetSplashes(CpuParticles3D drops,bool active,float feetY)
    {
        drops.Position=new(0,.025f-feetY,0);
        drops.Emitting=active;
    }

    private void PoseChao(float dt,bool moving,bool swimming)
    {
        if (moving) chaoGait+=dt*(swimming ? 8.5f : 12);
        ImportedChao.Gait(friendArt,chaoGait,moving,swimming);
    }

    private bool TryStepUp(Vector3 motion)
    {
        const float height = .43f;
        if (verticalSpeed != 0 || motion.LengthSquared()<.00001f) return false;
        Vector3 rise = Vector3.Up*height;
        // Probe far enough for the capsule to find support beyond the vertical edge.
        motion += motion.Normalized()*.28f;
        var elevated = player.GlobalTransform;
        elevated.Origin += rise;
        if (player.TestMove(player.GlobalTransform,rise) || player.TestMove(elevated,motion)) return false;
        elevated.Origin += motion;
        var support = new KinematicCollision3D();
        if (!player.TestMove(elevated,Vector3.Down*(height+.09f),support) || support.GetNormal().Y<.55f)
            return false;
        player.MoveAndCollide(rise);
        player.MoveAndCollide(motion);
        player.MoveAndCollide(Vector3.Down*(height+.09f));
        return true;
    }

    private void UpdatePickupPose(float dt)
    {
        bool picking=carrying || pickingItem;
        if (picking) pickupTime = Math.Min(PickupDuration,pickupTime+dt);
        float t = picking ? pickupTime/PickupDuration : 0;
        if (picking && t<.46f)
        {
            Vector3 approach=pickupStart-player.Position;
            approach.Y=0;
            float distance=Mathf.Clamp(approach.Length()-.89f,0,Mathf.Min(dt,.05f)*4.6f);
            if (distance>0) player.MoveAndCollide(approach.Normalized()*distance);
        }
        float crouch = picking ? Mathf.Sin(t*Mathf.Pi) : 0;
        float bend = crouch*.22f;
        if (pickingItem && pickupTime>=PickupDuration) pickingItem=false;
        bool handsFull = carrying || heldAnimal != null || heldFruit != null || heldEgg!=null || heldToy!=null;
        if (releasePose>0)
        {
            releasePose = Math.Max(0,releasePose-dt);
            float progress = 1-releasePose/SetDownDuration;
            crouch=throwingPose ? 0 : Mathf.Sin(progress*Mathf.Pi);
            bend = Mathf.Sin(progress*Mathf.Pi) * (throwingPose ? -.55f : .22f);
            if (loweringMori)
            {
                friend.Position=setDownFrom.Lerp(setDownTo,Mathf.SmoothStep(0,1,Mathf.Min(1,progress*2)));
                if (progress>=.5f) moriDistress.PutDown();
                if (releasePose==0 && !mainEggCracked && pet.CanAct && zone==GardenZone.Garden)
                    courtship.PutDown(friend);
                if (releasePose==0 && mainEggCracked && pet.Hatch()) { GardenEgg.ShellBurst(friend,GardenEgg.Pigment("White")); OnLifeChanged(); }
            }
            if (releaseObject!=null && progress>=.5f)
            {
                var putDown=releaseObject;
                releaseObject=null;
                putDown();
            }
        }
        pettingTime = Math.Max(0,pettingTime-dt);
        float treeGrip=0, treeSway=0;
        if (treeReachTime>0 && shakingTree!=null)
        {
            float previous=TreeActionDuration-treeReachTime;
            treeReachTime=Math.Max(0,treeReachTime-dt);
            float elapsed=TreeActionDuration-treeReachTime;
            if (elapsed<.28f)
            {
                var offset=shakingTree.Position-player.Position; offset.Y=0;
                player.MoveAndCollide(offset.Normalized()*Mathf.Clamp(offset.Length()-1.24f,0,2.6f*dt));
            }
            if (previous<.48f && elapsed>=.48f) shakingTree.Shake();
            treeGrip=Mathf.SmoothStep(0,1,Mathf.Clamp((elapsed-.28f)/.20f,0,1))*
                Mathf.SmoothStep(0,1,Mathf.Clamp(treeReachTime/.46f,0,1));
            treeSway=shakingTree.Sway;
            if (treeReachTime<=0) shakingTree=null;
        }
        float reach = pettingTime>0 ? Mathf.Sin((1-pettingTime/.95f)*Mathf.Pi) : 0;
        float stroke = Mathf.Sin(pettingTime*26);
        if (offerTime<=0) offerLean=Mathf.MoveToward(offerLean,0,dt*2.8f);
        playerArt.Rotation = new(throwingPose && releasePose>0 ? .30f*bend : 0,0,0);
        avatarWalk?.UpperBody(.52f*reach+.30f*treeGrip+.42f*crouch+.48f*offerLean,treeGrip*treeSway*.10f);
        if (offerLean>0 || crouch>0) avatarWalk?.Crouch(Mathf.Max(offerLean,crouch));
        if (picking || releasePose>0) playerArt.Position=new(0,-.34f*Mathf.Max(offerLean,crouch),0);
        bool shaking=Held(Key.X) && (carrying && pet.IsEgg || heldEgg is { Data.Hatched:false });
        float shake=shaking ? Mathf.Sin(time*38)*.73f : 0;
        avatarWalk?.Arms(handsFull && offerTime<=0 && eatTime<=0 && thinkTime<=0,
            Math.Max(reach,offerTime>0 ? .92f : machineLoad>.36f ? 1 : 0),stroke,shake);
        var acrossHands=Vector3.Right.Rotated(Vector3.Up,player.Rotation.Y);
        if (picking && t<.72f && crouch>0)
            avatarWalk?.ReachWithHands(pickupStart+Vector3.Up*.09f,acrossHands,crouch,.08f);
        if (!throwingPose && releasePose>0 && crouch>0)
        {
            float progress=1-releasePose/SetDownDuration;
            float reachDown=crouch*(1-Mathf.SmoothStep(.5f,.83f,progress));
            avatarWalk?.ReachWithHands(setDownTo+Vector3.Up*.18f,acrossHands,reachDown,.08f);
        }
        if (treeGrip>0 && shakingTree!=null)
        {
            Vector3 front=Vector3.Back.Rotated(Vector3.Up,player.Rotation.Y);
            Vector3 across=Vector3.Right.Rotated(Vector3.Up,player.Rotation.Y);
            avatarWalk?.ReachWithHands(shakingTree.Position-front*.58f+new Vector3(-treeSway*.052f,1.17f,0),
                across,treeGrip,.19f);
        }

    }

    private Vector3 KeepInGarden(Vector3 point, bool allowWater = false)
    {
        point.Y = 0;
        if (zone != GardenZone.Garden)
            return point.Length() > 11.7f ? point.Normalized()*11.7f : point;
        point.X = Mathf.Clamp(point.X,-GardenArt.GardenHalfWidth,GardenArt.GardenHalfWidth);
        point.Z = Mathf.Clamp(point.Z,-GardenArt.GardenHalfDepth,GardenArt.GardenHalfDepth);
        Vector3 pond = GardenArt.Pond;
        Vector3 fromPond = point - pond;
        if (!allowWater && fromPond.Length() < GardenArt.PondRadius+.3f)
            point = pond + (fromPond.LengthSquared() < .001f ? Vector3.Right : fromPond.Normalized()) * (GardenArt.PondRadius+.3f);
        foreach (var tree in GardenArt.TreePositions) point = AvoidCircle(point, tree, .90f);
        point = AvoidCircle(point, kitchen, 1.35f);
        return point;
    }

    private static Vector3 AvoidCircle(Vector3 point, Vector3 center, float radius)
    {
        Vector3 offset = point-center; offset.Y=0;
        return offset.Length() >= radius ? point : center + (offset.LengthSquared() < .001f ? Vector3.Right : offset.Normalized()) * radius;
    }

    private void MoveFriend(float dt)
    {
        if (!friend.Visible || racing || courtship.IsPaired(friend)) return;
        upsetTime=Math.Max(0,upsetTime-dt);
        moriDistress.Tick(dt);
        if (friendArt.FindChild("MoodOrb",true,false) is EmotionOrb moodOrb)
        {
            moodOrb.SetUpset(upsetTime>0);
            moodOrb.SetDistressed(moriDistress.Upset);
        }
        wake.Hide();
        petSplashes.Emitting=false;
        if (machineOpen && machinePet==pet) return;
        if (moriInFlight)
        {
            if (tossedMori && pet.CanAct)
            {
                if (moriDistress.ThrowStep(friend,friendArt,ref throwVelocity,dt,zone==GardenZone.Garden))
                {
                    moriInFlight=tossedMori=false;
                    wanderTarget=friend.Position;
                    wanderTime=3;
                    Save();
                }
                return;
            }
            moriInFlight = !ThrowMotion.Step(friend,ref throwVelocity,dt,hit=>
            {
                if (hit.GetCollider() is not GodotObject body || !body.HasMeta("stone")) return;
                if (pet.IsEgg) mainEggCracked=true;
                else if (upsetTime<10)
                {
                    upsetTime=12;
                    pet.Joy=Math.Max(0,pet.Joy-8);
                    Notify("Mori bumped into a rock and is upset.");
                    Save();
                }
            });
            friendArt.Rotation = moriInFlight ? new(.22f,0,Mathf.Sin(time*13)*.30f) : Vector3.Zero;
            if (!moriInFlight)
            {
                tossedMori=false;
                wanderTarget=friend.Position; wanderTime=3;
                if (mainEggCracked && pet.Hatch()) { GardenEgg.ShellBurst(friend,GardenEgg.Pigment("White")); OnLifeChanged(); }
            }
            return;
        }
        if (releasePose > 0 && loweringMori) return;
        if ((offerTime>0 || eatTime>0 || thinkTime>0) && offeredResident==null) return;
        if (pettingTime > 0)
        {
            friendArt.Rotation = new(0,0,Mathf.Sin(pettingTime*22)*.10f);
            return;
        }
        if (carrying)
        {
            Vector3 hands=HandPosition()-Vector3.Up*(pet.IsEgg ? .20f : .15f);
            friend.Position = PickupPoint(hands);
            friend.Rotation = player.Rotation;
            friendArt.Position = Vector3.Zero;
            if (pet.IsEgg && Held(Key.X))
            {
                mainEggShake+=dt;
                friendArt.Rotation=new(0,0,Mathf.Sin(mainEggShake*38)*.29f);
            }
            else friendArt.Rotation=Vector3.Zero;
            return;
        }
        if (!pet.CanAct || moriDistress.HoldPose(friendArt,pet,dt)) return;
        moriVoice.Step(friendArt,pet,sleeping || moriIslandNap.IsNapping,dt);
        if (world.GetNodeOrNull<ToyCar>("ToyCar")?.PlayPose(friend,friendArt)==true ||
            world.GetNodeOrNull<ToyCar>("Rattle")?.PlayPose(friend,friendArt)==true ||
            world.GetNodeOrNull<ToyCar>("Flute")?.PlayPose(friend,friendArt)==true) return;
        if (!chaoMotion.IsSwimmingAt(friend.Position) && chaoMotion.Recover(friendArt,dt)) return;
        if (sleeping)
        {
            ImportedChao.SleepPose(friendArt,ChaoRest.MinimumSleep-napRemaining);
            napRemaining-=dt;
            if (napRemaining<=0 && pet.Energy>=100)
            {
                sleeping=false; moriRest.Cancel(); wanderTime=0;
                chaoMotion.LeapFromBed(friend,pet);
            }
            return;
        }

        if (zone==GardenZone.Garden && !chaoMotion.Airborne && courtship.Sit(friend,friendArt,pet,"White",world,dt))
        {
            moriRest.Cancel();
            return;
        }
        if (CanInGarden?.Step(friend,friendArt,pet,chaoMotion,dt,
            callTime<=0 && !moriRest.Requested && !moriIslandNap.IsNapping && !swimmingTrip)==true) return;
        if (zone==GardenZone.Garden && !chaoMotion.LeavingPond && moriFruitReaction.Step(friend,friendArt,pet,chaoMotion,FruitCarrier,dt)) return;
        if (zone==GardenZone.Garden && !chaoMotion.LeavingPond && callTime<=0 && !moriRest.Requested &&
            moriIslandNap.Step(friend,friendArt,pet,chaoMotion,dt)) return;
        Vector3 beforeRest=friend.Position;
        if (!chaoMotion.Airborne && !chaoMotion.LeavingPond && !chaoMotion.IsSwimmingAt(friend.Position) && moriRest.Step(friend,pet,dt,out bool arrived))
        {
            PoseChao(dt,!arrived && FlatDistance(beforeRest,friend.Position)>dt*.075f,false);
            friendArt.Rotation=Vector3.Zero;
            friendArt.Position=Vector3.Zero;
            if (arrived) { sleeping=true; napRemaining=ChaoRest.MinimumSleep; ImportedChao.SleepPose(friendArt); }
            return;
        }
        bool startCliff=zone==GardenZone.Garden && callTime<=0 && !swimmingTrip && wanderTime<=0 &&
            (pet.Fly>=120 ? activity%4==3 : activity%6==4);
        if (startCliff) activity++;
        Vector3? cliff=zone==GardenZone.Garden && callTime<=0 && !swimmingTrip
            ? chaoMotion.CliffTarget(friend.Position,dt,startCliff) : null;
        if (cliff!=null) wanderTime=3;
        bool inWater = zone == GardenZone.Garden && chaoMotion.IsSwimmingAt(friend.Position);
        wake.Visible = inWater;
        wake.Scale = new Vector3(1,1,1)*(1 + .14f*Mathf.Sin(time*4));
        callTime = Math.Max(0,callTime-dt);
        following = callTime > 0 && FlatDistance(friend.Position,player.Position)>1.25f;
        if (!following) callTime = 0;

        Vector3 target;
        if (following) target = player.Position;
        else if (chaoMotion.LeavingPond && !swimmingTrip) target=GardenArt.SwimExit;
        else if (cliff is Vector3 rockTarget) target=rockTarget;
        else if (swimmingTrip)
        {
            Vector3 shallows = GardenArt.Pond + new Vector3(3,0,2);
            if (inWater) swimTime += dt;
            if (!leavingWater && (swimTime >= (pet.Swim<100 ? 1.1f : 5) || FlatDistance(friend.Position,shallows)<.45f))
            {
                leavingWater = true;
                if (swimTime > 1 && pet.Swim>=100) pet.Swim = Math.Min(990,pet.Swim+1);
            }
            target = leavingWater ? GardenArt.SwimExit : shallows;
            if (leavingWater && !inWater && FlatDistance(friend.Position,target)<.55f)
            {
                swimmingTrip = false;
                wanderTarget = friend.Position;
                wanderTime = 4;
            }
        }
        else
        {
            if (zone==GardenZone.Garden && pet.Power>=100 && pet.Fullness<72 && heldFruit==null)
            {
                var shakeable=world.GetChildren().OfType<FruitTree>().FirstOrDefault(tree=>
                    !tree.IsFeeder && !tree.HasFruitOnGround && tree.CanInteract &&
                    FlatDistance(friend.Position,tree.Position)<1.55f);
                if (shakeable!=null) shakeable.Shake();
            }
            var fruit = zone==GardenZone.Garden && pet.Fullness<76 && pet.FindsFood
                ? FruitSlots().Where(slot=>!slot.IsFeeder && slot.HasFruitOnGround &&
                    FlatDistance(slot.FruitPosition,friend.Position)<12)
                    .OrderBy(slot=>FlatDistance(slot.FruitPosition,friend.Position)).FirstOrDefault()
                : null;
            if (fruit != null)
            {
                target = fruit.FruitPosition;
                if (FlatDistance(target,friend.Position)<.52f && heldFruit==null && heldAnimal==null && heldToy==null)
                {
                    selfFoodStart=fruit.FruitPosition;
                    fruit.Hold(selfFoodStart);
                    heldFruit=fruit;
                    selfEating=true;
                    fruitMeal.Begin(fruit,pet,selfFoodStart);
                    eatTime=fruitMeal.Duration;
                    return;
                }
            }
            else
            {
                wanderTime -= dt;
                if (wanderTime <= 0)
                {
                    activity++;
                    if (zone == GardenZone.Garden && (pet.Energy<42 || (activity%5==0 && pet.Energy<96)))
                    {
                        moriRest.Request(friend.Position);
                        if (!inWater && !chaoMotion.LeavingPond) return;
                    }
                    if (zone == GardenZone.Garden && activity%4==2)
                    {
                        swimmingTrip = true;
                        leavingWater = false;
                        swimTime = 0;
                    }
                    wanderTarget = activity%2==1 ? friend.Position : KeepInGarden(friend.Position +
                        new Vector3(Mathf.Sin(time)*4,0,Mathf.Cos(time*.7f)*4));
                    wanderTime = activity%2==1 ? 3 : 5;
                }
                target = wanderTarget;
            }
        }

        if (!inWater && !following && !swimmingTrip && cliff==null && pet.Fullness>=76 &&
            world.GetNodeOrNull<ToyCar>("ToyCar") is { } toy && toy.Available(friend,pet))
        {
            if (toy.Use(friend,friendArt,pet,out var toyTarget)) return;
            target=toyTarget;
        }
        target=chaoMotion.RouteOffUpperPlatform(friend.Position,target);
        var difference=FlatDistance(target,friend.Position)<.17f ? Vector3.Zero :
            KeepInGarden(target,allowWater:swimmingTrip || inWater || cliff!=null || following)-friend.Position;
        difference.Y=0;
        if (cliff==null && !inWater) difference=ChaoMotion.AvoidNeighbors(friend,difference);
        bool moving=chaoMotion.Step(friend,difference,pet,false,inWater,dt,zone==GardenZone.Garden,out bool hardLanding);
        if (!inWater && !chaoMotion.LeavingPond && chaoMotion.BlockedTime>.7f && !following && cliff==null)
        {
            if (swimmingTrip && !leavingWater) leavingWater=true;
            else { swimmingTrip=false; wanderTime=0; }
        }
        inWater=zone==GardenZone.Garden && chaoMotion.IsSwimmingAt(friend.Position);
        wake.Visible=inWater;
        if (hardLanding)
        {
            upsetTime=9;
            pet.Joy=Math.Max(0,pet.Joy-5);
        }
        if (moving || inWater) chaoGait+=dt*Mathf.Clamp(chaoMotion.GroundSpeed*9,4,19);
        ImportedChao.MovePose(friendArt,chaoGait,moving,inWater,pet.Swim>=100,chaoMotion.Sprinting,chaoMotion.Airborne,chaoMotion.Flying);
        SetSplashes(petSplashes,inWater && (moving || pet.Swim<100),friend.Position.Y);
        SwimAudio.Paddle(mainSwimSound,dt,inWater && (moving || pet.Swim<100),ref mainSwimBeat);
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;
        Key code = key.PhysicalKeycode;
        if (machineOpen || machineOpening)
        {
            if (code==Key.Escape) CloseMachine();
            return;
        }
        if (shopOpen)
        {
            if (code==Key.Escape || code==Key.E) shopOpen=false;
            else if (code==Key.Left || code==Key.A) shopColor=(shopColor+ShopChoices-1)%ShopChoices;
            else if (code==Key.Right || code==Key.D) shopColor=(shopColor+1)%ShopChoices;
            else if (code==Key.Enter) BuyShopItem();
            UpdateUi();
            return;
        }
        if (code == Key.Escape)
        {
            paused = !paused; Save(); UpdateUi(); return;
        }
        if (paused || transition >= 0 || thinkTime>0) return;
        if (code == Key.M && music != null) music.StreamPaused = !music.StreamPaused;
        if (code == Key.B) showBag = !showBag;
        if (code == Key.G && CanStoreFruit())
        {
            if (heldFruit!.IsHeartFruit) pet.BagHeartFruit++;
            else pet.BagFruit++;
            heldFruit.Regrow(); heldFruit=null;
            Notify("Fruit stored in the bag."); Save();
        }
        if ((code==Key.H || code==Key.V) && CanOfferBagFruit(code==Key.V))
        {
            bool heart=code==Key.V;
            var fruit = new FruitTree { IsFeeder=true, SingleUse=true, IsHeartFruit=heart, Position=player.Position };
            world.AddChild(fruit);
            heldFruit=fruit;
            fruit.Hold(HandPosition());
            if (heart) pet.BagHeartFruit--;
            else pet.BagFruit--;
            Notify(heart ? "Face a Chao to offer the Heart Fruit." : "Face a Chao to offer the bag fruit.");
            Save();
        }
        if (code == Key.Tab) showStats = !showStats;
        if (code == Key.C) cameraRig.Yaw = player.Rotation.Y - Mathf.Pi;
        if (code == Key.Space && verticalSpeed <= 0)
        {
            var floor = player.MoveAndCollide(Vector3.Down*.08f, testOnly:true);
            if (floor != null && floor.GetNormal().Y > .6f) verticalSpeed = 7;
        }
        if (code == Key.Ctrl && dashCooldown <= 0) { dashTime = .3f; dashCooldown = 2; }
        if (code == Key.E) Interact();
        if (code == Key.F) PetFriend();
        if (code == Key.Q)
        {
            if (racing) { Cheer(); return; }
            if (zone==GardenZone.Garden)
                foreach(var resident in world.GetChildren().OfType<GardenEgg>()) resident.Call(player.Position);
            if (!friend.Visible || carrying || !pet.CanAct || offerTime>0 || eatTime>0) return;
            if (!pet.RespondsToCall)
            {
                Notify("Mori is not ready to come when called. Try feeding or hugging him.");
                return;
            }
            CanInGarden?.Cancel(friend);
            if (sleeping) chaoMotion.LeapFromBed(friend,pet);
            moriRest.Cancel(); moriIslandNap.Cancel(friendArt); ImportedChao.CloseEyes(friendArt,false);
            sleeping = false; following = true; callTime = 12;
            swimmingTrip = false;
            wanderTarget = friend.Position; wanderTime = 2;
            Notify("Mori heard your call.");
        }
        if (code == Key.R && heldEgg==null && pet.CanAct && offerTime<=0 && eatTime<=0)
        {
            if (zone != GardenZone.Garden || FlatDistance(player.Position, bed) > 2.5f) Notify("Find the resting spot in the garden.");
            else if (racing) Notify("Finish the race first.");
            else
            {
                if (!carrying) moriRest.Request(friend.Position);
                following=false; callTime=0; swimmingTrip=false;
                Notify("Mori is heading to the resting spot.");
            }
        }
        GetViewport().SetInputAsHandled();
    }

    private bool CanReachMeal(Vector3 position)
    {
        var toward=position-player.Position;
        toward.Y=0;
        return player.Position.DistanceTo(position)<1.35f && toward.LengthSquared()>.04f &&
            Vector3.Back.Rotated(Vector3.Up,player.Rotation.Y).Dot(toward.Normalized())>.7f;
    }

    private GardenEgg? ResidentMeal() => world.GetChildren().OfType<GardenEgg>()
        .FirstOrDefault(egg=>egg.HasMeal && CanReachMeal(egg.Position));

    private bool CanTakeMeal() =>
        zone==GardenZone.Garden && !carrying && heldEgg==null && heldToy==null && heldAnimal==null &&
        !pickingItem && releasePose<=0 && offerTime<=0 && thinkTime<=0 && !machineOpen && !machineOpening && machineLoad<=0 &&
        (eatTime>0 && heldFruit!=null ? CanReachMeal(offeredResident?.Position ?? friend.Position)
            : heldFruit==null && ResidentMeal()!=null);

    private void TakeMeal()
    {
        if (eatTime>0 && heldFruit!=null)
        {
            (offeredResident?.Chao ?? pet).ChangeRelationship(-.65f);
            var art=offeredResident?.Art ?? friendArt;
            (art.FindChild("MoodOrb",true,false) as EmotionOrb)?.SetMealUpset(false);
            if (offeredResident!=null) offeredResident.ShowUpset();
            else upsetTime=5;
            eatTime=0;
            FinishOffer();
            selfEating=false;
        }
        else heldFruit=ResidentMeal()?.TakeMeal();
        if (heldFruit==null) return;
        BeginPickup(heldFruit.FruitPosition);
        Save();
    }

    private void Interact()
    {
        if (CanTakeMeal())
        {
            TakeMeal();
            return;
        }
        if (pickingItem || machineOpening || machineLoad>0 || treeReachTime>0 || releasePose>0 || moriInFlight || offerTime>0 || thinkTime>0 || eatTime>0) return;
        bool throwItem = MovementAxes().LengthSquared()>0;
        if (heldEgg!=null)
        {
            ReleaseObject(heldEgg.Position,throwItem,impulse=>
            {
                if (!throwItem) heldEgg.Position=setDownTo;
                heldEgg.Release(impulse,throwItem);
                heldEgg=null;
            });
            return;
        }
        if (heldToy!=null)
        {
            ReleaseObject(heldToy.Position,throwItem,impulse=>
            {
                if (!throwItem) heldToy.Hold(setDownTo);
                heldToy.Toss(impulse);
                heldToy=null;
            });
            return;
        }
        if (zone==GardenZone.Lobby && FlatDistance(player.Position,ZoneArt.PlazaShop)<2.5f && !carrying)
        { shopOpen=true; return; }
        if (CanOpenEmptyMachine()) { OpenMachine(null,null); return; }
        if (!carrying && heldAnimal==null && heldFruit==null && NearbyToy() is { } toy)
        {
            heldToy=toy;
            if (toy.TakeFromPlay()) Save();
            BeginPickup(toy.Position);
            toy.Hold(pickupStart);
            Notify($"Face a Chao to offer the {toy.ItemName}.");
            return;
        }
        if (!carrying && heldAnimal==null && heldFruit==null && NearbyEgg() is GardenEgg egg)
        {
            heldEgg=egg;
            BeginPickup(egg.Position);
            Notify(egg.Data.Hatched ? $"You picked up {egg.Chao?.Name}." : "Hold X to rock the egg. E to set it down or throw while moving.");
            return;
        }
        if (carrying && throwItem)
        {
            ReleaseMori(true);
            return;
        }
        if (heldAnimal != null && throwItem)
        {
            heldAnimal.Throw(ThrowImpulse());
            StartReleasePose(true);
            heldAnimal = null;
            Notify("Animal thrown.");
            return;
        }
        if (heldFruit != null)
        {
            ReleaseObject(heldFruit.FruitPosition,throwItem,impulse=>
            {
                if (!throwItem) heldFruit.Place(setDownTo+Vector3.Up*.18f);
                else heldFruit.Release(impulse);
                heldFruit=null;
            });
            Notify(throwItem ? "Fruit thrown." : "You set down the fruit.");
            return;
        }
        if (racing) { FinishRace(0,cancelled:true); Notify("Race cancelled."); return; }
        if (friend.Visible && !pet.CanAct && FlatDistance(player.Position,friend.Position)<2.6f)
        {
            if (pet.IsEgg && !carrying)
            {
                carrying=true; pickupStart=friend.Position; pickupTime=0; mainEggShake=0;
                Face(friend.Position);
                Notify("Hold X to rock the egg. E to set it down.");
            }
            else if (pet.IsEgg) ReleaseMori(false);
            else Notify("Mori is still growing inside the cocoon.");
            return;
        }
        if (zone == GardenZone.Stadium && FlatDistance(player.Position, new(0,0,5)) < 2.5f) { carrying = false; StartRace(); return; }
        if (heldAnimal != null)
        {
            ReleaseObject(heldAnimal.Position,false,_=>
            {
                heldAnimal.Position=setDownTo;
                heldAnimal.ReleaseHere(setDownTo.Y);
                heldAnimal=null;
            });
            Notify("You set the animal down here.");
            return;
        }
        if (!carrying && NearbyAnimal() is TrainingAnimal animal)
        {
            heldAnimal = animal;
            BeginPickup(animal.Position);
            Notify($"Picked up {animal.AnimalName}. Bring it to Mori and press E.");
            return;
        }
        if (zone == GardenZone.Garden && !carrying && NearbyTree() is FruitTree tree)
        {
            if (!tree.HasFruitOnGround)
            {
                shakingTree=tree;
                treeReachTime=TreeActionDuration;
                Face(tree.Position);
            }
            else
            {
                heldFruit = tree;
                BeginPickup(tree.FruitPosition);
                heldFruit.Hold(pickupStart);
                Notify("Face Mori to offer the fruit.");
            }
            return;
        }
        if (carrying)
        {
            ReleaseMori(false);
            return;
        }
        if (friend.Visible && pet.CanAct && !courtship.IsPaired(friend) && FlatDistance(player.Position, friend.Position) < 2.2f)
        {
            CanInGarden?.Cancel(friend);
            courtship.Suspend(friend);
            moriDistress.PickUp(friendArt,sleeping || moriIslandNap.IsNapping);
            sleeping = false;
            moriRest.Cancel();
            moriIslandNap.Cancel(friendArt);
            ImportedChao.CloseEyes(friendArt,false);
            moriFruitReaction.Interrupt(friendArt);
            carrying = true;
            pickupStart = friend.Position;
            pettingTime = 0;
            pickupTime = 0;
            Vector3 toward = friend.Position-player.Position;
            player.Rotation = new(0,Mathf.Atan2(toward.X,toward.Z),0);
            Notify("You picked up Mori. Press E to set him down or carry him through an arch."); return;
        }
        Notify("Press E near Mori to pick him up. Walk through an arch to change areas.");
    }

    private System.Collections.Generic.IEnumerable<FruitTree> FruitSlots() =>
        world.GetChildren().OfType<FruitTree>().SelectMany(tree=>tree.Slots)
            .Where(slot=>!slot.IsQueuedForDeletion());

    private FruitTree? NearbyTree()
    {
        var trees=world.GetChildren().OfType<FruitTree>().Where(tree => !tree.IsQueuedForDeletion());
        return FruitSlots().Where(tree => tree.HasFruitOnGround && player.Position.DistanceTo(tree.FruitPosition)<2.05f)
            .OrderBy(tree => player.Position.DistanceSquaredTo(tree.FruitPosition)).FirstOrDefault()
            ?? trees.FirstOrDefault(tree => tree.CanShake &&
                FlatDistance(player.Position,tree.Position)<2.35f);
    }

    private void StartReleasePose(bool toss)
    {
        releasePose = SetDownDuration;
        throwingPose = toss;
        loweringMori = false;
    }

    private void BeginPickup(Vector3 at)
    {
        pickupStart=at;
        pickupTime=0;
        pickingItem=true;
        Face(at);
    }

    private Vector3 PickupPoint(Vector3 hands)
    {
        if (pickupTime<PickupDuration) hands.Y=Mathf.Max(hands.Y,pickupStart.Y);
        return pickupStart.Lerp(hands,
            Mathf.SmoothStep(0,1,Mathf.Clamp((pickupTime/PickupDuration-.38f)/.62f,0,1)));
    }

    private Vector3 CarryPoint(Vector3 hands, float height=0)
    {
        if (pickingItem) return PickupPoint(hands);
        if (releaseObject==null) return hands;
        float progress=1-releasePose/SetDownDuration;
        return setDownFrom.Lerp(setDownTo+Vector3.Up*height,Mathf.SmoothStep(0,1,Mathf.Min(1,progress*2)));
    }

    private void ReleaseObject(Vector3 at,bool toss,Action<Vector3> release)
    {
        StartReleasePose(toss);
        if (toss) { release(ThrowImpulse()); return; }
        setDownFrom=at;
        var ray=PhysicsRayQueryParameters3D.Create(at+Vector3.Up*.25f,at+Vector3.Down*4,1);
        var hit=GetWorld3D().DirectSpaceState.IntersectRay(ray);
        setDownTo=new(at.X,hit.Count>0 ? ((Vector3)hit["position"]).Y : player.Position.Y,at.Z);
        if (heldFruit!=null && FlatDistance(at,kitchen)<1.2f)
            setDownTo=new(at.X,.375f,at.Z);
        releaseObject=()=>release(Vector3.Zero);
    }

    private void ReleaseMori(bool toss)
    {
        carrying = false;
        chaoMotion.Reset();
        tossedMori=toss;
        StartReleasePose(toss);
        loweringMori = !toss;
        following = false; callTime = 0; wanderTime = 3;
        setDownFrom = friend.Position;
        setDownTo = KeepInGarden(friend.Position,allowWater:true);
        var ray = PhysicsRayQueryParameters3D.Create(setDownTo+Vector3.Up*(friend.Position.Y+1),setDownTo+Vector3.Down*3,1);
        var ground = GetWorld3D().DirectSpaceState.IntersectRay(ray);
        if (ground.Count>0) setDownTo.Y=((Vector3)ground["position"]).Y;
        wanderTarget = setDownTo;
        moriInFlight = toss;
        mainEggCracked = pet.IsEgg && !toss && mainEggShake>5;
        mainEggShake=0;
        if (toss)
        {
            throwVelocity = ThrowImpulse();
            if (pet.CanAct)
            {
                moriDistress.BeginThrow();
                pet.ChangeRelationship(-1.25f);
                Save();
            }
            pet.Joy = Math.Max(0,pet.Joy-4);
            pet.Bond = Math.Max(0,pet.Bond-1);
        }
        Notify(toss ? "Mori was thrown." : "You set Mori down.");
    }

    private void UpdateOffering(float dt)
    {
        bool walking=MovementAxes().LengthSquared()>.01f;
        bool Faces(Vector3 at,float range)
        {
            Vector3 delta=at-player.Position; delta.Y=0;
            Vector3 forward=Vector3.Back.Rotated(Vector3.Up,player.Rotation.Y);
            return player.Position.DistanceTo(at)<range && delta.LengthSquared()>.04f &&
                forward.Dot(delta.Normalized())>.76f;
        }
        if (offerTime>0 && (walking || !Faces(offeredResident?.Position ?? friend.Position,1.4f)))
        {
            if (heldFruit!=null)
            {
                (offeredResident?.Chao ?? pet).ChangeRelationship(-.28f);
                Save();
            }
            if (offeredResident!=null) offeredResident.ConfuseOverOffer();
            else moriFruitReaction.Confuse();
            offerTime=0;
            FinishOffer();
            var hands=HandPosition();
            heldFruit?.Hold(hands);
            heldToy?.Hold(hands);
            if (heldAnimal!=null) heldAnimal.Position=hands;
            return;
        }
        if (offerTime<=0 && eatTime<=0 && thinkTime<=0 && (heldFruit!=null || heldAnimal!=null || heldToy!=null) &&
            !racing && !carrying && !pickingItem && releasePose<=0 && machineLoad<=0 && !walking)
        {
            GardenEgg? candidate=zone==GardenZone.Garden ? world.GetChildren().OfType<GardenEgg>()
                .Where(egg=>egg.Chao is { CanAct:true } && egg.CanReceive &&
                    (heldFruit==null || heldFruit.IsHeartFruit || egg.CanAcceptFruit) && Faces(egg.Position,1.25f))
                .OrderBy(egg=>player.Position.DistanceSquaredTo(egg.Position)).FirstOrDefault() : null;
            bool mainReady=friend.Visible && !courtship.IsBlooming(friend) && !moriDistress.Busy && !sleeping && !moriIslandNap.IsNapping && pet.CanAct && !moriInFlight &&
                (heldFruit==null || heldFruit.IsHeartFruit || zone!=GardenZone.Garden || moriFruitReaction.CanAcceptFruit(pet)) &&
                Faces(friend.Position,1.25f);
            if (candidate!=null && mainReady && player.Position.DistanceSquaredTo(friend.Position)<=
                player.Position.DistanceSquaredTo(candidate.Position)) candidate=null;
            if (candidate!=null || mainReady)
            {
                offeredResident=candidate;
                offerTime=HandoffDuration;
                welcomeFruit=heldFruit!=null && (candidate?.AnticipatesFruit ?? moriFruitReaction.AnticipatesFruit);
                if (welcomeFruit)
                {
                    if (candidate!=null) candidate.ClearUpset();
                    else upsetTime=0;
                }
                rejectFruit=heldFruit!=null && (candidate?.Chao ?? pet).Fullness>=99.9f;
                rejectToy=heldToy!=null && (candidate?.Chao ?? pet).Fun>=99.9f;
                CharacterBody3D receiver=candidate!=null ? candidate : friend;
                Vector3 toPlayer=player.Position-receiver.Position;
                if (heldFruit==null) receiver.Rotation=new(0,Mathf.Atan2(toPlayer.X,toPlayer.Z),0);
                if (candidate!=null) candidate.ReserveForOffer();
                else
                {
                    moriFruitReaction.Interrupt(friendArt);
                    friendArt.Rotation=Vector3.Zero; friendArt.Position=Vector3.Zero;
                    wake.Hide(); petSplashes.Emitting=false;
                }
            }
        }
        CharacterBody3D subject=offeredResident!=null ? offeredResident : friend;
        PetState recipient=offeredResident?.Chao ?? pet;
        Node3D art=offeredResident?.Art ?? friendArt;
        Vector3 mouth=subject.Position+new Vector3(0,.225f,.135f).Rotated(Vector3.Up,subject.Rotation.Y);
        if (offerTime>0)
        {
            offerTime=Math.Max(0,offerTime-dt);
            float progress=1-offerTime/HandoffDuration;
            if (heldFruit!=null)
            {
                Vector3 toPlayer=player.Position-subject.Position;
                subject.Rotation=new(0,Mathf.LerpAngle(subject.Rotation.Y,
                    Mathf.Atan2(toPlayer.X,toPlayer.Z),1-Mathf.Exp(-21*dt)),0);
            }
            offerLean=Mathf.SmoothStep(0,1,Mathf.Clamp(progress/.83f,0,1));
            avatarWalk?.UpperBody(.48f*offerLean,0);
            playerArt.Position=new(0,-.47f*offerLean,0);
            avatarWalk?.Crouch(offerLean);
            var receiverFront=Vector3.Back.Rotated(Vector3.Up,subject.Rotation.Y);
            var pawGrip=subject.Position+Vector3.Up*.16f+receiverFront*.19f;
            ImportedChao.ReceivePose(art,Mathf.SmoothStep(0,1,Mathf.Clamp((progress-.50f)/.46f,0,1)));
            avatarWalk?.Arms(true,0,0);
            avatarWalk?.ReachWithHands(pawGrip,Vector3.Right.Rotated(Vector3.Up,player.Rotation.Y),offerLean,.065f);
            var handoff=HandPosition();
            heldFruit?.Hold(handoff);
            heldToy?.Hold(handoff);
            if (heldAnimal!=null) heldAnimal.Position=handoff;
            if (offerTime==0)
            {
                heldFruit?.Hold(pawGrip);
                heldToy?.Hold(pawGrip);
                if (heldAnimal!=null) heldAnimal.Position=pawGrip;
                if (heldAnimal!=null || heldToy!=null)
                {
                    thinkTime=2;
                    if (heldAnimal!=null) ImportedChao.PetAnimalPose(art,thinkTime);
                    else ImportedChao.ThinkPose(art,thinkTime);
                }
                else if (heldFruit!=null)
                {
                    fruitMeal.Begin(heldFruit,recipient,pawGrip,rejectFruit);
                    eatTime=fruitMeal.Duration; selfEating=false;
                    (art.FindChild("MoodOrb",true,false) as EmotionOrb)?.Celebrate();
                }
            }
        }
        else if (thinkTime>0)
        {
            thinkTime=Math.Max(0,thinkTime-dt);
            Vector3 item=subject.Position+new Vector3(-.085f,.16f,.14f).Rotated(Vector3.Up,subject.Rotation.Y);
            heldToy?.Hold(item);
            if (heldAnimal!=null)
                heldAnimal.Position=subject.Position+new Vector3(.04f,.16f,.14f).Rotated(Vector3.Up,subject.Rotation.Y);
            if (thinkTime>0)
            {
                if (heldAnimal!=null) ImportedChao.PetAnimalPose(art,thinkTime);
                else ImportedChao.ThinkPose(art,thinkTime);
            }
            else
            {
                ImportedChao.EndThink(art);
                if (heldToy!=null)
                {
                    if (!rejectToy && heldToy.BeginPlay(subject,recipient)) Notify($"{recipient.Name} is playing with the {heldToy.ItemName}.");
                    else
                    {
                        heldToy.Toss(RejectImpulse(subject));
                        Notify($"{recipient.Name} threw the {heldToy.ItemName} away.");
                    }
                    heldToy=null;
                }
                if (heldAnimal!=null)
                {
                    recipient.TrainWithAnimal(heldAnimal.Skill.ToString());
                    GardenEgg.ShellBurst(subject,Colors.White,sparkles:true);
                    heldAnimal.ReturnToHabitat(); heldAnimal=null;
                    Notify($"{recipient.Name} learned from the animal.");
                }
                FinishOffer();
                Save();
            }
        }
        else if (eatTime>0)
        {
            if (heldFruit!=null)
            {
                int bites=heldFruit.Bites;
                fruitMeal.Advance(dt,heldFruit,recipient,art,mouth);
                if (!selfEating && heldFruit.Bites>bites) recipient.ChangeRelationship(.12f*(heldFruit.Bites-bites));
            }
            eatTime=heldFruit!=null ? fruitMeal.Remaining : 0;
            if (heldFruit!=null && fruitMeal.Rejected)
            {
                fruitMeal.ThrowBehind(heldFruit,subject);
                heldFruit=null;
                if (offeredResident!=null) offeredResident.ShowUpset();
                else upsetTime=5;
                Notify($"{recipient.Name} is full and threw the fruit away.");
                FinishOffer(); selfEating=false;
                Save();
            }
            else if (heldFruit is { Bites: FruitTree.BiteCount })
            {
                eatTime=0;
                if (welcomeFruit && !selfEating)
                {
                    ImportedChao.SetHappy(art,false);
                    recipient.Joy=Math.Min(100,recipient.Joy+6);
                }
                Notify($"{recipient.Name} ate the fruit. Stamina +2 XP.");
                Save();
                heldFruit.Regrow(); heldFruit=null;
                FinishOffer(); selfEating=false;
            }
        }
        else
        {
            heldFruit?.Hold(CarryPoint(HandPosition(),.18f));
            heldToy?.Hold(CarryPoint(HandPosition()));
        }
    }

    private void FinishOffer() { offeredResident?.FinishOffer(); offeredResident=null; }

    private static Vector3 RejectImpulse(CharacterBody3D chao) =>
        Vector3.Right.Rotated(Vector3.Up,chao.Rotation.Y)*2.8f+Vector3.Up*2.4f;

    private ToyCar? NearbyToy() => world.GetChildren().OfType<ToyCar>()
        .Where(toy=>toy.CanPickUp && toy.Position.DistanceTo(player.Position)<1.55f)
        .OrderBy(toy=>toy.Position.DistanceSquaredTo(player.Position)).FirstOrDefault();

    private bool MoriNearby(float range) => friend.Visible && player.Position.DistanceTo(friend.Position)<range;
    private bool CanPetMori() => !courtship.IsBlooming(friend) && !moriDistress.Busy && !carrying && !racing && !sleeping && !moriInFlight &&
        heldAnimal == null && heldFruit == null && heldEgg==null && heldToy==null && pet.CanAct && petCooldown<=0 && MoriNearby(1.65f);
    private bool CanStoreFruit() => !racing && !pickingItem && releasePose<=0 && offerTime<=0 &&
        eatTime<=0 && heldFruit is { Bites:0 } && (heldFruit.IsHeartFruit ? pet.BagHeartFruit : pet.BagFruit)<12;
    private bool CanOfferBagFruit(bool heart=false) => !racing && (heart ? pet.BagHeartFruit : pet.BagFruit)>0 && heldFruit==null &&
        heldAnimal==null && heldEgg==null && heldToy==null && !carrying && offerTime<=0 && thinkTime<=0 && eatTime<=0 &&
        ((!sleeping && !moriInFlight && pet.CanAct && MoriNearby(2.1f)) ||
            (zone==GardenZone.Garden && world.GetChildren().OfType<GardenEgg>().Any(egg=>
                egg.CanReceive && egg.Chao is { CanAct:true } &&
                player.Position.DistanceTo(egg.Position)<2.1f)));

    private void Face(Vector3 target)
    {
        Vector3 delta=target-player.Position;
        if (delta.X*delta.X+delta.Z*delta.Z>.001f)
            player.Rotation=new(0,Mathf.Atan2(delta.X,delta.Z),0);
    }

    private void PetFriend()
    {
        if (zone==GardenZone.Garden && !carrying && heldEgg==null && heldFruit==null && heldAnimal==null)
        {
            var resident=world.GetChildren().OfType<GardenEgg>().Where(egg=>egg.CanPet && egg.Position.DistanceTo(player.Position)<1.65f)
                .OrderBy(egg=>egg.Position.DistanceSquaredTo(player.Position)).FirstOrDefault();
            if (resident!=null && (!CanPetMori() || resident.Position.DistanceSquaredTo(player.Position)<
                friend.Position.DistanceSquaredTo(player.Position)))
            {
                Face(resident.Position); resident.Pet(); pettingTime=.95f;
                Notify($"{resident.Chao?.Name} enjoyed being petted."); Save(); return;
            }
        }
        if (!CanPetMori()) return;
        Face(friend.Position);
        pettingTime=.95f;
        wanderTarget=friend.Position;
        wanderTime=2.5f;
        callTime=0;
        swimmingTrip=false;
        pet.Pet();
        (friendArt.FindChild("MoodOrb",true,false) as EmotionOrb)?.Celebrate();
        petCooldown=12;
        Notify("Mori enjoyed being petted. Care +1.");
        Save();
    }

    private TrainingAnimal? NearbyAnimal() => zone == GardenZone.Garden
        ? world.GetChildren().OfType<TrainingAnimal>().FirstOrDefault(animal =>
            animal.Available && FlatDistance(player.Position,animal.Position)<1.65f)
        : null;

    private void PopulateEggs()
    {
        if (testMode && zone==GardenZone.Garden)
            for (int i=0;i<3;i++)
                world.AddChild(new FruitTree { IsFeeder=true,Position=new(-5.46f+i*.46f,.37f,4) });
        if (zone!=GardenZone.Garden) return;
        foreach (var egg in pet.GardenEggs)
            world.AddChild(new GardenEgg { Data=egg,Position=new(egg.X,egg.Y,egg.Z),BedSpaces=bedSpaces,Courtship=courtship });
    }

    private GardenEgg? NearbyEgg() => zone!=GardenZone.Garden ? null :
        world.GetChildren().OfType<GardenEgg>().FirstOrDefault(egg=>egg!=machineResident && !egg.InFlight && !courtship.IsPaired(egg) &&
            player.Position.DistanceTo(egg.Position)<1.25f);

    private static Vector3 MachineRest => ZoneArt.ChaoMachine+Vector3.Up*.22f;

    private void UpdateMachineLoad(float dt)
    {
        if (zone!=GardenZone.Garden) return;
        var hatch=world.GetNodeOrNull<Node3D>("MachineHatch");
        if (hatch==null) return;
        if (machineLoad<=0 && !machineOpen && !machineOpening && !pickingItem &&
            (carrying && pet.CanAct && pickupTime>=PickupDuration || heldEgg?.Chao!=null) &&
            player.Position.Y>=.09f && player.Position.Y<.27f &&
            FlatDistance(player.Position,ZoneArt.MachineStep)<.53f)
        {
            machineLoad=.90f;
            machineThrowStarted=false;
            Face(ZoneArt.ChaoMachine);
        }
        if (machineLoad<=0)
        {
            hatch.Rotation=new(Mathf.MoveToward(hatch.Rotation.X,machineOpen || machineOpening ? 1.68f : 0,dt*6),0,0);
            if (machineOpening && Mathf.IsEqualApprox(hatch.Rotation.X,1.68f))
            {
                machineOpening=false;
                machineOpen=true;
                machinePanel.Show();
                machineChime.Play();
            }
            return;
        }
        Face(ZoneArt.ChaoMachine);
        machineLoad=Math.Max(0,machineLoad-dt);
        hatch.Rotation=new(Mathf.MoveToward(hatch.Rotation.X,1.68f,dt*5),0,0);
        if (machineLoad>.52f) return;
        if (!machineThrowStarted)
        {
            machineThrowStarted=true;
            machineTossStart=heldEgg?.Position ?? friend.Position;
        }
        float t=1-machineLoad/.52f;
        Vector3 at=machineTossStart.Lerp(MachineRest,t)+Vector3.Up*(Mathf.Sin(t*Mathf.Pi)*.34f);
        if (heldEgg!=null) heldEgg.Position=at;
        else friend.Position=at;
        if (machineLoad>0) return;
        if (heldEgg?.Chao is PetState resident)
        {
            var actor=heldEgg;
            heldEgg=null;
            actor.Park(MachineRest);
            OpenMachine(resident,actor);
        }
        else if (carrying && pet.CanAct)
        {
            carrying=false;
            friend.Position=MachineRest;
            OpenMachine(pet,null);
        }
    }

    private bool CanOpenEmptyMachine() => zone==GardenZone.Garden && !carrying &&
        heldEgg==null && heldFruit==null && heldAnimal==null && heldToy==null &&
        FlatDistance(player.Position,ZoneArt.MachineStep)<1.45f;

    private void OpenMachine(PetState? chao,GardenEgg? resident)
    {
        machinePet=chao; machineResident=resident; machineOpening=true;
        machineTitle.Text=$"CHAO MACHINE · {chao?.Name ?? "No Chao"}";
        renameInput.Text=chao?.Name ?? "";
        renameButton.Disabled=renameApply.Disabled=chao==null;
        renameInput.Hide(); renameApply.Hide();
    }

    private void RenameChao(string requested)
    {
        if (machinePet==null) return;
        string name=requested.Trim();
        if (name.Length==0) return;
        machinePet.Name=name.Length>18 ? name[..18] : name;
        Save();
        Notify($"Name saved: {machinePet.Name}");
        CloseMachine();
    }

    private void CloseMachine()
    {
        machineOpen=machineOpening=false;
        if (machineResident!=null) machineResident.Park(ZoneArt.ChaoMachine+Vector3.Back*2.50f);
        else if (machinePet==pet) { friend.Position=ZoneArt.ChaoMachine+Vector3.Back*2.50f; wanderTime=0; }
        machineResident=null; machinePet=null;
        machinePanel.Hide();
        Save();
    }

    private void UpdateEggs(float dt)
    {
        recentCardTime=Math.Max(0,recentCardTime-dt);
        if (zone!=GardenZone.Garden) return;
        foreach (var egg in world.GetChildren().OfType<GardenEgg>())
        {
            if (egg==machineResident) continue;
            if (egg==heldEgg)
            {
                egg.Hold(CarryPoint(HandPosition()-Vector3.Up*(egg.Data.Hatched ? .14f : .19f)),
                    !egg.Data.Hatched && Held(Key.X),dt);
                egg.Rotation=player.Rotation;
                continue;
            }
            bool wasHatched=egg.Data.Hatched;
            if (!egg.Advance(dt,FruitSlots(),world.GetNodeOrNull<ToyCar>("ToyCar"),FruitCarrier,
                world.GetNodeOrNull<ToyCar>("Rattle"),world.GetNodeOrNull<ToyCar>("Flute"),CanInGarden)) continue;
            if (!wasHatched && egg.Data.Hatched)
            {
                recentHatch=egg;
                recentCardTime=8;
                Notify("The shell cracked. A new Chao hatched!");
            }
            Save();
        }
    }

    private void LayGardenEgg(Vector3 position,string color)
    {
        if (pet.GardenEggs.Count>=12) return;
        var state=new GardenEggState { Color=color,X=position.X,Y=position.Y,Z=position.Z };
        pet.GardenEggs.Add(state);
        world.AddChild(new GardenEgg { Data=state,Position=position,BedSpaces=bedSpaces,Courtship=courtship });
        Notify($"A {color.ToLowerInvariant()} egg appeared!");
        Save();
    }

    private const int EggPrice=3;
    private void BuyShopItem()
    {
        if (HeartFruitSelected)
        {
            if (pet.BagHeartFruit>=12) { Notify("Your bag is full of Heart Fruit."); return; }
            if (pet.Stars<HeartFruitPrice) { Notify("You need 2 stars to buy Heart Fruit."); return; }
            pet.Stars-=HeartFruitPrice;
            pet.BagHeartFruit++;
            Notify("Heart Fruit added to your bag. Press V to take one out.");
            Save();
            return;
        }
        if (pet.GardenEggs.Count>=12) { Notify("The garden is full."); return; }
        if (pet.Stars<EggPrice) { Notify("Earn stars in the stadium to buy an egg."); return; }
        string color=GardenEgg.Colors[shopColor];
        pet.Stars-=EggPrice;
        pet.GardenEggs.Add(new GardenEggState { Color=color,X=-2-pet.GardenEggs.Count%4*.85f,Z=6+pet.GardenEggs.Count/4*.95f });
        Notify($"{color} egg delivered to the garden.");
        Save();
    }

    private void UpdateTrainingAnimals(float dt)
    {
        if (zone != GardenZone.Garden) return;
        foreach (var animal in world.GetChildren().OfType<TrainingAnimal>())
            animal.UpdateAnimal(dt,time,animal == heldAnimal);
        if (heldAnimal != null && offerTime<=0 && thinkTime<=0)
        {
            heldAnimal.Position = CarryPoint(HandPosition()-Vector3.Up*.12f);
            heldAnimal.Rotation = player.Rotation;
        }
    }

    private void BuildRaceCourse()
    {
        for (int i=0;i<Sections.Length;i++)
        {
            Vector3 direction = Track[i+1]-Track[i];
            Color color = new(Sections[i] switch
            {
                "Swim" => "638e9d", "Fly" => "9c9eb1", "Power" => "947f69", _ => "c4ad80"
            });
            var strip = GardenArt.Shape(world,new BoxMesh(),(Track[i]+Track[i+1])*.5f+Vector3.Up*.115f,
                new(2.1f,.045f,direction.Length()+.13f),color);
            strip.Rotation = new(0,Mathf.Atan2(direction.X,direction.Z),0);
            if (i == 0 || Sections[i] != Sections[i-1])
                GardenArt.Sign(world,Sections[i].ToUpperInvariant(),Track[i]+Vector3.Up*1.1f,GardenArt.Cream);
        }
    }

    private void StartRace()
    {
        if (zone != GardenZone.Stadium || racing || !pet.CanAct) return;
        if (!friend.Visible) { Notify("Bring Mori from the garden first."); return; }
        if (pet.Energy < 15) { Notify("Mori is tired. Let him rest in the garden."); return; }
        racing = true; sleeping = false; carrying = false; callTime = 0;
        raceTime = 45;
        moriProgress = 0; rivalProgress = 0;
        cheerTime = 0; cheerCooldown = 0;
        wake.Hide();
        rival = new Node3D();
        AddChild(rival);
        GardenArt.Creature(rival,false,"comet");
        GardenArt.Sign(rival,"Kite",new(0,.64f,0),GardenArt.Cream);
        PlaceRacer(friend,0,-.58f);
        PlaceRacer(rival,0,.58f);
        Notify("Mori vs. Kite. Press Q when Mori falls behind and the cheer prompt appears.",6);
    }

    private bool CanCheer() => racing && cheerCooldown<=0 && moriProgress+.10f<rivalProgress;

    private void Cheer()
    {
        if (!CanCheer()) return;
        cheerTime = 2.2f;
        cheerCooldown = 3.5f;
    }

    private void UpdateRace(float dt)
    {
        raceTime -= dt;
        cheerTime = Math.Max(0,cheerTime-dt);
        cheerCooldown = Math.Max(0,cheerCooldown-dt);
        int section = Math.Min((int)moriProgress,Sections.Length-1);
        int skill = Sections[section] switch
        {
            "Swim" => pet.Swim,
            "Fly" => pet.Fly,
            "Power" => pet.Power,
            _ => pet.Run
        };
        float speed = .25f+Math.Min(skill,500)*.0018f;
        if (cheerTime > 0) speed *= 1.43f;
        moriProgress += speed*dt;
        rivalProgress += .29f*dt;
        PlaceRacer(friend,moriProgress,-.58f);
        PlaceRacer(rival!,rivalProgress,.58f);
        friendArt.RotationDegrees = Vector3.Zero;
        friendArt.Position = new(0,Math.Abs(Mathf.Sin(time*13))*.10f,0);
        if (moriProgress >= Sections.Length || rivalProgress >= Sections.Length || raceTime <= 0)
            FinishRace(moriProgress >= Sections.Length && moriProgress >= rivalProgress ? 8 :
                raceTime > 0 && moriProgress >= Sections.Length*.65f ? 5 : 0);
    }

    private static void PlaceRacer(Node3D racer,float progress,float lane)
    {
        int section = Math.Min((int)progress,Sections.Length-1);
        Vector3 direction = (Track[section+1]-Track[section]).Normalized();
        float t = Mathf.Clamp(progress-section,0,1);
        float lift = Sections[section] == "Fly" ? Mathf.Sin(t*Mathf.Pi)*.68f : 0;
        racer.Position = Track[section].Lerp(Track[section+1],t) +
            direction.Cross(Vector3.Up)*lane + Vector3.Up*(.13f+lift);
        racer.Rotation = new(0,Mathf.Atan2(direction.X,direction.Z),0);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (paused || transition >= 0) return;
        if (@event is InputEventMouseMotion motion && Input.IsMouseButtonPressed(MouseButton.Right))
            cameraRig.Orbit(motion.Relative);
        if (@event is InputEventMouseButton button && button.Pressed)
        {
            if (button.ButtonIndex == MouseButton.WheelUp) cameraRig.Distance = Math.Max(3, cameraRig.Distance - .5f);
            if (button.ButtonIndex == MouseButton.WheelDown) cameraRig.Distance = Math.Min(9, cameraRig.Distance + .5f);
        }
    }

    private void CheckPortals()
    {
        if (portalCooldown > 0 || transition >= 0 || player.Position.Y > 1 ||
            pickingItem || releasePose>0 || offerTime>0 || eatTime>0 || thinkTime>0 || machineOpening || machineOpen) return;
        Vector3 exit = zone == GardenZone.Garden ? ZoneArt.GardenLobbyGate : new(0,0,-10);
        if (FlatDistance(player.Position, exit) < 1.3f)
            BeginTransition(zone == GardenZone.Lobby ? GardenZone.Garden : GardenZone.Lobby);
        else if (zone == GardenZone.Lobby && FlatDistance(player.Position, ZoneArt.PlazaStadiumPad) < 1.35f)
            BeginTransition(GardenZone.Stadium);
    }

    private void BeginTransition(GardenZone next)
    {
        if (transition >= 0 || next == zone) return;
        if (racing) FinishRace(0, cancelled:true);
        destination = next; transition = 0; zoneSwapped = false; Save();
    }

    private void AdvanceTransition(float dt)
    {
        transition += dt;
        if (transition >= .35f && !zoneSwapped) { SwitchZone(destination); zoneSwapped = true; }
        fade.Color = new Color(.04f,.08f,.12f, transition < .35f ? transition/.35f : 1-(transition-.35f)/.35f);
        if (transition >= .7f) { transition = -1; fade.Color = Colors.Transparent; }
    }

    private void SwitchZone(GardenZone next)
    {
        GardenZone previous = zone;
        if (friend.Visible) petZonePosition = friend.Position;
        if (previous==GardenZone.Garden)
            foreach (var tree in world.GetChildren().OfType<FruitTree>().Where(tree=>!tree.IsFeeder))
                fruitGrowth[tree.Position]=tree.Slots.Select(slot=>slot.CaptureGrowth()).ToArray();
        courtship.Clear();
        RemoveChild(world); world.QueueFree();
        heldAnimal = null;
        if (heldFruit is { IsHeartFruit:true,Bites:0 } && eatTime<=0) pet.BagHeartFruit++;
        heldFruit = null;
        heldEgg = null; recentHatch=null; recentCardTime=0; shopOpen=false; machineLoad=0; moriRest.Cancel();
        bedSpaces.Clear(); sleeping=false; moriFruitReaction.Interrupt(friendArt);
        moriIslandNap.Cancel(friendArt);
        offerTime = eatTime = thinkTime = 0;
        pickingItem=false; releaseObject=null;
        offerLean=0;
        heldToy=null;
        treeReachTime=0; shakingTree=null;
        moriInFlight = false;
        chaoMotion.Reset(); tossedMori=false;
        releasePose = 0;
        swimmingTrip = false; callTime = 0; following = false; wanderTime = 0;
        zone = next; world = new Node3D(); AddChild(world); ZoneArt.Build(world, zone);
        if (zone==GardenZone.Garden)
            foreach (var tree in world.GetChildren().OfType<FruitTree>().Where(tree=>!tree.IsFeeder))
                if (fruitGrowth.TryGetValue(tree.Position,out var growth))
                    foreach (var (slot,state) in tree.Slots.Zip(growth))
                        slot.RestoreGrowth(state.Growth,state.GroundPosition,state.Bites);
        if (zone == GardenZone.Stadium) BuildRaceCourse();
        PopulateEggs();
        player.Position = zone == GardenZone.Garden
            ? ZoneArt.GardenLobbyGate + new Vector3(0,0,3)
            : new(0,0,-7);
        player.Rotation = Vector3.Zero; verticalSpeed = 0;
        player.ResetPhysicsInterpolation();
        cameraRig.Yaw = Mathf.Pi; cameraRig.Follow(player.Position, 0, true);
        if (carrying) { petZone = zone; sleeping = false; }
        friend.Visible = petZone == zone;
        if (friend.Visible) friend.Position = carrying ? player.Position + Vector3.Right : KeepInGarden(petZonePosition);
        if (carrying) pickupTime = PickupDuration;
        portalCooldown = 1.3f;
        Notify(ZoneArt.Title(zone) + (friend.Visible ? " · Mori is here" : " · Mori is in another area"), 5);
        UpdateUi();
    }

    private void FinishRace(int reward, bool cancelled = false)
    {
        if (!racing) return;
        racing = false;
        rival?.QueueFree();
        rival = null;
        wanderTarget = friend.Position;
        wanderTime = 3;
        if (cancelled) return;
        pet.CompleteRace(reward);
        Save();
        Notify(reward == 8 ? "Mori won! Stars +8." :
            reward == 5 ? "Kite won. Mori earned 5 stars." : "Mori did not earn stars this race.",8);
    }

    private void Save()
    {
        try { SaveStore.Save(savePath, pet); saveTimer = 0; }
        catch (Exception ex) { GD.PushWarning(ex.Message); Notify("Could not save the game. Check access to the save folder.", 10); }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) { Save(); GetTree().Quit(); }
        if (what==NotificationApplicationFocusOut && !testMode && !paused)
        {
            pausedByFocus=paused=true;
            Save();
            UpdateUi();
        }
        if (what==NotificationApplicationFocusIn && pausedByFocus)
        {
            pausedByFocus=paused=false;
            UpdateUi();
        }
    }

    private static float FlatDistance(Vector3 a, Vector3 b) { a.Y = 0; b.Y = 0; return a.DistanceTo(b); }
    private void Notify(string text, float seconds = 4) { if (toast != null) toast.Text = text; messageTime = seconds; }

    private void BuildUi()
    {
        var layer = new CanvasLayer(); AddChild(layer);
        var ui = new Control { MouseFilter = Control.MouseFilterEnum.Ignore }; layer.AddChild(ui);
        ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        Panel(ui, new(24, 20, 340, 76), new("f9f3e5"));
        Text(ui, "Mori · Garden Adventure", 23, new(40, 29, 320, 32), GardenArt.Ink);
        zoneLabel = Text(ui, "", 13, new(40, 65, 320, 25), new("50685c"));
        Text(ui, "Right mouse drag / IJKL: Camera   ·   C: Recenter", 14, new(800, 28, 465, 28), GardenArt.Ink);
        details = new Control { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore }; ui.AddChild(details);
        Panel(details, new(24,112,238,257),new("f9f3e5"));
        formLabel=Text(details,"Mori",23,new(43,130,210,34),GardenArt.Ink);
        stats=Text(details,"",15,new(43,176,205,124),new("50685c"));
        readinessBar=new ProgressBar { Position=new(43,345), Size=new(198,10), ShowPercentage=false,
            MouseFilter=Control.MouseFilterEnum.Ignore };
        readinessBar.AddThemeStyleboxOverride("background",new StyleBoxFlat { BgColor=new("e4d3cb") });
        readinessBar.AddThemeStyleboxOverride("fill",new StyleBoxFlat { BgColor=new("cb6382") });
        details.AddChild(readinessBar);
        raceLabel = Text(ui, "", 22, new(400, 110, 700, 38), GardenArt.Ink);
        hintPanel = Panel(ui, new(170,665,940,50),new Color(.98f,.96f,.89f,.96f));
        machinePanel=new Control { Visible=false };
        ui.AddChild(machinePanel);
        Panel(machinePanel,new(388,223,504,272),new Color(.976f,.953f,.898f,.80f));
        machineTitle=Text(machinePanel,"CHAO MACHINE",23,new(411,244,454,36),GardenArt.Ink);
        renameButton=new Button { Text="Rename Chao",Position=new(415,302),Size=new(211,40) };
        var leaveButton=new Button { Text="Return to garden",Position=new(640,302),Size=new(217,40) };
        renameInput=new LineEdit { Position=new(415,365),Size=new(275,44),MaxLength=18,PlaceholderText="New Chao name" };
        renameApply=new Button { Text="Save name",Position=new(706,365),Size=new(150,44) };
        machinePanel.AddChild(renameButton); machinePanel.AddChild(leaveButton);
        machinePanel.AddChild(renameInput); machinePanel.AddChild(renameApply);
        renameButton.Pressed+=()=>{ renameInput.Show(); renameApply.Show(); renameInput.GrabFocus(); renameInput.SelectAll(); };
        leaveButton.Pressed+=CloseMachine;
        renameApply.Pressed+=()=>RenameChao(renameInput.Text);
        renameInput.TextSubmitted+=RenameChao;
        const int rate=22050;
        byte[] notes=new byte[rate/3*2];
        for (int i=0;i<notes.Length/2;i++)
        {
            float t=(float)i/rate, beat=t<.12f ? t : t-.12f;
            float envelope=Mathf.Clamp(beat/.012f,0,1)*Mathf.Exp(-beat*20);
            short value=(short)(Mathf.Sin(Mathf.Tau*(t<.12f ? 659 : 880)*beat)*envelope*5200);
            notes[2*i]=(byte)value; notes[2*i+1]=(byte)(value>>8);
        }
        machineChime=new AudioStreamPlayer { Stream=new AudioStreamWav {
            Format=AudioStreamWav.FormatEnum.Format16Bits,MixRate=rate,Data=notes },VolumeDb=-5 };
        AddChild(machineChime);
        shopPanel=new Control { Visible=false,MouseFilter=Control.MouseFilterEnum.Ignore };
        ui.AddChild(shopPanel);
        Panel(shopPanel,new(400,255,480,240),new("f9f3e5"));
        Text(shopPanel,"GARDEN SHOP",26,new(425,273,420,42),GardenArt.Ink);
        shopText=Text(shopPanel,"",18,new(425,322,420,150),GardenArt.Ink);
        bagPanel = new Control { Visible=false,MouseFilter=Control.MouseFilterEnum.Ignore };
        ui.AddChild(bagPanel);
        Panel(bagPanel,new(930,112,325,190),new("f9f3e5"));
        Text(bagPanel,"INVENTORY",20,new(948,128,286,30),GardenArt.Ink);
        bagText = Text(bagPanel,"",15,new(948,166,286,108),GardenArt.Ink);
        hint = Text(ui, "", 16, new(190, 680, 910, 28), GardenArt.Ink);
        toast = Text(ui, "", 16, new(170, 617, 940, 42), GardenArt.Ink); toast.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Panel(ui, new(24, 741, 1232, 40), new("304e49"));
        Text(ui, "WASD Move   Shift Run   Space Jump   Ctrl Dash   B Inventory   Tab Mori   M Music   Esc Pause", 14,
            new(40, 752, 1205, 28), new("f3ecd8"));
        pauseLabel = Text(ui, "PAUSED\nPress Esc to resume", 30, new(410, 260, 540, 180), GardenArt.Ink);
        pauseLabel.HorizontalAlignment = HorizontalAlignment.Center;
        pauseLabel.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new("fff5df"), ContentMarginTop = 25 });
        fade = new ColorRect { Color = Colors.Transparent, MouseFilter = Control.MouseFilterEnum.Ignore };
        ui.AddChild(fade); fade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

    }

    private static Panel Panel(Control parent, Rect2 rect, Color color)
    {
        var panel = new Panel { Position = rect.Position, Size = rect.Size, MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = color, CornerRadiusTopLeft = 15, CornerRadiusTopRight = 15,
            CornerRadiusBottomLeft = 15, CornerRadiusBottomRight = 15 }); parent.AddChild(panel); return panel;
    }

    private static Label Text(Control parent, string text, int size, Rect2 rect, Color color)
    {
        var label = new Label { Text = text, Position = rect.Position, Size = rect.Size, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", color);
        parent.AddChild(label); return label;
    }

    private static string SkillLine(string name, int progress) =>
        $"{name}   Lv. {progress/10}    {progress%10}/10 XP";

    private void UpdateUi()
    {
        if (formLabel == null) return;
        mainName.Text=pet.Name;
        PetState inspected=carrying ? pet : heldEgg?.Chao ?? (recentCardTime>0 ? recentHatch?.Chao : null) ?? pet;
        details.Visible=carrying || showStats || heldEgg?.Chao!=null || recentCardTime>0;
        formLabel.Text=inspected.Name;
        stats.Text=$"{SkillLine("Fly",inspected.Fly)}\n{SkillLine("Power",inspected.Power)}\n{SkillLine("Run",inspected.Run)}\n{SkillLine("Stamina",inspected.Stamina)}\n{SkillLine("Swim",inspected.Swim)}\n\nCourtship readiness  {inspected.CourtshipReadiness:0}%";
        readinessBar.Value=inspected.CourtshipReadiness;
        raceLabel.Text = racing ? $"{Sections[Math.Min((int)moriProgress,Sections.Length-1)].ToUpperInvariant()}  ·  Mori {Mathf.RoundToInt(moriProgress/Sections.Length*100)}%  /  Kite {Mathf.RoundToInt(rivalProgress/Sections.Length*100)}%" : "";
        var gardenTime=TimeSpan.FromSeconds(pet.GardenSeconds);
        zoneLabel.Text = $"{ZoneArt.Title(zone)}   ·   Garden {(int)gardenTime.TotalHours:00}:{gardenTime:mm\\:ss}";
        shopPanel.Visible=shopOpen;
        shopText.Text=$"{(HeartFruitSelected ? $"Heart Fruit · {HeartFruitPrice} stars" : $"{GardenEgg.Colors[shopColor]} egg · {EggPrice} stars")}\nBalance: {pet.Stars} stars\n\nLeft / Right: Item    Enter: Buy    Esc: Close";
        bagPanel.Visible = showBag;
        bagText.Text = $"Garden fruit   {pet.BagFruit}/12\nHeart Fruit   {pet.BagHeartFruit}/12  (V)\nRace stars   {pet.Stars}";
        hint.Text = ContextActions();
        hint.Visible = hintPanel.Visible = !paused && hint.Text.Length>0;
        toast.Visible = messageTime > 0; pauseLabel.Visible = paused;
    }

    private string ContextActions()
    {
        if (shopOpen || machineOpen || machineOpening || machineLoad>0) return "";
        if (!paused && transition<0 && CanTakeMeal()) return "E: Take fruit";
        if (paused || transition>=0 || releasePose>0 || moriInFlight || offerTime>0 || thinkTime>0 || eatTime>0) return "";
        if (racing) return CanCheer() ? "Q: Cheer     E: Cancel race" : "E: Cancel race";
        string action = "";
        if (heldEgg is { Data.Hatched:true }) return $"E: Set down or throw {heldEgg.Chao?.Name}";
        if (heldEgg!=null) return $"Hold X: Rock egg ({heldEgg.ShakeTime:0.0}s)     E: Set down or throw";
        if (heldToy!=null) return $"Face a Chao to offer the {heldToy.ItemName}   |   E: Set down or throw";
        if (carrying && pet.IsEgg) return $"Hold X: Rock egg ({mainEggShake:0.0}s)     E: Set down or throw";
        if (zone==GardenZone.Lobby && !carrying && FlatDistance(player.Position,ZoneArt.PlazaShop)<2.5f)
            return "E: Egg Shop";
        if (CanOpenEmptyMachine()) return "E: Open Chao Machine";
        if (!carrying && heldFruit==null && heldAnimal==null && NearbyToy() is { } toy) return $"E: Pick up {toy.ItemName}";
        if (!carrying && heldFruit==null && heldAnimal==null && NearbyEgg() is GardenEgg egg) return egg.Data.Hatched ? $"E: Pick up {egg.Chao?.Name}" : "E: Pick up egg";
        if (heldFruit!=null) action=CanStoreFruit() ? "E: Release fruit     G: Store in bag" : "E: Release fruit";
        else if (heldAnimal!=null) action=MoriNearby(2.6f) && pet.CanAct && !sleeping ? "Face Mori to hand over animal  |  E: Release" : "E: Release animal";
        else if (carrying) action="E: Set Mori down or throw while moving";
        else if (pet.IsEgg && MoriNearby(2.6f)) action="E: Pick up egg";
        else if (zone==GardenZone.Stadium && friend.Visible && pet.CanAct && pet.Energy>=15 &&
            FlatDistance(player.Position,new(0,0,5))<2.5f) action="E: Start race";
        else if (NearbyAnimal() is TrainingAnimal animal) action=$"E: Pick up {animal.AnimalName}";
        else if (NearbyTree() is FruitTree tree && tree.CanInteract)
            action=tree.HasFruitOnGround ? "E: Pick up fruit" : "E: Shake tree";
        else if (MoriNearby(2.2f) && pet.CanAct) action=$"E: Pick up {pet.Name}";
        if (CanPetMori()) action=AppendAction(action,$"F: Pet {pet.Name}");
        else if (zone==GardenZone.Garden && !carrying && heldEgg==null && heldFruit==null && heldAnimal==null &&
            world.GetChildren().OfType<GardenEgg>().Any(egg=>egg.CanPet && egg.Position.DistanceTo(player.Position)<1.65f))
            action=AppendAction(action,"F: Pet Chao");
        if (CanOfferBagFruit(true)) action=AppendAction(action,"V: Take Heart Fruit");
        if (CanOfferBagFruit()) action=AppendAction(action,"H: Take fruit from bag");
        if (zone==GardenZone.Garden && pet.CanAct && FlatDistance(player.Position,bed)<2.5f)
            action=AppendAction(action,"R: Rest");
        if (friend.Visible && pet.CanAct && !carrying && pet.RespondsToCall)
            action=AppendAction(action,"Q: Call Mori");
        return action;
    }

    private static string AppendAction(string current,string action) =>
        current.Length==0 ? action : current+"     "+action;

    private void RunSmokeChecks()
    {
        var start=friend.Position;
        for (int i=0;i<90;i++) MoveFriend(1f/60);
        if (FlatDistance(start,friend.Position)<.4f)
            throw new Exception("Mori did not start his first walk");
        var fruit=world.GetChildren().OfType<FruitTree>().First(tree=>tree.IsFeeder);
        var hungryTaster=new PetState { Fullness=18,Glutton=true };
        var tasterMeal=new FruitMeal();
        tasterMeal.Begin(fruit,new PetState { Fullness=75 },fruit.FruitPosition);
        float slowMeal=tasterMeal.Duration;
        tasterMeal.Begin(fruit,hungryTaster,fruit.FruitPosition);
        if (tasterMeal.Duration>=slowMeal) throw new Exception("Hunger and appetite did not speed up eating");
        tasterMeal.Advance(.9f,fruit,hungryTaster,friendArt,fruit.FruitPosition);
        if (fruit.Bites!=0) throw new Exception("Fruit shrank before the first chewing animation ended");
        tasterMeal.Advance(tasterMeal.Remaining-.1f,fruit,hungryTaster,friendArt,fruit.FruitPosition);
        if (fruit.Bites!=3 || hungryTaster.Care!=0)
            throw new Exception("Fruit was not kept through the third bite");
        tasterMeal.Advance(.15f,fruit,hungryTaster,friendArt,fruit.FruitPosition);
        if (fruit.Bites!=4 || hungryTaster.Care!=1)
            throw new Exception("The fourth bite did not finish the meal");
        fruit.Regrow();
        var swimCheck=new ChaoMotion();
        var shoreSwimmer=new PetState { Swim=250 };
        friend.Position=new(-2,0,-8.05f);
        for (int i=0;i<80;i++) swimCheck.Step(friend,Vector3.Forward*3,shoreSwimmer,false,true,1f/60,true,out _);
        if (Mathf.Abs(friend.Position.X+2)<.30f)
            throw new Exception("Swimming Chao got stuck against the ledge");
        foreach (var radial in new[]{Vector3.Back,Vector3.Left,Vector3.Right})
        {
            swimCheck.Reset();
            friend.Position=GardenArt.Pond+radial*(GardenArt.PondRadius-.25f);
            int transitions=0;
            bool wet=true;
            for (int i=0;i<3200 && (wet || FlatDistance(friend.Position,GardenArt.SwimExit)>.25f);i++)
            {
                var path=GardenArt.SwimExit-friend.Position; path.Y=0;
                swimCheck.Step(friend,path,shoreSwimmer,false,wet,1f/60,true,out _);
                bool nextWet=swimCheck.IsSwimmingAt(friend.Position);
                if (wet!=nextWet) transitions++;
                wet=nextWet;
            }
            if (transitions!=1 || wet || FlatDistance(friend.Position,GardenArt.SwimExit)>.25f)
                throw new Exception($"Chao could not leave the pond smoothly from {radial}: {friend.Position} ({transitions} transitions)");
        }
        var novice=new PetState();
        foreach (var entry in new[]{GardenArt.Pond+Vector3.Left*10,new Vector3(-1.55f,-.15f,-2.79f),
            GardenArt.Pond+Vector3.Back*11})
        {
            swimCheck.Reset();
            friend.Position=entry;
            float deepest=FlatDistance(entry,GardenArt.Pond);
            int struggleFrames=0;
            while (struggleFrames<7200 && (swimCheck.IsSwimmingAt(friend.Position) || swimCheck.LeavingPond))
            {
                bool wet=swimCheck.IsSwimmingAt(friend.Position);
                swimCheck.Step(friend,Vector3.Forward,novice,false,wet,1f/60,true,out _);
                if (wet) deepest=Mathf.Min(deepest,FlatDistance(friend.Position,GardenArt.Pond));
                struggleFrames++;
            }
            if (struggleFrames==7200 || deepest>8 || FlatDistance(friend.Position,GardenArt.SwimExit)>.40f)
                throw new Exception($"Novice Chao did not explore deep water and reach dry shore from {entry}: {friend.Position}, deepest radius {deepest}, {struggleFrames/60}s");
        }
        player.Position=new(8,0,6);
        friend.Position=new(9,0,6);
        Face(friend.Position);
        heldFruit=fruit;
        UpdateOffering(1f/60);
        if (offerTime<=0) throw new Exception("Fruit handoff did not begin");
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode=Key.W,Keycode=Key.W,Pressed=true });
        Input.FlushBufferedEvents();
        var before=player.Position;
        MovePlayer(.05f);
        UpdateOffering(1f/60);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode=Key.W,Keycode=Key.W,Pressed=false });
        Input.FlushBufferedEvents();
        if (offerTime>0 || heldFruit!=fruit || !moriFruitReaction.IsConfused || FlatDistance(before,player.Position)<.03f)
            throw new Exception("Walking away did not keep the fruit and confuse the Chao");
        Face(friend.Position);
        UpdateOffering(1f/60);
        if (offerTime<=0) throw new Exception("Chao did not resume the handoff when the player turned back");
        offerTime=0;
        FinishOffer();
        moriFruitReaction.Interrupt(friendArt);
        heldFruit=null; fruit.Regrow();
        var reaction=new FruitReaction();
        var hungry=new PetState { Fullness=20,Crybaby=true,Bond=2 };
        var run=new ChaoMotion();
        friend.Position=new(8,0,6);
        var carrier=new Vector3(12,0,6);
        bool noticed=false;
        for (int i=0;i<1500 && hungry.Bond==2;i++)
        {
            reaction.Step(friend,friendArt,hungry,run,carrier,1f/60);
            noticed|=friendArt.FindChildren("*","Label3D",true,false)
                .OfType<Label3D>().Any(label=>label.Visible && label.Text=="!");
        }
        reaction.Step(friend,friendArt,hungry,run,carrier,1f/60);
        if (!noticed || hungry.Bond!=1 || FlatDistance(friend.Position,carrier)>1.35f ||
            friendArt.GetNodeOrNull<Node3D>("Tears") is not { Visible:true })
            throw new Exception("Hungry Chao did not notice, chase, beg, and cry when ignored");
        reaction.Interrupt(friendArt);
        friend.Position=new(9,0,6);
        player.Position=new(8,0,6);
        Face(friend.Position);
        float originalJoy=pet.Joy, originalFullness=pet.Fullness;
        pet.Fullness=20;
        heldFruit=fruit;
        moriFruitReaction.Step(friend,friendArt,pet,chaoMotion,player.Position,1f/60);
        moriFruitReaction.Step(friend,friendArt,pet,chaoMotion,player.Position+Vector3.Left*2,1f/60);
        for (int i=0;i<280;i++)
            moriFruitReaction.Step(friend,friendArt,pet,chaoMotion,player.Position+Vector3.Left*2,1f/60);
        if (pet.Joy!=originalJoy || !moriFruitReaction.AnticipatesFruit)
            throw new Exception("Chao reacted negatively before five seconds");
        for (int i=0;i<80;i++) UpdateOffering(1f/60);
        if (eatTime<=0 || friendArt.FindChild("Heart",true,false) is not MeshInstance3D { Visible:true })
            throw new Exception("Chao did not welcome the returning fruit");
        RunOfferCheck();
        pet.Joy=originalJoy; pet.Fullness=originalFullness;
        pet.Bond--; pet.Care--; pet.Stamina-=2;
        var secondRest=new ChaoRest { Spaces=bedSpaces };
        moriRest.Request(ChaoRest.Bed+Vector3.Back*1.9f);
        secondRest.Request(ChaoRest.Bed+Vector3.Back*1.9f);
        if (bedSpaces.Count!=2) throw new Exception("Two Chao reserved the same bed space");
        moriRest.Cancel();
        secondRest.Cancel();
        foreach (float height in new[]{.9f,2.4f,3.3f})
            foreach (float across in new[]{-1.18f,0,1.18f})
            {
                Vector3 entrance=ZoneArt.GardenLobbyGate+new Vector3(across,height,.015f);
                var probe=PhysicsRayQueryParameters3D.Create(entrance+Vector3.Back*2.5f,entrance,1);
                if (GetWorld3D().DirectSpaceState.IntersectRay(probe).Count>0)
                    throw new Exception("Something blocks the front of the garden gate");
            }
        var balconyMotion=new ChaoMotion();
        var climber=new PetState { Run=120,Fly=20 };
        friend.Position=new(1.6f,0,-9.4f);
        bool reachedRail=false, jumpedRail=false;
        float balconyPeak=0;
        for (int i=0;i<2400 && !jumpedRail;i++)
        {
            var goal=balconyMotion.CliffTarget(friend.Position,1f/60,i==0);
            if (goal==null) break;
            var offset=goal.Value-friend.Position; offset.Y=0;
            balconyMotion.Step(friend,offset,climber,false,false,1f/60,true,out _);
            balconyPeak=Math.Max(balconyPeak,friend.Position.Y);
            if (friend.Position.Y is >5.85f and <7.36f &&
                FlatDistance(friend.Position,(GardenArt.BalconyRampStart+GardenArt.BalconyRampEnd)*.5f)<.62f &&
                balconyMotion.Airborne)
                throw new Exception($"Chao jumped while walking up the balcony ramp at {friend.Position}, toward {goal}");
            if (friend.Position.Y>=GardenArt.BalconyRail.Y-.12f &&
                FlatDistance(friend.Position,GardenArt.BalconyRail)<.40f) reachedRail=true;
            if (reachedRail && friend.Position.Z>GardenArt.BalconyRail.Z+.45f &&
                balconyMotion.Airborne) jumpedRail=true;
        }
        if (!jumpedRail) throw new Exception($"Chao did not climb and jump from the balcony rail: {friend.Position}, peak {balconyPeak}");
        player.Position=ZoneArt.MachineStep;
        Interact();
        if (machinePanel.Visible || !machineOpening)
            throw new Exception("Machine menu appeared before the hatch opened");
        for (int i=0;i<40 && !machineOpen;i++) UpdateMachineLoad(1f/60);
        if (!machineOpen || !renameButton.Disabled || machinePet!=null || !machineChime.Playing)
            throw new Exception("Empty machine did not open with disabled actions and a chime");
        CloseMachine();
        var toy=world.GetNode<ToyCar>("ToyCar");
        friend.Position=toy.Position-Vector3.Back*.40f;
        var preference=new PetState { LikesToyCars=false };
        if (toy.Use(friend,friendArt,preference,out _))
            throw new Exception("Chao played with a toy it does not like");
        preference.LikesToyCars=true;
        if (!toy.Use(friend,friendArt,preference,out _))
            throw new Exception("Chao could not play with a toy it likes");
        toy.Advance(5);
        if (toy.PlayPose(friend,friendArt) || (toy.Position-friend.Position).Dot(Vector3.Back)<.45f ||
            Mathf.Abs(toy.Position.Y-friend.Position.Y)>.07f)
            throw new Exception("Chao did not leave the toy on the ground in front");
        player.Position=new(7,0,6);
        friend.Position=new(8,0,6);
        Face(friend.Position);
        pet.LikesToyCars=false;
        heldToy=toy;
        UpdateOffering(1f/60);
        player.Rotation+=Vector3.Up*Mathf.Pi;
        UpdateOffering(1f/60);
        if (offerTime>0 || heldToy!=toy || !moriFruitReaction.IsConfused)
            throw new Exception("Turning away did not cancel the toy handoff");
        moriFruitReaction.Interrupt(friendArt);
        Face(friend.Position);
        for (int i=0;i<37;i++) UpdateOffering(1f/60);
        if (offerLean<.7f || offerTime<=0 || heldToy.Position.DistanceTo(HandPosition())>.015f ||
            heldToy.Position.DistanceTo(friend.Position+Vector3.Up*.16f)>.60f)
            throw new Exception($"The item did not follow the avatar's hands to the Chao: {heldToy.Position} / {friend.Position}");
        for (int i=0;i<25;i++) UpdateOffering(1f/60);
        if (thinkTime<=0 || heldToy!=toy || !friendArt.FindChildren("*","Label3D",true,false)
            .OfType<Label3D>().Any(label=>label.Text=="?" && label.Visible))
            throw new Exception("Chao did not think about the toy before rejecting it");
        RunOfferCheck();
        if (heldToy!=null || toy.CanPickUp) throw new Exception("Rejected toy was not thrown");
        for (int i=0;i<150;i++) toy.Advance(1f/60);
        if (!toy.CanPickUp) throw new Exception("Rejected toy could not be picked back up");
        pet.LikesToyCars=true; pet.Fun=30;
        heldToy=toy; RunOfferCheck();
        if (heldToy!=null || pet.Fun<=30 || !toy.PlayPose(friend,friendArt))
            throw new Exception("Chao did not play with the offered toy");
        toy.Advance(5);
        var rejectedFruit=world.GetChildren().OfType<FruitTree>().First(tree=>tree.IsFeeder);
        heldFruit=rejectedFruit; pet.Fullness=100;
        for (int i=0;i<170;i++) UpdateOffering(1f/60);
        if (heldFruit!=rejectedFruit || rejectedFruit.Bites!=0 || eatTime<=0)
            throw new Exception("A full Chao skipped the slow chewing animation");
        RunOfferCheck();
        if (heldFruit!=null || upsetTime<=0 || rejectedFruit.HasFruitOnGround ||
            rejectedFruit.Bites>1 || (rejectedFruit.FruitPosition-friend.Position).Dot(friend.GlobalBasis.Z)>=0)
            throw new Exception("Full Chao did not throw away the fruit with an upset reaction");
        rejectedFruit.Regrow();
        pet.Fullness=72; upsetTime=0;
        var age=new PetState { AgeSeconds=2*PetState.SecondsPerChaoYear+1 };
        if (age.AgeYears!=2) throw new Exception("Chao age conversion failed");
        float splashBeat=0;
        SwimAudio.Paddle(mainSwimSound,1f/60,true,ref splashBeat);
        if (!mainSwimSound.Playing || splashBeat<=0)
            throw new Exception("Swimming did not trigger a splash sound");
        var bonusEgg=world.GetChildren().OfType<GardenEgg>().First();
        bonusEgg.Hold(bonusEgg.Position+Vector3.Up*.7f,true,5.1f);
        bonusEgg.Release(Vector3.Zero,false);
        for(int i=0;i<180 && bonusEgg.InFlight;i++) UpdateEggs(1f/60);
        UpdateUi();
        if (!details.Visible || bonusEgg.Chao==null || !formLabel.Text.Contains(bonusEgg.Chao.Name))
            throw new Exception("Hatchling did not get its card");
        player.Position=bonusEgg.Position+Vector3.Right*.8f;
        Interact();
        if (heldEgg!=bonusEgg) throw new Exception("Could not pick up the hatchling");
        UpdatePickupPose(PickupDuration);
        player.Position=ZoneArt.MachineStep+Vector3.Up*.13f;
        for(int i=0;i<90 && !machineOpen;i++) UpdateMachineLoad(1f/60);
        if (!machineOpen || machinePet!=bonusEgg.Chao) throw new Exception("Machine did not accept the hatchling");
        RenameChao("Pebble");
        if (bonusEgg.Chao.Name!="Pebble" || machineOpen) throw new Exception("Machine did not rename the hatchling");
        bonusEgg.Park(ChaoRest.Bed+Vector3.Up*.85f);
        bonusEgg.Release(Vector3.Zero,false);
        for (int i=0;i<120 && bonusEgg.InFlight;i++) bonusEgg.Advance(1f/60);
        if (bonusEgg.InFlight || bonusEgg.Position.Y<.39f)
            throw new Exception("Placed Chao clipped through the cushion");
        bonusEgg.Park(ZoneArt.ChaoMachine+Vector3.Back*2.5f);
        foreach (string color in new[]{"Black","Yellow"})
        {
            var sample=new GardenEgg { Data=new GardenEggState { Color=color,Hatched=true } };
            world.AddChild(sample);
            foreach (string part in new[]{"Body_2","FootL","PaddleR","Center_Mouth"})
                if (sample.FindChild(part,true,false) is not MeshInstance3D mesh ||
                    mesh.MaterialOverride is not StandardMaterial3D paint || paint.AlbedoColor!=GardenEgg.Pigment(color))
                    throw new Exception($"{color} Chao did not color {part}");
            if (sample.FindChild("Left_Eye",true,false) is not MeshInstance3D eye ||
                eye.MaterialOverride!=null || (color=="Black" && eye.GetChildCount()==0))
                throw new Exception($"{color} Chao did not preserve its eye material and contrast");
            if (sample.FindChild("Wings",true,false) is not MeshInstance3D wings || wings.MaterialOverride!=null)
                throw new Exception($"{color} Chao wings lost their original material");
            if (color=="Black" && sample.FindChild("FootL",true,false) is Node3D foot)
            {
                var rest=foot.Position;
                ImportedChao.Gait(sample.Art,Mathf.Pi*.5f,true,false);
                if (foot.Position.DistanceTo(rest)<.02f)
                    throw new Exception("Black Chao foot did not stride");
            }
            world.RemoveChild(sample);
            sample.QueueFree();
        }
        if (!bonusEgg.Data.Hatched) throw new Exception("Garden egg did not hatch after being rocked and set down");
        var testRock=new Node3D();
        AddChild(testRock);
        ZoneArt.Solid(testRock,new(-1,.85f,8),new(1.3f,1.7f,.4f),stone:true);
        var tossed=new GardenEgg { Data=new GardenEggState(),Position=new(-1,.55f,9.4f) };
        world.AddChild(tossed);
        tossed.Release(Vector3.Forward*5,true);
        for(int i=0;i<180 && tossed.InFlight;i++) tossed.Advance(1f/60);
        if (!tossed.Data.Hatched) throw new Exception("Egg did not hatch after hitting stone");
        tossed.QueueFree();
        friend.Position=new(-1,.55f,9.4f);
        throwVelocity=Vector3.Forward*5;
        moriInFlight=true;
        float joyBefore=pet.Joy;
        for(int i=0;i<180 && moriInFlight;i++) MoveFriend(1f/60);
        if (upsetTime<=0 || pet.Joy>=joyBefore ||
            friendArt.FindChild("MoodOrb",true,false) is not EmotionOrb)
            throw new Exception("Mori did not react to the stone impact");
        upsetTime=0;
        RemoveChild(testRock);
        testRock.QueueFree();
        friend.Position=new(-11,2,-14.5f);
        throwVelocity=Vector3.Forward*8;
        moriInFlight=true;
        for(int i=0;i<180 && moriInFlight;i++) MoveFriend(1f/60);
        if (upsetTime<=0 || friend.Position.Z < -17)
            throw new Exception("Waterfall rock did not stop and upset a thrown Chao");
        upsetTime=0;
        string editorDirectory=System.IO.Path.GetDirectoryName(savePath)!;
        var bridge=new ChaoEditorBridge(editorDirectory);
        bridge.Poll(new Dictionary<string,PetState> { ["main"]=pet,[bonusEgg.Data.Id]=bonusEgg.Chao },true);
        using (var json=System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(
            System.IO.Path.Combine(editorDirectory,"chao-editor-state.json"))))
        {
            var session=json.RootElement.GetProperty("session").GetString();
            if (json.RootElement.GetProperty("chao").GetArrayLength()!=2)
                throw new Exception("Editor roster omitted the hatchling");
            System.IO.File.WriteAllText(System.IO.Path.Combine(editorDirectory,"chao-editor-request.json"),
                System.Text.Json.JsonSerializer.Serialize(new { session,id="smoke",target=bonusEgg.Data.Id,patch=new { Swim=345,Joy=77.5f } }));
            var edited=bridge.Poll(new Dictionary<string,PetState> { ["main"]=pet,[bonusEgg.Data.Id]=bonusEgg.Chao },true);
            if (edited==null || edited.Swim!=345 || edited.Joy!=77.5f || pet.Swim==345 || bridge.EditedTarget!=bonusEgg.Data.Id)
                throw new Exception("Live editor patch failed");
        }
        var motionCheck=new ChaoMotion();
        var gaitCheck=new PetState { Run=750 };
        friend.Position=new(8,.01f,0);
        for (int i=0;i<75;i++) motionCheck.Step(friend,Vector3.Right*12,gaitCheck,true,false,1f/60,true,out _);
        if (!motionCheck.Sprinting) throw new Exception("High Run Chao did not accelerate into a sprint");
        motionCheck.Reset();
        friend.Position=new(-2,.82f,-10);
        for (int i=0;i<150;i++)
        {
            var goal=new Vector3(-2,0,-12.7f)-friend.Position; goal.Y=0;
            motionCheck.Step(friend,goal,new PetState(),false,false,1f/60,true,out _);
        }
        if (friend.Position.Y<1.54f) throw new Exception($"Chao did not jump onto the cliff ledge: {friend.Position}");
        motionCheck.Reset();
        friend.Position=new(8,2.5f,0);
        bool hardLanding=false;
        for (int i=0;i<120 && !hardLanding;i++)
            motionCheck.Step(friend,Vector3.Zero,gaitCheck,false,false,1f/60,true,out hardLanding);
        if (!hardLanding || !motionCheck.Recover(friendArt,.1f))
            throw new Exception("Tall fall did not trigger a tumble");
        motionCheck.Reset();
        friend.Position=new(8,2.5f,0);
        gaitCheck.Fly=300;
        motionCheck.Step(friend,Vector3.Right*3,gaitCheck,false,false,1f/60,true,out _);
        if (motionCheck.Flying || friend.Position.Y>=2.5f)
            throw new Exception("Falling Chao floated instead of descending");
        motionCheck.Reset();
        friend.Position=new(-2,.77f,-8.50f);
        for (int i=0;i<25 && !motionCheck.Airborne;i++)
            motionCheck.Step(friend,Vector3.Back*2,new PetState(),false,false,1f/60,true,out _);
        if (!motionCheck.Airborne || friend.Position.Y<=.77f)
            throw new Exception($"Chao stepped off a high ledge without jumping: {friend.Position}");
        float headingBefore=friend.Rotation.Y;
        for (int i=0;i<10;i++)
            motionCheck.Step(friend,Vector3.Right*3,new PetState(),false,false,1f/60,true,out _);
        if (!motionCheck.Airborne || Mathf.Abs(Mathf.AngleDifference(headingBefore,friend.Rotation.Y))>.05f)
            throw new Exception("Chao turned during a jump");
        bool descended=false, bounced=false;
        for (int i=0;i<150 && motionCheck.Airborne;i++)
        {
            float beforeLanding=friend.Position.Y;
            motionCheck.Step(friend,Vector3.Right*3,new PetState(),false,false,1f/60,true,out _);
            if (friend.Position.Y<beforeLanding-.005f) descended=true;
            if (descended && friend.Position.Y>beforeLanding+.005f) bounced=true;
        }
        if (bounced || !motionCheck.IsSwimmingAt(friend.Position))
            throw new Exception("Chao did not start swimming on entering the pond");
        var dryLedge=new Node3D();
        world.AddChild(dryLedge);
        ZoneArt.Solid(dryLedge,new(10,.4f,0),new(1,.8f,1));
        motionCheck.Reset();
        friend.Position=new(10,.77f,.38f);
        descended=bounced=false;
        for (int i=0;i<100;i++)
        {
            float beforeLanding=friend.Position.Y;
            motionCheck.Step(friend,Vector3.Back*2,new PetState(),false,false,1f/60,true,out _);
            if (friend.Position.Y<beforeLanding-.005f) descended=true;
            if (descended && friend.Position.Y>beforeLanding+.005f) bounced=true;
        }
        if (!bounced) throw new Exception("Dry landing did not produce a small rebound");
        dryLedge.QueueFree();
        // Интеграционная проверка настоящей сцены в отдельном тестовом сохранении.
        player.Position = kitchen; friend.Position = kitchen + Vector3.Right;
        Face(friend.Position);
        float oldFood = pet.Fullness;
        Interact();
        if (heldFruit?.IsFeeder!=true) throw new Exception("Feeder fruit is not usable");
        RunOfferCheck();
        if (pet.Fullness <= oldFood) throw new Exception("Automatic feeding did not work");
        PetFriend();
        if (pet.Care != 2) throw new Exception("Petting did not work");
        if (camera.Projection != Camera3D.ProjectionType.Perspective) throw new Exception("Not third person perspective");
        cameraRig.Yaw = Mathf.Pi/2;
        if (cameraRig.RelativeDirection(new(0,-1)).X > -.99f) throw new Exception("Movement not relative to camera");
        cameraRig.Yaw = 0;
        // Поднять питомца и пройти через выход без нажатия кнопки перехода.
        player.Position = new(0,0,0); friend.Position = new(1,0,0); Interact();
        if (!carrying) throw new Exception("Pickup failed");
        UpdateUi();
        if (!details.Visible) throw new Exception("Pickup did not open Mori stats");
        Vector3 liftedFrom = friend.Position;
        UpdatePickupPose(.29f);
        MoveFriend(.02f);
        if (playerArt.Position.Y>=-.05f) throw new Exception("Pickup did not begin with a crouch");
        UpdatePickupPose(.23f);
        MoveFriend(.02f);
        if (friend.Position.Y<=liftedFrom.Y+.12f) throw new Exception("Pickup did not animate the lift");
        player.Position = ZoneArt.GardenLobbyGate; portalCooldown = 0; CheckPortals();
        if (transition < 0) throw new Exception("Portal did not trigger by walking");
        AdvanceTransition(.36f); AdvanceTransition(.36f);
        if (zone != GardenZone.Lobby || !friend.Visible || !carrying || pet.Care != 2) throw new Exception("Lobby transition lost pet");
        CheckPortals();
        if (transition >= 0) throw new Exception("Portal bounced on arrival");
        player.Position=ZoneArt.PlazaShop;
        carrying=false;
        pet.Stars=3;
        int eggsBefore=pet.GardenEggs.Count;
        Interact();
        if (!shopOpen) throw new Exception("Egg Shop did not open");
        _UnhandledKeyInput(new InputEventKey { PhysicalKeycode=Key.Right,Pressed=true });
        _UnhandledKeyInput(new InputEventKey { PhysicalKeycode=Key.Enter,Pressed=true });
        if (pet.Stars!=0 || pet.GardenEggs.Count!=eggsBefore+1 || pet.GardenEggs[^1].Color!="Red")
            throw new Exception("Egg Shop purchase failed");
        _UnhandledKeyInput(new InputEventKey { PhysicalKeycode=Key.Escape,Pressed=true });
        carrying=true;

        player.Position = ZoneArt.PlazaStadiumPad; portalCooldown = 0; CheckPortals();
        AdvanceTransition(.36f); AdvanceTransition(.36f);
        if (zone != GardenZone.Stadium) throw new Exception("Stadium entrance failed");
        carrying = false;
        StartRace();
        pet.Swim = pet.Fly = pet.Run = pet.Power = 160;
        pet.Stamina = 160;
        for (int i=0;i<3000 && racing;i++) UpdateRace(1f/60);
        if (pet.Stars != 8 || pet.Agility != 3 || racing) throw new Exception("Race failed");
        StartRace();
        rivalProgress=.2f;
        Cheer();
        if (cheerTime<=0) throw new Exception("A trailing racer did not get the cheer boost");
        raceTime = .01f;
        _Process(.02);
        if (racing || pet.Stars != 8) throw new Exception("Race timeout failed");
        carrying = true; SwitchZone(GardenZone.Garden); carrying = false;
        cameraRig.Yaw = 0;
        player.Position = bed;
        _UnhandledKeyInput(new InputEventKey { PhysicalKeycode = Key.R, Pressed = true });
        if (!moriRest.Requested || sleeping) throw new Exception("Rest command teleported Mori");
        friend.Position=bed+Vector3.Back*1.9f;
        moriRest.Cancel(); moriRest.Request(friend.Position);
        for(int i=0;i<150 && !sleeping;i++) MoveFriend(1f/60);
        if (!sleeping || friend.Position.Y<.40f) throw new Exception("Mori did not walk onto the bed");
        pet.Energy=100;
        for(int i=0;i<60;i++) MoveFriend(1f/60);
        if (!sleeping || napRemaining<ChaoRest.MinimumSleep-1.1f ||
            Mathf.Abs(friendArt.RotationDegrees.Z-88)>.5f)
            throw new Exception("Mori did not settle into a full nap on the bed");
        _UnhandledKeyInput(new InputEventKey { PhysicalKeycode = Key.Q, Pressed = true });
        if (!sleeping || following) throw new Exception("Low bond should not wake Mori");
        pet.Bond = PetState.CallBondThreshold;
        _UnhandledKeyInput(new InputEventKey { PhysicalKeycode = Key.Q, Pressed = true });
        if (sleeping || !following) throw new Exception("Wake key failed at high bond");
        _UnhandledKeyInput(new InputEventKey { PhysicalKeycode = Key.Escape, Pressed = true });
        if (!paused) throw new Exception("Pause key failed");
        player.Position = new(0,0,5);
        var oldPosition = player.Position;
        _Process(.1);
        if (player.Position != oldPosition) throw new Exception("Pause changed position");
        _UnhandledKeyInput(new InputEventKey { PhysicalKeycode = Key.Escape, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.W, Keycode = Key.W, Pressed = true });
        Input.FlushBufferedEvents();
        MovePlayer(.2f);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.W, Keycode = Key.W, Pressed = false });
        Input.FlushBufferedEvents();
        if (player.Position.Z >= oldPosition.Z) throw new Exception("Keyboard movement failed");
        _UnhandledKeyInput(new InputEventKey { PhysicalKeycode = Key.Space, Pressed = true });
        MovePlayer(.1f);
        if (player.Position.Y <= 0) throw new Exception("Jump failed");
        for (int i=0;i<20;i++) MovePlayer(.1f);
        if (Mathf.Abs(player.Position.Y) > .025f) throw new Exception("Jump did not land");
        Save(); var loaded = SaveStore.Load(savePath, out _);
        if (loaded.Stars != 8) throw new Exception("Save failed");
        pet = new PetState { Care = 10 };
        if (!pet.Evolve() || pet.Form != "bloom") throw new Exception("Evolution failed");
        var tree = world.GetChildren().OfType<FruitTree>().First(t=>!t.IsFeeder);
        player.Position = tree.Position - Vector3.Right*1.5f;
        friend.Position = tree.FruitPosition;
        pet.Fullness = 50;
        Interact();
        for (int i=0;i<90;i++) { UpdatePickupPose(1f/60); tree.UpdateFruit(1f/60); }
        if (tree.HasFruitOnGround || tree.FruitPosition.Y<3.4f)
            throw new Exception("Fruit fell before the shaking finished");
        for (int i=0;i<180;i++) { UpdatePickupPose(1f/60); tree.UpdateFruit(1f/60); }
        if (!tree.HasFruitOnGround) throw new Exception("Tree did not drop fruit");
        player.Position=tree.FruitPosition+Vector3.Left*.75f;
        friend.Position=player.Position+Vector3.Right;
        Face(friend.Position);
        Interact();
        RunOfferCheck();
        if (tree.HasFruitOnGround || pet.Fullness <= 50) throw new Exception("Tree fruit did not feed pet");
        if (pet.Stamina < 2) throw new Exception("Fruit did not train Stamina");
        tree.Shake();
        for (int i=0;i<90;i++) tree.UpdateFruit(1f/60);
        if (tree.HasFruitOnGround) throw new Exception("Unripe fruit fell from tree");
        foreach (var slot in tree.Slots) slot.UpdateFruit(140);
        tree.Shake();
        for (int i=0;i<240;i++)
            foreach (var slot in tree.Slots) slot.UpdateFruit(1f/60);
        if (tree.Slots.Count(slot=>slot.HasFruitOnGround)!=3)
            throw new Exception("Three mature fruits did not fall");
        var smartChao=world.GetChildren().OfType<GardenEgg>().First(egg=>egg.Chao!=null);
        var smartStart=smartChao.Position;
        smartChao.Chao!.Intelligence=80;
        smartChao.Chao.Initiative=65;
        smartChao.Chao.Fullness=50;
        smartChao.Park(tree.FruitPosition+Vector3.Right*.35f);
        for (int i=0;i<1500 && smartChao.Chao.Care==0;i++)
            smartChao.Advance(1f/60,FruitSlots());
        if (smartChao.Chao.Fullness<=50 || tree.HasFruitOnGround)
            throw new Exception("A hungry Chao with high intelligence and initiative did not eat a fallen fruit");
        smartChao.Park(smartStart);
        foreach (var slot in tree.Slots) slot.Regrow();
        var bagFruit=world.GetChildren().OfType<FruitTree>().First(t=>t.IsFeeder);
        heldFruit=bagFruit;
        bagFruit.Hold(HandPosition());
        _UnhandledKeyInput(new InputEventKey { PhysicalKeycode=Key.G,Pressed=true });
        if (heldFruit!=null || pet.BagFruit!=1) throw new Exception("Inventory did not store the fruit");
        if (SaveStore.Load(savePath,out _).BagFruit!=1) throw new Exception("Inventory was not saved");
        player.Position=friend.Position+Vector3.Right;
        pet.Fullness=50;
        _UnhandledKeyInput(new InputEventKey { PhysicalKeycode=Key.H,Pressed=true });
        Face(friend.Position);
        RunOfferCheck();
        if (pet.BagFruit!=0 || pet.Fullness<=50) throw new Exception("Inventory fruit did not feed Mori");
        var bird = world.GetChildren().OfType<TrainingAnimal>().First(a => a.Skill == AnimalSkill.Fly);
        int oldFly = pet.Fly;
        player.Position = bird.Position + Vector3.Right;
        Interact();
        if (heldAnimal != bird) throw new Exception("Could not pick up a training animal");
        UpdatePickupPose(PickupDuration);
        player.Position = new(7,0,10);
        friend.Position = new(1,0,2);
        UpdateTrainingAnimals(0);
        Vector3 release = bird.Position;
        Interact();
        for (int i=0;i<30;i++) { UpdateTrainingAnimals(1f/60); UpdatePickupPose(1f/60); }
        if (heldAnimal != null || FlatDistance(bird.Position,release)>.8f)
            throw new Exception("Released animal teleported away");
        player.Position = bird.Position + Vector3.Right;
        Interact();
        friend.Position = player.Position + Vector3.Forward;
        Face(friend.Position);
        RunOfferCheck();
        if (heldAnimal != null || bird.Available || pet.Fly != oldFly+12)
            throw new Exception("Training animal did not grant Fly XP");
        friend.Position = GardenArt.SwimExit;
        following = false;
        callTime = 0;
        swimmingTrip = true;
        leavingWater = false;
        swimTime = 0;
        pet.Swim=Math.Max(100,pet.Swim);
        int oldSwim = pet.Swim;
        bool sawWake = false;
        for (int i=0; i<1200 && swimmingTrip; i++)
        {
            MoveFriend(1f/60);
            sawWake |= wake.Visible;
        }
        if (!sawWake || swimmingTrip || pet.Swim <= oldSwim)
            throw new Exception("Mori did not complete a swim and return to shore");

        var cameraWall = new Node3D();
        AddChild(cameraWall);
        ZoneArt.Solid(cameraWall,new(0,1.5f,9),new(4,3,.5f));
        cameraRig.Yaw = 0;
        cameraRig.Follow(new(0,0,7),0,true);
        if (camera.GlobalPosition.Z >= 8.65f)
            throw new Exception("Camera entered an obstacle");
        RemoveChild(cameraWall);
        cameraWall.QueueFree();
        player.Position = GardenArt.Pond+new Vector3(0,0,4);
        for (int i=0;i<45;i++) MovePlayer(1f/60);
        if (!playerWake.Visible || Mathf.Abs(player.Position.Y+GardenArt.PondDepth)>.025f ||
            FlatDistance(player.Position,GardenArt.Pond)>GardenArt.PondRadius)
            throw new Exception("Player could not stand in the pond");
        player.Position = GardenArt.Pond+new Vector3(9.65f,0,9.65f);
        cameraRig.Yaw = 0;
        DriveShoreCheck(210,Key.W,Key.A);
        if (player.Position.Y > -.265f) throw new Exception("Could not wade down the shore");
        DriveShoreCheck(240,Key.S,Key.D);
        if (player.Position.Y < -.045f || FlatDistance(player.Position,GardenArt.Pond)<GardenArt.PondRadius)
            throw new Exception($"Could not walk back out of the pond: {player.Position}");
        var testStone = new Node3D();
        AddChild(testStone);
        ZoneArt.Solid(testStone,new(0,.13f,10),new(1,.26f,.95f));
        player.Position = new(0,.015f,9.15f);
        verticalSpeed = 0;
        var smallHit = player.MoveAndCollide(Vector3.Back*.28f);
        bool stepped = smallHit != null && TryStepUp(smallHit.GetRemainder());
        if (!stepped || player.Position.Y<.19f)
            throw new Exception($"Low obstacle: contact={smallHit != null}, stepped={stepped}, position={player.Position}, verticalSpeed={verticalSpeed}");
        RemoveChild(testStone);
        testStone.QueueFree();

        player.Position = new(-12.6f,-GardenArt.PondDepth,-7.45f);
        verticalSpeed=0;
        DriveShoreCheck(85,Key.W);
        if (player.Position.Y<.28f)
            throw new Exception($"Could not walk onto the island without jumping: {player.Position}");
        var islandNap=new IslandNap();
        var swimmer=new PetState { Swim=120,Energy=60 };
        var islandMotion=new ChaoMotion();
        friend.Position=new(-8.7f,0,-10.39f);
        for (int i=0;i<3700 && !islandNap.IsNapping;i++)
            islandNap.Step(friend,friendArt,swimmer,islandMotion,1f/60);
        if (!islandNap.IsNapping || friendArt.FindChild("Left_Eye",true,false) is not MeshInstance3D { Visible:false })
            throw new Exception($"Chao did not reach the island and close its eyes: {friend.Position}");
        islandNap.Cancel(friendArt);
        if (importedAvatar == null || avatarWalk == null) throw new Exception("The supplied avatar and leg rig did not load");

        var firstLedge = GardenArt.Ledges[0];
        player.Position = new(firstLedge.Center.X,.01f,firstLedge.Center.Z+2.6f);
        var cliffHit = player.MoveAndCollide(Vector3.Forward*2.6f);
        if (cliffHit == null || cliffHit.GetNormal().Z < .7f)
            throw new Exception("Player passed through the side of a ledge");
        player.Position = new(firstLedge.Center.X,1.1f,firstLedge.Center.Z);
        for (int i=0;i<12;i++) MovePlayer(.04f);
        if (Mathf.Abs(player.Position.Y-.8f) > .025f) throw new Exception("Could not land on ledge");
        _UnhandledKeyInput(new InputEventKey { PhysicalKeycode = Key.Space, Pressed = true });
        MovePlayer(.05f);
        if (player.Position.Y <= .85f) throw new Exception("Could not jump from ledge");
        verticalSpeed = 0;
        pet = new PetState(); petZone = GardenZone.Garden; petZonePosition = new(1.8f,0,3);
        pet = PetState.NewEgg();
        friendArt.QueueFree();
        friendArt = BuildLifeArt();
        player.Position = new(0,0,5);
        friend.Position = new(1,0,5);
        Interact();
        if (!carrying || !pet.IsEgg) throw new Exception("Egg pickup failed");
        UpdatePickupPose(PickupDuration);
        MoveFriend(.02f);
        mainEggShake=5.1f;
        Interact();
        UpdatePickupPose(SetDownDuration);
        if (pet.IsEgg || !pet.CanAct) throw new Exception("Rocked egg did not hatch on placement");
        pet.Care = 10;
        pet.TrainWithAnimal("Power");
        if (!pet.AdvanceLife(90) || !pet.InCocoon) throw new Exception("Cocoon did not form");
        OnLifeChanged();
        var cocoonSave = SaveStore.Load(savePath,out _);
        if (!cocoonSave.InCocoon || cocoonSave.RecentAnimalSkill != "Power")
            throw new Exception("Cocoon did not survive save/load");
        if (!pet.AdvanceLife(9) || pet.Form != "stone") throw new Exception("Adult form did not emerge");
        OnLifeChanged();
        pet = new PetState();
        friendArt.QueueFree();
        friendArt = BuildLifeArt();
        player.Position = new(0,0,4);
        friend.Position = new(1,0,4);
        Interact();
        if (!carrying) throw new Exception("Could not pick up Mori for release checks");
        UpdatePickupPose(PickupDuration);
        MoveFriend(.02f);
        float carriedHeight = friend.Position.Y;
        Interact();
        UpdatePickupPose(.14f);
        if (carrying || releasePose<=0 || friend.Position.Y>=carriedHeight || friend.Position.Y<=.04f)
            throw new Exception("Mori did not lower gradually");
        UpdatePickupPose(.34f);
        if (Mathf.Abs(friend.Position.Y-setDownTo.Y)>.025f) throw new Exception("Set-down did not reach the surface");
        Interact();
        UpdatePickupPose(PickupDuration);
        MoveFriend(.02f);
        Vector3 launchPosition = friend.Position;
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode=Key.W, Keycode=Key.W, Pressed=true });
        Input.FlushBufferedEvents();
        Interact();
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode=Key.W, Keycode=Key.W, Pressed=false });
        Input.FlushBufferedEvents();
        if (!moriInFlight || carrying) throw new Exception("Moving action did not throw Mori");
        for (int i=0;i<180 && moriInFlight;i++) MoveFriend(1f/60);
        if (moriInFlight || FlatDistance(launchPosition,friend.Position)<1)
            throw new Exception("Thrown Mori did not land at a new position");
        if (pet.RelationshipWith()>=0 || friendArt.RotationDegrees.X<85)
            throw new Exception("Throw did not affect the relationship or land face down");
        UpdatePickupPose(SetDownDuration);
        var landing=friend.Position;
        MoveFriend(2.9f);
        if (friend.Position!=landing || friendArt.RotationDegrees.X<85)
            throw new Exception("Thrown Chao got up too early");
        MoveFriend(.2f);
        MoveFriend(.02f);
        if (friendArt.GetNodeOrNull<Node3D>("Tears") is not { Visible:true })
            throw new Exception("Thrown Chao did not cry after lying still");
        moriDistress.PickUp(friendArt,true);
        moriDistress.PutDown();
        MoveFriend(29f);
        if (friend.Position!=landing || !moriDistress.Upset || !moriDistress.Busy)
            throw new Exception("Woken Chao did not stay upset and stand still");
        MoveFriend(32f);
        if (moriDistress.Upset || moriDistress.Busy)
            throw new Exception("Woken Chao did not finish sulking");
        MoveFriend(.02f);
        if (music == null || music.Stream is not AudioStreamWav { LoopMode: AudioStreamWav.LoopModeEnum.Forward })
            throw new Exception("Music loop did not load");
        var orb = friendArt.GetChildren().OfType<EmotionOrb>().Single();
        orb._PhysicsProcess(1f/60);
        var orbRest = orb.Position;
        friend.Position += Vector3.Right*.12f;
        orb._PhysicsProcess(1f/60);
        if (orb.Position.DistanceTo(orbRest)<.025f)
            throw new Exception("Orb did not lag behind motion");
        for (int i=0;i<120;i++) orb._PhysicsProcess(1f/60);
        if (orb.Position.DistanceTo(orbRest)>.005f)
            throw new Exception("Orb did not return to rest");
        if (world.GetNodeOrNull<AudioStreamPlayer3D>("WaterfallSound")?.Stream is not AudioStreamWav)
            throw new Exception("Waterfall spatial audio did not load");
        CheckHeartFruitAndCourtship();
        CheckWaterInteractions();
        SetPreviewPosition();
        Notify("Explore the garden. Carry Mori through an arch to visit another area.", 30);
    }

    private void CheckWaterInteractions()
    {
        cameraRig.Yaw=0;
        player.Position=kitchen+Vector3.Back*2.4f;
        verticalSpeed=0;
        DriveShoreCheck(110,Key.W);
        if (player.Position.Y>.04f || FlatDistance(player.Position,kitchen)<1.22f)
            throw new Exception($"Walking into the feeder lifted the player: {player.Position}");
        player.Position=GardenArt.Pond+new Vector3(3,6,3);
        verticalSpeed=0;
        for (int i=0;i<90 && !impactSound.Playing;i++) MovePlayer(1f/60);
        if (!impactSound.Playing || !impactSpray.Emitting)
            throw new Exception("High fall into the pond did not trigger the large splash");
        player.Position=GardenArt.Pond+new Vector3(4,-GardenArt.PondDepth,4);
        verticalSpeed=0;
        DriveShoreCheck(60,Key.W);
        if (!wadeSound.Playing) throw new Exception("Walking in the pond did not trigger a water step");
        var bedJump=new ChaoMotion();
        friend.Position=ChaoRest.Bed+new Vector3(.39f,.43f,.33f);
        var novice=new PetState();
        bedJump.LeapFromBed(friend,novice);
        for (int i=0;i<90 && bedJump.Airborne;i++)
            bedJump.Step(friend,Vector3.Zero,novice,false,false,1f/60,true,out _);
        if (bedJump.Airborne || friend.Position.Y>.12f || FlatDistance(friend.Position,ChaoRest.Bed)<1.55f)
            throw new Exception($"Chao did not jump clear of the bed: {friend.Position}");
        var can=CanInGarden ?? throw new Exception("Watering can is missing");
        friend.Position=can.Position+Vector3.Back;
        var route=new ChaoMotion();
        can.Advance(200);
        if (can.Step(friend,friendArt,new PetState { Intelligence=240 },route,.016f,true))
            throw new Exception("Chao without enough kindness took the watering can");
        var gardener=new PetState { Intelligence=240,Kindness=65 };
        var inside=can.GetNode<Node3D>("WaterInside");
        var drops=can.GetNode<CpuParticles3D>("WateringDrops");
        bool filled=false, poured=false, done=false;
        for (int i=0;i<4800;i++)
        {
            can.Advance(1f/30);
            if (!can.Step(friend,friendArt,gardener,route,1f/30,true)) { done=true; break; }
            filled|=inside.Visible;
            poured|=drops.Emitting;
        }
        if (!filled || !poured || !done || inside.Visible || can.Position.DistanceTo(friend.Position)<.43f)
            throw new Exception($"Watering trip did not finish: filled={filled}, poured={poured}, done={done}, can={can.Position}, Chao={friend.Position}");
    }

    private void CheckHeartFruitAndCourtship()
    {
        shopColor=GardenEgg.Colors.Length;
        pet.Stars=4;
        BuyShopItem();
        BuyShopItem();
        if (pet.BagHeartFruit!=2 || pet.Stars!=0) throw new Exception("Heart Fruit was not sold into the bag");
        var parent=new GardenEgg
        {
            Data=new GardenEggState { Color="Pink",Hatched=true,Chao=new PetState { Fullness=100 } },
            Position=new(13.1f,0,2),Courtship=courtship
        };
        world.AddChild(parent);
        friend.Position=new(12,0,2);
        pet.Fullness=100;
        foreach (var target in new[]{friend.Position,parent.Position})
        {
            player.Position=target+Vector3.Back;
            Face(target);
            _UnhandledKeyInput(new InputEventKey { PhysicalKeycode=Key.V,Pressed=true });
            if (heldFruit?.IsHeartFruit!=true) throw new Exception("Heart Fruit could not be taken from the bag");
            RunOfferCheck();
        }
        if (pet.BagHeartFruit!=0 || pet.CourtshipReadiness!=100 || parent.Chao!.CourtshipReadiness!=100)
            throw new Exception("Each Heart Fruit should prepare one Chao even if it is full");
        if (!courtship.Sit(friend,friendArt,pet,"White",world,.5f))
            throw new Exception("The first ready Chao did not sit among flowers");
        var nest=friend.Position;
        parent.Hold(parent.Position,false,.02f);
        parent.Release(Vector3.Zero,false);
        for (int i=0;i<30 && !courtship.IsBlooming(parent);i++) parent.Advance(1f/60);
        int eggCount=pet.GardenEggs.Count;
        courtship.Advance(1f,true,LayGardenEgg);
        if (!courtship.IsPaired(parent) || pet.GardenEggs.Count!=eggCount)
            throw new Exception("The placed parent did not start courtship before the egg appeared");
        courtship.Advance(1.8f,true,LayGardenEgg);
        var egg=pet.GardenEggs.Last();
        if (pet.GardenEggs.Count!=eggCount+1 || egg.Hatched || egg.Color is not ("Pink" or "White") ||
            new Vector3(egg.X,egg.Y,egg.Z).DistanceTo(nest)>.03f || pet.CourtshipReadiness!=0)
            throw new Exception("Courtship did not lay an egg with a parent's shell color at the nest");
        courtship.Advance(4f,true,LayGardenEgg);
        if (!courtship.IsPaired(parent)) throw new Exception("Courtship celebration ended too soon");
        courtship.Advance(1f,true,LayGardenEgg);
        if (courtship.IsBlooming(friend) || courtship.IsBlooming(parent) ||
            SaveStore.Load(savePath,out _).GardenEggs.Count!=eggCount+1)
            throw new Exception("Flowers did not clear or the new egg was not saved");
        pet.CourtshipReadiness=100;
        pet.BloomRemaining=1;
        if (!courtship.Sit(friend,friendArt,pet,"White",world,.5f) ||
            courtship.Sit(friend,friendArt,pet,"White",world,.51f) ||
            courtship.IsBlooming(friend) || pet.CourtshipReadiness!=0)
            throw new Exception("A Chao that waited alone did not leave courtship when the window expired");
        parent.QueueFree();
    }

    private void RunOfferCheck()
    {
        UpdatePickupPose(PickupDuration);
        for(int i=0;i<2100;i++) UpdateOffering(1f/60);
    }

    private void DriveShoreCheck(int steps,params Key[] keys)
    {
        foreach (Key key in keys)
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode=key,Keycode=key,Pressed=true });
        Input.FlushBufferedEvents();
        for (int i=0;i<steps;i++) MovePlayer(1f/60);
        foreach (Key key in keys)
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode=key,Keycode=key,Pressed=false });
        Input.FlushBufferedEvents();
    }

    private void SetPreviewPosition()
    {
        player.Position = new(0,0,5); player.Rotation = new(0,Mathf.Pi,0);
        if (zone == GardenZone.Garden) { friend.Position = new(1.8f,0,3); following = false; wanderTime = 100; wanderTarget = friend.Position; }
        cameraRig.Yaw = 0; cameraRig.Pitch = .22f; cameraRig.Follow(player.Position,0,true);
    }
}
