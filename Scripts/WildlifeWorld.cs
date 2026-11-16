using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mori;

internal partial class WildlifeWorld : Node3D
{
    internal PetState SaveData { get; set; }=null!;
    internal List<WildAnimal> Animals { get; }=new();
    internal Action SaveRequested { get; set; }=()=>{};
    internal Action<string> Message { get; set; }=_=>{};
    private readonly Random random=new(582);
    private float respawnCheck;
    internal bool Unlocked(WildlifeHabitat habitat)
    {
        string region=WorldCatalog.Default.Areas.TryGetValue(habitat.Area,out var area) ? area.Region ?? area.Id : habitat.Area;
        return habitat.RequiredFlag==null || SaveData.Expedition.AreaFlags.TryGetValue(region,out var flags) && flags.Contains(habitat.RequiredFlag);
    }
    internal Vector3? SafeHome(WildAnimal animal,ExpeditionNavigation nav)
    {
        for(int i=0;i<40;i++)
        {
            var point=nav.Nearest(animal.Home+new Vector3(Mathf.Cos(i*2.4f),0,Mathf.Sin(i*2.4f))*(i/8f));
            if(ExpeditionCompanion.Flat(point,animal.Home)<animal.Habitat.Radius && nav.Safe(point) && Animals.All(a=>a==animal || !a.Present || ExpeditionCompanion.Flat(point,a.Position)>.8f))return point;
        }
        return null;
    }
    internal Vector3 ChoosePoint(WildAnimal animal,ExpeditionNavigation nav,Vector3? preferred)
    {
        for(int i=0;i<20;i++)
        {
            float angle=(float)random.NextDouble()*Mathf.Tau,radius=Mathf.Sqrt((float)random.NextDouble())*animal.Habitat.Radius;
            var raw=i==0 && preferred!=null ? preferred.Value : animal.Home+new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*radius;
            var point=nav.Nearest(raw);
            if(ExpeditionCompanion.Flat(point,animal.Home)>animal.Habitat.Radius+1 || !nav.Safe(point) || nav.Path(animal.Position,point).Length==0)continue;
            return point;
        }
        return animal.Position;
    }
    internal bool VisibleTo(Vector3 eye,WildAnimal animal)
    {
        var hit=GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(eye,animal.GlobalPosition+Vector3.Up*.2f,1));return hit.Count==0;
    }
    internal WildAnimal? Nearby(CharacterBody3D player)=>Animals.Where(a=>a.Present && a.Species.Obtainable &&
        ExpeditionCompanion.Flat(a.Position,player.Position)<1.85f && Mathf.Abs(a.Position.Y-player.Position.Y)<1.2f &&
        player.GlobalBasis.Z.Dot((a.Position-player.Position).Normalized())>.1f && VisibleTo(player.Position+Vector3.Up*.8f,a))
        .OrderBy(a=>player.Position.DistanceSquaredTo(a.Position)).FirstOrDefault();
    internal string Hint(CharacterBody3D player)
    {
        var animal=Nearby(player);if(animal==null)return "";
        return animal.Fleeing ? "Let the animal settle" : animal.ReadyToJoin ? $"E: Befriend {animal.Species.Name} · Animal bag {SaveData.Wildlife.Total}/{WildlifeProgress.Capacity}" :
            $"E: Greet {animal.Species.Name} · Approach slowly and stay nearby   F: Ask a Chao";
    }
    internal bool Interact(CharacterBody3D player)
    {
        var animal=Nearby(player);if(animal==null)return false;
        SaveData.Wildlife.Meet(animal.Species.Id);
        if(animal.Fleeing){Message("Give the animal a little space to settle.");return true;}
        if(new Vector2(player.Velocity.X,player.Velocity.Z).Length()>4.3f){Message("Slow down and let the animal get to know you.");return true;}
        if(!animal.ReadyToJoin){Message($"{animal.Species.Name} is watching you. Stay calm nearby, or let a Chao say hello.");return true;}
        if(animal.Species.RequiresFamiliarity && SaveData.Wildlife.Discoveries.GetValueOrDefault(animal.Species.Id)?.Met!=true || animal.Species.RequiredFood!=null)
        {Message("This animal needs a different introduction.");return true;}
        if(!SaveData.Wildlife.Obtain(animal.Habitat.Id,animal.Species.Id,animal.Generation))
        {Message("Your animal bag is full. Bring some animals to the garden first.");return true;}
        var effect=new Node3D { Position=animal.Position };GetParent().AddChild(effect);
        GardenEgg.ShellBurst(effect,Colors.White,sparkles:true);ChaoSounds.Play(effect,"mood");
        var fade=effect.CreateTween();fade.TweenInterval(1.2);fade.TweenCallback(Callable.From(effect.QueueFree));
        animal.SetPresent(false);SaveRequested();Message($"{animal.Species.Name} joined you! B: Inventory · Bring it to a Chao in the garden.");return true;
    }
    internal bool Ask(CharacterBody3D player,ExpeditionTeam team)
    {
        var animal=Nearby(player);if(animal==null)return false;
        foreach(var pet in team.Members.OrderBy(m=>m.Body.Position.DistanceSquaredTo(animal.Position)))
            if(team.Discoveries.Request(pet,animal.Interest)){Message($"{pet.Pet.State.Name} is going to greet the {animal.Species.Name}.");return true;}
        Message("Your Chao need time or rest. You can make friends on your own.");return true;
    }
    internal void Step(float dt,CharacterBody3D player,Camera3D camera,ExpeditionTeam team)
    {
        respawnCheck-=dt;
        foreach(var animal in Animals)
        {
            var slot=SaveData.Wildlife.Slot(animal.Habitat.Id);
            if(!animal.Present && respawnCheck<=0 && Unlocked(animal.Habitat))
            {
                var safe=SafeHome(animal,team.Navigation);
                if(safe!=null)
                {
                    var point=safe.Value;
                    bool hidden=ExpeditionCompanion.Flat(point,player.Position)>9 &&
                        (camera.IsPositionBehind(point) || !camera.IsPositionInFrustum(point+Vector3.Up*.2f) || !VisibleAt(camera.GlobalPosition,point));
                    if(!slot.Collected && hidden || SaveData.Wildlife.Respawn(animal.Habitat.Id,hidden,true))
                    {animal.Respawn(point,slot.Generation);SaveRequested();}
                }
            }
            if(animal.Present)animal.Step(dt,player,team,this);
            if(team.Members.Any(m=>SaveData.Expedition.Memory(m.Pet.Id).Find(animal.Interest.Definition.Id)!=null))SaveData.Wildlife.Meet(animal.Species.Id);
        }
        if(respawnCheck<=0)respawnCheck=2;
    }
    private bool VisibleAt(Vector3 eye,Vector3 point)=>GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(eye,point+Vector3.Up*.2f,1)).Count==0;
}
