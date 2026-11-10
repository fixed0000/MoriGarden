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
    private static readonly PropertyInfo[] fields=typeof(PetState).GetProperties()
        .Where(p=>p.CanWrite && p.Name!=nameof(PetState.Version) && p.Name!=nameof(PetState.ManualLocation) && p.Name!=nameof(PetState.GardenEggs)
            && p.Name!=nameof(PetState.SocialLinks) && p.Name!=nameof(PetState.CharacterRelationships) && p.Name!=nameof(PetState.GardenSeconds)).ToArray();

    public ChaoEditorBridge(string directory) => this.directory=directory;

    public string EditedTarget { get; private set; } = "main";

    public PetState? Poll(Dictionary<string,PetState> roster,bool canEdit)
    {
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
                    string target=request["target"]?.GetValue<string>() ?? "main";
                    if (!roster.TryGetValue(target,out var current))
                        status="This Chao is no longer in the garden.";
                    else if (!canEdit) status="Finish the current race or transition first.";
                    else
                    {
                        var values=JsonSerializer.SerializeToNode(current)!.AsObject();
                        var patch=request["patch"]!.AsObject();
                        foreach (var (key,value) in patch)
                        {
                            if (!fields.Any(field=>field.Name==key)) throw new ArgumentException("Unknown parameter: "+key);
                            values[key]=value?.DeepClone();
                        }
                        var next=values.Deserialize<PetState>() ?? throw new ArgumentException("Empty editor update");
                        next.Validate();
                        if (request["relationship"] is JsonObject relationship)
                        {
                            string other=relationship["other"]?.GetValue<string>() ?? "";
                            if (other==target || !roster.ContainsKey(other))
                                throw new ArgumentException("Choose a different Chao in the garden.");
                            bool? met=relationship["met"]?.GetValue<bool>();
                            int? score=relationship["score"]?.GetValue<int>();
                            var owner=target=="main" ? next : roster["main"];
                            var link=owner.SocialLink(target,other,create:true)!;
                            if (met is bool flag) { link.Met=flag; link.Introduced|=flag; }
                            if (score is int value) link.Score=Math.Clamp(value,-100,100);
                        }
                        next.GardenEggs=current.GardenEggs;
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
        var ownerState=roster["main"];
        var chao=roster.Select(entry=>new { id=entry.Key,
            parameters=fields.ToDictionary(field=>field.Name,field=>field.GetValue(entry.Value)),
            relationships=roster.Where(peer=>peer.Key!=entry.Key && !peer.Value.IsEgg).Select(peer=>
            {
                var link=ownerState.SocialLink(entry.Key,peer.Key);
                return new { id=peer.Key, name=peer.Value.Name, met=link?.Met ?? false,
                    score=link?.Score ?? 0, status=link?.Status ?? "Stranger" };
            }).ToArray() }).ToArray();
        string path=Path.Combine(directory,"chao-editor-state.json");
        File.WriteAllText(path+".tmp",JsonSerializer.Serialize(new { session,status,lastRequest,chao }));
        File.Move(path+".tmp",path,true);
        return updated;
    }
}
