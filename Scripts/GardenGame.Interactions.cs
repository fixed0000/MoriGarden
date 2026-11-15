using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private GodotObject? NearestPickup()
    {
        var options=new List<(GodotObject Target,Vector3 At)>();
        if(NearbyToy() is {} toy) options.Add((toy,toy.Position));
        if(NearbyEgg() is {} egg) options.Add((egg,egg.Position));
        if(NearbyAnimal() is {} animal) options.Add((animal,animal.Position));
        if(zone==GardenZone.Garden && NearbyTree() is {} tree)
            options.Add((tree,tree.HasFruitOnGround ? tree.FruitPosition : tree.Position));
        if(friend.Visible && (pet.CanAct || pet.IsEgg) && !courtship.IsPaired(friend) && MoriNearby(pet.IsEgg ? 2.6f : 2.2f))
            options.Add((friend,friend.Position));
        return options.OrderBy(item=>player.Position.DistanceSquaredTo(item.At)).Select(item=>item.Target).FirstOrDefault();
    }
}
