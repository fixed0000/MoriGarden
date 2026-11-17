using System;
using System.Collections.Generic;
using System.Linq;

namespace Mori;

// Pure progression rules. Progress is an integer percentage, not skill points.
public sealed class ChaoStat
{
    public int Grade {get;set;}=2;
    public int Level {get;set;}
    public int Points {get;set;}
    public int Progress {get;set;}
    public void Validate(bool hidden)
    {
        Grade=Math.Clamp(Grade,0,hidden ? 255 : 5);
        Level=Math.Clamp(Level,0,99);
        Points=Math.Clamp(Points,0,hidden ? 4000 : 3266);
        Progress=Math.Clamp(Progress,0,99);
    }
    public int AddProgress(int amount,bool hidden,Random random)
    {
        Validate(hidden);long bar=Math.Max(0,(long)Progress+amount);int levels=0;
        while(bar>=100 && Level<99)
        {
            bar-=100;Level++;levels++;
            Points=Math.Min(hidden ? 4000 : 3266,Points+13+Grade*3+random.Next(-2,3));
        }
        Progress=(int)Math.Min(99,bar);return levels;
    }
    public ChaoStat Reborn()=>new(){Grade=Grade,Level=1,Points=Points/10,Progress=0};
}

public sealed class ChaoStatistics
{
    public ChaoStat Swim {get;set;}=new();
    public ChaoStat Fly {get;set;}=new();
    public ChaoStat Run {get;set;}=new();
    public ChaoStat Power {get;set;}=new();
    public ChaoStat Stamina {get;set;}=new();
    public ChaoStat Luck {get;set;}=new(){Grade=0};
    public ChaoStat Intelligence {get;set;}=new(){Grade=0};
    public static readonly string[] Names={"Swim","Fly","Run","Power","Stamina","Luck","Intelligence"};
    public static bool Hidden(string name)=>name is "Luck" or "Intelligence";
    public ChaoStat Get(string name)=>name switch{"Swim"=>Swim,"Fly"=>Fly,"Run"=>Run,"Power"=>Power,"Stamina"=>Stamina,"Luck"=>Luck,"Intelligence"=>Intelligence,_=>throw new ArgumentException("Unknown stat: "+name)};
    public void Validate()
    {
        Swim??=new();Fly??=new();Run??=new();Power??=new();Stamina??=new();Luck??=new(){Grade=0};Intelligence??=new(){Grade=0};
        foreach(string name in Names)Get(name).Validate(Hidden(name));
    }
    public ChaoStatistics Reborn()=>new(){Swim=Swim.Reborn(),Fly=Fly.Reborn(),Run=Run.Reborn(),Power=Power.Reborn(),Stamina=Stamina.Reborn(),Luck=Luck.Reborn(),Intelligence=Intelligence.Reborn()};
    public void Promote(string skill)
    {
        if(Hidden(skill))return;var stat=Get(skill);stat.Grade=Math.Min(5,stat.Grade+1);
    }
    public static ChaoStatistics Fresh(Random random)
    {
        var result=new ChaoStatistics();
        // MoriGarden birth distribution; not an assertion of original genetics.
        foreach(string name in Names)result.Get(name).Grade=Hidden(name) ? 0 : random.Next(0,6);
        return result;
    }
    public static ChaoStatistics Legacy(params int[] values)
    {
        var result=new ChaoStatistics();
        for(int i=0;i<Names.Length;i++)
        {
            var stat=result.Get(Names[i]);int cap=Hidden(Names[i]) ? 4000 : 3266;
            stat.Points=(int)Math.Round(Math.Clamp(values[i],0,990)*cap/990d,MidpointRounding.AwayFromZero);
            int legacy=Math.Clamp(values[i],0,990);
            // Preserve the former displayed level/bar rather than pretending
            // we can reconstruct a history of random per-level point gains.
            stat.Level=legacy==0 ? 0 : Math.Clamp(legacy/10,1,99);
            stat.Progress=legacy%10*10;
        }
        return result;
    }
    // Existing world distances/coefficient curves remain in their calibrated
    // units. The input is always actual points; grade/level never affect pace.
    public static int Ability(int points,bool hidden=false)=>(int)Math.Round(Math.Clamp(points,0,hidden ? 4000 : 3266)*990d/(hidden ? 4000 : 3266));
    public string Summary(string name)
    {
        var s=Get(name);return $"{name}  Lv {s.Level:00}  {s.Points} pts  {s.Progress}%";
    }
    public static void ValidateEditor(string name,ChaoStat value,ChaoStat? previous=null)
    {
        int cap=Hidden(name) ? 4000 : 3266,grade=Hidden(name) ? 255 : 5;
        if(!Names.Contains(name) || value.Grade<0 || value.Grade>grade || value.Level<0 || value.Level>99 || value.Points<0 || value.Points>cap || value.Progress<0 || value.Progress>99 || value.Level==0 && value.Points!=0)
            throw new ArgumentException($"Invalid {name}: grade 0–{grade}, level 0–99, points 0–{cap}, progress 0–99; level 0 has no points.");
        bool importedUnchanged=previous!=null && previous.Grade==value.Grade && previous.Level==value.Level && previous.Points==value.Points;
        int minimum=Math.Max(0,value.Level-1)*11;
        int maximum=Math.Min(cap,(Hidden(name) ? 400 : 326)+value.Level*(15+value.Grade*3));
        if(!importedUnchanged && (value.Points<minimum || value.Points>maximum))
            throw new ArgumentException($"{name}: points do not fit this level/grade, even allowing inherited points. Use a consistent preset.");
    }
}

