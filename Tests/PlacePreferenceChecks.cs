using System;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mori;

internal static class PlacePreferenceChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var catalog=LeisureDestinationCatalog.All;var ids=catalog.Select(d=>d.Id).ToArray();var memory=new ChaoMemory();
        check(catalog.Count==8 && ids.Distinct().Count()==8 && ids.SequenceEqual(new[]{"garden/leisure/route-0","garden/leisure/route-1","garden/leisure/route-2","garden/leisure/route-3","garden/leisure/sky-0","garden/leisure/sky-1","garden/leisure/sky-2","garden/leisure/sky-3"}),"Place K: eight explicit stable IDs and ordering");
        WorldPoint[][] expected={new[]{new WorldPoint(12,0,12),new(29,0,12),new(46,0,12),new(64,0,12)},new[]{new WorldPoint(-15,0,14),new(-25,0,14),new(-35,0,14),new(-45,0,14)},new[]{new WorldPoint(36,0,5),new(36,0,17),new(36,0,29),new(36,0,41)},new[]{new WorldPoint(-10,0,25),new(-22,0,30),new(-34,0,35),new(-46,0,40)},new[]{new WorldPoint(0,0,12)},new[]{new WorldPoint(11,0,10)},new[]{new WorldPoint(-2,0,4)},new[]{new WorldPoint(14,0,-1)}};
        check(catalog.Select((d,i)=>d.Points.SequenceEqual(expected[i])).All(x=>x),"Place L: all existing route points and sky anchors are unchanged");
        check(ids.All(id=>ChaoLeisurePreferenceScorer.Evaluate(memory,"garden",id).TotalBias==0),"Place A: neutral Chao has zero bias at all eight places");
        ChaoLeisurePreferenceScorer.RecordSuccess(memory,"garden",ids[0]);
        var score=ChaoLeisurePreferenceScorer.Evaluate(memory,"garden",ids[0]);
        check(Math.Abs(score.LearnedBias-.05f)<.000001f && memory.Events.Single().Place==catalog[0].End,"Place B: first recorded completion learns its exact destination and endpoint");
        check(!ChaoLeisurePreferenceScorer.RecordSuccess(memory,"garden",ids[0]) && memory.Events.Single().Occurrences==1,"Place E: existing memory cooldown refuses duplicate immediate credit");
        for(int i=0;i<4;i++){memory.Advance(120);ChaoLeisurePreferenceScorer.RecordSuccess(memory,"garden",ids[0]);}
        check(memory.Events.Count==1 && memory.Events.Single().Occurrences==5 && ChaoLeisurePreferenceScorer.Evaluate(memory,"garden",ids[0]).LearnedBias==.25f,"Place C: five recorded visits coalesce and saturate at .25");
        string unchanged=JsonSerializer.Serialize(memory);memory.Advance(11700);string before=JsonSerializer.Serialize(memory);
        check(Math.Abs(ChaoLeisurePreferenceScorer.Evaluate(memory,"garden",ids[0]).LearnedBias-.125f)<.00001f && before==JsonSerializer.Serialize(memory),"Place D: inherited memory half-life decays the learned view without writing data");
        check(ChaoLeisurePreferenceScorer.Evaluate(memory,"coastal-cove",ids[0]).TotalBias==0 && !ChaoLeisurePreferenceScorer.RecordSuccess(memory,"coastal-cove",ids[0]),"Place J: same coordinates in another area cannot inherit garden learning");
        memory=JsonSerializer.Deserialize<ChaoMemory>(unchanged)!;
        check(ChaoLeisurePreferenceScorer.Evaluate(memory,"garden",ids[0]).SpatialBias==0,"Place R: new leisure memories never count again as generic spatial bias");
        memory.Record(ChaoMemoryKind.Pleasant,"old-quiet-rest","garden","A quiet rest.",.6f,.5f,place:catalog[0].Points[0]);
        check(Math.Abs(ChaoLeisurePreferenceScorer.Evaluate(memory,"garden",ids[0]).SpatialBias-.054f)<.000001f,"Existing spatial evidence near a route's first waypoint still contributes once");
        memory.Record(ChaoMemoryKind.Unpleasant,"bad-spot","garden","An unpleasant moment.",-1,1,place:catalog[1].End);
        check(ChaoLeisurePreferenceScorer.Evaluate(memory,"garden",ids[1]).SpatialBias==-.15f,"Spatial influence has its separate negative bound");
        var recent=ChaoLeisurePreferenceScorer.Candidates(memory,"garden",ids,ids[0]);
        check(Math.Abs(recent[0].SelectionWeight-ChaoLeisurePreferenceScorer.Evaluate(memory,"garden",ids[0]).SelectionWeight*.85f)<.000001f &&
            ChaoLeisurePreferenceScorer.Candidates(memory,"garden",ids,ids[1])[0].SelectionWeight==ChaoLeisurePreferenceScorer.Evaluate(memory,"garden",ids[0]).SelectionWeight,"Place P: only the recent successful destination gets the temporary penalty");
        check(ChaoLeisurePreferenceScorer.Candidates(memory,"garden",new[]{ids[0]},ids[0])[0].SelectionWeight==ChaoLeisurePreferenceScorer.Evaluate(memory,"garden",ids[0]).SelectionWeight,"Single candidate disables the recent-place penalty");
        var absent=ChaoLeisurePreferenceScorer.Candidates(memory,"garden",new[]{ids[1],ids[2],ids[3]});
        check(Enumerable.Range(0,1000).All(i=>ChaoLeisurePreferenceScorer.Choose(absent,new Random(i))?.Id!=ids[0]),"Place Q: unavailable favorite cannot enter weighted selection");
        var memories=new[]{new ChaoMemory(),new ChaoMemory(),new ChaoMemory()};
        for(int n=0;n<5;n++)for(int i=0;i<2;i++){memories[i].Advance(120);ChaoLeisurePreferenceScorer.RecordSuccess(memories[i],"garden",ids[i]);}
        int[][] counts=new int[3][];const int trials=50000;
        for(int p=0;p<3;p++)
        {
            counts[p]=new int[4];var choices=ChaoLeisurePreferenceScorer.Candidates(memories[p],"garden",ids.Take(4));
            for(int seed=0;seed<trials;seed++){var selected=ChaoLeisurePreferenceScorer.WeightedChoice(choices,new Random(seed+9100))!;counts[p][LeisureDestinationCatalog.Find(selected.Id)!.Index]++;}
            Console.WriteLine($"PLACE_DISTRIBUTION: pet={p} trials={trials} routes="+string.Join(',',counts[p]));
        }
        check(counts[2].All(n=>Math.Abs(n/(double)trials-.25)<.006),"Place M: neutral route distribution within .006 of uniform over 50000 independent seeds");
        check(Math.Abs(counts[0][0]/(double)trials-1.25/4.25)<.006 && Math.Abs(counts[1][1]/(double)trials-1.25/4.25)<.006,"Place N/S: matched seeds reveal independent learned preferences near the expected 29.4 percent");
        check(counts.All(c=>c.All(n=>n>1000)),"Place O: every available route remains possible");
        var all=ChaoLeisurePreferenceScorer.Candidates(memories[0],"garden",ids);var random=new Random(819);int sky=0;
        for(int i=0;i<trials;i++)if(ChaoLeisurePreferenceScorer.Choose(all,random)!.Type==LeisureDestinationType.Sky)sky++;
        check(Math.Abs(sky/(double)trials-.32)<.006,"Route/sky choice preserves the 32 percent sky balance");
        check(ChaoLeisurePreferenceScorer.Choose(ChaoLeisurePreferenceScorer.Candidates(memory,"garden",Array.Empty<string>()),random)==null,"No available destinations returns failure");
        var pet=new PetState();pet.EnsurePersonality();pet.EnsureStatistics();var innate=pet.Personality!.Copy();pet.LearnedPreferences.Credit(LearnedItemGroup.Toy,"rattle",0,"old-toy");var items=JsonSerializer.Serialize(pet.LearnedPreferences);var neutral=SaveStore.Snapshot(pet);pet.History=memories[0];
        for(int i=0;i<1000;i++)ChaoLeisurePreferenceScorer.Candidates(pet.History,"garden",ids);
        check(pet.Personality.Equals(innate) && items==JsonSerializer.Serialize(pet.LearnedPreferences),"Place T/W: place evaluation leaves innate and learned item data unchanged");
        check(new ChaoActivityCoordinator(new Random(1)).Evaluate(pet,PersonalityDecisionChecks.All()).Select(s=>s.Score).SequenceEqual(new ChaoActivityCoordinator(new Random(1)).Evaluate(neutral,PersonalityDecisionChecks.All()).Select(s=>s.Score)),"Place U: acquired place memory does not alter category scores");
        string folder=Path.Combine(Path.GetTempPath(),"MoriPlace-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try
        {
            pet.GardenEggs.Clear();pet.GardenEggs.Add(new(){Id="original-peer",Hatched=true,Chao=new PetState()});string file=Path.Combine(folder,"pet.json");SaveStore.Save(file,pet);var loaded=SaveStore.Load(file,out _);
            check(loaded.GardenEggs.Single().Id=="original-peer" && loaded.History.Events.Any(e=>e.Source==ids[0]) && loaded.Personality!.Equals(innate) && JsonSerializer.Serialize(loaded.LearnedPreferences)==items,"Place V: existing saves persist exact Chao IDs, memories and item preferences");
            var bridge=new ChaoEditorBridge(folder);var roster=ExpeditionRoster.Available(loaded).ToDictionary(c=>c.Id,c=>c.State);bridge.Poll(roster,true);var snap=JsonNode.Parse(File.ReadAllText(Path.Combine(folder,"chao-editor-state.json")))!;string events=JsonSerializer.Serialize(loaded.History);
            File.WriteAllText(Path.Combine(folder,"chao-editor-request.json"),JsonSerializer.Serialize(new{session=snap["session"]!.GetValue<string>(),id="edit",target="main",patch=new{Fullness=70}}));var edited=bridge.Poll(roster,true)!;
            check(JsonSerializer.Serialize(edited.History)==events && JsonSerializer.Serialize(edited.LearnedPreferences)==items,"Place X: unrelated editor edit retains place memories and learned items");
        }
        finally{Directory.Delete(folder,true);}
    }
}
