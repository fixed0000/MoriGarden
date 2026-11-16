using System;
using System.Collections.Generic;
using System.Linq;

namespace Mori;

public enum SportMode { Race,Karate }
public enum RaceSurface { Run,Swim,Fly,Power,Rest,Puzzle,Choice }
public sealed record SportsEvent(string Id,string Title,SportMode Mode,string Category,int Level,int Rating,RaceSurface[] Sections,int Bouts=5,string Course="");
public static class SportsCatalog
{
    public static readonly string[] RaceCategories={"Beginner"};
    public static readonly string[] KarateRanks={"Beginner","Standard","Expert","Super"};
    public static readonly (string Id,string Title,RaceSurface Skill)[] BeginnerCourses={
        ("crab-pool","Crab Pool",RaceSurface.Swim),("stump-valley","Stump Valley",RaceSurface.Fly),
        ("mushroom-forest","Mushroom Forest",RaceSurface.Run),("block-canyon","Block Canyon",RaceSurface.Power)};
    public static readonly SportsEvent[] Events=Build();
    // Historical IDs remain readable; they do not masquerade as wins on the new courses.
    private static readonly SportsEvent[] Legacy=BuildLegacy();
    private static SportsEvent[] Build()
    {
        var list=new List<SportsEvent>();
        foreach(var course in BeginnerCourses)for(int level=1;level<=3;level++)
            list.Add(new("race-"+course.Id+"-"+level,course.Title+" · Level "+level,SportMode.Race,"Beginner",level,25+level*45,new[]{RaceSurface.Run,course.Skill,RaceSurface.Run},Course:course.Id));
        for(int rank=0;rank<4;rank++)list.Add(new("karate-"+KarateRanks[rank].ToLowerInvariant(),KarateRanks[rank]+" Karate",SportMode.Karate,KarateRanks[rank],rank+1,new[]{45,220,480,770}[rank],Array.Empty<RaceSurface>()));
        return list.ToArray();
    }
    private static SportsEvent[] BuildLegacy()
    {
        var list=new List<SportsEvent>();
        foreach(string category in new[]{"beginner","swim","fly","run","power","challenge"})for(int level=1;level<=3;level++)
            list.Add(new("race-"+category+"-"+level,"Previous "+category+" race · Level "+level,SportMode.Race,"Legacy",level,70,new[]{RaceSurface.Run},Course:"mushroom-forest"));
        list.Add(new("karate-master","Previous Master Karate",SportMode.Karate,"Legacy",4,770,Array.Empty<RaceSurface>(),3));return list.ToArray();
    }
    public static SportsEvent? Find(string id)=>Events.Concat(Legacy).FirstOrDefault(e=>e.Id==id);
    public static bool Open(SportsEvent e,PetState pet)
    {
        bool Won(string id)=>pet.Athletics.Results.TryGetValue(id,out var result) && result.Wins>0;
        if(e.Category=="Legacy")return false;
        if(e.Mode==SportMode.Karate)return e.Level<4 || KarateRanks.Take(3).All(rank=>Won("karate-"+rank.ToLowerInvariant()));
        return e.Level==1 || Won("race-"+e.Course+"-"+(e.Level-1));
    }
    public static string Requirement(SportsEvent e)=>e.Mode==SportMode.Karate ? "Clear Beginner, Standard and Expert to unlock Super." : "Win the preceding level on this course.";
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
        new("luma","Luma","Yellow","Balanced",1,1,.95f,1,1.1f,480,450,-15),
        new("pebble","Pebble","Black","Climber",.9f,.8f,.8f,1.3f,1.15f,580,440,25),
        new("pipkin","Pipkin","Purple","Agile",1.25f,.8f,1.15f,.7f,1.05f,650,670,-10)};
}
