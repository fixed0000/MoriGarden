using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mori;

internal static class ChaoMemoryDiagnosticChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var p=new PetState{Personality=new(),PersonalitySchemaVersion=1};p.EnsureStatisticsTree();p.GardenEggs.Clear();p.History.Seconds=1000;
        var peer=new PetState{Name="Same name",Personality=new(),PersonalitySchemaVersion=1};
        var roster=new Dictionary<string,PetState>{{"main",p},{"white",peer},{"black",new(){Name="Same name"}}};
        var empty=ChaoMemoryDiagnostics.Capture(p,"main",roster);
        check(empty.available && empty.count==0 && empty.capacity==64 && empty.events.Length==0,"Memory diagnostics A empty retained history");
        p.GardenSeconds=600;peer.GardenSeconds=0;
        check(ChaoMemoryDiagnostics.Capture(peer,"white",roster).gardenSeconds==600,"Memory diagnostics residents use existing root garden clock, not their unused local field");
        var h=p.History;h.PastUnknown=true;h.Visited.Add("garden");h.LastArea="garden";
        int i=0;foreach(var kind in Enum.GetValues<ChaoMemoryKind>())
        {
            h.Events.Add(new(){Kind=kind,Source="source-"+i,Area="garden",Description="Plain <text> ; [not Tcl] "+kind,FirstAt=100,LastAt=200+i,Occurrences=i==0 ? 10 : 1,
                Valence=(i%3-1)*.5f,Importance=.55f,Place=i%2==0 ? new WorldPoint(1,2,3) : null,Participants=i%2==0 ? new[]{"white","missing"} : Array.Empty<string>()});i++;
        }
        var d=ChaoMemoryDiagnostics.Capture(p,"main",roster);
        check(d.available && d.events.Length==13 && d.events.Select(e=>e.kind).Distinct().Count()==13,"Memory diagnostics B all thirteen real kinds including negative");
        check(d.events[0].occurrences==10 && d.events.Count(e=>e.source=="source-0")==1,"Memory diagnostics D repetitions remain one merged interval");
        check(d.events[0].firstAt==100 && d.events[0].lastAt==200 && d.events[0].ageSeconds==800 && d.events[0].spanSeconds==100,"Memory diagnostics E active times and span preserved");
        check(d.events.Select((e,n)=>e.influence==h.Events[n].Influence(h.Seconds) && e.halfLife==1800+h.Events[n].Importance*18000).All(x=>x),"Memory diagnostics F exact model Influence and variable half-life");
        float prior=d.events[0].influence;h.Seconds+=12000;var older=ChaoMemoryDiagnostics.Capture(p,"main",roster);
        check(older.events[0].influence<prior && h.Events[0].LastAt==200,"Memory diagnostics G passive decay never rewrites LastAt");
        check(d.positive>0 && d.negative>0 && d.neutral>0 && d.events.All(e=>e.tone==(e.valence>0 ? "Positive" : e.valence<0 ? "Negative" : "Neutral")),"Memory diagnostics H actual signed Valence classification");
        check(d.events[0].participants[0].id=="white" && d.events[0].participants[0].name=="Same name" && d.events[0].participants[1].id=="missing" && !d.events[0].participants[1].inRoster && roster.Count==3,"Memory diagnostics I/J stable participant IDs, unknown participant not fabricated");
        check(d.events[0].place==new WorldPoint(1,2,3) && d.events[1].place==null,"Memory diagnostics K/L exact position and absence");
        check(older.partners.All(r=>r.partnerBias==h.PartnerBias(r.id)) && older.partners.Single(r=>r.id=="black").count==0,"Memory diagnostics M PartnerBias matches model, same names independent");
        check(older.places.All(r=>r.placeBias==h.PlaceBias(r.area,r.point)),"Memory diagnostics N raw spatial matches model");
        check(older.objects.All(r=>r.objectBias==h.ObjectBias(r.source,r.area)),"Memory diagnostics O ObjectBias shares canonical nonlinear helper");
        var mixed=new ChaoMemory{Seconds=1000};
        mixed.Events.Add(new(){Kind=ChaoMemoryKind.Pleasant,Source="object",Area="garden",Description="positive",FirstAt=0,LastAt=0,Occurrences=4,Importance=.9f,Valence=.01f});
        mixed.Events.Add(new(){Kind=ChaoMemoryKind.Unpleasant,Source="object",Area="garden",Description="negative",FirstAt=0,LastAt=0,Importance=.5f,Valence=-1});
        float success=mixed.Events[0].Influence(1000)*1.6f,failure=mixed.Events[1].Influence(1000);
        check(mixed.ObjectBias("object","garden")==Math.Clamp(success*.10f-failure*.24f/(1+success),-.25f,.25f),"Memory diagnostics nonlinear success/failure preserves sign-only weighting and repetitions");
        var route=LeisureDestinationCatalog.All[0];h.Events.Clear();h.Seconds=1000;
        h.Events.Add(new(){Kind=ChaoMemoryKind.Pleasant,Source=route.Id,Area="garden",Description="route",FirstAt=0,LastAt=1000,Valence=.55f,Importance=.55f,Place=route.End,Occurrences=3});
        var leisure=ChaoMemoryDiagnostics.Capture(p,"main",roster).leisure.Single(r=>r.referenceScore.Id==route.Id).referenceScore;
        check(leisure==ChaoLeisurePreferenceScorer.Evaluate(h,"garden",route.Id) && leisure.SpatialBias==0 && Math.Abs(leisure.LearnedBias-.15f)<.000001,"Memory diagnostics P leisure completion not counted twice");
        h.Events.Clear();for(i=0;i<64;i++)h.Events.Add(new(){Kind=ChaoMemoryKind.Pleasant,Source="cap-"+i,Area="garden",Description="cap",FirstAt=1000,LastAt=1000,Valence=1,Importance=1,Place=new(0,0,0),Participants=new[]{"white"}});
        d=ChaoMemoryDiagnostics.Capture(p,"main",roster);
        check(d.count==64 && d.events.Length==64,"Memory diagnostics C supports all retained capacity");
        check(d.partners.Single(r=>r.id=="white").partnerBias==.4f && d.places[0].placeBias==.6f,"Memory diagnostics Q positive raw bias clamps");
        foreach(var e in h.Events)e.Valence=-1;d=ChaoMemoryDiagnostics.Capture(p,"main",roster);
        check(d.partners.Single(r=>r.id=="white").partnerBias==-.3f && d.places[0].placeBias==-.3f,"Memory diagnostics Q negative raw bias clamps");
        string before=JsonSerializer.Serialize(p);var list=h.Events;var first=h.Events[0];
        for(i=0;i<50;i++)ChaoMemoryDiagnostics.Capture(p,"main",roster);
        check(before==JsonSerializer.Serialize(p) && ReferenceEquals(h.Events,list) && ReferenceEquals(h.Events[0],first),"Memory diagnostics R/S/T/U exact read-only state and collection identities");
        var detached=ChaoMemoryDiagnostics.Capture(p,"main",roster);detached.events[0].participants[0]=new("different","changed",false);
        check(h.Events[0].Participants[0]=="white","Memory diagnostics DTO arrays do not alias live participant arrays");
        check(!before.Contains("memoryDiagnostics") && !before.Contains("MemoryEditorVersion"),"Memory diagnostics V no persisted diagnostic schema");
        var second=h.Events[1];h.Events[1]=h.Events[0];d=ChaoMemoryDiagnostics.Capture(p,"main",roster);
        check(!d.available && ReferenceEquals(h.Events[0],h.Events[1]),"Memory diagnostics duplicate identities reported without deduplication");h.Events[1]=second;
        h.Events[0].LastAt=double.NaN;before=JsonSerializer.Serialize(h.Events.Skip(1));d=ChaoMemoryDiagnostics.Capture(p,"main",roster);
        check(!d.available && d.error!=null && double.IsNaN(h.Events[0].LastAt) && h.Events.Count==64 && before==JsonSerializer.Serialize(h.Events.Skip(1)),"Memory diagnostics malformed memory reported without Validate or repair");h.Events[0].LastAt=1000;
        string dir=Path.Combine(Path.GetTempPath(),"MoriMemoryDiagnostics-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        try
        {
            var bridge=new ChaoEditorBridge(dir);bridge.Poll(roster,true);var snap=JsonNode.Parse(File.ReadAllText(Path.Combine(dir,"chao-editor-state.json")))!;
            before=JsonSerializer.Serialize(p);
            File.WriteAllText(Path.Combine(dir,"chao-editor-request.json"),new JsonObject{["id"]="memory-injection",["session"]=snap["session"]!.DeepClone(),["target"]="main",["patch"]=new JsonObject{["Joy"]=99},["memoryDiagnostics"]=new JsonObject{["deleteEvent"]="source"}}.ToJsonString());
            check(bridge.Poll(roster,true)==null && JsonSerializer.Serialize(p)==before,"Memory diagnostics request section cannot edit or delete memory, mixed rollback");
            check(snap["chao"]!.AsArray()[0]!["memoryDiagnostics"]!["events"]!.AsArray().Count==64,"Memory diagnostics bridge publishes every retained row");
            string file=Path.Combine(dir,"pet.json");SaveStore.Save(file,p);var loaded=SaveStore.Load(file,out _);
            check(loaded.History.Events.Count==64 && !File.ReadAllText(file).Contains("memoryDiagnostics"),"Memory diagnostics save/load remains existing format");
        }
        finally{Directory.Delete(dir,true);}
    }
}
