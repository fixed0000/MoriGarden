using System;
using System.Text.Json.Serialization;

namespace Mori;

public sealed partial class PetState
{
    public int StatSchemaVersion {get;set;}
    public ChaoStatistics? Statistics {get;set;}
    public int StatReincarnations {get;set;}
    private int legacySwim,legacyFly,legacyRun,legacyPower,legacyStamina,legacyLuck,legacyIntelligence;
    // JSON/source compatibility aliases, all pointing to the same canonical data.
    public int Swim {get=>Statistics?.Swim?.Points ?? legacySwim;set{if(Statistics?.Swim is {} s)s.Points=value;else legacySwim=value;}}
    public int Fly {get=>Statistics?.Fly?.Points ?? legacyFly;set{if(Statistics?.Fly is {} s)s.Points=value;else legacyFly=value;}}
    public int Run {get=>Statistics?.Run?.Points ?? legacyRun;set{if(Statistics?.Run is {} s)s.Points=value;else legacyRun=value;}}
    public int Power {get=>Statistics?.Power?.Points ?? legacyPower;set{if(Statistics?.Power is {} s)s.Points=value;else legacyPower=value;}}
    public int Stamina {get=>Statistics?.Stamina?.Points ?? legacyStamina;set{if(Statistics?.Stamina is {} s)s.Points=value;else legacyStamina=value;}}
    public int Luck {get=>Statistics?.Luck?.Points ?? legacyLuck;set{if(Statistics?.Luck is {} s)s.Points=value;else legacyLuck=value;}}
    public int Intelligence {get=>Statistics?.Intelligence?.Points ?? legacyIntelligence;set{if(Statistics?.Intelligence is {} s)s.Points=value;else legacyIntelligence=value;}}
    public void EnsureStatistics()
    {
        if(StatSchemaVersion is <0 or >1)throw new ArgumentException("Unsupported stat schema");
        if(Statistics==null)
        {
            if(StatSchemaVersion!=0)throw new ArgumentException("Missing stat records");
            Statistics=ChaoStatistics.Legacy(legacySwim,legacyFly,legacyRun,legacyPower,legacyStamina,legacyLuck,legacyIntelligence);
        }
        StatSchemaVersion=1;Statistics.Validate();StatReincarnations=Math.Clamp(StatReincarnations,0,10000);
    }
    public void EnsureStatisticsTree()
    {
        EnsureStatistics();if(GardenEggs!=null)foreach(var egg in GardenEggs)egg?.Chao?.EnsureStatisticsTree();
    }
    public ChaoStat Stat(string name){EnsureStatistics();return Statistics!.Get(name);}
    private int Ability(string name){EnsureStatistics();return ChaoStatistics.Ability(Statistics!.Get(name).Points,ChaoStatistics.Hidden(name));}
    [JsonIgnore] public int SwimAbility=>Ability("Swim");
    [JsonIgnore] public int FlyAbility=>Ability("Fly");
    [JsonIgnore] public int RunAbility=>Ability("Run");
    [JsonIgnore] public int PowerAbility=>Ability("Power");
    [JsonIgnore] public int StaminaAbility=>Ability("Stamina");
    [JsonIgnore] public int LuckAbility=>Ability("Luck");
    [JsonIgnore] public int IntelligenceAbility=>Ability("Intelligence");
    public int TrainProgress(string name,int progress,Random? random=null)=>Stat(name).AddProgress(progress,ChaoStatistics.Hidden(name),random ?? Random.Shared);
    public void InitializeFreshStatistics(Random? random=null)
    {Statistics=ChaoStatistics.Fresh(random ?? Random.Shared);StatSchemaVersion=1;StatReincarnations=0;}
    // Not an automatic death/life-cycle system. Call only from a future explicit
    // reincarnation event; identity, age, relationships and achievements untouched.
    public void ReincarnateStatistics()
    {EnsureStatistics();Statistics=Statistics!.Reborn();StatReincarnations=Math.Min(10000,StatReincarnations+1);}
    public void TrainWithSpecies(string species,Random? random=null)
    {
        if(!CanAct)return;var effect=AnimalTraining.Find(species);random??=Random.Shared;
        TrainProgress("Swim",effect.Swim,random);TrainProgress("Fly",effect.Fly,random);TrainProgress("Run",effect.Run,random);TrainProgress("Power",effect.Power,random);
        TrainProgress("Luck",20,random);TrainProgress("Intelligence",20,random);
        ChangeRelationship(.45f);RecentAnimalSkill=effect.Skill;Joy=Math.Min(100,Joy+4);
    }
}
