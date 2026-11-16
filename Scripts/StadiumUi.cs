using System;
using System.Linq;
using Godot;

namespace Mori;

internal partial class StadiumUi : CanvasLayer
{
    private Control menu=null!,hud=null!,result=null!;
    private OptionButton pets=null!,modes=null!,events=null!;
    private Label details=null!,eventInfo=null!,status=null!,resultText=null!;
    private Button enter=null!,cheer=null!,next=null!;
    private SubViewport preview=null!;
    private Node3D? previewRoot;
    private ImportedChao? previewArt;
    private PetState? previewState;
    private Func<PetState> owner=null!;
    private string[] petIds=Array.Empty<string>(),eventIds=Array.Empty<string>();
    internal Action<string,string>? Start;
    internal Action? Leave,Next,Cheer;
    internal bool MenuVisible=>menu.Visible;
    internal bool ResultsVisible=>result.Visible;
    internal void Initialize(Func<PetState> data){owner=data;}
    private static Panel Box(Control root,Rect2 rect)
    {
        var panel=new Panel{Position=rect.Position,Size=rect.Size};root.AddChild(panel);panel.AddThemeStyleboxOverride("panel",new StyleBoxFlat{BgColor=new(.96f,.94f,.87f,.94f),CornerRadiusTopLeft=14,CornerRadiusTopRight=14,CornerRadiusBottomLeft=14,CornerRadiusBottomRight=14});return panel;
    }
    private static Label Text(Control root,string value,int size,Rect2 rect)
    {var label=new Label{Text=value,Position=rect.Position,Size=rect.Size,AutowrapMode=TextServer.AutowrapMode.WordSmart};label.AddThemeFontSizeOverride("font_size",size);label.AddThemeColorOverride("font_color",GardenArt.Ink);root.AddChild(label);return label;}
    private static Button Button(Control root,string label,Rect2 rect,Action action)
    {var b=new Button{Text=label,Position=rect.Position,Size=rect.Size};root.AddChild(b);b.Pressed+=action;return b;}
    public override void _Ready()
    {
        Layer=9;menu=new Control();AddChild(menu);menu.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var shade=new ColorRect{Color=new(0,0,0,.35f)};menu.AddChild(shade);shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var panel=Box(menu,new(200,100,880,570));Text(panel,"CHAO STADIUM",28,new(24,16,600,42));
        Text(panel,"Choose your Chao",17,new(25,69,340,28));pets=new OptionButton{Position=new(24,101),Size=new(350,38)};panel.AddChild(pets);
        var viewportContainer=new SubViewportContainer{Position=new(24,151),Size=new(170,230),Stretch=true};panel.AddChild(viewportContainer);
        preview=new SubViewport{Size=new(170,230),OwnWorld3D=true,TransparentBg=true,RenderTargetUpdateMode=SubViewport.UpdateMode.Always};viewportContainer.AddChild(preview);
        details=Text(panel,"",15,new(208,151,190,340));
        Text(panel,"Competition",17,new(420,69,400,28));modes=new OptionButton{Position=new(420,101),Size=new(430,38)};modes.AddItem("Chao Race");modes.AddItem("Chao Karate");panel.AddChild(modes);
        events=new OptionButton{Position=new(420,155),Size=new(430,38)};panel.AddChild(events);eventInfo=Text(panel,"",16,new(420,211,430,265));
        enter=Button(panel,"Enter competition",new(420,490,280,43),()=>{if(pets.Selected>=0 && events.Selected>=0)Start?.Invoke(petIds[pets.Selected],eventIds[events.Selected]);});
        Button(panel,"Back",new(714,490,136,43),()=>Leave?.Invoke());Text(panel,"Tab / arrows: navigate · Enter: choose · Esc: back",12,new(25,530,370,30));
        pets.ItemSelected+=_=>Refresh();modes.ItemSelected+=_=>Refresh();events.ItemSelected+=_=>RefreshDetails();
        hud=new Control();AddChild(hud);var hudPanel=Box(hud,new(24,112,310,296));status=Text(hudPanel,"",15,new(16,12,278,192));
        cheer=Button(hudPanel,"Q · Cheer",new(16,210,278,32),()=>Cheer?.Invoke());Button(hudPanel,"Withdraw",new(16,250,278,32),()=>Leave?.Invoke());
        result=new Control();AddChild(result);var results=Box(result,new(820,130,430,560));Text(results,"COMPETITION RESULTS",21,new(24,16,380,38));resultText=Text(results,"",15,new(24,68,380,365));
        next=Button(results,"Next bout",new(24,445,380,42),()=>Next?.Invoke());Button(results,"Return to Stadium",new(24,495,380,42),()=>Leave?.Invoke());HideAll();
    }
    internal void HideAll(){menu.Hide();hud.Hide();result.Hide();}
    internal void Open()
    {
        HideAll();menu.Show();var roster=ExpeditionRoster.Available(owner());petIds=roster.Select(p=>p.Id).ToArray();pets.Clear();foreach(var p in roster)pets.AddItem(p.State.Name+" · "+p.Color);
        int selected=Array.IndexOf(petIds,owner().Stadium.LastPet);if(petIds.Length>0)pets.Select(Math.Max(0,selected));Refresh();pets.GrabFocus();
    }
    internal void Select(string petId,SportMode mode)
    {int index=Array.IndexOf(petIds,petId);if(index>=0)pets.Select(index);modes.Select((int)mode);Refresh();}
    private void Refresh()
    {
        string previous=events.Selected>=0 && events.Selected<eventIds.Length ? eventIds[events.Selected] : "";events.Clear();
        var competitions=SportsCatalog.Events.Where(e=>(int)e.Mode==modes.Selected).ToArray();eventIds=competitions.Select(e=>e.Id).ToArray();
        var pet=pets.Selected>=0 && pets.Selected<petIds.Length ? ExpeditionRoster.Available(owner()).FirstOrDefault(p=>p.Id==petIds[pets.Selected]) : null;
        foreach(var e in competitions){events.AddItem(e.Title+(pet!=null && !SportsCatalog.Open(e,pet.State) ? " · Locked" : ""));events.SetItemDisabled(events.ItemCount-1,pet==null || !SportsCatalog.Open(e,pet.State));}
        int chosen=Array.IndexOf(eventIds,previous);if(chosen<0 || pet==null || !SportsCatalog.Open(competitions[chosen],pet.State))chosen=Array.FindIndex(competitions,e=>pet!=null && SportsCatalog.Open(e,pet.State));
        if(chosen>=0)events.Select(chosen);BuildPreview(pet);RefreshDetails();
    }
    private void RefreshDetails()
    {
        var pet=pets.Selected>=0 && pets.Selected<petIds.Length ? ExpeditionRoster.Available(owner()).FirstOrDefault(p=>p.Id==petIds[pets.Selected]) : null;
        if(pet==null){details.Text="No eligible, hatched Chao.";enter.Disabled=true;return;}
        var s=pet.State;details.Text=$"{s.Name}\n{pet.Color} · {s.LifeStage}\nEnergy {s.Energy:0}/100\n\nRun {s.Run/10}   Swim {s.Swim/10}\nFly {s.Fly/10}   Power {s.Power/10}\nStamina {s.Stamina/10}\n\nMedals {s.Athletics.Medals.Count}\nCups {s.Athletics.Cups.Count}";
        var e=events.Selected>=0 && events.Selected<eventIds.Length ? SportsCatalog.Find(eventIds[events.Selected]) : null;
        enter.Disabled=s.Energy<15 || e==null || !SportsCatalog.Open(e,s);
        if(e!=null)
        {
            s.Athletics.Results.TryGetValue(e.Id,out var record);
            string suggestion=new[]{("Run",s.Run),("Swim",s.Swim),("Fly",s.Fly),("Power",s.Power)}.OrderByDescending(p=>p.Item2).First().Item1;
            eventInfo.Text=e.Title+"\n\n"+(e.Mode==SportMode.Race ? string.Join(" → ",e.Sections)+"\nFour participants · Q: Cheer" : "Three automatic bouts\nPower, movement, defence and recovery\nQ: Cheer · Leave between bouts")+
                $"\n\nWins: {record?.Wins ?? 0}"+(record?.BestSeconds>0 ? $"\nBest time: {record.BestSeconds:0.00}s" : "")+$"\nSuggested specialty: {suggestion}"+(s.Energy<15 ? "\nRest before entering." : "");
        }
    }
    private void BuildPreview(ExpeditionPet? pet)
    {
        if(previewRoot!=null){preview.RemoveChild(previewRoot);previewRoot.QueueFree();}previewArt=null;previewState=pet?.State;if(pet==null)return;
        previewRoot=new Node3D();preview.AddChild(previewRoot);
        var environment=new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new("dce6d6"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightEnergy=.8f,AmbientLightColor=Colors.White}};previewRoot.AddChild(environment);
        previewRoot.AddChild(new DirectionalLight3D{RotationDegrees=new(-35,-25,0),LightEnergy=.7f});
        previewArt=(ImportedChao)GardenEgg.BuildChaoArt(previewRoot,pet.State,pet.Color);ImportedChao.BindPresentation(previewArt,pet.State,false,pet.Id);
        var camera=new Camera3D{Position=new(.55f,.36f,.8f),Current=true};previewRoot.AddChild(camera);camera.LookAt(new Vector3(0,.23f,0));
    }
    internal void Hud(string text,bool canCheer,float cooldown)
    {menu.Hide();hud.Show();result.Hide();status.Text=text;cheer.Disabled=!canCheer;cheer.Text=cooldown>0 ? $"Q · Cheer ({cooldown:0}s)" : "Q · Cheer";}
    internal void Results(string text,bool more,string action="Next bout")
    {menu.Hide();hud.Hide();result.Show();resultText.Text=text;next.Visible=more;next.Text=action;if(more)next.GrabFocus();}
    public override void _Process(double delta)
    {if(MenuVisible && previewArt!=null){ImportedChao.ApplyDevelopment(previewArt,previewState!);ImportedChao.MovePose(previewArt,0,false,false,false,false,false,false);}}
}
