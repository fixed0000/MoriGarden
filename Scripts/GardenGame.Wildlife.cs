using System;
using System.Linq;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private WildlifeWorld? Wildlife=>zone==GardenZone.Expedition ? world.GetNodeOrNull<WildlifeWorld>("Wildlife") : null;
    private OptionButton bagAnimals=null!;
    private Button takeAnimalButton=null!;
    private Label animalBagLabel=null!;
    private void AdvanceWildlife(float dt)
    {
        pet.Wildlife.Advance(dt);
        if(Wildlife is not {} wildlife || expeditionTeam==null)return;
        wildlife.SaveData=pet;wildlife.SaveRequested=Save;wildlife.Message=text=>Notify(text,6);
        wildlife.Step(dt,player,camera,expeditionTeam);
    }
    private int AvailableAnimal(string id)=>Math.Max(0,pet.Wildlife.Count(id)-world.GetChildren().OfType<TrainingAnimal>().Count(a=>a.InventorySpecies==id && !a.IsQueuedForDeletion()));
    private void BuildAnimalBag()
    {
        Panel(bagPanel,new(885,525,370,155),new("f9f3e5"));
        animalBagLabel=Text(bagPanel,"ANIMALS",15,new(904,532,330,26),GardenArt.Ink);
        bagAnimals=new OptionButton { Position=new(904,566),Size=new(330,36) };bagPanel.AddChild(bagAnimals);
        takeAnimalButton=new Button { Text="Take Selected Animal",Position=new(904,610),Size=new(330,34) };
        takeAnimalButton.Pressed+=TakeSelectedAnimal;bagPanel.AddChild(takeAnimalButton);
        Text(bagPanel,"Bring animals to a Chao in the garden.",11,new(904,649,330,25),GardenArt.Ink);
    }
    private void RefreshAnimalBag()
    {
        if(bagAnimals==null)return;
        var labels=AnimalCatalog.All.Select(a=>$"{a.Name} · {a.Skill}   ×{AvailableAnimal(a.Id)}").ToArray();
        bool rebuild=bagAnimals.ItemCount!=labels.Length;
        if(!rebuild)for(int i=0;i<labels.Length;i++)rebuild|=bagAnimals.GetItemText(i)!=labels[i];
        if(rebuild)
        {
            int selected=Math.Max(0,bagAnimals.Selected);bagAnimals.Clear();foreach(var text in labels)bagAnimals.AddItem(text);bagAnimals.Select(selected);
        }
        animalBagLabel.Text=$"ANIMALS   {pet.Wildlife.Total}/{WildlifeProgress.Capacity}";
        takeAnimalButton.Disabled=zone!=GardenZone.Garden || !HandsAvailable || bagAnimals.Selected<0 || AvailableAnimal(AnimalCatalog.All[bagAnimals.Selected].Id)==0;
    }
    private void TakeSelectedAnimal()
    {
        if(zone!=GardenZone.Garden || !HandsAvailable || bagAnimals.Selected<0)return;
        string id=AnimalCatalog.All[bagAnimals.Selected].Id;if(AvailableAnimal(id)<=0)return;
        var kind=AnimalCatalog.Find(id)!;
        // Keep ownership in the bag until training succeeds. Dropped/held actors reserve one entry.
        // Leaving or loading returns these temporary views to the bag, never creating extra animals.
        var animal=new TrainingAnimal { Skill=Enum.Parse<AnimalSkill>(kind.Skill),IsParrot=id=="parrot",IsGorilla=id=="gorilla",InventorySpecies=id,
            Position=HandPosition(),Home=player.Position };
        world.AddChild(animal);heldAnimal=animal;BeginPickup(animal.Position);showBag=false;
        Notify($"Face a Chao to offer the {kind.Name}. Unused animals return to your bag when you leave.");
    }
    private void ConsumeBagAnimal(TrainingAnimal animal)
    {
        if(animal.InventorySpecies is {} id && !pet.Wildlife.Take(id))throw new InvalidOperationException("Animal ownership was lost before training.");
    }
}
