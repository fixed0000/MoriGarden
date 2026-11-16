using System;
using System.Linq;
using Godot;

namespace Mori;

internal partial class StadiumBroadcast : Control
{
    private ChaoRace? race;
    private RaceEntrant? watching;
    private ChaoKarate? fight;
    private int round,rounds,wins;
    internal void Race(ChaoRace value,RaceEntrant selected){fight=null;race=value;watching=selected;Show();QueueRedraw();}
    internal void Karate(ChaoKarate value,int bout,int count,int victories){race=null;fight=value;round=bout;rounds=count;wins=victories;Show();QueueRedraw();}
    internal bool IsKarate=>fight!=null;
    private static readonly Color Navy=new("15395a"),Gold=new("f6c445"),White=new("fff8df");
    private void Caption(string text,Vector2 at,int size,Color color,HorizontalAlignment alignment=HorizontalAlignment.Left,float width=-1)
    {
        var font=ThemeDB.FallbackFont;DrawStringOutline(font,at,text,alignment,width,size,3,new("10243c"));DrawString(font,at,text,alignment,width,size,color);
    }
    private void Bar(Rect2 rect,float value,Color fill)
    {
        DrawStyleBox(new StyleBoxFlat{BgColor=Navy,BorderColor=White,BorderWidthBottom=2,BorderWidthLeft=2,BorderWidthRight=2,BorderWidthTop=2,CornerRadiusTopLeft=7,CornerRadiusTopRight=7,CornerRadiusBottomLeft=7,CornerRadiusBottomRight=7},rect);
        var inner=new Rect2(rect.Position+Vector2.One*4,new Vector2((rect.Size.X-8)*Mathf.Clamp(value,0,1),rect.Size.Y-8));DrawRect(inner,fill);
        for(int i=1;i<20;i++){float x=rect.Position.X+4+i*(rect.Size.X-8)/20;DrawLine(new(x,rect.Position.Y+4),new(x,rect.End.Y-4),new Color(0,0,0,.12f));}
    }
    public override void _Draw()
    {
        if(fight!=null){DrawKarate();return;}
        if(race==null || watching==null)return;
        
        Caption(watching.Place.ToString(),new(30,88),76,new("81d7f5"));Caption("/ "+race.Entrants.Count,new(92,83),34,White);
        Caption("TIME",new(470,36),20,Gold);Caption(TimeSpan.FromSeconds(Math.Max(0,race.Time-3)).ToString(@"mm\:ss\.ff"),new(465,82),36,White);
        Caption(watching.Pet.Name,new(805,35),25,White);Caption("Stamina",new(805,62),18,White);Bar(new(805,73,400,25),watching.Fatigue/watching.Capacity,watching.Fatigue<=0 ? new("cf7175") : Gold);
        DrawRect(new(16,136,118,260),new Color(.035f,.10f,.17f,.52f));
        Vector2 Map(System.Numerics.Vector3 p)=>new(74+p.X*4,152+p.Z*3.7f);
        for(int i=1;i<race.Course.Samples.Count;i++)DrawLine(Map(race.Course.Samples[i-1].Point.Position),Map(race.Course.Samples[i].Point.Position),new("d6dbbe"),3);
        for(int i=0;i<race.Entrants.Count;i++){var racer=race.Entrants[i];var at=Map(race.Course.Position(racer.Distance,0,racer.Pet));DrawCircle(at,racer==watching ? 5 : 3,racer==watching ? Gold : new("a5d9e7"));}
        int row=0;foreach(var racer in race.Standings){Caption($"{racer.Place}  {racer.Pet.Name}",new(24,443+row*23),16,racer==watching ? Gold : White);row++;}
        Caption("← / → Watch Chao",new(24,690),15,White);
        DrawRect(new(0,736,1280,64),new Color(.035f,.10f,.17f,1));Caption("Q / Space · Cheer",new(480,775),21,Gold);Caption("Esc · Leave",new(1090,775),17,White);
        float progress=watching.Distance/race.Course.Length;Bar(new(210,709,860,15),progress,new("8ebcbc"));
        if(race.Countdown>0)Caption(Mathf.CeilToInt(race.Countdown).ToString(),new(0,400),90,Gold,HorizontalAlignment.Center,1280);
        else if(race.Time<3.7f)Caption("GO!",new(0,400),90,new("99e898"),HorizontalAlignment.Center,1280);
        else if(watching.Fatigue<=0)Caption("OUT OF ENERGY",new(0,160),25,new("f4a781"),HorizontalAlignment.Center,1280);
    }
    private void DrawKarate()
    {
        var match=fight!;
        DrawRect(new(0,0,1280,126),new Color(.04f,.11f,.23f,.65f));
        Caption("HEALTH",new(42,24),14,White);Caption("HEALTH",new(768,24),14,White);
        Bar(new(42,32,470,30),match.A.Health/match.A.MaxHealth,Gold);Bar(new(768,32,470,30),match.B.Health/match.B.MaxHealth,Gold);
        Caption(match.A.Pet.Name,new(42,92),25,White);Caption(match.B.Pet.Name,new(768,92),25,White);
        DrawCircle(new(640,59),51,Navy);DrawArc(new(640,59),51,0,Mathf.Tau,64,White,3);
        Caption("TIME",new(590,39),15,White,HorizontalAlignment.Center,100);Caption(Mathf.CeilToInt(match.Remaining).ToString(),new(585,87),46,Gold,HorizontalAlignment.Center,110);
        Caption($"ROUND {round} / {rounds}",new(535,132),17,White,HorizontalAlignment.Center,210);
        for(int i=0;i<rounds;i++){DrawCircle(new(51+i*24,115),7,i<wins ? new("ef8d9c") : Navy);DrawArc(new(51+i*24,115),7,0,Mathf.Tau,16,White,1);}
        DrawRect(new(0,705,1280,95),new Color(.04f,.11f,.23f,.72f));Bar(new(42,720,470,28),match.A.Zeal/KarateFighter.MaxZeal,new("ea91be"));Bar(new(768,720,470,28),match.B.Zeal/KarateFighter.MaxZeal,new("ea91be"));
        Caption("Zeal",new(565,745),26,White,HorizontalAlignment.Center,150);Caption("Q / Space · Cheer",new(0,787),17,White,HorizontalAlignment.Center,1280);
        if(match.Countdown>0)Caption(Mathf.CeilToInt(match.Countdown).ToString(),new(0,370),74,Gold,HorizontalAlignment.Center,1280);
        else if(match.Time<3.6f)Caption("FIGHT!",new(0,260),60,Gold,HorizontalAlignment.Center,1280);
        if(match.Finished)
        {
            bool draw=match.Winner==null;Caption(draw ? "DRAW" : match.Winner==match.A.Id ? "WIN" : "LOSE",new(115,250),62,match.Winner==match.A.Id ? new("ffd389") : new("8bd7f0"),HorizontalAlignment.Center,360);
            Caption(draw ? "DRAW" : match.Winner==match.B.Id ? "WIN" : "LOSE",new(810,250),62,match.Winner==match.B.Id ? new("ffd389") : new("8bd7f0"),HorizontalAlignment.Center,360);
            Caption(match.Method.ToUpperInvariant(),new(0,183),25,White,HorizontalAlignment.Center,1280);
        }
    }

}
