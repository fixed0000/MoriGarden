using System;
using System.IO;
using System.Linq;
using Mori;

internal static class DiscoveryChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var item=new DiscoveryDefinition("meadow/test-plant",DiscoveryKind.Plant,new(0,0,0),10,1.4f,DiscoveryReaction.Inspect|DiscoveryReaction.Share);
        var pet=new PetState { Curiosity=100,Initiative=900,Bond=100,Sociability=100,Energy=90 };
        var memory=new DiscoveryMemory();
        check(memory.Knowledge(item.Id,0)==DiscoveryKnowledge.Unknown,"Unseen interests remain unknown");
        memory.Notice(item.Id,0);
        check(memory.Knowledge(item.Id,1)==DiscoveryKnowledge.Noticed && !memory.CanConsider(item,30),"Noticing persists without repeated immediate reactions");
        check(memory.Complete(item,pet,10) && memory.Knowledge(item.Id,20)==DiscoveryKnowledge.Recent,"Physical study creates a recent discovery");
        check(!memory.Complete(item,pet,11) && memory.Find(item.Id)!.Visits==1,"Duplicate completion cannot farm visits or experience");
        check(!memory.CanConsider(item,1209) && memory.CanConsider(item,1210),"Repeat research requires twenty active expedition minutes");
        memory.Complete(item,pet,1210);memory.Complete(item,pet,2410);
        check(memory.Knowledge(item.Id,2700)==DiscoveryKnowledge.Familiar,"Repeated calm contacts create familiarity");
        var once=item with { Repeatable=false };
        check(!memory.CanConsider(once,1e6),"Nonrepeatable interests stay completed");
        var fresh=new DiscoveryMemory();var rng=new Random(17);int high=0,low=0,lowInit=0,alerts=0;
        var shy=new PetState { Curiosity=-80,Initiative=20,Sociability=10,Energy=90 };
        var hesitant=new PetState { Curiosity=100,Initiative=0,Sociability=30,Energy=90 };
        for(int i=0;i<2000;i++)
        {
            var a=DiscoveryDecisions.Choose(item,pet,fresh,0,rng);if(a.Choice==DiscoveryChoice.Approach)high++;if(a.SharePlayer)alerts++;
            if(DiscoveryDecisions.Choose(item,shy,fresh,0,rng).Choice==DiscoveryChoice.Approach)low++;
            if(DiscoveryDecisions.Choose(item,hesitant,fresh,0,rng).Choice==DiscoveryChoice.Approach)lowInit++;
        }
        check(high>low*4,"Curious independent pets approach much more often than cautious pets");
        check(high>lowInit*3,"High curiosity without initiative still favors observation");
        check(DiscoveryDecisions.Choose(item,shy,fresh,0,new Random(1)).ThoughtSeconds>DiscoveryDecisions.Choose(item,pet,fresh,0,new Random(1)).ThoughtSeconds,"Cautious pets watch and deliberate longer");
        check(alerts>800 && !DiscoveryDecisions.Choose(item,new PetState { Bond=0 },fresh,0,new Random(1)).SharePlayer,"Player attention depends on real bond and sociability");
        check(DiscoveryDecisions.Choose(item,new PetState { Energy=10 },fresh,0,new Random(1)).Choice==DiscoveryChoice.Ignore,"Exhausted pets postpone discovery");
        check(DiscoveryDecisions.Appeal(item,pet,memory,2450)==0 && DiscoveryDecisions.Appeal(item,pet,memory,4000)<DiscoveryDecisions.Appeal(item,pet,fresh,4000),"Recent and familiar objects lose novelty");
        check(!DiscoveryDecisions.Choose(item,pet,memory,4000,new Random(17)).SharePlayer,"Familiar objects never trigger a new-discovery call");
        var learner=new PetState { Initiative=100 };var bounded=new DiscoveryMemory();
        for(int i=0;i<500;i++)bounded.Complete(item with { Id="object-"+i },learner,i*1200);
        check(bounded.Objects.Count==DiscoveryMemory.Capacity && bounded.Rewarded.Count==64,"Discovery storage and reward ledgers have fixed bounds");
        check(learner.Initiative==108 && bounded.InitiativeGained==8,"A lifetime experience cap preserves base personality");
        int before=learner.Initiative;
        for(int i=0;i<50;i++)bounded.Complete(item with { Id="object-499" },learner,1e6+i*1200);
        check(learner.Initiative==before && bounded.Affinity.Values.All(v=>v<=1),"Repeated study cannot grind initiative or unbounded preferences");
        var shared=new DiscoveryMemory();int relationshipPoints=0;
        for(int i=0;i<1000;i++)relationshipPoints+=shared.ShareSuccess("same-stone","peer");
        check(relationshipPoints==0 && shared.SharedRewards.Count==1,"The same shared object cannot farm friendship");
        var calmShared=new DiscoveryMemory();int slowPoints=0;
        for(int i=0;i<5;i++)slowPoints+=calmShared.ShareSuccess("place-"+i,"peer");
        check(slowPoints==1,"Five different shared discoveries grant one directed relationship point");
        for(int i=0;i<1000;i++)relationshipPoints+=shared.ShareSuccess("stone-"+i,"peer");
        check(relationshipPoints==6 && shared.SharedRewards.Count<=96,"Shared experience improves existing relationships slowly with a cap");
        bounded.Objects.Add(new() { Id="bad",NoticedAt=double.NaN });bounded.Validate();
        check(!bounded.Objects.Any(r=>r.Id=="bad") && bounded.Objects.Count<=48,"Validation removes invalid memory entries and preserves the size limit");
        var owner=new PetState();owner.Expedition.ExplorationSeconds=2500;owner.Expedition.Discoveries["main"]=memory;
        owner.Expedition.Discoveries["stable-peer-id"]=new();
        var dir=Path.Combine(Path.GetTempPath(),"MoriDiscoveryTests-"+Guid.NewGuid());
        try
        {
            var path=Path.Combine(dir,"pet.json");SaveStore.Save(path,owner);var restored=SaveStore.Load(path,out var warning);
            check(warning=="" && restored.Expedition.ExplorationSeconds==2500 && restored.Expedition.Memory("main").Find(item.Id)?.Visits==3 &&
                restored.Expedition.Memory("stable-peer-id").Knowledge(item.Id,2500)==DiscoveryKnowledge.Unknown,"Per-pet memory and its active clock survive save/load without cross-contamination");
        }
        finally { if(Directory.Exists(dir))Directory.Delete(dir,true); }
    }
}
