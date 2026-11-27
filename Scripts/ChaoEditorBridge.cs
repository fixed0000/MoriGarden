using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mori;

internal sealed class ChaoEditorBridge
{
    private readonly string directory;
    private readonly string session=Guid.NewGuid().ToString("N");
    private string lastRequest="";
    private string status="Ready";
    private long publication;
    private readonly HashSet<string> processed=new(StringComparer.Ordinal);
    private readonly Dictionary<string,double> rhythmOffsets=new(StringComparer.Ordinal);
    private static readonly PropertyInfo[] fields=typeof(PetState).GetProperties()
        .Where(p=>p.CanWrite && (p.PropertyType.IsPrimitive || p.PropertyType==typeof(string)) && p.Name!=nameof(PetState.SpecialFruitBag) && p.Name!=nameof(PetState.SeedBag) && p.Name!=nameof(PetState.GardenTrees) && p.Name!=nameof(PetState.Version) && p.Name!=nameof(PetState.ManualLocation) && p.Name!=nameof(PetState.GardenEggs)
            && p.Name!=nameof(PetState.SocialLinks) && p.Name!=nameof(PetState.Expedition) && p.Name!=nameof(PetState.CharacterRelationships) && p.Name!=nameof(PetState.GardenSeconds)
            && p.Name!=nameof(PetState.Stadium) && p.Name!=nameof(PetState.Athletics)
            && p.Name!=nameof(PetState.Statistics) && p.Name!=nameof(PetState.StatSchemaVersion) && p.Name!=nameof(PetState.StatReincarnations)
            && p.Name!=nameof(PetState.PersonalitySchemaVersion) && !ChaoStatistics.Names.Contains(p.Name)).ToArray();

    public ChaoEditorBridge(string directory) => this.directory=directory;

    public string EditedTarget { get; private set; } = "main";

