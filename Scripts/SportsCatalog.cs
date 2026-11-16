using System;
using System.Collections.Generic;
using System.Linq;

namespace Mori;

public enum SportMode { Race,Karate }
public enum RaceSurface { Run,Swim,Fly,Power,Rest }
public sealed record SportsEvent(string Id,string Title,SportMode Mode,string Category,int Level,int Rating,RaceSurface[] Sections,int Bouts=3);
public static class SportsCatalog
{
    public static readonly string[] RaceCategories={"Beginner","Swim","Fly","Run","Power","Challenge"};
    public static readonly string[] KarateRanks={"Beginner","Standard","Expert","Master"};
    public static readonly SportsEvent[] Events=Build();
    private static SportsEvent[] Build()
    {
        var list=new List<SportsEvent>();
        foreach(string category in RaceCategories)for(int level=1;level<=3;level++)
        {
            var sections=category switch {
                "Beginner"=>new[]{RaceSurface.Run,RaceSurface.Run,RaceSurface.Rest,RaceSurface.Run,RaceSurface.Run,RaceSurface.Rest},
                "Swim"=>new[]{RaceSurface.Run,RaceSurface.Swim,RaceSurface.Swim,RaceSurface.Rest,RaceSurface.Swim,RaceSurface.Run},
                "Fly"=>new[]{RaceSurface.Run,RaceSurface.Fly,RaceSurface.Fly,RaceSurface.Rest,RaceSurface.Fly,RaceSurface.Run},
                "Run"=>new[]{RaceSurface.Run,RaceSurface.Run,RaceSurface.Run,RaceSurface.Rest,RaceSurface.Run,RaceSurface.Run},
                "Power"=>new[]{RaceSurface.Run,RaceSurface.Power,RaceSurface.Power,RaceSurface.Rest,RaceSurface.Power,RaceSurface.Run},
                _=>new[]{RaceSurface.Run,RaceSurface.Swim,RaceSurface.Fly,RaceSurface.Power,RaceSurface.Rest,RaceSurface.Run}};
            list.Add(new("race-"+category.ToLowerInvariant()+"-"+level,category+" Race · Level "+level,SportMode.Race,category,level,
                category=="Beginner" ? 25+level*45 : category=="Challenge" ? 350+level*170 : 130+level*130,sections));
        }
        for(int rank=0;rank<4;rank++)list.Add(new("karate-"+KarateRanks[rank].ToLowerInvariant(),KarateRanks[rank]+" Karate",SportMode.Karate,KarateRanks[rank],rank+1,
            new[]{45,220,480,770}[rank],Array.Empty<RaceSurface>()));
        return list.ToArray();
    }
    public static SportsEvent? Find(string id)=>Events.FirstOrDefault(e=>e.Id==id);
    public static bool Open(SportsEvent e,PetState pet)
    {
        bool Won(string id)=>pet.Athletics.Results.TryGetValue(id,out var result) && result.Wins>0;
        if(e.Mode==SportMode.Karate)return e.Level==1 || Won("karate-"+KarateRanks[e.Level-2].ToLowerInvariant());
        if(e.Category=="Beginner")return e.Level==1 || Won("race-beginner-"+(e.Level-1));
        if(e.Level>1)return Won("race-"+e.Category.ToLowerInvariant()+"-"+(e.Level-1));
        return e.Category=="Challenge" ? new[]{"swim","fly","run","power"}.All(c=>Won("race-"+c+"-1")) : Won("race-beginner-3");
    }
    public static string Requirement(SportsEvent e)=>e.Mode==SportMode.Karate ? "Win the preceding Karate tournament." : e.Level>1 ? "Win the preceding level in this category." : e.Category=="Challenge" ? "Win level 1 in all four specialist races." : "Win all Beginner Race levels.";
}
public sealed record SportsRival(string Id,string Name,string Color,string Style,float Run,float Swim,float Fly,float Power,float Stamina,int Initiative,int Intelligence,int Aggression)
{
    public PetState Create(int rating)=>new(){Name=Name,Form=rating>=550 ? Power>1.2f ? "stone" : Fly>1.2f ? "comet" : "swift" : "seed",
        Run=(int)Math.Clamp(rating*Run,0,990),Swim=(int)Math.Clamp(rating*Swim,0,990),Fly=(int)Math.Clamp(rating*Fly,0,990),Power=(int)Math.Clamp(rating*Power,0,990),
        Stamina=(int)Math.Clamp(rating*Stamina,0,990),Luck=Math.Clamp(rating/3,0,330),Intelligence=Intelligence,Initiative=Initiative,Aggressiveness=Aggression,
        Energy=95,Joy=80,Sociability=50,Bond=0};
}
public static class SportsRivals
{
    public static readonly SportsRival[] All={
        new("kite","Kite","Blue","Runner",1.35f,.7f,.9f,.8f,.95f,780,300,10),
        new("ripple","Ripple","Cyan","Swimmer",.85f,1.4f,.65f,.9f,1.1f,430,520,-30),
        new("zephyr","Zephyr","White","Glider",1,.7f,1.4f,.65f,.9f,700,480,0),
        new("ember","Ember","Red","Striker",.85f,.7f,.75f,1.4f,1,850,250,75),
        new("moss","Moss","Green","Defender",.8f,.95f,.8f,1.1f,1.3f,270,850,-65),
        new("pipkin","Pipkin","Purple","Agile",1.25f,.8f,1.15f,.7f,1.05f,650,670,-10)};
}
