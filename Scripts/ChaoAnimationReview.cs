using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Mori;

// Standalone authoring scene: never instantiates GardenGame, a save store or the
// editor bridge. Private reference JPEGs are read as files, not exported assets.
public partial class ChaoAnimationReview : Node3D
{
    private ImportedChao art=null!;
    private Node3D actor=null!;
    private Camera3D camera=null!;
    private OptionButton motions=null!,angles=null!,forms=null!,speeds=null!;
    private Label status=null!,clock=null!;
    private TextureRect reference=null!;
    private HSlider timeline=null!;
    private string[] ids=Array.Empty<string>();
    private string[] referenceFiles=Array.Empty<string>();
    private int referenceIndex=-1;
    private float time;
    private bool playing=true,changing;
    private bool capture,checkRig;
    private int checkTicks;
    private int captureTick,captureIndex;
    private string selected="race_run_normal";
    private readonly string[] allIds={"race_ready","race_start","race_run_slow","race_run_normal","race_run_fast","race_cheer","race_exhausted","race_stumble","race_fall","race_prone","race_rise","race_water_enter","race_swim","race_water_exit","race_takeoff","race_fly","race_fly_failed","race_land","race_climb_prepare","race_climb","race_mount","race_finish","race_stop","race_win","race_lose","karate_intro","karate_ready","karate_idle","karate_approach","karate_punch","karate_strike_variant","karate_kick","karate_strong","karate_miss","karate_guard","karate_dodge","karate_hit_light","karate_hit_strong","karate_knockback","karate_unbalanced","karate_knockdown","karate_rise","karate_meditate","karate_meditate_exit","karate_ring_out","karate_win","karate_lose"};
    private Label Text(Control root,string value,Rect2 area,int size=16)
    {var label=new Label{Text=value,Position=area.Position,Size=area.Size,AutowrapMode=TextServer.AutowrapMode.WordSmart};root.AddChild(label);label.AddThemeFontSizeOverride("font_size",size);return label;}
    private Button Button(Control root,string value,Rect2 area,Action action)
    {var button=new Button{Text=value,Position=area.Position,Size=area.Size};root.AddChild(button);button.Pressed+=action;return button;}
    public override void _Ready()
    {
        DisplayServer.WindowSetTitle("Chao Animation Review — MoriGarden");
        capture=OS.GetCmdlineUserArgs().Contains("--animation-capture");checkRig=OS.GetCmdlineUserArgs().Contains("--animation-check");
        AddChild(new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new("263a3c"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=Colors.White,AmbientLightEnergy=.7f}});
        AddChild(new DirectionalLight3D{RotationDegrees=new(-35,-30,0),LightEnergy=.8f,ShadowEnabled=true});
        GardenArt.Shape(this,new PlaneMesh{Size=new(8,8)},new(0,-.003f,0),Vector3.One,new("a1a79a"));ZoneArt.Solid(this,new(0,-.10f,0),new(8,.2f,8));
        actor=new Node3D();AddChild(actor);camera=new Camera3D{Current=true,Fov=45};AddChild(camera);BuildModel();
        var layer=new CanvasLayer();AddChild(layer);var ui=new Control();layer.AddChild(ui);ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        Text(ui,"CHAO ANIMATION REVIEW",new(25,14,720,45),26);
        status=Text(ui,"",new(25,57,1220,65),16);
        motions=new OptionButton{Position=new(25,125),Size=new(390,36)};ui.AddChild(motions);ids=allIds;
        foreach(string id in ids)motions.AddItem(id+(SportMotions.Find(id)==null ? " · Unverified / not authored" : " · Approximate"));motions.Select(Array.IndexOf(ids,selected));motions.ItemSelected+=_=>Select(ids[motions.Selected]);
        angles=new OptionButton{Position=new(430,125),Size=new(180,36)};ui.AddChild(angles);foreach(string value in new[]{"Front","Side","Back","Game camera"})angles.AddItem(value);angles.Select(3);
        forms=new OptionButton{Position=new(625,125),Size=new(200,36)};ui.AddChild(forms);foreach(string value in new[]{"Child · Blue","Adult · Green","Power · Black","Fly · Yellow"})forms.AddItem(value);forms.ItemSelected+=_=>BuildModel();
        speeds=new OptionButton{Position=new(840,125),Size=new(150,36)};ui.AddChild(speeds);foreach(string value in new[]{"0.25×","0.5×","1×","2×"})speeds.AddItem(value);speeds.Select(2);
        reference=new TextureRect{Position=new(735,210),Size=new(510,382),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered};ui.AddChild(reference);
        Text(ui,"Private original reference · not exported",new(735,600,510,30));
        Text(ui,"MoriGarden · key pose playback",new(25,610,660,35));
        Button(ui,"Play / Pause",new(25,660,160,40),()=>playing=!playing);Button(ui,"− 1/60 s",new(200,660,140,40),()=>Step(-1f/60));Button(ui,"+ 1/60 s",new(355,660,140,40),()=>Step(1f/60));Button(ui,"Restart",new(510,660,130,40),()=>time=0);
        timeline=new HSlider{Position=new(25,720),Size=new(900,25),MinValue=0,MaxValue=1,Step=.001};ui.AddChild(timeline);timeline.ValueChanged+=v=>{if(!changing){playing=false;time=(float)v;}};
        clock=Text(ui,"",new(950,713,290,40));Text(ui,"Space: pause · ← / →: step · 1–4: camera · Esc: close review",new(25,758,1100,30));
        Select(selected);
    }
    private void BuildModel()
    {
        if(art!=null){actor.RemoveChild(art);art.QueueFree();}
        int choice=forms?.Selected ?? 0;var pet=new PetState{Run=600,Energy=100};
        if(choice>0){pet.GardenSeconds=20000;pet.Form=choice==2 ? "stone" : choice==3 ? "comet" : "sprout";}
        art=(ImportedChao)GardenEgg.BuildChaoArt(actor,pet,new[]{"Blue","Green","Black","Yellow"}[Math.Clamp(choice,0,3)]);ImportedChao.ApplyDevelopment(art,pet);ImportedChao.PrepareSportRig(art);
        art.Visible=SportMotions.Find(selected)!=null;
    }
    private void Select(string id)
    {
        selected=id;motions.Select(Array.IndexOf(ids,id));art.Visible=SportMotions.Find(id)!=null;time=0;clock.Text="";referenceIndex=-1;reference.Texture=null;var clip=SportMotions.Find(id);
        status.Text=clip==null ? "UNVERIFIED — No authored reconstruction. This candidate is not played using an invented fallback." : $"{clip.Status} · {clip.Seconds:0.000} s · Original {clip.SourceStart:0.000}–{clip.SourceEnd:0.000} s\n{clip.Notes}";
        timeline.MaxValue=clip?.Seconds ?? 1;
        string path=ProjectSettings.GlobalizePath("res://artifacts/animation-reference/"+id);
        referenceFiles=Directory.Exists(path) ? Directory.GetFiles(path,"frame-*.jpg").OrderBy(f=>f).ToArray() : Array.Empty<string>();
        if(referenceFiles.Length>0)reference.Texture=ImageTexture.CreateFromImage(Image.LoadFromFile(referenceFiles[0]));
    }
    private void Step(float delta){playing=false;time=Mathf.Clamp(time+delta,0,(float)timeline.MaxValue);}
    public override void _Process(double delta)
    {
        if(art==null || status==null)return;
        if(capture)
        {
            playing=false;
            int capturePhase=captureTick++%4;
            if(capturePhase==0)
            {
                if(captureIndex>=SportMotions.All.Length*32){GD.Print("ANIMATION_CAPTURE_OK: all authored motions, sixteen phases and two views; no saves opened");GetTree().Quit();return;}
                int motion=captureIndex/32,view=captureIndex%32/16,frame=captureIndex%16;
                if(selected!=SportMotions.All[motion].Id)Select(SportMotions.All[motion].Id);
                angles.Select(view==0 ? 1 : 3);time=SportMotions.All[motion].Seconds*frame/16;captureIndex++;
            }
            else if(capturePhase==3)
            {
                int last=captureIndex-1;string directory=ProjectSettings.GlobalizePath("res://artifacts/animation-reference/review/"+selected);Directory.CreateDirectory(directory);
                GetViewport().GetTexture().GetImage().SaveJpg(Path.Combine(directory,$"view-{last%32/16}-frame-{last%16:00}.jpg"),.88f);
            }
        }
        if(checkRig && ++checkTicks==3)
        {
            try
            {
            int poses=0,grounded=0;
            for(int form=0;form<4;form++)
            {
                forms.Select(form);BuildModel();var scale=art.Scale;
                foreach(var motion in SportMotions.All)for(int frame=0;frame<=32;frame++)
                {
                    var sampled=ImportedChao.ApplySportMotion(art,motion,motion.Seconds*frame/32);
                    var broken=art.FindChildren("*","Node3D",true,false).OfType<Node3D>().FirstOrDefault(n=>!n.GlobalTransform.IsFinite());
                    if(!art.Scale.IsEqualApprox(scale) || !art.Transform.IsFinite() || broken!=null)throw new Exception($"Invalid sport pose: form={form} clip={motion.Id} frame={frame} scale={art.Scale} expected={scale} node={broken?.GetPath()}");
                    if(sampled.Grounded)
                    {
                        float bottom=float.MaxValue;
                        foreach(var mesh in art.GetChild(0).FindChildren("*","MeshInstance3D",true,false).OfType<MeshInstance3D>().Where(m=>m.Mesh!=null && m.IsVisibleInTree()))
                            for(int surface=0;surface<mesh.Mesh.GetSurfaceCount();surface++)
                                foreach(var vertex in mesh.Mesh.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array())bottom=Mathf.Min(bottom,(mesh.GlobalTransform*vertex).Y);
                        if(bottom<-.004f || bottom>.012f)throw new Exception($"Grounded pose lost floor contact: {motion.Id}, form={form}, frame={frame}, bottom={bottom}");
                        grounded++;
                    }
                    poses++;
                }
            }
            Select("race_stop");if(art.Visible)throw new Exception("Unverified animation played an invented fallback");
            BuildModel();if(art.Visible)throw new Exception("Changing form exposed an unverified animation");
            File.WriteAllText(ProjectSettings.GlobalizePath("res://artifacts/animation-reference/authored-key-poses.json"),JsonSerializer.Serialize(SportMotions.All,new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
            GD.Print($"ANIMATION_RIG_OK: {poses} poses, {grounded} floor contacts, four forms, finite transforms, preserved scale, unsupported motion hidden after form changes, no saves opened");GetTree().Quit();return;
            }catch(Exception error){GD.PushError("ANIMATION_RIG_FAILED: "+error);GetTree().Quit(1);return;}
        }
        var clip=SportMotions.Find(selected);
        if(clip!=null)
        {
            if(playing){time+=(float)delta*new[]{.25f,.5f,1,2}[speeds.Selected];if(time>clip.Seconds){if(clip.Loop)time%=clip.Seconds;else{time=clip.Seconds;playing=false;}}}
            ImportedChao.ApplySportMotion(art,clip,time);
            changing=true;timeline.Value=time;changing=false;
            int index=Math.Clamp((int)(time/clip.Seconds*Math.Max(0,referenceFiles.Length-1)),0,Math.Max(0,referenceFiles.Length-1));
            if(referenceFiles.Length>0 && index!=referenceIndex){referenceIndex=index;reference.Texture=ImageTexture.CreateFromImage(Image.LoadFromFile(referenceFiles[index]));}
            clock.Text=$"{time:0.000} / {clip.Seconds:0.000} s";
        }
        actor.Rotation=new(0,selected.StartsWith("karate_") ? Mathf.Pi/2 : 0,0);
        Vector3 cameraLocal=angles.Selected switch{0=>new(0,.29f,1.20f),1=>new(1.20f,.29f,0),2=>new(0,.29f,-1.20f),_=>selected.StartsWith("karate_") ? new(-1.10f,.41f,.45f) : new(.82f,.41f,-1.04f)};
        camera.Position=actor.ToGlobal(cameraLocal);
        // Keep the model on the left, outside the reference panel.
        camera.HOffset=.22f;camera.LookAt(new Vector3(0,.26f,0));
    }
    public override void _UnhandledKeyInput(InputEvent e)
    {
        if(e is not InputEventKey key || !key.Pressed || key.Echo)return;
        if(key.Keycode==Key.Space)playing=!playing;else if(key.Keycode==Key.Left)Step(-1f/60);else if(key.Keycode==Key.Right)Step(1f/60);else if(key.Keycode==Key.Escape)GetTree().Quit();
        else if(key.Keycode>=Key.Key1 && key.Keycode<=Key.Key4)angles.Select((int)(key.Keycode-Key.Key1));
    }
}
