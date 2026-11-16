using System;
using System.Linq;
using System.Numerics;
using Mori;

internal static class StadiumMotionChecks
{
    internal static void Run(Action<bool,string> check)
    {
        (float Time,float Swim,float Climb,bool[] Stages,float Jump,int Gates) Traverse(string key,int fly=650,int power=650,int swim=650)
        {
            var pet=new PetState{Run=450,Fly=fly,Power=power,Swim=swim,Stamina=990,Luck=990,Energy=100};
            var race=new ChaoRace(SportsCatalog.Find("race-"+key+"-1")!,new[]{new RaceEntrant("a","Green",pet,8),new RaceEntrant("b","Blue",SaveStore.Snapshot(pet),9)});
            var stages=new bool[Enum.GetValues<RaceObstaclePhase>().Length];float inWater=0,climb=0,jump=0;var a=race.Entrants[0];
            while(!race.Finished)
            {
                var old=a.Traversal.Position;float distance=a.Distance;bool frozen=a.Traversal.OwnsMovement && a.Traversal.Phase==RaceObstaclePhase.Prepare;
                race.Step(1f/60);stages[(int)a.Traversal.Phase]=true;
                if(frozen && a.Traversal.Phase==RaceObstaclePhase.Prepare && a.Distance!=distance)throw new Exception("Preparation advanced race progress");
                if(a.Traversal.Phase==RaceObstaclePhase.Swim)inWater+=1f/60;if(a.Traversal.Phase==RaceObstaclePhase.Climb)climb+=1f/60;
                jump=Math.Max(jump,Vector3.Distance(old,a.Traversal.Position));
            }
            return(a.FinishedAt,inWater,climb,stages,jump,a.Traversal.CompletedActions);
        }
        var pool=Traverse("crab-pool");var weakSwim=Traverse("crab-pool",swim:100);
        check(pool.Stages[(int)RaceObstaclePhase.EnterWater] && pool.Stages[(int)RaceObstaclePhase.LeaveWater],"Pool entrants own timed entry and exit actions");
        check(weakSwim.Swim>pool.Swim*2 && weakSwim.Time>pool.Time,"Weak swimming costs real race time, including shore transitions");
        var flight=Traverse("stump-valley");var weakFlight=Traverse("stump-valley",fly:30);
        check(flight.Stages[(int)RaceObstaclePhase.Prepare] && flight.Stages[(int)RaceObstaclePhase.Takeoff] && flight.Stages[(int)RaceObstaclePhase.Glide] && flight.Stages[(int)RaceObstaclePhase.Land],"A good flier prepares, jumps, glides and lands before running");
        check(weakFlight.Stages[(int)RaceObstaclePhase.FailedFlight] && weakFlight.Stages[(int)RaceObstaclePhase.Climb] && weakFlight.Stages[(int)RaceObstaclePhase.Mount],"A failed flight descends, recovers and climbs the actual stump");
        check(weakFlight.Time>flight.Time+2 && weakFlight.Gates>flight.Gates,"A fallback route has a measurable time cost and extra actions");
        var canyon=Traverse("block-canyon");var weakPower=Traverse("block-canyon",power:50);
        check(canyon.Gates==4 && canyon.Stages[(int)RaceObstaclePhase.Mount],"Both canyon walls require preparation and a top-out action");
        Console.WriteLine($"RACE_MOTION_TIMES: Swim 650/100={pool.Swim:0.00}/{weakSwim.Swim:0.00}s; Fly 650/30 total={flight.Time:0.00}/{weakFlight.Time:0.00}s; Power 650/50 climb={canyon.Climb:0.00}/{weakPower.Climb:0.00}s");
        check(weakPower.Climb>canyon.Climb*2 && weakPower.Time>canyon.Time+4,"Power changes actual vertical climbing time, not an animation multiplier");
        check(new[]{pool,weakSwim,flight,weakFlight,canyon,weakPower}.All(t=>t.Jump<.13f),"Physical racer trajectories have no instantaneous wall, shore or landing jumps");
        var valley=new RaceCourse(SportsCatalog.Find("race-stump-valley-1")!);float start=valley.Lengths.Take(2).Sum();
        bool wall=true;for(int lane=0;lane<8;lane++){var pos=valley.Position(start+valley.Lengths[2]*.85f,(lane-3.5f)*.55f,new(){Fly=30});float radius=Vector2.Distance(new(pos.X,pos.Z),new(valley.Anchors[3].X,valley.Anchors[3].Z));wall&=radius>3.16f && radius<3.22f;}
        check(wall,"Every fallback lane follows the stump's curved physical front face");
        var racer=new RaceEntrant("a","Blue",new(){Power=700,Run=500,Fly=700,Stamina=990,Luck=990,Energy=100},1);var race2=new ChaoRace(SportsCatalog.Find("race-block-canyon-1")!,new[]{racer,new RaceEntrant("b","White",SaveStore.Snapshot(racer.Pet),2)});
        while(!racer.Traversal.OwnsMovement)race2.Step(.02f);check(!race2.Cheer("a"),"Preparation and mounting cannot be skipped by cheering");
        race2.Trip(racer);float at=racer.Distance;race2.Step(.9f);check(at==racer.Distance && racer.Action==RaceAction.Prone,"A fall pauses both the obstacle controller and race progress");

        ChaoKarate Strike(Vector2 target)
        {
            var a=new KarateFighter("a","Green",new(){Power=700},Vector2.Zero,3){Facing=Vector2.UnitX,Action=KarateAction.Attack,Duration=1,ActionTime=.40f};
            var b=new KarateFighter("b","Blue",new(){Swim=990,Stamina=990},target,4){Action=KarateAction.Wait,Duration=1000};
            return new(a,b);
        }
        var hit=Strike(new(.35f,0));hit.Step(3.04f);check(hit.A.Hits==0,"A windup cannot damage before the visible impact phase");hit.Step(.04f);
        check(hit.A.Hits==1 && Vector2.Distance(hit.A.ImpactPoint,hit.B.Position)<.15f,"A hit records the same finite tip used by the visual animation");
        var side=Strike(new(.30f,.25f));side.Step(3.1f);check(side.A.Hits==0 && side.A.Misses==1,"An opponent outside the limb's lateral reach is missed");
        check(Vector2.Distance(KarateContact.Tip(Vector2.Zero,Vector2.UnitX,0),Vector2.Zero)<.34f,"Miss animation reach is bounded independently of opponent distance");
        var edge=Strike(new(1.18f,0));edge.A.Position=new(.83f,0);edge.Step(4);
        check(edge.Method=="Ring Out" && edge.B.RingFalling && edge.Winner=="a","A real knockback crosses the boundary and identifies the Ring Out victim");
        var before=edge.B.Position;float height=edge.B.OutsideHeight;edge.Step(.016f);
        check(Vector2.Distance(before,edge.B.Position)<.07f && edge.B.OutsideHeight<=height,"Ring Out continues its impact velocity without a presentation teleport");edge.Step(1);
        check(edge.B.OutsideHeight==-.35f,"The Ring Out victim falls to the existing outside surface");
        var recovery=Strike(new(.35f,0));recovery.Step(3.7f);recovery.B.Action=KarateAction.Wait;recovery.B.Duration=1000;bool recovered=false;
        for(int i=0;i<120;i++){recovery.Step(.01f);recovered|=recovery.A.Action==KarateAction.Recover;}
        check(recovered,"An attack has a distinct finite recovery before the next decision");
    }
}