    private GardenRhythmContext? RhythmFor(string id,GardenRhythmContext? world)
    {
        if(world==null)return null;
        if(!rhythmOffsets.TryGetValue(id,out double offset))rhythmOffsets[id]=offset=GardenRhythmContext.OffsetFor(id);
        return world with{StableChaoId=id,PersonalPhaseOffset=offset};
    }
    public PetState? Poll(Dictionary<string,PetState> roster,bool canEdit,GardenRhythmContext? worldRhythm=null,IReadOnlyDictionary<string,ChaoExpressionDiagnostic>? expressions=null)
    {
        foreach(var pet in roster.Values){pet.EnsureStatistics();pet.EnsurePersonality();}
        Directory.CreateDirectory(directory);
        string command=Path.Combine(directory,"chao-editor-request.json");
        PetState? updated=null;
        if (File.Exists(command))
        {
            try
            {
                var request=JsonNode.Parse(File.ReadAllText(command))!.AsObject();
                string id=request["id"]?.GetValue<string>() ?? "";
                if (request["session"]?.GetValue<string>()==session && id.Length>0 && id!=lastRequest)
                {
                    lastRequest=id;
                    if(!processed.Add(id)){status="Duplicate request ignored.";Publish();return null;}
                    if(request.Any(p=>p.Key is not ("session" or "id" or "target" or "patch" or "statistics" or "relationship" or "personality" or "personalityRevision" or "learnedAdjustments" or "learnedRevision")))
                        throw new ArgumentException("Unknown editor request section.");
                    string target=request["target"]?.GetValue<string>() ?? "main";
                    if (!roster.TryGetValue(target,out var current))
                        status="This Chao is no longer in the garden.";
                    else if (!canEdit) status="Finish the current race or transition first.";
                    else
                    {
                        var values=JsonSerializer.SerializeToNode(current)!.AsObject();
                        var next=values.Deserialize<PetState>() ?? throw new ArgumentException("Empty editor update");
                        JsonObject Section(string key)=>!request.ContainsKey(key) || request[key]==null && key is "statistics" or "relationship" ? new() : request[key] as JsonObject ?? throw new ArgumentException("Invalid editor section: "+key);
                        var patch=Section("patch");var stats=Section("statistics");var personality=Section("personality");var relation=Section("relationship");
                        string? revision=request["personalityRevision"]?.GetValue<string>();
                        foreach (var (key,value) in patch)
                        {
                            if (!fields.Any(field=>field.Name==key)) throw new ArgumentException("Unknown parameter: "+key);
                            var field=fields.Single(f=>f.Name==key);
                            if(value==null && field.PropertyType!=typeof(string))throw new ArgumentException("Missing parameter: "+key);
                            field.SetValue(next,value?.Deserialize(field.PropertyType));
                        }
                        foreach(var (name,record) in stats)
                            {
                                var old=JsonSerializer.SerializeToNode(next.Stat(name))!.AsObject();
                                if(record is not JsonObject changes)throw new ArgumentException("Invalid stat update");
                                foreach(var (field,value) in changes)
                                {
                                    if(field is not ("Grade" or "Level" or "Points" or "Progress"))throw new ArgumentException("Unknown stat field");
                                    old[field]=value?.DeepClone();
                                }
                                var updatedStat=old.Deserialize<ChaoStat>() ?? throw new ArgumentException("Empty stat update");
                                ChaoStatistics.ValidateEditor(name,updatedStat,next.Stat(name));
                                var destination=next.Stat(name);destination.Grade=updatedStat.Grade;destination.Level=updatedStat.Level;destination.Points=updatedStat.Points;destination.Progress=updatedStat.Progress;
                            }
                        ChaoPersonalityEditor.Apply(next,current,personality,revision,patch);
                        ChaoLearnedEditor.Apply(next,current,Section("learnedAdjustments"),request["learnedRevision"]?.GetValue<string>());
                        // Prepare shared relationships independently, including resident-target requests.
                        List<ChaoRelationship>? preparedLinks=null;
                        ChaoRelationship? preparedLink=null;
                        if(relation.Count>0)
                        {
                            if(relation.Any(p=>p.Key is not ("other" or "met" or "score")))throw new ArgumentException("Unknown relationship field");
                            string other=relation["other"]?.GetValue<string>() ?? "";
                            if(other==target || !roster.ContainsKey(other))throw new ArgumentException("Choose a different Chao in the garden.");
                            bool? met=relation.ContainsKey("met") ? relation["met"]?.GetValue<bool>() ?? throw new ArgumentException("Invalid introduction flag") : null;
                            int? score=relation.ContainsKey("score") ? relation["score"]?.GetValue<int>() ?? throw new ArgumentException("Invalid relationship score") : null;
                            if(score is < -100 or >100)throw new ArgumentException("Affinity must be between -100 and 100.");
                            preparedLinks=JsonSerializer.Deserialize<List<ChaoRelationship>>(JsonSerializer.Serialize(roster["main"].SocialLinks))!;
                            var link=preparedLinks.Find(item=>item.Matches(target,other));
                            if(link==null){link=new ChaoRelationship{First=target,Second=other};preparedLinks.Add(link);}
                            if(met is bool flag){link.Met=flag;link.Introduced|=flag;}
                            if(score is int n)link.SetScoreFrom(target,n);
                            preparedLink=link;
                        }
                        next.Validate();
                        if(target=="main" && preparedLinks!=null)next.SocialLinks=preparedLinks;
                        next.GardenEggs=current.GardenEggs;
                        next.GardenTrees=current.GardenTrees;
                        next.KeepCurrentRuntimeFrom(current);
                        // Commit only after all validation/runtime preparation. Preserve existing link
                        // identities used by the garden's relation views and social executors.
                        if(target!="main" && preparedLink!=null)
                        {
                            var live=roster["main"].SocialLinks.Find(item=>item.Matches(preparedLink.First,preparedLink.Second));
                            if(live==null)roster["main"].SocialLinks.Add(preparedLink);
                            else {live.Score=preparedLink.Score;live.ReverseScore=preparedLink.ReverseScore;live.Met=preparedLink.Met;live.Introduced=preparedLink.Introduced;live.Partner=preparedLink.Partner;}
                        }
                        updated=next;
                        EditedTarget=target;
                        roster[target]=next;
                        status="Applied";
                    }
                }
            }
            catch(Exception ex) when (ex is IOException or JsonException or ArgumentException or InvalidOperationException)
            { status=ex.Message; }
        }
        Publish();return updated;
        void Publish()
        {
        var ownerState=roster["main"];
        var chao=roster.Select(entry=>new { id=entry.Key,
            parameters=fields.ToDictionary(field=>field.Name,field=>field.GetValue(entry.Value)),
            statistics=entry.Value.Statistics,
            personalitySchemaVersion=entry.Value.PersonalitySchemaVersion,
            personality=entry.Value.Personality,
            personalityRevision=ChaoPersonalityEditor.Revision(entry.Value),
            learnedRevision=ChaoLearnedEditor.Revision(entry.Value),
            learnedEditor=ChaoLearnedEditor.Capture(entry.Value),
            memoryDiagnostics=ChaoMemoryDiagnostics.Capture(entry.Value,entry.Key,roster),
            learnedPreferences=entry.Value.LearnedPreferenceDiagnostics(),
            activityHabits=entry.Value.ActivityHabitDiagnostics(),
            dailyRhythm=ChaoDailyRhythmScorer.Capture(entry.Value,RhythmFor(entry.Key,worldRhythm)),
            placePreferences=entry.Value.PlacePreferenceDiagnostics(),
            socialPreferences=entry.Value.SocialPreferenceDiagnostics(ownerState,entry.Key,roster),
            expression=expressions?.GetValueOrDefault(entry.Key),
            voice=expressions?.GetValueOrDefault(entry.Key)?.Voice,
            idleGestures=expressions?.GetValueOrDefault(entry.Key)?.IdleGestures,
            currentState=ChaoStateDiagnostics.Capture(entry.Value,expressions?.GetValueOrDefault(entry.Key)),
            relationships=roster.Where(peer=>peer.Key!=entry.Key && !peer.Value.IsEgg).Select(peer=>
            {
                var link=ownerState.SocialLink(entry.Key,peer.Key);
                return new { id=peer.Key, name=peer.Value.Name, met=link?.Met ?? false,
                    score=link?.ScoreFrom(entry.Key) ?? 0, status=link?.StatusFrom(entry.Key) ?? "Stranger" };
            }).ToArray() }).ToArray();
        string path=Path.Combine(directory,"chao-editor-state.json");
        string snapshotJson=JsonSerializer.Serialize(new { session,status,lastRequest,publication=++publication,profileDirectory=Path.GetFullPath(directory),chao });
        // Windows readers/OneDrive can briefly deny replacement of the snapshot.
        // Keep an already applied edit authoritative and retry publication next
        // Poll; never replay the command or touch the actual garden save here.
        try{File.WriteAllText(path+".tmp",snapshotJson);File.Move(path+".tmp",path,true);}
        catch(IOException) { }
        catch(UnauthorizedAccessException) { }
        }
    }
}
