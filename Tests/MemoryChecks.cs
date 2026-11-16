using System;
using System.IO;
using System.Linq;
using Mori;

internal static class MemoryChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var pet=new PetState{Curiosity=-60,Initiative=80,Sociability=20,Kindness=0,Bond=10};var memory=pet.History;
        memory.Visit(WorldCatalog.Meadow,"Windfall Meadow");memory.Visit(WorldCatalog.Meadow,"Windfall Meadow");
        check(memory.Events.Count==1 && memory.Events[0].Kind==ChaoMemoryKind.FirstVisit,"Entering an area records one first visit, not one every frame");
        memory.Advance(601);memory.Visit(WorldCatalog.Greenhouse,"Forgotten Greenhouse");memory.Visit(WorldCatalog.Meadow,"Windfall Meadow");
        check(memory.Events.Any(e=>e.Kind==ChaoMemoryKind.ReturnVisit),"Returning after another area becomes a distinct memory");
        check(memory.Record(ChaoMemoryKind.FailedAttempt,"stone",WorldCatalog.Meadow,"Could not reach a stone.",-.7f,.7f),"A real failed attempt can be remembered");
        check(!memory.Record(ChaoMemoryKind.FailedAttempt,"stone",WorldCatalog.Meadow,"Could not reach a stone.",-.7f,.7f),"Repeating an immediate event is coalesced");
        float caution=memory.ObjectBias("stone",WorldCatalog.Meadow);
        check(caution<0,"A past failure adds a bounded cautious bias");
        memory.Advance(150);memory.Record(ChaoMemoryKind.IndependentStudy,"stone",WorldCatalog.Meadow,"Explored a stone.",.7f,.6f);
        check(memory.ObjectBias("stone",WorldCatalog.Meadow)>caution,"Calm success can gradually overcome a bad experience");
        var item=new DiscoveryDefinition("stone",DiscoveryKind.Stone,new(0,0,0),8,1,DiscoveryReaction.Inspect);
        var unexperienced=new PetState{Curiosity=pet.Curiosity,Initiative=pet.Initiative,Energy=pet.Energy};
        var failOnly=new PetState{Curiosity=pet.Curiosity,Initiative=pet.Initiative,Energy=pet.Energy};failOnly.History.Record(ChaoMemoryKind.FailedAttempt,"stone",WorldCatalog.Meadow,"A failed attempt.",-.7f,.8f);
        check(DiscoveryDecisions.Appeal(item,failOnly,new(),0)<DiscoveryDecisions.Appeal(item,unexperienced,new(),0),"Memory changes actual discovery appeal without replacing personality");
        memory.Record(ChaoMemoryKind.SharedStudy,"flower",WorldCatalog.Meadow,"Explored with Fern.",.8f,.7f,new[]{"fern"});
        check(memory.PartnerBias("fern")>memory.PartnerBias("stranger"),"Shared experience favors the same companion");
        memory.Record(ChaoMemoryKind.AnimalSeen,"animal-rabbit",WorldCatalog.Meadow,"Met a rabbit.",.6f,.7f);
        check(memory.KnowsAnimal("rabbit") && !memory.KnowsAnimal("songbird"),"Animal recognition is personal and species-specific");
        memory.Record(ChaoMemoryKind.Pleasant,"river",WorldCatalog.Meadow,"Enjoyed the riverside.",.8f,.8f,place:new(4,0,7));
        check(memory.PlaceBias(WorldCatalog.Meadow,new(4,0,7))>memory.PlaceBias(WorldCatalog.Meadow,new(40,0,7)),"Favorite places affect nearby destination weights");
        float influence=memory.Events.First(e=>e.Source=="river").Influence(memory.Seconds);memory.Advance(40000);
        check(memory.Events.First(e=>e.Source=="river").Influence(memory.Seconds)<influence*.6f,"Emotional influence fades with active time");
        int baseline=pet.Curiosity;
        for(int i=0;i<300;i++)memory.Grow(pet,"one-object","Curiosity",.22f);
        check(pet.Curiosity==baseline,"One repeated action cannot farm personality");
        for(int i=0;i<300;i++)memory.Grow(pet,"unique-"+i,"Curiosity",.22f);
        check(pet.Curiosity>baseline && pet.Curiosity<=baseline+8 && pet.Curiosity<0,"Many different successes give small capped growth and preserve cautious character");
        var other=new PetState();check(other.History.Events.Count==0 && other.History.Confidence==0,"Histories and confidence evidence are not shared between pets");
        memory.Record(ChaoMemoryKind.Pleasant,"cherished",WorldCatalog.Meadow,"A cherished moment.",1,1);
        for(int i=0;i<150;i++)memory.Record(ChaoMemoryKind.Pleasant,"small-"+i,WorldCatalog.Meadow,"A minor moment.",.2f,.1f);
        check(memory.Events.Count<=ChaoMemory.Capacity && memory.Events.Any(e=>e.Source=="cherished") && memory.GrowthSources.Count<=128,"Salient events survive bounded memory eviction; growth ledger is separately bounded");
        var resident=new PetState();resident.History.Events=Enumerable.Range(0,150).Select(i=>new ChaoMemoryEvent{Kind=ChaoMemoryKind.Pleasant,Source="event-"+i,Area="garden",Description="A quiet moment.",Importance=.2f}).ToList();
        pet.GardenEggs.Add(new(){Id="resident",Hatched=true,Chao=resident});pet.Validate();
        check(resident.History.Events.Count==64,"Save validation bounds every resident's history as well as Mori's");
        var owner=new PetState();var friend=new PetState{Name="Fern"};var link=owner.SocialLink("main","fern",true)!;link.Met=true;
        for(int i=0;i<7;i++)ChaoExperience.Together(owner,"main",owner,"fern",friend,ChaoMemoryKind.SharedStudy,"site-"+i,WorldCatalog.Meadow);
        check(link.ScoreFrom("main")>0 && owner.SocialLinks.Count==1 && friend.SocialLinks.Count==0,"Shared experience updates the existing directed relationship, never a parallel friendship");
        int score=link.ScoreFrom("main");for(int i=0;i<100;i++)ChaoExperience.Together(owner,"main",owner,"fern",friend,ChaoMemoryKind.SharedStudy,"site-0",WorldCatalog.Meadow);
        check(link.ScoreFrom("main")==score,"Repeated shared events cannot farm friendship");
        check(ChaoExpressiveness.Resolve(true,false,false,true,100,ChaoExpression.Delighted)==ChaoExpression.Sleepy && ChaoExpressiveness.Resolve(false,false,true,true,100,ChaoExpression.Delighted)==ChaoExpression.Annoyed,"Sleep and distress outrank happy expression requests");
        check(ChaoExpressiveness.Intensity(new PetState{Sociability=100,Initiative=990})>ChaoExpressiveness.Intensity(new PetState{Sociability=0,Initiative=0}),"Personality changes expressive intensity continuously");
        var random=new Random(3);var intervals=Enumerable.Range(0,20).Select(_=>ChaoExpressiveness.BlinkDelay(random,pet)).ToArray();
        check(intervals.Distinct().Count()>15 && intervals.All(t=>t>=1.9f && t<=7.1f),"Blink intervals vary within restrained limits");
        string folder=Path.Combine(Path.GetTempPath(),"MoriMemory-"+Guid.NewGuid());
        try
        {
            string file=Path.Combine(folder,"pet.json");SaveStore.Save(file,pet);var read=SaveStore.Load(file,out var warning);
            check(warning=="" && read.History.Events.Count==memory.Events.Count && read.Curiosity==pet.Curiosity && read.History.GrowthSources.Count==memory.GrowthSources.Count,"Memory, bounded experience growth and personality survive save/load");
            File.WriteAllText(file,"{\"Name\":\"Legacy\"}");read=SaveStore.Load(file,out _);check(read.Name=="Legacy" && read.History.Events.Count==0,"Old saves gain an empty history without fabricated events");
            read.History.Visit(WorldCatalog.Meadow,"Windfall Meadow");check(!read.History.Events[0].Description.Contains("first time"),"Legacy pets do not claim a first-ever visit when their earlier history is unknown");
        }
        finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }
}
