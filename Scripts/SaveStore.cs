using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mori;

public static class SaveStore
{
    public static PetState Load(string path, out string message)
    {
        message = "";
        if (!File.Exists(path) && !File.Exists(path+".bak") && !HasExistingData(path)) return PetState.NewEgg();
        try { return Read(path); }
        catch (Exception ex) when(ex is UnsupportedPersonalitySchemaException or UnsupportedLearnedPreferencesSchemaException or UnsupportedActivityHabitsSchemaException)
        { throw new SaveLoadException(ex.Message,ex); }
        catch (Exception ex) when (ex is IOException or JsonException or ArgumentException or UnauthorizedAccessException)
        {
            try
            {
                var backup = Read(path + ".bak");
                message = "The main save could not be read. A backup was loaded.";
                return backup;
            }
            catch (Exception schemaError) when(schemaError is UnsupportedPersonalitySchemaException or UnsupportedLearnedPreferencesSchemaException or UnsupportedActivityHabitsSchemaException)
            { throw new SaveLoadException(schemaError.Message,schemaError); }
            catch
            {
                throw new SaveLoadException("The garden could not be loaded. Original files were kept; no replacement Chao was created. Recover a verified copy before saving.",ex);
            }
        }
    }

    private static bool HasExistingData(string path)
    {
        string folder=Path.GetDirectoryName(Path.GetFullPath(path))!;
        return File.Exists(path+".tmp") || File.Exists(path+".damaged") || File.Exists(Path.Combine(folder,"chao-editor-state.json")) ||
            Directory.Exists(Path.Combine(folder,"slots")) && Directory.EnumerateFiles(Path.Combine(folder,"slots"),"slot-*.json*").Any();
    }

    public static PetState? LoadSlot(string path)
    {
        foreach (var candidate in new[]{path,path+".bak"})
        {
            if (!File.Exists(candidate)) continue;
            try { return Read(candidate); }
            catch (Exception ex) when(ex is UnsupportedPersonalitySchemaException or UnsupportedLearnedPreferencesSchemaException or UnsupportedActivityHabitsSchemaException) { return null; }
            catch (Exception ex) when (ex is IOException or JsonException or ArgumentException or UnauthorizedAccessException) { }
        }
        return null;
    }

    public static PetState Snapshot(PetState pet) { pet.EnsureStatisticsTree();pet.EnsurePersonalityTree();return JsonSerializer.Deserialize<PetState>(JsonSerializer.SerializeToUtf8Bytes(pet))!; }

    private static PetState Read(string path)
    {
        string json=File.ReadAllText(path);
        var root=JsonNode.Parse(json) as JsonObject ?? throw new ArgumentException("Invalid garden record");
        SaveIntegrity.Prepare(root);
        var pet = root.Deserialize<PetState>()
                  ?? throw new ArgumentException("Empty save");
        SaveIntegrity.Validate(pet);
        using(var document=JsonDocument.Parse(json))
        {
            if(!document.RootElement.TryGetProperty("History",out var history) || history.ValueKind!=JsonValueKind.Object)pet.History.PastUnknown=true;
            if(document.RootElement.TryGetProperty("GardenEggs",out var eggs) && eggs.ValueKind==JsonValueKind.Array)
            {
                int index=0;foreach(var egg in eggs.EnumerateArray())
                {
                    if(index<pet.GardenEggs.Count && pet.GardenEggs[index].Chao is {} chao && egg.TryGetProperty("Chao",out var child) && child.ValueKind==JsonValueKind.Object &&
                        (!child.TryGetProperty("History",out var childHistory) || childHistory.ValueKind!=JsonValueKind.Object))chao.History.PastUnknown=true;
                    index++;
                }
            }
        }
        return pet;
    }

    public static void Save(string path, PetState pet)
    {
        SaveIntegrity.Validate(pet);
        // Never overwrite a file that we cannot fully read, even if another
        // caller tries to save a fresh or partially recovered garden over it.
        if(File.Exists(path))
        {
            try{Read(path);}
            catch(Exception ex) when(ex is IOException or JsonException or ArgumentException or UnauthorizedAccessException)
            {throw new SaveLoadException("Existing save is unreadable. It was not overwritten; choose a separate recovery copy.",ex);}
        }
        else if(File.Exists(path+".bak"))
        {
            try{Read(path+".bak");}
            catch(Exception ex) when(ex is IOException or JsonException or ArgumentException or UnauthorizedAccessException)
            {throw new SaveLoadException("The only backup is unreadable. Saving a replacement garden was stopped.",ex);}
        }
        else if(File.Exists(path+".tmp") || File.Exists(path+".damaged"))
            throw new SaveLoadException("Unresolved save data exists. Keep it and recover to a separate folder first.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(pet, new JsonSerializerOptions { WriteIndented = true }));
        if (File.Exists(path))
        {
            // Не заменяем хорошую резервную копию повреждённым файлом.
            try { Read(path); File.Copy(path, path + ".bak", true); }
            catch (Exception ex) when (ex is IOException or JsonException or ArgumentException) { }
        }
        File.Move(path + ".tmp", path, true);
    }
}
