using Godot;

namespace Mori;

// A common component for every category. Scenery and availability can change independently of the AI.
internal partial class DiscoveryInterest : Node3D
{
    internal DiscoveryDefinition Definition { get; set; }=null!;
    internal IDiscoveryInteraction? Interaction { get; init; }
    internal Node3D? VisibilityRoot { get; init; }
    internal bool Available { get; set; }=true;
    internal Vector3 LookPoint=>GlobalPosition+Vector3.Up*Definition.LookHeight;
    internal bool Usable(PetState owner)=>GodotObject.IsInstanceValid(this) && IsInsideTree() && Available &&
        (Definition.RequiredFlag==null || owner.Expedition.AreaFlags.TryGetValue(Definition.AreaId,out var flags) && flags.Contains(Definition.RequiredFlag));
}
