using System;
using System.IO;
using Mori;

int checks = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new Exception(label);
    Console.WriteLine("PASS " + label); checks++;
}
var pet = new PetState { Fullness = 90, Energy = 90 };
Check(pet.Feed() && pet.Fullness == 100, "Food clamps at 100");
Check(!pet.Feed() && pet.Care == 1, "Full pet cannot farm care by feeding");
pet.Tick(10000, false);
Check(pet.Fullness == 0 && pet.Energy == 0 && pet.Joy == 0, "Needs never become negative");
pet.Tick(1000, true);
Check(pet.Energy == 100, "Sleeping restores energy up to 100");
pet = new PetState { Care = 9 };
Check(!pet.Evolve(), "Evolution requires full growth");
pet.Pet(); Check(pet.Evolve() && pet.Form == "bloom", "Care creates Bloom");
Check(!pet.Evolve(), "Evolution triggers only once");
pet = new PetState();
for (int i = 0; i < 4; i++) pet.CompleteRace(8);
Check(pet.Evolve() && pet.Form == "comet" && pet.Stars == 32, "Races create Comet and award stars");
pet.CompleteRace(0);
Check(pet.BestScore == 8 && pet.Agility == 12, "Empty race gives no agility and preserves record");
var dir = Path.Combine(Path.GetTempPath(), "MoriTests-" + Guid.NewGuid());
var path = Path.Combine(dir, "pet.json");
try
{
    SaveStore.Save(path, pet);
    var restored = SaveStore.Load(path, out var warning);
    Check(warning == "" && restored.Form == "comet" && restored.Stars == 32, "Progress survives save/load");
    pet.Stars = 40; SaveStore.Save(path, pet);
    File.WriteAllText(path, "{broken");
    restored = SaveStore.Load(path, out warning);
    Check(restored.Stars == 32 && warning.Length > 0, "Corrupt save recovers backup");
    File.WriteAllText(path + ".bak", "null");
    restored = SaveStore.Load(path, out warning);
    Check(restored.Form == "seed" && File.Exists(path + ".damaged"), "Unrecoverable save retained for inspection");
    File.WriteAllText(path, "{\"Version\":99}");
    restored = SaveStore.Load(path, out warning);
    Check(warning.Length > 0, "Unsupported version is reported");
}
finally
{
    // Удаляются только файлы, созданные этим тестом, без рекурсивного удаления.
    foreach (var file in Directory.GetFiles(dir)) File.Delete(file);
    Directory.Delete(dir);
}
var egg = PetState.NewEgg();
Check(!egg.CanAct && !egg.AdvanceLife(10), "Egg remains until hatch time");
Check(egg.AdvanceLife(18) && egg.LifeStage == "Child", "Egg hatches while time advances");
var child = new PetState { Care = 10 };
child.TrainWithAnimal("Swim");
Check(!child.AdvanceLife(89) && child.LifeStage == "Child", "Child needs age and growth before evolving");
Check(child.AdvanceLife(1) && child.InCocoon && !child.Feed(), "Growing child enters cocoon");
Check(child.AdvanceLife(9) && child.LifeStage == "Adult" && child.Form == "tide", "Recent animal training shapes adult form");
Console.WriteLine($"{checks} checks passed.");
