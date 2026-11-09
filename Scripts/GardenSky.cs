using System.Linq;
using Godot;

namespace Mori;

internal partial class GardenSky : Node3D
{
    private readonly Environment environment;
    private readonly ShaderMaterial sky=new() { Shader=new Shader { Code="""
        shader_type sky;
        uniform float day=1.0;
        uniform float dusk=0.0;
        uniform float seconds=0.0;
        uniform vec3 sun_dir;
        uniform vec3 moon_dir;
        float hash(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453); }
        float noise(vec2 p) {
            vec2 i=floor(p), f=fract(p);
            f=f*f*(3.0-2.0*f);
            return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);
        }
        void sky() {
            float h=max(EYEDIR.y,0.0);
            vec3 horizon=mix(vec3(.075,.105,.20),vec3(.66,.85,.98),day);
            horizon=mix(horizon,vec3(.92,.48,.29),dusk*.44);
            COLOR=mix(horizon,mix(vec3(.021,.036,.095),vec3(.20,.52,.86),day),smoothstep(0.0,.82,h));
            vec2 p=EYEDIR.xz/(h+.22)*4.3+vec2(seconds*.018,seconds*.006);
            float cloud=noise(p)*.60+noise(p*2.05)*.27+noise(p*4.2)*.13;
            float cover=smoothstep(.53,.69,cloud)*smoothstep(.02,.19,h);
            COLOR=mix(COLOR,mix(vec3(.12,.16,.25),vec3(.96,.98,1.0),day),cover*.91);
            vec2 stars=SKY_COORDS*vec2(490,245);
            float star=(1.0-smoothstep(.07,.19,length(fract(stars)-.5)))*step(.993,hash(floor(stars)));
            COLOR+=vec3(.85,.90,1.0)*star*(1.0-day)*(1.0-cover)*smoothstep(.05,.24,h);
            float sun_distance=distance(EYEDIR,sun_dir);
            float sun_disk=1.0-smoothstep(.023,.028,sun_distance);
            float sun_glow=(1.0-smoothstep(.022,.17,sun_distance))*.37;
            COLOR+=mix(vec3(1.0,.77,.43),vec3(1.0,.95,.77),day)*
                (sun_disk+sun_glow)*(1.0-cover*.78)*smoothstep(-.032,.012,sun_dir.y);
            float moon_distance=distance(EYEDIR,moon_dir);
            float moon_disk=1.0-smoothstep(.028,.032,moon_distance);
            float moon_mottling=.80+.20*noise(EYEDIR.xz*340.0+EYEDIR.y*198.0);
            COLOR+=vec3(.76,.86,1.0)*moon_disk*moon_mottling*
                (1.0-cover*.68)*(1.0-day*.62)*smoothstep(-.03,.014,moon_dir.y);
        }
        """ } };
    private DirectionalLight3D sun=null!;
    private DirectionalLight3D moon=null!;
    private readonly Node3D[] gulls=new Node3D[3];
    private readonly Node3D[,] wings=new Node3D[3,2];
    private readonly MeshInstance3D[] gullShadows=new MeshInstance3D[3];
    private readonly ShaderMaterial softShadow=new() { Shader=new Shader { Code="""
        shader_type spatial;
        render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled;
        void fragment() {
            vec2 p=(UV-.5)*2.0;
            float blur=1.0-smoothstep(.05,1.0,length(p));
            ALBEDO=vec3(.11,.14,.15);
            ALPHA=blur*blur*.21;
        }
        """ } };

    public GardenSky(Environment environment) => this.environment=environment;

