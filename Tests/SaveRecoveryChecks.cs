using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mori;

internal static class SaveRecoveryChecks
{
    private static string[] Ids(PetState p)=>new[]{"main"}.Concat(p.GardenEggs.Select(e=>e.Id)).ToArray();
    internal static void Run(Action<bool,string> check)
    {
        string directory=Path.Combine(Path.GetTempPath(),"MoriRecoveryTests-"+Guid.NewGuid());Directory.CreateDirectory(directory);
        string file=Path.Combine(directory,"pet.json");
        try
        {
            string legacy="{\"Name\":\"Mori\",\"Form\":\"swift\",\"AgeSeconds\":1234,\"Run\":120,\"GardenEggs\":[{\"Id\":\"friend-a\",\"Hatched\":true,\"Color\":\"Black\",\"Chao\":{\"Name\":\"Friend\",\"Form\":\"seed\",\"Fly\":80}}]}";
            File.WriteAllText(file,legacy);var p=SaveStore.Load(file,out string warning);
            check(warning=="" && p.Name=="Mori" && p.Form=="swift" && p.AgeSeconds==1234 && p.RunAbility==120,"Recovery 1: a legacy Mori record loads with its existing identity and development");
            check(p.GardenEggs.Single().Chao!.StatSchemaVersion==1 && p.GardenEggs.Single().Id=="friend-a","Recovery 3: absent new fields migrate without dropping a resident");
            var expected=Ids(p);var before=JsonSerializer.SerializeToNode(p);SaveStore.Save(file,p);var again=SaveStore.Load(file,out _);
            check(expected.SequenceEqual(Ids(again)) && JsonNode.DeepEquals(before,JsonSerializer.SerializeToNode(again)),"Recovery 2/8: Load-Save-Load preserves all resident IDs and canonical data");
            SaveStore.Save(file,again);again=SaveStore.Load(file,out _);
            check(expected.SequenceEqual(Ids(again)) && again.Run==p.Run && again.GardenEggs.Count==1,"Recovery 5: repeated migration cannot duplicate or change Chao");
            var roster=ExpeditionRoster.Available(again).ToDictionary(e=>e.Id,e=>e.State);var bridge=new ChaoEditorBridge(directory);bridge.Poll(roster,true);
            string session=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"chao-editor-state.json"))).RootElement.GetProperty("session").GetString()!;
            File.WriteAllText(Path.Combine(directory,"chao-editor-request.json"),JsonSerializer.Serialize(new{session,id="edit-friend",target="friend-a",patch=new{Joy=81}}));
            var edited=bridge.Poll(roster,true);again.GardenEggs[0].Chao=edited;
            check(edited?.Joy==81 && again.Name=="Mori" && expected.SequenceEqual(Ids(again)),"Recovery 6: editing a resident cannot replace Mori or erase another ID");
            var team=ExpeditionRoster.Resolve(again,new[]{"friend-a","main"});again.Expedition.LastTeamIds=team;
            again.Stadium.Cancel();SaveStore.Save(file,again);var returned=SaveStore.Load(file,out _);
            check(team.SequenceEqual(ExpeditionRoster.Resolve(returned,team)) && expected.SequenceEqual(Ids(returned)),"Recovery 7: team selection/return ownership leaves the saved roster intact");
            File.WriteAllText(file,"{broken");File.WriteAllText(file+".bak","null");string[] initial={File.ReadAllText(file),File.ReadAllText(file+".bak")};
            bool failed=false;try{SaveStore.Load(file,out _);}catch(SaveLoadException){failed=true;}
            bool refused=false;try{SaveStore.Save(file,new PetState());}catch(SaveLoadException){refused=true;}
            check(failed && refused && File.ReadAllText(file)==initial[0] && File.ReadAllText(file+".bak")==initial[1] && !File.Exists(file+".tmp"),"Recovery 4: a failed load and subsequent save cannot overwrite originals with a new garden");
            foreach(string bad in new[]{"{}","{\"Name\":\"Mori\",\"GardenEggs\":[{\"Id\":\"lost\",\"Hatched\":true,\"Chao\":null}]}","{\"Name\":\"Mori\",\"GardenEggs\":[{\"Id\":\"main\"}]}","{\"Name\":\"Mori\",\"GardenEggs\":[{\"Id\":\"same\"},{\"Id\":\"same\"}]}","{\"Name\":\"Mori\",\"GardenEggs\":[{\"Id\":\"bad\",\"Hatched\":\"yes\"}]}"})
            {
                File.WriteAllText(file,bad);check(SaveStore.LoadSlot(file)==null && File.ReadAllText(file)==bad,"Damaged or ambiguous Chao records fail as a whole, without filtering the roster");
            }
            File.Delete(file);File.WriteAllText(file+".bak",legacy);var backup=SaveStore.Load(file,out warning);
            check(backup.Name=="Mori" && backup.GardenEggs.Count==1 && warning.Length>0 && !File.Exists(file),"A missing primary file loads an existing backup, without writing a new primary");
            File.Delete(file+".bak");File.WriteAllText(file+".tmp",legacy);failed=false;try{SaveStore.Load(file,out _);}catch(SaveLoadException){failed=true;}
            check(failed && !File.Exists(file),"Interrupted save evidence prevents an automatic new-game replacement");
            File.Delete(file+".tmp");File.WriteAllText(file,"{\"Name\":\"Mori\",\"GardenEggs\":[{\"Hatched\":true,\"Chao\":{\"Name\":\"Legacy friend\"}}]}");
            var first=SaveStore.LoadSlot(file)!;var second=SaveStore.LoadSlot(file)!;
            check(first.GardenEggs[0].Id==second.GardenEggs[0].Id && first.GardenEggs[0].Chao!.GardenEggs.Count==0,"Pre-ID records receive deterministic identities and no invented nested eggs");
        }
        finally{foreach(string path in Directory.GetFiles(directory))File.Delete(path);Directory.Delete(directory);}
    }
    internal static void Audit(string input,string directory)
    {
        string artifacts=Path.GetFullPath("artifacts")+Path.DirectorySeparatorChar;
        if(!Path.GetFullPath(input).StartsWith(artifacts,StringComparison.OrdinalIgnoreCase) || !Path.GetFullPath(directory).StartsWith(artifacts,StringComparison.OrdinalIgnoreCase))throw new Exception("Recovery audits must use the private artifacts copies only");
        Directory.CreateDirectory(directory);string output=Path.Combine(directory,"pet.json");if(File.Exists(output))throw new Exception("Do not overwrite an earlier recovery result");
        string source=File.ReadAllText(input);var pet=SaveStore.Load(input,out string warning);var baseline=JsonSerializer.SerializeToNode(pet);
        SaveStore.Save(output,pet);var next=SaveStore.Load(output,out _);
        if(!JsonNode.DeepEquals(baseline,JsonSerializer.SerializeToNode(next)) || File.ReadAllText(input)!=source)throw new Exception("Recovery round-trip changed content or input");
        var roster=ExpeditionRoster.Available(next).ToDictionary(p=>p.Id,p=>p.State);new ChaoEditorBridge(directory).Poll(roster,false);
        var summary=new { passed=true,source=Path.GetFullPath(input),output=Path.GetFullPath(output),warning,roster=roster.Select(e=>new{id=e.Key,e.Value.Name,e.Value.Form,e.Value.AgeSeconds,stats=e.Value.Statistics}).ToArray(),canonicalRoundTripEqual=true,inputUnchanged=true };
        File.WriteAllText(Path.Combine(directory,"recovery-check.json"),JsonSerializer.Serialize(summary,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"RECOVERY_AUDIT_OK: {roster.Count} existing Chao, full canonical round-trip, source unchanged, editor roster exported");
    }
}
