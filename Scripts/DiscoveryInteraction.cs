namespace Mori;

// Optional physical response attached to an ordinary discovered object, never a second movement planner.
internal interface IDiscoveryInteraction
{
    DiscoveryPlan Prepare(DiscoveryPlan plan,ExpeditionCompanion pet);
    float InspectSeconds(PetState state);
    bool Inspected(ExpeditionCompanion pet,bool requested);
    void Pose(ExpeditionCompanion pet,float time);
    Godot.Vector3? ApproachPoint { get; }
}
