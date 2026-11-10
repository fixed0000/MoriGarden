using System;
using System.IO;
using System.Text.Json;

namespace Mori;

public static class SaveStore
{
    public static PetState Load(string path, out string message)
    {
        message = "";
        if (!File.Exists(path)) return PetState.NewEgg();
        try { return Read(path); }
        catch (Exception ex) when (ex is IOException or JsonException or ArgumentException or UnauthorizedAccessException)
        {
            try
            {
                var backup = Read(path + ".bak");
                message = "The main save could not be read. A backup was loaded.";
                return backup;
            }
            catch
            {
                message = "The save could not be read. Started a new garden and kept the old file as .damaged.";
                try { File.Copy(path, path + ".damaged", true); }
                catch { message = "Could not read or back up the save. Check access to the save folder."; }
                return new PetState();
            }
        }
    }

    public static PetState? LoadSlot(string path)
    {
        foreach (var candidate in new[]{path,path+".bak"})
        {
            if (!File.Exists(candidate)) continue;
            try { return Read(candidate); }
            catch (Exception ex) when (ex is IOException or JsonException or ArgumentException or UnauthorizedAccessException) { }
        }
        return null;
    }

    public static PetState Snapshot(PetState pet) =>
        JsonSerializer.Deserialize<PetState>(JsonSerializer.SerializeToUtf8Bytes(pet))!;

    private static PetState Read(string path)
    {
        var pet = JsonSerializer.Deserialize<PetState>(File.ReadAllText(path))
                  ?? throw new ArgumentException("Empty save");
        pet.Validate();
        return pet;
    }

    public static void Save(string path, PetState pet)
    {
        pet.Validate();
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
