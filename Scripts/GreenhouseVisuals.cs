using System.Linq;
using Godot;

namespace Mori;

internal sealed class GreenhouseVisuals
{
    private readonly Node3D plants;
    private readonly Node3D blooms;
    private readonly ShaderMaterial foliage;
    private readonly MeshInstance3D[] water;
    private readonly float[] wet={0,0,0,0};
    private readonly Node3D filter;
    internal GreenhouseVisuals(Node3D world)
    {
        plants=world.GetNode<Node3D>("CentralBed/RecoveringPlants");foliage=(ShaderMaterial)((MeshInstance3D)plants.GetChild(0)).MaterialOverride;
        blooms=new Node3D { Name="Moonblossoms" };plants.AddChild(blooms);
        for(int i=0;i<12;i++)
        {
            var p=new Vector3(Mathf.Sin(i*2.4f)*1.8f,1.35f+i%3*.13f,Mathf.Cos(i*2.4f)*1.8f);
            GreenhouseArt.Beam(blooms,new(p.X,0,p.Z),p,.018f,new("608759"));
            for(int petal=0;petal<5;petal++)GardenArt.Ball(blooms,p+new Vector3(Mathf.Sin(petal*Mathf.Tau/5)*.1f,0,Mathf.Cos(petal*Mathf.Tau/5)*.1f),new(.16f,.09f,.16f),new("c4b3d5"));
            GardenArt.Ball(blooms,p+Vector3.Up*.035f,Vector3.One*.09f,new("e1cd84"));
        }
        water=new MeshInstance3D[4];
        for(int i=0;i<4;i++)
        {
            int index=i;var rect=GreenhouseArt.Channels[i];
            water[i]=WaterArt.Build(world,new("Channel"+i,rect,-.045f,GreenhouseArt.Height,Area:p=>rect.HasPoint(p)));
            water[i].Name="IrrigationWater"+index;water[i].Visible=false;
        }
        filter=new Node3D { Name="CanalFilter",Position=new(6.6f,0,-3) };world.AddChild(filter);
        for(int i=-2;i<=2;i++)GreenhouseArt.Box(filter,new(i*.18f,.15f,0),new(.055f,.5f,.9f),new("796b4b"));
        GreenhouseArt.Solid(filter,new(0,.15f,0),new(.82f,.5f,.9f));
        GreenhouseArt.Beam(world,new(-7.7f,.14f,7),new(-8.7f,.14f,7),.14f,new("617366"));
        GreenhouseArt.Beam(world,new(7,.12f,-1),new(7,.12f,-3),.14f,new("617366"));
        GreenhouseArt.Beam(world,new(7.8f,.08f,-7),new(7.8f,.08f,-3),.06f,new("ad9866"));
    }
    internal bool Wet(Vector2 p,CompactMissionState state,bool pressure)
    {
        int channel=GreenhouseArt.Channel(p);return channel>=0 && Enabled(channel,state,pressure);
    }
    private static bool Enabled(int section,CompactMissionState state,bool pressure)=>state.Completed || state.Element(GreenhouseRules.Intake).Value==1 &&
        (section==0 || section==3 && state.Element(GreenhouseRules.Diverter).Value==1 ||
            section==1 && state.Element(GreenhouseRules.Diverter).Value==2 || section==2 && state.Element(GreenhouseRules.Diverter).Value==2 && pressure);
    internal void Update(CompactMissionState state,bool pressure,float dt,bool snap=false)
    {
        for(int i=0;i<4;i++)
        {
            wet[i]=snap ? (Enabled(i,state,pressure) ? 1 : 0) : Mathf.MoveToward(wet[i],Enabled(i,state,pressure) ? 1 : 0,dt*.8f);
            water[i].Visible=wet[i]>.01f;
            // Water eases into the existing stone canal instead of popping into view.
            water[i].Transparency=1-wet[i];
        }
        float growth=Mathf.SmoothStep(0,1,state.Restoration);
        plants.Scale=new(1,Mathf.Lerp(.28f,1.15f,growth),1);
        foliage.SetShaderParameter("tint",new Color("82764c").Lerp(new("548958"),growth));
        blooms.Visible=growth>.35f;blooms.Scale=Vector3.One*Mathf.Clamp((growth-.35f)/.65f,.01f,1);
        filter.Position=new(filter.Position.X,Mathf.Lerp(filter.Position.Y,pressure || state.Completed ? .6f : 0,snap ? 1 : 1-Mathf.Exp(-4*dt)),filter.Position.Z);
    }
}
