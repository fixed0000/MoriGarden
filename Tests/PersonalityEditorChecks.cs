using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mori;

internal static class PersonalityEditorChecks
{
    internal static void Run(Action<bool,string> check)
    {
        string dir=Path.Combine(Path.GetTempPath(),"MoriPersonalityEditor-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        try
        {
            var main=new PetState{Name="Mori",Personality=new(){Curious=-50,Naive=-40},PersonalitySchemaVersion=1};
            var white=new PetState{Name="White Chao",Personality=new(){Chatty=-80},PersonalitySchemaVersion=1};
            main.GardenEggs.Clear();
            main.GardenEggs.Add(new(){Id="white",Hatched=true,Chao=white,Color="White"});main.EnsureStatisticsTree();main.EnsurePersonalityTree();
            var roster=new Dictionary<string,PetState>{{"main",main},{"white",white}};var bridge=new ChaoEditorBridge(dir);bridge.Poll(roster,true);
            JsonObject Snapshot()=>JsonNode.Parse(File.ReadAllText(Path.Combine(dir,"chao-editor-state.json")))!.AsObject();
            int serial=0;
            PetState? Send(JsonObject request)
            {
                request["session"] ??= Snapshot()["session"]!.DeepClone();request["id"] ??="req-"+(++serial);request["target"] ??="main";request["patch"] ??=new JsonObject();
                string target=request["target"]!.GetValue<string>();
                if(!request.ContainsKey("personalityRevision") && roster.ContainsKey(target))request["personalityRevision"]=ChaoPersonalityEditor.Revision(roster[target]);
                File.WriteAllText(Path.Combine(dir,"chao-editor-request.json"),request.ToJsonString());var result=bridge.Poll(roster,true);
                if(result!=null && target!="main")roster["main"].GardenEggs.Single(e=>e.Id==target).Chao=result;
                return result;
            }
            string All()=>JsonSerializer.Serialize(roster);
            JsonObject P(string key,JsonNode? value)=>new(){["personality"]=new JsonObject{[key]=value}};
            var before=roster["main"].Personality!.Copy();Send(P("Curious",80));
            check(roster["main"].Personality!.Curious==80 && ChaoPersonalityEditor.Names.Where(n=>n!="Curious").All(n=>ChaoPersonalityEditor.Get(before,n)==ChaoPersonalityEditor.Get(roster["main"].Personality!,n)),"Personality editor A/K one canonical trait only, main ID");
            Send(new(){["personality"]=new JsonObject{["Chatty"]=65,["Energetic"]=-40,["Naive"]=-100}});
            check(roster["main"].Personality!.Chatty==65 && roster["main"].Personality!.Energetic==-40 && roster["main"].Personality!.Naive==-100,"Personality editor B/C multiple traits and negative Naive axis");
            Send(P("Curious",100));check(roster["main"].Personality!.Curious==100,"Personality editor D positive bound");
            foreach(var (label,value) in new (string,JsonNode?)[]{("low",JsonValue.Create(-101)),("high",JsonValue.Create(101)),("string",JsonValue.Create("high")),("null",null),("boolean",JsonValue.Create(true)),("fraction",JsonNode.Parse("1.0"))})
            {string old=All();check(Send(P("Energetic",value))==null && All()==old,"Personality editor E/F/G invalid value atomic: "+label);}
            foreach(string key in new[]{"UnknownTrait","PersonalitySchemaVersion","SavedCuriosity"}){string old=All();check(Send(P(key,50))==null && All()==old,"Personality editor H/I whitelist "+key);}
            string unchanged=All();Send(new(){["personality"]=new JsonObject()});check(All()==unchanged,"Personality editor J empty patch preserves all data");
            Send(new(){["target"]="white",["personality"]=new JsonObject{["Chatty"]=80}});check(roster["white"].Personality!.Chatty==80 && roster["main"].Personality!.Chatty==65,"Personality editor L/N independent resident edits");
            unchanged=All();check(Send(new(){["target"]="missing",["personality"]=new JsonObject{["Chatty"]=80}})==null && All()==unchanged,"Personality editor M/Z missing ID creates/removes no Chao");
            unchanged=All();check(Send(new(){["session"]="old-session",["personality"]=new JsonObject{["Chatty"]=1}})==null && All()==unchanged,"Personality editor O wrong session rejected");
            Send(new(){["id"]="repeat",["personality"]=new JsonObject{["Curious"]=10}});Send(P("Curious",20));unchanged=All();
            check(Send(new(){["id"]="repeat",["personality"]=new JsonObject{["Curious"]=30}})==null && All()==unchanged,"Personality editor P nonconsecutive duplicate request cannot replay");
            string revision=ChaoPersonalityEditor.Revision(roster["main"]);Send(P("Curious",21));unchanged=All();
            check(Send(new(){["personalityRevision"]=revision,["patch"]=new JsonObject{["Joy"]=90},["personality"]=new JsonObject{["Curious"]=50}})==null && All()==unchanged,"Personality editor Q stale revision rejects ordinary changes too");
            var cases=new[]{
                new JsonObject{["personality"]=new JsonObject{["Curious"]=20,["Chatty"]=101}},
                new JsonObject{["statistics"]=new JsonObject{["Run"]=new JsonObject{["Level"]=100}},["personality"]=new JsonObject{["Curious"]=20}},
                new JsonObject{["relationship"]=new JsonObject{["other"]="missing",["score"]=30},["personality"]=new JsonObject{["Curious"]=20}},
                new JsonObject{["target"]="white",["relationship"]=new JsonObject{["other"]="main",["score"]=30},["personality"]=new JsonObject{["Curious"]=150}},
                new JsonObject{["target"]="white",["relationship"]=new JsonObject{["other"]="main",["score"]=30},["patch"]=new JsonObject{["SocialNeed"]=101},["personality"]=new JsonObject{["Curious"]=20}},
                new JsonObject{["relationship"]=new JsonObject{["other"]="white",["score"]=101},["personality"]=new JsonObject{["Curious"]=20}},
                new JsonObject{["personality"]=new JsonArray(1,2)},
            };
            for(int i=0;i<cases.Length;i++){unchanged=All();var links=roster["main"].SocialLinks;check(Send(cases[i])==null && All()==unchanged && ReferenceEquals(links,roster["main"].SocialLinks),"Personality editor R/S/T/U late validation all-or-nothing "+i);}
            unchanged=All();Send(new(){["target"]="white",["relationship"]=new JsonObject{["other"]="main",["met"]=true,["score"]=32},["personality"]=new JsonObject{["Curious"]=45}});
            check(roster["white"].Personality!.Curious==45 && roster["main"].SocialLink("white","main")!.ScoreFrom("white")==32,"Personality editor successful resident personality/relationship transaction");
            foreach(string legacy in ChaoPersonalityEditor.Legacy){unchanged=All();JsonNode val=legacy is "Glutton" or "Crybaby" ? JsonValue.Create(true)! : JsonValue.Create(10)!;check(Send(new(){["patch"]=new JsonObject{[legacy]=val},["personality"]=new JsonObject{["Curious"]=22}})==null && All()==unchanged,"Personality editor Y rejects mixed legacy adapter "+legacy);}
            foreach(string legacy in new[]{"SavedCuriosity","SavedKindness","SavedGlutton","SavedCrybaby","PersonalitySchemaVersion","Personality"}){unchanged=All();check(Send(new(){["patch"]=new JsonObject{[legacy]=1}})==null && All()==unchanged,"Personality editor ordinary patch cannot inject "+legacy);}
            var oldPet=roster["main"];var activity=new ChaoActivityCoordinator(new Random(7));oldPet.GardenActivity=activity;var signals=oldPet.VocalSignals;var oldPersonality=oldPet.Personality!.Copy();
            var tree=JsonSerializer.SerializeToNode(oldPet)!.AsObject();Send(P("Curious",100));var newTree=JsonSerializer.SerializeToNode(roster["main"])!.AsObject();tree.Remove("Personality");newTree.Remove("Personality");
            check(JsonNode.DeepEquals(tree,newTree) && ReferenceEquals(roster["main"].GardenActivity,activity) && ReferenceEquals(roster["main"].VocalSignals,signals),"Personality editor V runtime state, needs, experience and unrelated save data preserved");
            string file=Path.Combine(dir,"pet.json");SaveStore.Save(file,roster["main"]);var loaded=SaveStore.Load(file,out _);
            check(loaded.Personality!.Equals(roster["main"].Personality) && loaded.GardenEggs.Single().Chao!.Personality!.Equals(roster["white"].Personality),"Personality editor W exact main/resident save/load");
            check(ChaoPersonalityEditor.Revision(loaded)==ChaoPersonalityEditor.Revision(roster["main"]),"Personality editor deterministic revision survives round-trip");
            bool missing=false;try{ChaoPersonalityEditor.Revision(new());}catch(ArgumentException){missing=true;}check(missing,"Personality editor X missing model never invented");
            check(roster.Count==2 && roster["main"].GardenEggs.Count==1,"Personality editor Z roster stable through valid/invalid requests");
        }
        finally{Directory.Delete(dir,true);}
    }
}
