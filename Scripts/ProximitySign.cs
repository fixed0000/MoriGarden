using Godot;

namespace Mori;

public partial class ProximitySign : Node3D
{
    private Node3D? player;
    public override void _Process(double delta)
    {
        player ??= GetTree().GetFirstNodeInGroup("garden_player") as Node3D;
        Visible = player != null && GlobalPosition.DistanceSquaredTo(player.GlobalPosition)<42;
    }
}
