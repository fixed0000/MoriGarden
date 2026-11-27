using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using Mori;

internal static class LearnedEditorChecks
{
    internal static void Run(Action<bool,string> check)
    {
        string dir=Path.Combine(Path.GetTempPath(),"MoriLearnedEditor-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        try
        {
            var neutral=new PetState{PersonalitySchemaVersion=1,Personality=new()};neutral.EnsureStatisticsTree();string neutralJson=JsonSerializer.Serialize(neutral);var neutralSnapshot=JsonSerializer.SerializeToNode(ChaoLearnedEditor.Capture(neutral))!;
            check(neutralSnapshot["rows"]!.AsArray().Count==17 && neutralSnapshot["rows"]!.AsArray().All(r=>r!["effectiveBias"]!.GetValue<float>()==0) && JsonSerializer.Serialize(neutral)==neutralJson,"Learned editor initial 17 neutral values; reading creates no records");
            var main=new PetState{Name="Mori",PersonalitySchemaVersion=1,Personality=new()};main.GardenEggs.Clear();
            var white=new PetState{Name="White Chao",PersonalitySchemaVersion=1,Personality=new()};
            main.GardenEggs.Add(new(){Id="white",Hatched=true,Color="White",Chao=white});main.EnsureStatisticsTree();main.EnsurePersonalityTree();
            main.History.Seconds=white.History.Seconds=1000;
            main.LearnedPreferences.Credit(LearnedItemGroup.Toy,"rattle",900,"real-toy");main.ActivityHabits.Credit(ChaoActivity.Play,900,"real-play");
            var roster=new Dictionary<string,PetState>{{"main",main},{"white",white}};var bridge=new ChaoEditorBridge(dir);bridge.Poll(roster,true);int serial=0;
            JsonObject Snapshot()=>JsonNode.Parse(File.ReadAllText(Path.Combine(dir,"chao-editor-state.json")))!.AsObject();
            string All()=>JsonSerializer.Serialize(roster);
            PetState? Send(JsonObject request)
            {
                request["session"] ??= Snapshot()["session"]!.DeepClone();request["id"] ??="learned-"+(++serial);request["target"] ??="main";request["patch"] ??=new JsonObject();
                string id=request["target"]!.GetValue<string>();
                if(roster.ContainsKey(id)){request["learnedRevision"] ??=ChaoLearnedEditor.Revision(roster[id]);request["personalityRevision"] ??=ChaoPersonalityEditor.Revision(roster[id]);}
                File.WriteAllText(Path.Combine(dir,"chao-editor-request.json"),request.ToJsonString());var next=bridge.Poll(roster,true);
                if(next!=null && id!="main")roster["main"].GardenEggs.Single(e=>e.Id==id).Chao=next;return next;
            }
            JsonObject Edit(string group,string id,JsonNode? n)=>new(){["learnedAdjustments"]=new JsonObject{[group]=new JsonObject{[id]=n}}};
            void Reject(JsonObject patch,string label){string old=All();check(Send(patch)==null && All()==old,"Learned editor reject atomically: "+label);}
            var oldEntry=main.LearnedPreferences.ToyPreferences["rattle"];string innate=JsonSerializer.Serialize(main.Personality);
            string fixedBefore=JsonSerializer.Serialize(new{main.FavoriteToy,main.FavoriteFruit,main.FavoriteInstrument,main.LikesToyCars});
            check(Send(Edit("toys","rattle",.25m))!=null,"Learned editor A toy Bias accepted");
            var e=roster["main"].LearnedPreferences.ToyPreferences["rattle"];
            check(e.Bias==.25f && e.LastUpdateSeconds==1000 && e.ExperienceCount==oldEntry.ExperienceCount && e.LastCreditSeconds==900,"Learned editor B effective rebase preserves real history");
            Send(Edit("fruits",FruitKinds.Blue,-.30m));Send(Edit("habits","Play",.10m));
            check(roster["main"].LearnedPreferences.FruitPreferences[FruitKinds.Blue].Bias==-.3f && roster["main"].ActivityHabits.Entries["Play"].Bias==.1f,"Learned editor C/D fruit and habit bounds");
            check(innate==JsonSerializer.Serialize(roster["main"].Personality) && fixedBefore==JsonSerializer.Serialize(new{roster["main"].FavoriteToy,roster["main"].FavoriteFruit,roster["main"].FavoriteInstrument,roster["main"].LikesToyCars}) && roster["main"].ActivityHabits.Entries.Count==1,"Learned editor fixed favorites, all 13 traits and other habits preserved");
            foreach(string group in new[]{"toys","fruits","habits"})
            {
                string id=group=="toys" ? "rattle" : group=="fruits" ? FruitKinds.Blue : "Play";
                foreach(var value in new JsonNode?[]{JsonValue.Create(.301m),JsonValue.Create(-.301m),JsonValue.Create(.0001m),JsonValue.Create(".1"),JsonValue.Create(true),null,new JsonObject{["Bias"]=.1}})
                    Reject(Edit(group,id,value),"E/F strict numeric "+group+" "+value);
            }
            Reject(Edit("habits","Play",-.001m),"G negative habit");
            Reject(Edit("toys","rattle",JsonNode.Parse("1e-100")),"G no decimal underflow rounding");
            Reject(Edit("toys","rattle",JsonNode.Parse("0.10000000000000000000000000000000001")),"G no long decimal rounding");
            foreach(string key in new[]{"Eat","Sleep","Wander","Unknown","SchemaVersion","LastCreditSeconds","ExperienceCount"})Reject(Edit("habits",key,.02m),"H unknown category/metadata "+key);
            Reject(Edit("places","rattle",.1m),"I unknown group");Reject(Edit("toys","Toy Car",.1m),"I noncanonical ID");
            Reject(new(){["learnedAdjustments"]=null},"I null section");Reject(new(){["learnedAdjustments"]=new JsonObject{["toys"]=null}},"I null group");
            string before=All();Send(new(){["learnedAdjustments"]=new JsonObject()});check(All()==before,"Learned editor J empty patch no data change");
            Send(Edit("toys","drums",0));check(!roster["main"].LearnedPreferences.ToyPreferences.ContainsKey("drums"),"Learned editor K missing neutral stays absent");
            Send(Edit("toys","drums",.12m));var manual=roster["main"].LearnedPreferences.ToyPreferences["drums"];
            check(manual.ExperienceCount==0 && manual.LastCreditSeconds==0 && manual.LastUpdateSeconds==1000,"Learned editor L manual creation invents no experience");
            Send(Edit("toys","rattle",0));check(roster["main"].LearnedPreferences.ToyPreferences["rattle"].ExperienceCount==1,"Learned editor M neutral preserves existing history");
            var residentPatch=Edit("toys","flute",.2m);residentPatch["target"]="white";Send(residentPatch);
            check(roster["white"].LearnedPreferences.ToyPreferences["flute"].Bias==.2f && !roster["main"].LearnedPreferences.ToyPreferences.ContainsKey("flute"),"Learned editor N independent stable target");
            string rev=ChaoLearnedEditor.Revision(roster["main"]);roster["main"].History.Seconds+=12000;
            check(rev==ChaoLearnedEditor.Revision(roster["main"]) && Math.Abs(roster["main"].LearnedPreferences.EffectiveBias(LearnedItemGroup.Toy,"drums",13000)-.06f)<.00001,"Learned editor O clock decay does not stale revision");
            var timePatch=Edit("toys","drums",.2m);timePatch["learnedRevision"]=rev;Send(timePatch);
            check(roster["main"].LearnedPreferences.ToyPreferences["drums"].LastUpdateSeconds==13000 && roster["main"].LearnedPreferences.EffectiveBias(LearnedItemGroup.Toy,"drums",13000)==.2f,"Learned editor P server time, not snapshot time, rebases effective Bias");
            rev=ChaoLearnedEditor.Revision(roster["main"]);roster["main"].LearnedPreferences.Credit(LearnedItemGroup.Toy,"rattle",13000,"new-real");
            var stale=Edit("toys","drums",.1m);stale["learnedRevision"]=rev;stale["patch"]=new JsonObject{["Joy"]=99};Reject(stale,"Q new real experience invalidates entire stale patch");
            var future=roster["main"].LearnedPreferences.ToyPreferences["drums"];future.LastUpdateSeconds=14000;Reject(Edit("toys","drums",.1m),"R future timestamps");future.LastUpdateSeconds=13000;
            Reject(new(){["learnedAdjustments"]=new JsonObject{["toys"]=new JsonObject{["rattle"]=.1,["drums"]=.4}},["personality"]=new JsonObject{["Curious"]=60},["patch"]=new JsonObject{["Joy"]=90}},"S mixed late invalid bias rollback");
            var mix=Edit("fruits",FruitKinds.Garden,.1m);mix["learnedAdjustments"]!["habits"]=new JsonObject{["Leisure"]=.05};mix["personality"]=new JsonObject{["Curious"]=60};mix["patch"]=new JsonObject{["Joy"]=90};mix["statistics"]=new JsonObject{["Run"]=new JsonObject{["Progress"]=10}};mix["relationship"]=new JsonObject{["other"]="white",["score"]=25};
            check(Send(mix)!=null && roster["main"].Personality!.Curious==60 && roster["main"].Joy==90 && roster["main"].ActivityHabits.Entries["Leisure"].Bias==.05f && roster["main"].Stat("Run").Progress==10 && roster["main"].SocialLink("main","white")!.ScoreFrom("main")==25,"Learned editor T mixed ordinary/personality/item/habit/stats/relationship transaction");
            Reject(new(){["target"]="white",["learnedAdjustments"]=new JsonObject{["habits"]=new JsonObject{["Play"]=.2}},["relationship"]=new JsonObject{["other"]="main",["score"]=55}},"U invalid learned update cannot modify shared relationship");
            var target=Edit("toys","rattle",.1m);target["target"]="missing";Reject(target,"V missing target");var session=Edit("toys","rattle",.1m);session["session"]="old";Reject(session,"V old session");
            var duplicate=Edit("toys","rattle",.1m);duplicate["id"]="same";Send(duplicate);Send(Edit("toys","rattle",.2m));duplicate=Edit("toys","rattle",.3m);duplicate["id"]="same";Reject(duplicate,"V nonconsecutive replay");
            var p=roster["main"];p.History.Seconds=13010;Send(Edit("habits","Play",.04m));p=roster["main"];
            check(!p.LearnedPreferences.Credit(LearnedItemGroup.Toy,"rattle",13010,"new-real") && !p.LearnedPreferences.Credit(LearnedItemGroup.Toy,"rattle",13010,"too-soon"),"Learned editor W item replay ledger and credit cooldown survive rebind");
            check(!p.ActivityHabits.Credit(ChaoActivity.Play,13010,"real-play"),"Learned editor W habit replay ledger survives rebind");
            p.History.Seconds=13130;Send(Edit("toys","rattle",.15m));p=roster["main"];
            check(p.LearnedPreferences.Credit(LearnedItemGroup.Toy,"rattle",13130,"later-real") && p.ActivityHabits.Credit(ChaoActivity.Play,13130,"later-habit") && p.ActivityHabits.Entries["Play"].ExperienceCount==2,"Learned editor X real learning continues after manual adjustment");
            string file=Path.Combine(dir,"pet.json");SaveStore.Save(file,p);var loaded=SaveStore.Load(file,out _);
            check(ChaoLearnedEditor.Revision(p)==ChaoLearnedEditor.Revision(loaded) && loaded.GardenEggs.Single().Id=="white" && loaded.GardenEggs.Single().Chao!.LearnedPreferences.ToyPreferences["flute"].Bias==.2f,"Learned editor Y save/load all pets and history");
            var json=JsonSerializer.SerializeToNode(p)!.AsObject();json.Remove("LearnedPreferences");json.Remove("ActivityHabits");File.WriteAllText(file,json.ToJsonString());loaded=SaveStore.Load(file,out _);
            check(loaded.LearnedPreferences.ToyPreferences.Count==0 && loaded.ActivityHabits.Entries.Count==0,"Learned editor Z legacy save still neutral");
            foreach(string key in new[]{"LearnedPreferences","ActivityHabits"})
            {
                var futureJson=JsonSerializer.SerializeToNode(p)!.AsObject();futureJson[key]!["SchemaVersion"]=99;string futureFile=Path.Combine(dir,key+"-future.json");string original=futureJson.ToJsonString();File.WriteAllText(futureFile,original);bool rejected=false;
                try{SaveStore.Load(futureFile,out _);}catch(SaveLoadException){rejected=true;}
                check(rejected && File.ReadAllText(futureFile)==original,"Learned editor future schema preserves original file: "+key);
            }
            var captured=JsonSerializer.SerializeToNode(ChaoLearnedEditor.Capture(p))!;before=JsonSerializer.Serialize(p);ChaoLearnedEditor.Revision(p);ChaoLearnedEditor.Capture(p);
            check(captured["rows"]!.AsArray().Count==17 && JsonSerializer.Serialize(p)==before,"Learned editor snapshot read-only and bounded to 17 types");
            check(ChaoLearnedPreferences.HalfLife==12000 && ChaoActivityHabits.HalfLife==24000 && ChaoLearnedPreferences.CreditInterval==45 && ChaoActivityHabits.CreditInterval==120 && ChaoLearnedPreferences.ReplayCapacity==128 && ChaoActivityHabits.ReplayCapacity==128,"Learned editor leaves learning constants unchanged");
        }
        finally{Directory.Delete(dir,true);}
    }
}
