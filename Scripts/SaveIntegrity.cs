using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Mori;

// All-or-nothing validation: a malformed resident must never become a newly
// generated hatchling, disappear from a filtered roster, or receive a random ID.
internal static class SaveIntegrity
{
    internal static void Prepare(JsonObject pet,int depth=0)
    {
        if(depth>16 || !new[]{"Name","Form","Fullness","IsEgg","Swim","Statistics","AgeSeconds"}.Any(pet.ContainsKey))
            throw new ArgumentException("Missing or invalid Chao record. The original save was kept.");
        if(pet["ActivityHabits"] is JsonObject habits && habits["SchemaVersion"] is JsonValue schema &&
            schema.TryGetValue<int>(out int version) && version!=1)throw new UnsupportedActivityHabitsSchemaException();
        if(!pet.ContainsKey("GardenEggs"))pet["GardenEggs"]=new JsonArray();
        if(pet["GardenEggs"] is not JsonArray eggs)throw new ArgumentException("Invalid saved Chao list.");
        var ids=new HashSet<string>(StringComparer.Ordinal){"main"};
        int index=0;
        foreach(var item in eggs)
        {
            if(item is not JsonObject egg)throw new ArgumentException("Invalid entry in saved Chao list.");
            if(!egg.ContainsKey("Id"))
            {
                // Only pre-ID formats are assigned a stable migration identity.
                string material=index+":"+egg.ToJsonString();
                egg["Id"]="legacy-"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)))[..24].ToLowerInvariant();
            }
            if(egg["Id"] is not JsonValue idValue || !idValue.TryGetValue<string>(out var id))throw new ArgumentException("Invalid saved Chao identity.");
            if(string.IsNullOrWhiteSpace(id) || !ids.Add(id))throw new ArgumentException("Missing or duplicate saved Chao identity.");
            bool hatched=false;
            if(egg.ContainsKey("Hatched") && (egg["Hatched"] is not JsonValue flag || !flag.TryGetValue<bool>(out hatched)))throw new ArgumentException("Invalid saved hatchling flag.");
            if(egg["Chao"] is JsonObject child)Prepare(child,depth+1);
            else if(hatched || egg["Chao"]!=null)throw new ArgumentException("A saved hatchling has no readable Chao record. Nothing was removed.");
            index++;
        }
    }
    internal static void Validate(PetState pet,int depth=0)
    {
        if(depth>16)throw new ArgumentException("Invalid nested Chao data.");
        pet.Validate();var ids=new HashSet<string>(StringComparer.Ordinal){"main"};
        foreach(var egg in pet.GardenEggs)
        {
            if(string.IsNullOrWhiteSpace(egg.Id) || !ids.Add(egg.Id) || egg.Hatched && egg.Chao==null)
                throw new ArgumentException("Incomplete Chao roster. Saving was stopped to protect existing pets.");
            if(egg.Chao!=null)Validate(egg.Chao,depth+1);
        }
    }
}

public sealed class SaveLoadException : System.IO.IOException
{
    public SaveLoadException(string message,Exception? cause=null):base(message,cause){}
}
