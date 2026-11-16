using System;
using System.IO;
using System.Linq;
using Mori;

internal static class GreenhouseChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var pet=new PetState();var state=GreenhouseRules.State(pet);
        check(WorldCatalog.Default.Areas[WorldCatalog.Greenhouse].Kind==WorldAreaKind.ExpeditionSite &&
            WorldCatalog.Default.Areas[WorldCatalog.Greenhouse].Region==WorldCatalog.Meadow,"Greenhouse is a site inside the existing meadow");
        check(WorldCatalog.Default.Routes.Any(r=>r.From==WorldCatalog.Meadow && r.To==WorldCatalog.Greenhouse) &&
            WorldCatalog.Default.Routes.Any(r=>r.From==WorldCatalog.Greenhouse && r.To==WorldCatalog.Meadow),"Greenhouse has a bidirectional graph route");
        check(!GreenhouseRules.Advance(pet,true,30) && state.Restoration==0,"Plate alone cannot solve irrigation");
        state.Element(GreenhouseRules.Intake).Value=1;
        check(!GreenhouseRules.Advance(pet,true,30) && state.Restoration==0,"Water supply alone cannot restore the bed");
        state.Element(GreenhouseRules.Diverter).Value=1;
        check(!GreenhouseRules.WaterRunning(state,true),"Side-bed routing does not water the central bed");
        state.Element(GreenhouseRules.Diverter).Value=2;
        check(!GreenhouseRules.Advance(pet,false,30) && state.Restoration==0,"Closed filter blocks the correctly routed water");
        check(!GreenhouseRules.Advance(pet,true,9) && Math.Abs(state.Restoration-.5f)<.001f,"Correctly linked mechanisms gradually restore plants");
        GreenhouseRules.Advance(pet,false,30);
        check(Math.Abs(state.Restoration-.5f)<.001f,"Interrupting flow preserves progress without corrupting the puzzle");
        check(!GreenhouseRules.ClaimReward(pet),"Unfinished sites cannot grant seeds");
        var temp=Path.Combine(Path.GetTempPath(),"MoriGreenhouseTests-"+Guid.NewGuid());
        try
        {
            var file=Path.Combine(temp,"pet.json");pet.ManualLocation=new(4,0,0,0,17,1,0,3,AreaId:WorldCatalog.Greenhouse);
            SaveStore.Save(file,pet);var loaded=SaveStore.Load(file,out var warning);var partial=GreenhouseRules.State(loaded);
            check(warning=="" && partial.Element(GreenhouseRules.Intake).Value==1 && partial.Element(GreenhouseRules.Diverter).Value==2 &&
                Math.Abs(partial.Restoration-.5f)<.001f && loaded.ManualLocation?.AreaId==WorldCatalog.Greenhouse,"Partial mission, linked mechanisms and site location survive save/load");
            check(GreenhouseRules.Advance(loaded,true,9) && partial.Completed && loaded.Expedition.AreaFlags[WorldCatalog.Meadow].Contains("greenhouse-restored"),"Restoration completes once and records a future unlock fact");
            check(GreenhouseRules.ClaimReward(loaded) && loaded.SeedCount(FruitKinds.Moonblossom)==1,"Completion grants one rare seed through the existing inventory");
            for(int i=0;i<1000;i++){GreenhouseRules.Advance(loaded,true,30);GreenhouseRules.ClaimReward(loaded);}
            check(loaded.SeedCount(FruitKinds.Moonblossom)==1 && !partial.ResetUnfinished(),"Repeat visits and resets cannot farm the first-clear reward");
            SaveStore.Save(file,loaded);var revisit=SaveStore.Load(file,out _);
            check(GreenhouseRules.State(revisit).Completed && GreenhouseRules.State(revisit).RewardClaimed &&
                GreenhouseRules.WaterRunning(GreenhouseRules.State(revisit),false) && !GreenhouseRules.ClaimReward(revisit),"Completed greenery and irrigation remain restored after loading without requiring plate occupancy");
        }
        finally {if(Directory.Exists(temp))Directory.Delete(temp,true);}
        var full=new PetState();GreenhouseRules.State(full).Completed=true;full.SeedBag[FruitKinds.Moonblossom]=12;
        check(!GreenhouseRules.ClaimReward(full) && !GreenhouseRules.State(full).RewardClaimed,"A full bag keeps the rare reward pending");
        full.SeedBag[FruitKinds.Moonblossom]=11;
        check(GreenhouseRules.ClaimReward(full) && full.SeedCount(FruitKinds.Moonblossom)==12,"Pending reward can be collected once space becomes available");
        check(FruitKinds.CanGrow(FruitKinds.Moonblossom) && !FruitKinds.ShopSeeds.Contains(FruitKinds.Moonblossom),"The rare seed is plantable but absent from the ordinary shop");
        full.AddFruit(FruitKinds.Moonblossom,1);full.Validate();
        check(full.FruitCount(FruitKinds.Moonblossom)==1 && FruitKinds.Special.Length==4,"Rare harvests keep their identity in the existing bag without changing the four birth favorites");
        var beginner=new PetState { Curiosity=-50,Initiative=0,Intelligence=0,Energy=90 };
        var expert=new PetState { Curiosity=90,Initiative=900,Intelligence=950,Energy=90 };
        check(PuzzleReasoning.ActionChance(beginner)>.05f && PuzzleReasoning.ActionChance(expert)>PuzzleReasoning.ActionChance(beginner)*3,"Even inexperienced Chao can try; intelligence and initiative strongly influence independent actions");
        check(PuzzleReasoning.ThoughtSeconds(expert)<PuzzleReasoning.ThoughtSeconds(beginner) && PuzzleReasoning.ThoughtSeconds(expert)>=4,"Intelligent Chao understand mechanisms faster without skipping thought");
        var memory=new DiscoveryMemory();var mechanism=new DiscoveryDefinition("greenhouse/switch",DiscoveryKind.Unknown,new(0,0,0),10,1,DiscoveryReaction.Inspect);
        memory.Complete(mechanism,beginner,0);
        var changed=mechanism with { Revision=1 };
        check(memory.CanConsider(changed,1),"A changed mechanism can be noticed without changing its stable object ID");
        memory.Notice(changed.Id,1,changed.Revision);
        check(!memory.CanConsider(changed,2),"Noticing a changed mechanism cannot retrigger every frame");
        for(int i=1;i<100;i++)memory.Complete(mechanism with { Revision=i },beginner,i+1);
        check(beginner.Initiative==0 && memory.Rewarded.Count==1,"Cycling mechanisms cannot farm personality experience");
        state.Element(GreenhouseRules.Crate).Positioned=true;state.Element(GreenhouseRules.Crate).X=float.NaN;state.Validate();
        check(!state.Element(GreenhouseRules.Crate).Positioned && state.ResetUnfinished() && state.Elements.Count==0,"Invalid movable positions restore safely and unfinished puzzles can reset");
    }
}
