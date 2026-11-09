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
        }
        """ } };
    private DirectionalLight3D sun=null!;
    private readonly Node3D[] gulls=new Node3D[3];
    private readonly Node3D[,] wings=new Node3D[3,2];

    public GardenSky(Environment environment) => this.environment=environment;

    public override void _Ready()
    {
        environment.BackgroundMode=Environment.BGMode.Sky;
        environment.Sky=new Sky { SkyMaterial=sky, ProcessMode=Sky.ProcessModeEnum.Realtime };
        sun=new DirectionalLight3D { ShadowEnabled=true, DirectionalShadowMaxDistance=70 };
        AddChild(sun);
        for (int i=0;i<gulls.Length;i++)
        {
            var gull=new Node3D { Visible=false };
            gulls[i]=gull;
            AddChild(gull);
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
        }
        Advance(0,true);
    }

    public void Advance(float seconds,bool garden)
    {
        float angle=Mathf.Tau*(seconds/600+.22f);
        float altitude=Mathf.Sin(angle);
        float light=Mathf.SmoothStep(-.22f,.25f,altitude);
        float dusk=1-Mathf.SmoothStep(.02f,.35f,Mathf.Abs(altitude));
        sky.SetShaderParameter("day",light);
        sky.SetShaderParameter("dusk",dusk);
        sky.SetShaderParameter("seconds",seconds);
        sun.RotationDegrees=new(-14-56*Mathf.Abs(altitude),angle*180/Mathf.Pi,0);
        sun.LightEnergy=Mathf.Lerp(.19f,.68f,light);
        sun.LightColor=new Color("889bd7").Lerp(new Color("fff3dd"),light).Lerp(new Color("ffad7a"),dusk*.35f);
        environment.AmbientLightColor=new Color("9dabdd").Lerp(new Color("eaf3ff"),light);
        environment.AmbientLightEnergy=Mathf.Lerp(.27f,.53f,light);
        float flight=Mathf.PosMod(seconds+51,83);
        for (int i=0;i<gulls.Length;i++)
        {
            var gull=gulls[i];
            gull.Visible=garden && light>.6f && flight<15;
            if (!gull.Visible) continue;
            gull.Position=new(-34+flight*4.4f-i*2.7f,24+i*1.4f+Mathf.Sin(flight*.6f+i)*.4f,-9+i*3.8f);
            gull.RotationDegrees=new(0,90,6*Mathf.Sin(flight*.6f));
            float flap=Mathf.Sin(flight*9+i)*.45f;
            wings[i,0].Rotation=new(0,0,flap);
            wings[i,1].Rotation=new(0,0,-flap);
        }
    }
}
