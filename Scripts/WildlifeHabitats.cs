using System.Linq;
using Godot;

namespace Mori;

internal sealed record WildlifeHabitat(string Id,string Species,Vector2 Center,float Radius,string Area,string? RequiredFlag=null);
internal static class WildlifeHabitats
{
    internal static readonly WildlifeHabitat[] All={
        new("meadow-grass-rabbit","rabbit",new(-7,15),4,WorldCatalog.Meadow),
        new("meadow-oak-songbird","songbird",new(9,17),3.5f,WorldCatalog.Meadow),
        new("meadow-forest-cub","bear-cub",new(-19,-9),4,WorldCatalog.Meadow),
        new("meadow-shore-penguin","penguin",new(14,9),2.6f,WorldCatalog.Meadow),
        new("greenhouse-songbird","songbird",new(-9,-10),2.6f,WorldCatalog.Greenhouse,"greenhouse-restored")
    };
    internal static void Build(Node3D root,PetState save,string area)
    {
        var habitats=All.Where(h=>h.Area==area).ToArray();if(habitats.Length==0)return;
        var wildlife=new WildlifeWorld { Name="Wildlife",SaveData=save };root.AddChild(wildlife);
        foreach(var habitat in habitats)
        {
            var species=AnimalCatalog.Find(habitat.Species)!;
            var at=new Vector3(habitat.Center.X,ExpeditionArt.Height(habitat.Center.X,habitat.Center.Y,area)+.02f,habitat.Center.Y);
            var actor=new WildAnimal { Name=habitat.Id,Habitat=habitat,Species=species,Skill=System.Enum.Parse<AnimalSkill>(species.Skill),IsParrot=species.Id=="parrot",IsGorilla=species.Id=="gorilla",Home=at,Position=at };
            root.AddChild(actor);actor.BuildInterest(save.Wildlife.Slot(habitat.Id).Generation);wildlife.Animals.Add(actor);
            actor.SetPresent(!save.Wildlife.Slot(habitat.Id).Collected && wildlife.Unlocked(habitat));
        }
    }
}
