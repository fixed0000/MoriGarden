using System;
using System.IO;
using System.Linq;
using Mori;

internal static class WildlifeChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var data=new WildlifeProgress();
        check(AnimalCatalog.All.Select(a=>a.Id).Distinct().Count()==6,"Animal species have stable unique IDs and reuse the four training effects");
        check(AnimalCatalog.Find("rabbit")!.Speed>AnimalCatalog.Find("bear-cub")!.Speed && AnimalCatalog.Find("rabbit")!.Caution>AnimalCatalog.Find("bear-cub")!.Caution,"Rabbit and bear differ in movement and caution");
        data.Meet("rabbit");check(data.Discoveries["rabbit"].Met && data.Discoveries["rabbit"].Obtained==0,"Meeting an animal does not grant it");
        check(data.Obtain("grass","rabbit",0) && data.Total==1 && data.Discoveries["rabbit"].Obtained==1,"A befriended animal enters the shared inventory and discovery ledger");
        check(!data.Obtain("grass","rabbit",0) && data.Total==1,"The same habitat generation cannot be collected twice");
        check(!data.Respawn("grass",true,true),"Collected habitats retain their cooldown");
        data.Advance(600);check(!data.Respawn("grass",false,true) && !data.Respawn("grass",true,false),"Respawn requires a safe off-camera location");
        check(data.Respawn("grass",true,true) && data.Slot("grass").Generation==1,"A cooled habitat creates one new generation");
        check(!data.Obtain("grass","rabbit",0) && data.Obtain("grass","rabbit",1),"A stale actor cannot duplicate a later habitat generation");
        while(data.Total<WildlifeProgress.Capacity)data.Store("songbird");
        check(!data.Obtain("forest","bear-cub",0) && !data.Slot("forest").Collected && !data.Store("rabbit"),"A full bag preserves the wild animal and rejects extra ownership");
        check(data.Take("rabbit") && data.Obtain("forest","bear-cub",0),"Freeing one bag slot allows a later encounter");
        check(!data.Take("missing") && !data.Obtain("unknown","missing",0),"Unknown species cannot be trained or captured");
        var pet=new PetState { Wildlife=data,Run=100,Fly=100,Swim=100,Power=100 };
        foreach(var kind in AnimalCatalog.All)
        {
            var before=pet.Stat(kind.Skill).Progress;
            pet.TrainWithSpecies(kind.Id,new Random(3));
            check(pet.Stat(kind.Skill).Progress!=before && pet.RecentAnimalSkill==kind.Skill,$"{kind.Name} uses the shared species progression path");
        }
        check(AnimalEncounterRules.CalmRate(AnimalCatalog.All[0],3.7f)>0 && AnimalEncounterRules.CalmRate(AnimalCatalog.All[0],5.5f)<0,"Ordinary walking is calm and sprinting is startling");
        var kindPet=new PetState { Intelligence=990,Kindness=100,Sociability=100 };
        check(AnimalEncounterRules.ChaoHelp(kindPet)>AnimalEncounterRules.ChaoHelp(new PetState{Intelligence=0,Kindness=-100,Sociability=0}) && AnimalEncounterRules.ChaoHelp(new PetState())>0,"A Chao helps even without high intelligence; traits affect the help");
        var directory=Path.Combine(Path.GetTempPath(),"MoriWildlife-"+Guid.NewGuid());
        try
        {
            string file=Path.Combine(directory,"pet.json");SaveStore.Save(file,pet);var loaded=SaveStore.Load(file,out var warning);
            check(warning=="" && loaded.Wildlife.Total==12 && loaded.Wildlife.Slot("grass").Collected && loaded.Wildlife.Discoveries["bear-cub"].Obtained==1,"Inventory, cooldowns, generations and discoveries survive saving");
            File.WriteAllText(file,"{\"Name\":\"Legacy\"}");loaded=SaveStore.Load(file,out _);
            check(loaded.Wildlife.Total==0 && loaded.Name=="Legacy","Old saves load without wildlife fields");
        }
        finally {if(Directory.Exists(directory))Directory.Delete(directory,true);}
        data.Bag["rabbit"]=999;data.Bag["invalid"]=99;data.Slot("bad").Cooldown=float.NaN;data.Validate();
        check(data.Total==12 && !data.Bag.ContainsKey("invalid") && float.IsFinite(data.Slot("bad").Cooldown),"Validation bounds inventory and repairs invalid cooldowns");
    }
}
