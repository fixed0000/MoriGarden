using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mori;

internal sealed class ChaoInterests
{
    private enum Stage { Idle, Approach, Idea, Play, Meal, Shake }
    private Stage stage;
    private ToyCar? toy;
    private FruitTree? fruit;
    private bool tree;
    private bool favorite;
    private float delay=5, elapsed, gait;
    private FruitMeal meal=new();
    private string? mealGiver;
    internal void ContinueGivenMeal(FruitTree food,FruitMeal progress)
    {
        fruit=food; toy=null; tree=false; stage=Stage.Meal; elapsed=0; meal=progress; mealGiver=PetState.CurrentHero;
    }

    public void Reconsider() => delay=0;

    public bool Busy => stage!=Stage.Idle;
    public bool HasMeal => stage==Stage.Meal;

    public FruitTree? TakeMeal(Node3D art,PetState chao)
    {
        if (!HasMeal) return null;
        var taken=fruit;
        chao.ChangeRelationship(-.65f);
        Finish(art);
        return taken;
    }

    public bool InterruptMeal(CharacterBody3D actor,Node3D art)
    {
        if (!HasMeal || fruit==null) return false;
        var at=actor.Position+actor.Basis.Z*.55f;
        var floor=actor.GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(at+Vector3.Up*.5f,at-Vector3.Up,1));
        fruit.Place((floor.Count>0 ? (Vector3)floor["position"] : at)+Vector3.Up*.18f);
        Finish(art); return true;
    }

    public void Cancel(CharacterBody3D actor,Node3D art)
    {
        if (stage==Stage.Idle) return;
        if (HasMeal) fruit?.Release(Vector3.Zero);
        if (stage==Stage.Play) toy?.CancelPlay(actor);
        Finish(art);
    }

    public bool Step(CharacterBody3D actor,Node3D art,PetState chao,ChaoMotion motion,float dt,
        bool canStart,IEnumerable<FruitTree> fruits,IEnumerable<ToyCar> toys,out bool changed)
    {
        changed=false;
        if (stage==Stage.Idle)
        {
            delay=Mathf.Max(0,delay-dt);
            if (!canStart || delay>0) return false;
            delay=6;
            Choose(actor,chao,fruits,toys);
            if (stage==Stage.Idle) return false;
        }
        elapsed+=dt;
        var orb=Orb(art);
        switch (stage)
        {
            case Stage.Approach:
            case Stage.Idea:
                if (!Available(actor,chao) || elapsed>24) { Finish(art); return false; }
                Vector3 target=toy?.Position ?? (tree ? fruit!.Position : fruit!.FruitPosition);
                Vector3 offset=target-actor.Position;
                offset.Y=0;
                float reach=tree ? 1.48f : toy?.IsInstrument==true ? .28f : .51f;
                if (stage==Stage.Approach)
                {
                    if (offset.Length()>reach || motion.Airborne)
                    {
                        bool wet=motion.IsSwimmingAt(actor.Position);
                        bool moving=motion.Step(actor,ChaoMotion.AvoidNeighbors(actor,offset),chao,false,wet,dt,true,out _);
                        gait+=dt*Mathf.Clamp(motion.GroundSpeed*9,4,19);
                        ImportedChao.MovePose(art,gait,moving,wet,chao.Swim>=100,motion.Sprinting,motion.Airborne,motion.Flying);
                        return true;
                    }
                    Face(actor,offset);
                    Next(Stage.Idea);
                }
                if (offset.Length()>reach+.20f || motion.IsSwimmingAt(actor.Position))
                {
                    ImportedChao.EndThink(art);
                    orb?.SetIdea(null,false);
                    Next(Stage.Approach);
                    return true;
                }
                if (elapsed<ChaoThought.Minimum+2.5f)
                {
                    ImportedChao.IdeaPose(art);
                    orb?.SetIdea(ChaoThought.Glyph(elapsed),ChaoThought.Inspired(elapsed));
                    return true;
                }
                if (elapsed<ChaoThought.IdeaEnd)
                {
                    ImportedChao.IdeaPose(art,1-Mathf.SmoothStep(0,1,(elapsed-ChaoThought.Minimum-2.5f)/.20f));
                    orb?.SetIdea("",true);
                    return true;
                }
                ImportedChao.EndThink(art);
                Face(actor,offset);
                ImportedChao.MovePose(art,0,false,false,false,false,false,false);
                orb?.SetIdea("",true);
                if (toy!=null)
                {
                    if (!toy.BeginPlay(actor,chao)) { Finish(art); return false; }
                    Next(Stage.Play);
                    toy.PlayPose(actor,art);
                }
                else if (tree)
                {
                    fruit!.Shake();
                    Next(Stage.Shake);
                }
                else
                {
                    fruit!.Hold(target);
                    meal.Begin(fruit,chao,target);
                    Next(Stage.Meal);
                }
                return true;
            case Stage.Play:
                if (GodotObject.IsInstanceValid(toy) && toy!.PlayedBy(actor)) return true;
                Finish(art);
                return false;
            case Stage.Shake:
                if (elapsed<FruitTree.ShakeDuration)
                {
                    ImportedChao.ReceivePose(art,.82f+.13f*Mathf.Sin(elapsed*22));
                    return true;
                }
                Finish(art);
                return false;
            case Stage.Meal:
                var mouth=actor.Position+new Vector3(0,.225f,.135f).Rotated(Vector3.Up,actor.Rotation.Y);
                int previousBites=fruit!.Bites;
                meal.Advance(dt,fruit!,chao,art,mouth);
                if (mealGiver!=null && fruit.Bites>previousBites) chao.ChangeRelationship(.12f*(fruit.Bites-previousBites),mealGiver);
                if (!meal.Rejected && fruit!.Bites<FruitTree.BiteCount) return true;
                if (meal.Rejected) meal.ThrowBehind(fruit!,actor,art);
                else fruit!.Regrow();
                Finish(art);
                changed=true;
                return true;
            default:
                return false;
        }
    }

    private void Choose(CharacterBody3D actor,PetState chao,IEnumerable<FruitTree> fruits,IEnumerable<ToyCar> toys)
    {
        float best=0;
        foreach (var item in toys)
        {
            if (!item.Available(actor,chao)) continue;
            bool preferred=item.IsFavorite(chao);
            float score=(100-chao.Fun)*(preferred ? 2.15f : 1)/
                (1+actor.Position.DistanceTo(item.Position)*.19f);
            if (score<=best) continue;
            best=score; toy=item; fruit=null; favorite=preferred; tree=false;
        }
        foreach (var food in fruits)
        {
            bool preferred=IsFavorite(food,chao);
            bool loose=FoodAvailable(food,actor,chao);
            bool canShake=(!food.IsSpecial || preferred) && !loose && chao.Power>=100 && chao.Fullness<72 && food.CanShake &&
                actor.Position.DistanceTo(food.Position)<7;
            if (!loose && !canShake) continue;
            float distance=actor.Position.DistanceTo(loose ? food.FruitPosition : food.Position);
            float score=(100-chao.Fullness)*(loose ? 1.15f : .74f)*(preferred ? 2.15f : 1)/(1+distance*.19f);
            if (score<=best) continue;
            best=score; fruit=food; toy=null; favorite=preferred; tree=canShake;
        }
        if (best>0) Next(Stage.Approach);
    }

    private static bool IsFavorite(FruitTree fruit,PetState chao) =>
        chao.FavoriteFruit==fruit.Kind;

    private static bool FoodAvailable(FruitTree food,CharacterBody3D actor,PetState chao)
    {
        bool favorite=IsFavorite(food,chao);
        return (!food.IsFeeder || food.SingleUse) && (!food.IsSpecial || favorite) && food.HasFruitOnGround &&
            (chao.FindsFood ? chao.Fullness<76 : chao.Fullness<45 && !food.IsHeartFruit) &&
            actor.Position.DistanceTo(food.FruitPosition)<(favorite ? 12 : 8);
    }

    private bool Available(CharacterBody3D actor,PetState chao) =>
        GodotObject.IsInstanceValid(toy) ? toy!.Available(actor,chao) :
        GodotObject.IsInstanceValid(fruit) && (tree ? fruit!.CanShake : FoodAvailable(fruit!,actor,chao));

    private void Finish(Node3D art)
    {
        if (stage is Stage.Meal or Stage.Shake) ImportedChao.PauseAfterAction(art);
        delay=favorite ? 5 : 11;
        Next(Stage.Idle);
        toy=null;
        fruit=null;
        mealGiver=null;
        tree=favorite=false;
        Orb(art)?.SetMealUpset(false);
        Orb(art)?.SetMealHappy(false);
        Orb(art)?.SetIdea(null,false);
        ImportedChao.EndThink(art);
    }

    private void Next(Stage next) { stage=next; elapsed=0; }

    private static EmotionOrb? Orb(Node3D art) => art.GetNodeOrNull<EmotionOrb>("MoodOrb");

    private static void Face(CharacterBody3D actor,Vector3 direction)
    {
        if (direction.LengthSquared()>.001f)
            actor.Rotation=new(0,Mathf.Atan2(direction.X,direction.Z),0);
    }
}
