using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mori;

internal static class PersonalityModelChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var scales=typeof(ChaoPersonality).GetProperties();
        check(scales.Length==13 && scales.All(p=>p.PropertyType==typeof(int)),"Personality contains exactly thirteen integer scales and no needs");
        var personality=new ChaoPersonality();
        for(int i=0;i<scales.Length;i++)scales[i].SetValue(personality,-96+i*16);
        personality.Validate();
        check(personality.Equals(JsonSerializer.Deserialize<ChaoPersonality>(JsonSerializer.Serialize(personality))),"All thirteen distinct signed values survive JSON exactly");
        var clone=personality.Copy();clone.Fickle++;
        check(!clone.Equals(personality) && clone.Fickle!=personality.Fickle,"Personality copies are independent and compare by all scales");
        foreach(var scale in scales)
        {
            foreach(int value in new[]{-100,100}){var p=new ChaoPersonality();scale.SetValue(p,value);p.Validate();}
            bool rejected=true;
            foreach(int value in new[]{-101,101}){var p=new ChaoPersonality();scale.SetValue(p,value);try{p.Validate();rejected=false;}catch(ArgumentException){}}
            check(rejected,"Range validation rejects both invalid ends: "+scale.Name);
        }
        var legacy=JsonSerializer.Deserialize<PetState>("{\"Name\":\"Old\",\"Curiosity\":-70,\"Kindness\":-45,\"Glutton\":true,\"Crybaby\":true,\"Sociability\":82,\"Initiative\":777,\"Aggressiveness\":-18,\"ActivityPreference\":5,\"Clumsy\":true,\"Dexterous\":true}")!;
        check(legacy.PersonalitySchemaVersion==1 && legacy.Personality!.Curious==-70 && legacy.Personality.Kindness==-45 && legacy.Personality.BigEater==75 && legacy.Personality.Energetic==-75,"Legacy signed traits and booleans migrate deterministically with correct poles");
        check(legacy.Personality!.Naive==0 && legacy.Personality.Solitude==0 && legacy.Personality.Chatty==0 && legacy.Personality.Charm==0 && legacy.Personality.Skillful==0 && legacy.Sociability==82 && legacy.Initiative==777 && legacy.Aggressiveness==-18 && legacy.ActivityPreference==5 && legacy.Clumsy && legacy.Dexterous,"Unrelated legacy traits are retained, never guessed into different SA2B scales");
        var zero=JsonSerializer.Deserialize<PetState>("{\"Name\":\"Old\",\"Personality\":null}")!;
        check(zero.Personality!.Equals(new ChaoPersonality()),"Absent or null version-zero personality migrates to neutral without randomness");
        string canonical=JsonSerializer.Serialize(personality);
        foreach(string json in new[]{"{\"Personality\":"+canonical+",\"PersonalitySchemaVersion\":1,\"Curiosity\":100,\"Kindness\":100,\"Glutton\":true,\"Crybaby\":true}","{\"Curiosity\":100,\"Kindness\":100,\"Glutton\":true,\"Crybaby\":true,\"PersonalitySchemaVersion\":1,\"Personality\":"+canonical+"}"})
            check(JsonSerializer.Deserialize<PetState>(json)!.Personality!.Equals(personality),"Canonical personality wins over stale JSON aliases regardless of property order");
        legacy.Curiosity=19;legacy.Kindness=-23;legacy.Glutton=false;legacy.Crybaby=false;
        check(legacy.Personality!.Curious==19 && legacy.Personality.Kindness==-23 && legacy.Personality.BigEater==0 && legacy.Personality.Energetic==0,"Legacy code adapters write directly into canonical storage");
        legacy.Personality.BigEater=92;legacy.Personality.Energetic=-91;legacy.Glutton=true;legacy.Crybaby=true;
        check(legacy.Personality.BigEater==92 && legacy.Personality.Energetic==-91,"Unchanged boolean editor values do not quantize full personality scales");
        legacy.EnsurePersonality();var stable=legacy.Personality.Copy();
        for(int i=0;i<50;i++){legacy.EnsurePersonality();legacy.Tick(1,false);legacy.Pet();legacy.FeedBite(true);legacy.History.Grow(legacy,"event-"+i,"Kindness",.22f);legacy.History.Grow(legacy,"study-"+i,"Curiosity",.22f);}
        check(stable.Equals(legacy.Personality),"Tick, feeding, petting and experience do not rewrite innate personality");
        check(ChaoPersonality.CreateInitial(new Random(123)).Equals(ChaoPersonality.CreateInitial(new Random(123))),"New personality generation is reproducible with a fixed seed");
        var random=new Random(427);var samples=Enumerable.Range(0,1000).Select(_=>ChaoPersonality.CreateInitial(random)).ToArray();
        check(samples.All(p=>scales.All(s=>Math.Abs((int)s.GetValue(p)!)<=80)) && samples.Count(p=>Math.Abs(p.Curious)>70)<50 && samples.Select(p=>p.Curious).Distinct().Count()>100,"Documented initial distribution avoids extreme personalities for most newborns");
        var newborn=PetState.NewEgg();check(newborn.PersonalitySchemaVersion==1 && newborn.Personality!=null,"New game eggs receive personality once at creation");
        bool reroll=false;try{newborn.InitializeFreshPersonality();}catch(ArgumentException){reroll=true;}
        check(reroll,"Existing personality cannot accidentally be rerolled as a newborn");
        string directory=Path.Combine(Path.GetTempPath(),"MoriPersonality-"+Guid.NewGuid());Directory.CreateDirectory(directory);
        string file=Path.Combine(directory,"pet.json");
        try
        {
            var owner=new PetState{Name="Mori",Form="swift",AgeSeconds=4321,Curiosity=-33,Kindness=22};owner.GardenEggs.Clear();
            owner.Personality=personality.Copy();owner.PersonalitySchemaVersion=1;owner.History.Record(ChaoMemoryKind.Pleasant,"memory","garden","Kept memory",.7f,.6f);
            for(int i=0;i<2;i++)owner.GardenEggs.Add(new(){Id="friend-"+i,Color=i==0?"Black":"Yellow",Hatched=true,Chao=new(){Name="Friend "+i,Curiosity=30+i*25,Kindness=-30-i*25}});
            owner.SocialLink("main","friend-0",true)!.Met=true;owner.SocialLink("main","friend-0")!.Score=18;
            SaveStore.Save(file,owner);var before=JsonSerializer.SerializeToNode(owner);
            for(int i=0;i<3;i++){owner=SaveStore.Load(file,out _);SaveStore.Save(file,owner);}
            check(JsonNode.DeepEquals(before,JsonSerializer.SerializeToNode(owner)) && owner.GardenEggs.Select(e=>e.Id).SequenceEqual(new[]{"friend-0","friend-1"}) && owner.GardenEggs[0].Chao!.Curiosity!=owner.GardenEggs[1].Chao!.Curiosity,"Three distinct identities, personality, development, memories and relationships survive repeated migration/save/load");
            var node=JsonNode.Parse(File.ReadAllText(file))!.AsObject();
            check(!node.ContainsKey("Curiosity") && !node.ContainsKey("Kindness") && !node.ContainsKey("Glutton") && !node.ContainsKey("Crybaby") && node["Personality"]!.AsObject().Count==13,"New JSON writes one personality, without stale overlapping aliases");
            var roster=ExpeditionRoster.Available(owner).ToDictionary(e=>e.Id,e=>e.State);var bridge=new ChaoEditorBridge(directory);bridge.Poll(roster,true);
            string state=Path.Combine(directory,"chao-editor-state.json");var snapshot=JsonNode.Parse(File.ReadAllText(state))!;
            check(snapshot["chao"]!.AsArray().Count==3 && snapshot["chao"]![0]!["personalitySchemaVersion"]!.GetValue<int>()==1 && snapshot["chao"]![0]!["personality"]!.AsObject().Count==13,"Editor snapshot exposes one versioned personality per real Chao");
            PetState Edit(string id,object patch,object? statistics=null,object? relationship=null)
            {
                File.WriteAllText(Path.Combine(directory,"chao-editor-request.json"),JsonSerializer.Serialize(new{session=snapshot["session"]!.GetValue<string>(),id,target="main",patch,statistics,relationship}));
                return bridge.Poll(roster,true) ?? throw new Exception("Editor rejected test request: "+File.ReadAllText(state));
            }
            var edited=Edit("unrelated",new{Joy=81});
            check(edited.Personality!.Equals(personality) && edited.Joy==81,"Editor unrelated-field patch retains all thirteen values exactly");
            edited=Edit("adapters",new{Curiosity=-41,Kindness=-52,Glutton=true,Crybaby=true});
            check(edited.Personality!.Curious==-41 && edited.Personality.Kindness==-52 && edited.Glutton && edited.Crybaby,"Editor compatibility patches update canonical personality, not discarded aliases");
            var expected=edited.Personality.Copy();
            edited=Edit("sports-and-links",new{Joy=82},new{Run=new{Grade=2,Level=1,Points=22,Progress=0}},new{other="friend-0",met=true,score=27});
            SaveStore.Save(file,edited);var loaded=SaveStore.Load(file,out _);
            check(loaded.Personality!.Equals(expected) && loaded.Stat("Run").Points==22 && loaded.SocialLink("main","friend-0")!.ScoreFrom("main")==27 && loaded.GardenEggs.Count==2,"Editor sports and relationship patches survive save/load without changing personality or roster");
            edited.RecentAnimalSkill="Run";
            edited=Edit("clear-optional",new{RecentAnimalSkill=(string?)null});
            check(edited.RecentAnimalSkill==null && edited.Personality!.Equals(expected),"Editor can still clear optional animal fields without replacing personality");
            File.WriteAllText(Path.Combine(directory,"chao-editor-request.json"),JsonSerializer.Serialize(new{session=snapshot["session"]!.GetValue<string>(),id="bad-canonical",target="main",patch=new{Curiosity=101}}));
            check(bridge.Poll(roster,true)==null && ReferenceEquals(roster["main"],edited) && edited.Personality!.Equals(expected),"Invalid editor personality update is atomic and preserves the actual pet");
            string good=File.ReadAllText(file);
            foreach(string bad in new[]{"{\"Name\":\"Mori\",\"PersonalitySchemaVersion\":1}","{\"Name\":\"Mori\",\"PersonalitySchemaVersion\":1,\"Personality\":null}","{\"Name\":\"Mori\",\"Personality\":{}}",good.Replace("\"Curious\": -41","\"Curious\": 101"),"{\"Name\":\"Mori\",\"GardenEggs\":[{\"Id\":\"friend\",\"Hatched\":true,\"Chao\":{\"Name\":\"Friend\",\"PersonalitySchemaVersion\":1}}]}"})
            {
                File.WriteAllText(file,bad);if(File.Exists(file+".bak"))File.Delete(file+".bak");
                bool failed=false;try{SaveStore.Load(file,out _);}catch(SaveLoadException){failed=true;}
                bool blocked=false;try{SaveStore.Save(file,new PetState());}catch(SaveLoadException){blocked=true;}
                check(failed && blocked && File.ReadAllText(file)==bad && !File.Exists(file+".tmp"),"Invalid/missing personality fails without deleting a resident or overwriting original");
            }
            File.WriteAllText(file,"{\"Name\":\"Future\",\"PersonalitySchemaVersion\":2}");File.WriteAllText(file+".bak",good);
            bool future=false;try{SaveStore.Load(file,out _);}catch(SaveLoadException){future=true;}
            check(future && SaveStore.LoadSlot(file)==null && File.ReadAllText(file).Contains("Future"),"Unsupported future personality cannot silently fall back to an older backup");
        }
        finally { foreach(string path in Directory.GetFiles(directory))File.Delete(path);Directory.Delete(directory); }
    }
}
