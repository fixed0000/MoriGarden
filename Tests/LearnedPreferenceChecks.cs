using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mori;

internal static class LearnedPreferenceChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var p=new PetState();var prefs=p.LearnedPreferences;
        check(prefs.ToyPreferences.Count==0 && prefs.FruitPreferences.Count==0 && LearnedPreferenceIds.Toys.All(id=>prefs.EffectiveBias(LearnedItemGroup.Toy,id,0)==0),"Learned A: empty independent model reads as neutral");
        check(prefs.Credit(LearnedItemGroup.Toy,"rattle",0,"first") && Math.Abs(prefs.EffectiveBias(LearnedItemGroup.Toy,"rattle",0)-.07f)<.000001f,"Learned B: real completed outcome learns .07 initially");
        string initial=JsonSerializer.Serialize(prefs);
        check(!prefs.Credit(LearnedItemGroup.Toy,"rattle",100,"first") && initial==JsonSerializer.Serialize(prefs),"Learned C: duplicate completion cannot alter persistent data");
        check(!prefs.Credit(LearnedItemGroup.Toy,"rattle",44,"too-soon") && prefs.Credit(LearnedItemGroup.Toy,"rattle",45,"next") && prefs.ToyPreferences["rattle"].ExperienceCount==2 && !prefs.Credit(LearnedItemGroup.Toy,"rattle",100,"too-soon"),"Learned D: 45s credit interval and suppressed-event replay protection");
        var saturated=new ChaoLearnedPreferences();for(int i=0;i<1500;i++)saturated.Credit(LearnedItemGroup.Toy,"flute",i*45,"complete-"+i);
        check(saturated.ToyPreferences["flute"].Bias is >.28f and <=.30f && saturated.ToyPreferences["flute"].ExperienceCount==1000 && saturated.ProcessedCompletionCount==128,"Learned E: learning saturates; counts and replay ledger are bounded");
        prefs=new();prefs.Credit(LearnedItemGroup.Fruit,FruitKinds.Garden,0,"food");initial=JsonSerializer.Serialize(prefs);
        check(Math.Abs(prefs.EffectiveBias(LearnedItemGroup.Fruit,FruitKinds.Garden,12000)-.035f)<.000001f && JsonSerializer.Serialize(prefs)==initial,"Learned F: one half-life halves effective bias without rewriting stored data");
        check(prefs.Credit(LearnedItemGroup.Fruit,FruitKinds.Garden,12000,"confirmed-bad",-1) && prefs.FruitPreferences[FruitKinds.Garden].Bias<0,"Learned G: prepared confirmed-negative handler reduces bias");
        var a=new PetState();var b=new PetState();var c=new PetState();a.EnsurePersonality();a.EnsureStatistics();var innate=a.Personality!.Copy();
        string favorites=JsonSerializer.Serialize(new{a.FavoriteToy,a.FavoriteFruit,a.FavoriteInstrument,a.LikesToyCars});
        for(int i=0;i<1000;i++){a.LearnedPreferences.Credit(LearnedItemGroup.Toy,"rattle",i*60,"a-"+i);b.LearnedPreferences.Credit(LearnedItemGroup.Toy,"flute",i*60,"b-"+i);}
        check(a.LearnedPreferences.ToyPreferences.Keys.Single()=="rattle" && b.LearnedPreferences.ToyPreferences.Keys.Single()=="flute" && c.LearnedPreferences.ToyPreferences.Count==0,"Learned H: three Chao retain independent experience");
        check(a.Personality.Equals(innate),"Learned I: 1000 outcomes do not alter thirteen innate values");
        check(favorites==JsonSerializer.Serialize(new{a.FavoriteToy,a.FavoriteFruit,a.FavoriteInstrument,a.LikesToyCars}),"Learned J: fixed favorites and car compatibility are unchanged");
        var trained=new ChaoLearnedPreferences{ToyPreferences=new(){["rattle"]=new(){Bias=.25f,ExperienceCount=5}}};
        check(50*trained.Multiplier(LearnedItemGroup.Toy,"rattle",0)>50*trained.Multiplier(LearnedItemGroup.Toy,"flute",0) && 50/(1+6*.19f)*trained.Multiplier(LearnedItemGroup.Toy,"rattle",0)<50/(1+.5f*.19f),"Learned K/L: bounded target multiplier helps equal targets but does not erase distance");
        var options=PersonalityDecisionChecks.All();var neutral=new PetState{Fullness=5};var learned=SaveStore.Snapshot(neutral);learned.LearnedPreferences=trained;
        check(new ChaoActivityCoordinator(new Random(1)).Evaluate(neutral,options).Select(s=>s.Score).SequenceEqual(new ChaoActivityCoordinator(new Random(1)).Evaluate(learned,options).Select(s=>s.Score)) && new ChaoActivityCoordinator(new Random(1)).Choose(learned,options)==ChaoActivity.Eat,"Learned M: learned target bias never changes category scores or starvation priority");
        check(!trained.Credit(LearnedItemGroup.Toy,"Toy Car",0,"alias") && !trained.Credit(LearnedItemGroup.Toy,"toy car",0,"alias2") && !trained.Credit(LearnedItemGroup.Fruit,"Imaginary Fruit",0,"fake") && trained.ToyPreferences.Count==1,"Learned IDs: aliases/unknown types cannot create records");
        string directory=Path.Combine(Path.GetTempPath(),"MoriLearned-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            string file=Path.Combine(directory,"pet.json");p=new PetState{FavoriteFruit=FruitKinds.Blue};p.GardenEggs.Clear();p.GardenEggs.Add(new(){Id="learned-peer",Hatched=true,Chao=b});p.History.Seconds=80;
            p.LearnedPreferences.Credit(LearnedItemGroup.Toy,"rattle",80,"saved-completion");SaveStore.Save(file,p);var loaded=SaveStore.Load(file,out _);string first=JsonSerializer.Serialize(loaded);SaveStore.Save(file,loaded);loaded=SaveStore.Load(file,out _);
            check(first==JsonSerializer.Serialize(loaded) && loaded.GardenEggs[0].Id=="learned-peer" && loaded.LearnedPreferences.ToyPreferences["rattle"].ExperienceCount==1,"Learned Q: save/load/load preserves roster, personality, data and bias exactly");
            check(!loaded.LearnedPreferences.Credit(LearnedItemGroup.Toy,"rattle",90,"new-session-completion") && !File.ReadAllText(file).Contains("saved-completion") && loaded.LearnedPreferences.ProcessedCompletionCount==1,"Learned replay: last credit persists, runtime completion IDs do not serialize");
            var node=JsonSerializer.SerializeToNode(loaded)!.AsObject();node.Remove("LearnedPreferences");File.WriteAllText(file,node.ToJsonString());loaded=SaveStore.Load(file,out _);
            check(loaded.LearnedPreferences.ToyPreferences.Count==0 && loaded.FavoriteFruit==FruitKinds.Blue,"Learned R: absent legacy field migrates to empty preferences without changing fixed taste");
            p.LearnedPreferences.Credit(LearnedItemGroup.Toy,"flute",140,"editor-source");var roster=ExpeditionRoster.Available(p).ToDictionary(x=>x.Id,x=>x.State);var bridge=new ChaoEditorBridge(directory);bridge.Poll(roster,true);
            var snapshot=JsonNode.Parse(File.ReadAllText(Path.Combine(directory,"chao-editor-state.json")))!;
            File.WriteAllText(Path.Combine(directory,"chao-editor-request.json"),JsonSerializer.Serialize(new{session=snapshot["session"]!.GetValue<string>(),id="edit-unrelated",target="main",patch=new{Fullness=70},statistics=new{Run=new{Grade=2,Level=1,Points=40,Progress=25}}}));
            var edited=bridge.Poll(roster,true)!;
            check(edited.Fullness==70 && edited.Stat("Run").Progress==25 && JsonSerializer.Serialize(edited.LearnedPreferences)==JsonSerializer.Serialize(p.LearnedPreferences) && !edited.LearnedPreferences.Credit(LearnedItemGroup.Toy,"flute",240,"editor-source"),"Learned S: editor unrelated edits preserve entries and runtime replay protection");
            check(snapshot["chao"]![0]!["learnedPreferences"]!["entries"]!.AsArray().Count==14,"Learned diagnostics: editor exposes all fourteen supported types read-only");
            File.WriteAllText(Path.Combine(directory,"chao-editor-request.json"),JsonSerializer.Serialize(new{session=snapshot["session"]!.GetValue<string>(),id="read-only",target="main",patch=new{LearnedPreferences=new{SchemaVersion=1}}}));
            check(bridge.Poll(roster,true)==null && roster["main"].LearnedPreferences.ToyPreferences["flute"].ExperienceCount==1,"Learned editor: preference replacement patches are rejected");
            File.WriteAllText(Path.Combine(directory,"chao-editor-request.json"),JsonSerializer.Serialize(new{session=snapshot["session"]!.GetValue<string>(),id="busy-snapshot",target="main",patch=new{Fullness=68}}));
            using(var reader=new FileStream(Path.Combine(directory,"chao-editor-state.json"),FileMode.Open,FileAccess.Read,FileShare.Read))
            {edited=bridge.Poll(roster,true);check(edited?.Fullness==68 && edited.LearnedPreferences.ToyPreferences["flute"].ExperienceCount==1,"Learned editor: a locked snapshot cannot discard an applied edit or its preferences");}
            bridge.Poll(roster,true);var published=JsonNode.Parse(File.ReadAllText(Path.Combine(directory,"chao-editor-state.json")))!;
            check(published["lastRequest"]!.GetValue<string>()=="busy-snapshot" && published["status"]!.GetValue<string>()=="Applied","Learned editor: snapshot publication retries after reader unlock without replaying the edit");
            string good=JsonSerializer.Serialize(p);var bad=JsonNode.Parse(good)!;bad["LearnedPreferences"]!["SchemaVersion"]=2;File.WriteAllText(file,bad.ToJsonString());File.WriteAllText(file+".bak",good);bool refused=false;
            try{SaveStore.Load(file,out _);}catch(SaveLoadException){refused=true;}
            check(refused && SaveStore.LoadSlot(file)==null,"Learned future schema: no silent downgrade to an older backup");
            foreach(var corrupt in new[]{good.Replace("\"Bias\":0.07","\"Bias\":0.9"),good.Replace("\"LastCreditSeconds\":80","\"LastCreditSeconds\":-1"),good.Replace("\"rattle\":","\"unknown-toy\":"),good.Replace("\"LearnedPreferences\":{","\"LearnedPreferences\":null,\"discarded\":{")})
            {
                File.WriteAllText(file,corrupt);File.WriteAllText(file+".bak","null");bool protectedSave=false;try{SaveStore.Save(file,p);}catch(SaveLoadException){protectedSave=true;}
                check(protectedSave && File.ReadAllText(file)==corrupt,"Learned corrupt data is rejected without overwriting the source");
            }
        }
        finally{Directory.Delete(directory,true);}
    }
}
