using System;
using System.IO;
using Mori;

int checks = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new Exception(label);
    Console.WriteLine("PASS " + label); checks++;
}
var pet = new PetState { Fullness = 90, Energy = 90 };
Check(pet.Feed() && pet.Fullness == 100, "Food clamps at 100");
Check(!pet.Feed() && pet.Care == 1, "Full pet cannot farm care by feeding");
pet.Tick(10000, false);
Check(pet.Fullness == 0 && pet.Energy == 0 && pet.Joy == 0, "Needs never become negative");
pet.Tick(1000, true);
Check(pet.Energy == 100, "Sleeping restores energy up to 100");
pet = new PetState { Care = 9 };
Check(!pet.Evolve(), "Evolution requires full growth");
pet.Pet(); Check(pet.Evolve() && pet.Form == "bloom", "Care creates Bloom");
Check(!pet.Evolve(), "Evolution triggers only once");
pet = new PetState();
for (int i = 0; i < 4; i++) pet.CompleteRace(8);
Check(pet.Evolve() && pet.Form == "comet" && pet.Stars == 32, "Races create Comet and award stars");
pet.CompleteRace(0);
Check(pet.BestScore == 8 && pet.Agility == 12, "Empty race gives no agility and preserves record");
var dir = Path.Combine(Path.GetTempPath(), "MoriTests-" + Guid.NewGuid());
var path = Path.Combine(dir, "pet.json");
try
{
    SaveStore.Save(path, pet);
    var restored = SaveStore.Load(path, out var warning);
    Check(warning == "" && restored.Form == "comet" && restored.Stars == 32, "Progress survives save/load");
    pet.Stars = 40; SaveStore.Save(path, pet);
    File.WriteAllText(path, "{broken");
    restored = SaveStore.Load(path, out warning);
    Check(restored.Stars == 32 && warning.Length > 0, "Corrupt save recovers backup");
    File.WriteAllText(path + ".bak", "null");
    restored = SaveStore.Load(path, out warning);
    Check(restored.Form == "seed" && File.Exists(path + ".damaged"), "Unrecoverable save retained for inspection");
    File.WriteAllText(path, "{\"Version\":99}");
    restored = SaveStore.Load(path, out warning);
    Check(warning.Length > 0, "Unsupported version is reported");
}
finally
{
    // РЈРґР°Р»СЏСЋС‚СЃСЏ С‚РѕР»СЊРєРѕ С„Р°Р№Р»С‹, СЃРѕР·РґР°РЅРЅС‹Рµ СЌС‚РёРј С‚РµСЃС‚РѕРј, Р±РµР· СЂРµРєСѓСЂСЃРёРІРЅРѕРіРѕ СѓРґР°Р»РµРЅРёСЏ.
    foreach (var file in Directory.GetFiles(dir)) File.Delete(file);
    Directory.Delete(dir);
}
var egg = PetState.NewEgg();
Check(!egg.CanAct && !egg.AdvanceLife(10), "Egg remains until hatch time");
Check(egg.AdvanceLife(18) && egg.LifeStage == "Child", "Egg hatches while time advances");
var child = new PetState { Care = 10 };
child.TrainWithAnimal("Swim");
Check(!child.AdvanceLife(PetState.FirstEvolutionSeconds-1) && child.LifeStage == "Child", "Child remains young before three garden hours");
Check(child.AdvanceLife(1) && child.InCocoon && !child.Feed(), "Growing child enters cocoon");
Check(child.AdvanceLife(9) && child.LifeStage == "Adult" && child.Form == "tide", "Recent animal training shapes adult form");
Check(!child.AdvanceLife(PetState.SecondEvolutionSeconds-1), "Second evolution is gradual before twelve adult hours");
Check(child.AdvanceLife(1) && child.LifeStage=="Second evolution", "Second evolution matures after twelve adult hours without a cocoon");
Check(PetState.BallMissChance(new() { Clumsy=true })>PetState.BallMissChance(new() { Dexterous=true,ActivityPreference=5 }), "Personality affects missed catches");
var relation=new ChaoRelationship { First="a",Second="b",Score=10 };
relation.SetScoreFrom("b",-40);
Check(relation.ScoreFrom("a")==10 && relation.ScoreFrom("b")==-40,"Chao can have different attitudes toward each other");
var natural=new PetState();
Check(natural.AdvanceLife(PetState.FirstEvolutionSeconds) && natural.AdvanceLife(9) && natural.LifeStage=="Adult", "Natural growth does not require care points");
var fed=new PetState();
for (int i=0;i<4;i++) fed.FeedBite(i==3);
Check(fed.AdvanceLife(PetState.FirstEvolutionSeconds-210), "Four bites bring first evolution forward by three and a half minutes");
var personality=new PetState { ActivityPreference=5,Clumsy=true,AdultSeconds=1234,FavoriteInstrument="Synthesizer" };
personality.SocialLinks.Add(relation);
var restoredPersonality=System.Text.Json.JsonSerializer.Deserialize<PetState>(System.Text.Json.JsonSerializer.Serialize(personality))!;
restoredPersonality.Validate();
Check(restoredPersonality.AdultSeconds==1234 && restoredPersonality.Clumsy && restoredPersonality.ActivityPreference==5 && restoredPersonality.FavoriteInstrument=="Synthesizer" && restoredPersonality.SocialLinks[0].ScoreFrom("b")==-40, "Development, personality and directed attitudes survive serialization");
var oldSave=System.Text.Json.JsonSerializer.Deserialize<PetState>("{\"Version\":1,\"Name\":\"Old Mori\",\"ManualLocation\":{\"Zone\":0,\"MoriZone\":0,\"PlayerX\":1,\"PlayerY\":0,\"PlayerZ\":2,\"MoriX\":3,\"MoriY\":0,\"MoriZ\":4}}")!;
oldSave.Validate();
Check(oldSave.Expedition.VisitedAreas.Count==0 && oldSave.ManualLocation!.AreaId==null && oldSave.Name=="Old Mori", "Old garden saves load without expedition fields");
var catalog=WorldCatalog.Default;
Check(catalog.IsOpen(WorldCatalog.Meadow,oldSave.Expedition) && !catalog.IsExpedition("garden") && catalog.IsExpedition(WorldCatalog.Meadow), "Expeditions are available separately from permanent gardens");
var extension=new WorldCatalog(new[]{new WorldArea("region-a","Region A",WorldAreaKind.ExpeditionRegion,InitiallyOpen:true),
    new WorldArea("region-b","Region B",WorldAreaKind.ExpeditionRegion),new WorldArea("site-a","Site A",WorldAreaKind.ExpeditionSite,"region-a")},
    new[]{new WorldRoute("a-b","region-a","region-b",new(0,0,0),new(1,0,0)),new WorldRoute("a-site","region-a","site-a",new(2,0,0),new(0,0,1))});