public sealed record AnimalTrainingEffect(string Species,string Skill,int Swim,int Fly,int Run,int Power,bool Confirmed,string Source);
public static class AnimalTraining
{
    private const string Source="https://chao-island.com/wiki/Small_Animals";
    public static readonly AnimalTrainingEffect[] Effects={
        new("penguin","Swim",36,12,8,-24,true,Source),
        new("parrot","Fly",0,48,-20,4,true,Source),
        new("rabbit","Run",0,8,40,-16,true,Source),
        new("bear-cub","Power",8,-16,4,36,true,Source),
        new("gorilla","Power",-4,-8,4,40,true,Source),
        new("songbird","Fly",8,40,-12,4,false,"MoriGarden custom songbird; no exact SA2B species mapping")};
    public static AnimalTrainingEffect Find(string id)=>Effects.FirstOrDefault(e=>e.Species==id) ?? throw new ArgumentException("Unknown training animal: "+id);
}

public sealed record FruitTrainingEffect(string Kind,int PerBite,int FinalBite,bool Confirmed,string Source);
public static class FruitTraining
{
    private const string Source="https://chao-island.com/wiki/Fruit";
    public static readonly FruitTrainingEffect[] Effects={
        new(FruitKinds.Garden,10,10,true,Source),new(FruitKinds.Heart,10,10,true,Source),
        new(FruitKinds.Blue,10,10,false,"MoriGarden square fruit; stamina amount follows SA2B Square Fruit"),
        new(FruitKinds.Yellow,10,10,false,"MoriGarden triangle fruit; stamina amount follows SA2B Triangle Fruit"),
        new(FruitKinds.Red,10,10,false,"MoriGarden round fruit; stamina amount follows SA2B Round Fruit"),
        new(FruitKinds.Green,10,10,false,"MoriGarden custom diamond fruit"),
        new(FruitKinds.Moonblossom,10,10,false,"MoriGarden custom mission fruit")};
    public static int Progress(string kind,bool last)
    {
        var effect=Effects.FirstOrDefault(e=>e.Kind==kind) ?? throw new ArgumentException("Unknown fruit: "+kind);
        return last ? effect.FinalBite : effect.PerBite;
    }
}