    public override void _Ready()
    {
        environment.BackgroundMode=Environment.BGMode.Sky;
        environment.Sky=new Sky { SkyMaterial=sky, ProcessMode=Sky.ProcessModeEnum.Realtime };
        sun=new DirectionalLight3D { ShadowEnabled=true, DirectionalShadowMaxDistance=70 };
        AddChild(sun);
        moon=new DirectionalLight3D { LightColor=new Color("889bd7"),ShadowEnabled=false };
        AddChild(moon);
        for (int i=0;i<gulls.Length;i++)
        {
            var gull=new Node3D { Visible=false };
            gulls[i]=gull;
            AddChild(gull);
            var shadow=new MeshInstance3D { Mesh=new QuadMesh { Size=new(3.9f,2.55f) },
                MaterialOverride=softShadow, CastShadow=GeometryInstance3D.ShadowCastingSetting.Off,Visible=false };
            AddChild(shadow);
            gullShadows[i]=shadow;
            GardenArt.Ball(gull,Vector3.Zero,new(.26f,.23f,.82f),new("e8edf1"));
            for (int side=0;side<2;side++)
            {
                int sign=side==0 ? -1 : 1;
                var wing=new Node3D();
                wings[i,side]=wing;
                gull.AddChild(wing);
                GardenArt.Ball(wing,new(sign*.40f,0,0),new(.90f,.065f,.35f),new("e8edf1"));
                GardenArt.Ball(wing,new(sign*.83f,0,-.09f),new(.48f,.05f,.19f),new("485268"));
            }
            foreach (var mesh in gull.FindChildren("*","MeshInstance3D",true,false).OfType<MeshInstance3D>())
                mesh.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
        }
        Advance(0,true);
    }

    public void Advance(float seconds,bool garden)
    {
        float cycle=Mathf.PosMod(seconds+145,600);
        float fraction=cycle<410 ? .5f*cycle/410 : .5f+.5f*(cycle-410)/190;
        float angle=Mathf.Tau*fraction;
        float altitude=Mathf.Sin(angle);
        var sunDirection=new Vector3(Mathf.Cos(angle),altitude*.76f,-.25f).Normalized();
        var moonDirection=new Vector3(-sunDirection.X,-sunDirection.Y,.15f).Normalized();
        float light=Mathf.SmoothStep(-.07f,.09f,altitude);
        float dusk=1-Mathf.SmoothStep(.012f,.13f,Mathf.Abs(altitude));
        sky.SetShaderParameter("day",light);
        sky.SetShaderParameter("dusk",dusk);
        sky.SetShaderParameter("seconds",seconds);
        sky.SetShaderParameter("sun_dir",sunDirection);
        sky.SetShaderParameter("moon_dir",moonDirection);
        sun.LookAt(-sunDirection);
        moon.LookAt(-moonDirection);
        sun.LightEnergy=.68f*light*Mathf.SmoothStep(-.025f,.065f,altitude);
        moon.LightEnergy=.19f*(1-light);
        sun.LightColor=new Color("fff3dd").Lerp(new Color("ffad7a"),dusk*.55f);
        environment.AmbientLightColor=new Color("9dabdd").Lerp(new Color("eaf3ff"),light);
        environment.AmbientLightEnergy=Mathf.Lerp(.27f,.53f,light);
        float flight=Mathf.PosMod(seconds+51,83);
        for (int i=0;i<gulls.Length;i++)
        {
            var gull=gulls[i];
            gull.Visible=garden && light>.6f && flight<15;
            if (!gull.Visible) { gullShadows[i].Hide(); continue; }
            gull.Position=new(-34+flight*4.4f-i*2.7f,24+i*1.4f+Mathf.Sin(flight*.6f+i)*.4f,-9+i*3.8f);
            gull.RotationDegrees=new(0,90,6*Mathf.Sin(flight*.6f));
            MoveGullShadow(gullShadows[i],gull.GlobalPosition,sunDirection);
            float flap=Mathf.Sin(flight*9+i)*.45f;
            wings[i,0].Rotation=new(0,0,flap);
            wings[i,1].Rotation=new(0,0,-flap);
        }
    }
    private void MoveGullShadow(MeshInstance3D shadow,Vector3 from,Vector3 towardSun)
    {
        var ray=PhysicsRayQueryParameters3D.Create(from,from-towardSun*80,1);
        var surface=GetWorld3D().DirectSpaceState.IntersectRay(ray);
        shadow.Visible=surface.Count>0;
        if (!shadow.Visible) return;
        var normal=(Vector3)surface["normal"];
        var at=(Vector3)surface["position"];
        shadow.GlobalPosition=at+normal*.027f;
        shadow.Quaternion=new Quaternion(Vector3.Back,normal);
        shadow.Scale=Vector3.One*Mathf.Clamp(from.DistanceTo(at)/23,.85f,1.55f);
    }

}