var progress=new ExpeditionProgress();
Check(!System.Linq.Enumerable.Any(extension.Exits("region-a",progress)), "Locked regions and sites cannot be entered");
progress.UnlockedAreas.Add("region-b"); progress.UnlockedAreas.Add("site-a");
Check(System.Linq.Enumerable.Count(extension.Exits("region-a",progress))==2,"New regions and nested sites unlock through data without new travel code");
progress.VisitedAreas.Add("region-b"); progress.AreaFlags["region-b"]=new() { "route-opened" }; progress.UnlockedAreas.Add("future-id");
var expeditionSave=new PetState { Expedition=progress,ManualLocation=new(4,0,1,0,2,3,0,4,AreaId:"region-b") };
var expeditionDir=Path.Combine(Path.GetTempPath(),"MoriExpeditionTests-"+Guid.NewGuid());
try {
    var expeditionPath=Path.Combine(expeditionDir,"pet.json"); SaveStore.Save(expeditionPath,expeditionSave);
    var loaded=SaveStore.Load(expeditionPath,out var notice);
    Check(notice=="" && loaded.ManualLocation?.AreaId=="region-b" && loaded.Expedition.VisitedAreas.Contains("region-b") &&
        loaded.Expedition.AreaFlags["region-b"].Contains("route-opened") && loaded.Expedition.UnlockedAreas.Contains("future-id"),"Expedition unlocks, progress and location survive disk save/load, including future IDs");
} finally { if(Directory.Exists(expeditionDir)) Directory.Delete(expeditionDir,true); }
var invalid=false;
try { _=new WorldCatalog(new[]{new WorldArea("bad","Bad",WorldAreaKind.ExpeditionSite,"missing")},Array.Empty<WorldRoute>()); }
catch(ArgumentException) { invalid=true; }
Check(invalid,"Catalog rejects orphan expedition sites");
var rosterOwner=new PetState();
rosterOwner.GardenEggs.Add(new() { Id="black",Hatched=true,Color="Black",Chao=new PetState { Name="Mori" } });
rosterOwner.GardenEggs.Add(new() { Id="shell",Hatched=false,Chao=new PetState() });
rosterOwner.GardenEggs.Add(new() { Id="cocoon",Hatched=true,Chao=new PetState { CocoonSeconds=10 } });
Check(ExpeditionRoster.Available(rosterOwner).Count==2,"Only active hatched Chao enter a team, regardless of matching names");
var chosen=ExpeditionRoster.Resolve(rosterOwner,new[]{"black","black","missing","main"});
Check(chosen.Count==2 && chosen[0]=="black" && chosen[1]=="main","Team IDs exclude duplicates and missing pets");
var real=ExpeditionRoster.Available(rosterOwner)[1].State;real.Energy=23;
Check(ReferenceEquals(real,rosterOwner.GardenEggs.Find(e=>e.Id=="black")!.Chao) && rosterOwner.GardenEggs.Find(e=>e.Id=="black")!.Chao!.Energy==23,"Team modifies live pet state without copies");
for(int i=0;i<3;i++)rosterOwner.GardenEggs.Add(new() { Id="extra-"+i,Hatched=true,Chao=new PetState() });
Check(ExpeditionRoster.Resolve(rosterOwner,new[]{"main","black","extra-0","extra-1","extra-2"}).Count==3,"A party never exceeds three eligible identities");
var outgoing=new PetState { Curiosity=100,Initiative=990,Sociability=100,Kindness=100,Aggressiveness=-100,Energy=90 };
var cautious=new PetState { Curiosity=-100,Initiative=0,Sociability=0,Kindness=-100,Aggressiveness=100,Energy=90 };
Check(ExpeditionDecisions.Weight(ExpeditionIntent.Explore,outgoing,false)>ExpeditionDecisions.Weight(ExpeditionIntent.Explore,cautious,false)*3 &&
    ExpeditionDecisions.Weight(ExpeditionIntent.Social,outgoing,true)>ExpeditionDecisions.Weight(ExpeditionIntent.Social,cautious,true),"Continuous curiosity, initiative and social traits change decision weights");
Check(ExpeditionDecisions.Weight(ExpeditionIntent.Sleep,new PetState { Energy=3 },false)>5 &&
    ExpeditionDecisions.Weight(ExpeditionIntent.Sleep,outgoing,false)==0 && ExpeditionDecisions.Weight(ExpeditionIntent.Social,outgoing,false)==0,"Sleep requires fatigue and social actions require a suitable companion");
rosterOwner.Expedition.LastTeamIds=new() { "black","main","black" };rosterOwner.Validate();
var teamDir=Path.Combine(Path.GetTempPath(),"MoriTeamTests-"+Guid.NewGuid());
try {
    var file=Path.Combine(teamDir,"team.json");SaveStore.Save(file,rosterOwner);var read=SaveStore.Load(file,out var note);
    Check(note=="" && read.Expedition.LastTeamIds.Count==2 && read.Expedition.LastTeamIds[0]=="black" &&
        ReferenceEquals(ExpeditionRoster.Available(read)[1].State,read.GardenEggs.Find(e=>e.Id=="black")!.Chao),"Team survives save/load and rebinds existing identities");
} finally { if(Directory.Exists(teamDir))Directory.Delete(teamDir,true); }
Check(ExpeditionRoster.Resolve(new PetState(),null).Count==0,"Old saves require no new party fields");
DiscoveryChecks.Run(Check);
GreenhouseChecks.Run(Check);
Console.WriteLine($"{checks} checks passed.");
