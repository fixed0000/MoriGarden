using System;
using System.Linq;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private void CheckSocialAndAnimationCorrections()
    {
        introductions.Cancel(); musicGroup.Cancel(); ballGame.Cancel();
        moriInterests.Cancel(friend,friendArt); moriLeisure.Cancel(friendArt); moriFruitReaction.Interrupt(friendArt);
        moriRest.Cancel(); moriIslandNap.Cancel(friendArt); moriPillar.Cancel(); moriDistress.Reset();
        sleeping=carrying=moriInFlight=swimmingTrip=false; callTime=pettingTime=releasePose=0;
        chaoMotion.Reset(); FinishOffer();
        pet=new PetState { Run=80,Fun=20,Energy=90,Fullness=80,Sociability=90 };
        friend.Visible=true; friend.Position=new(22,0,14.4f); friend.Rotation=Vector3.Zero;
        ImportedChao.ApplyDevelopment(friendArt,pet); ImportedChao.StandForAction(friendArt);
        ImportedChao.ClearActionPause(friendArt);
        var guest=new GardenEgg { Data=new GardenEggState { Hatched=true,Chao=new PetState { Run=80,Fun=20 } },Position=new(22,0,15) };
        world.AddChild(guest);
        var food=new FruitTree { IsFeeder=true,SingleUse=true }; world.AddChild(food);
        player.Position=new(22,0,16); Face(guest.Position); heldFruit=food; food.Hold(HandPosition());
        UpdateOffering(.016f);
        if(offeredResident!=guest || offerTime<=0) throw new Exception("Gift skipped the nearest resident for Mori");
        FinishOffer(); offerTime=0;
        friend.Position=new(22,0,15.1f); guest.Position=new(22,0,14.8f);
        UpdateOffering(.016f);
        if(offeredResident!=null || offerTime<=0) throw new Exception("Gift skipped nearby Mori for a farther resident");
        FinishOffer(); offerTime=0; heldFruit=null; world.RemoveChild(food); food.QueueFree();
        var car=world.GetNode<ToyCar>("ToyCar"); var carAt=car.Position;
        car.Position=new(22,0,15.7f);
        if(NearestPickup()!=car) throw new Exception("A nearby object lost priority to a farther Chao");
        car.Position=new(22,0,17.2f);
        if(NearestPickup()!=friend) throw new Exception("A farther toy overrides the nearest Chao");
        car.Position=carAt;
        friend.Position=new(22,0,15); guest.Position=new(24,0,15);
        player.Position=new(28,0,17); ImportedChao.ClearActionPause(friendArt);
        moriLeisure.BeginRoute(0);
        if(!SocialCandidates(true).Any(c=>c.Actor==friend) || !SocialCandidates(true).Any(c=>c.Actor==guest))
            throw new Exception("Walking Chao are still unavailable for shared play");
        var ball=world.GetNode<ToyCar>("BeachBall"); ball.FinishBall();
        var link=pet.SocialLink("main",guest.Data.Id,true)!; link.Met=link.Introduced=true;
        if(!ball.BeginPlay(friend,pet)) throw new Exception("Leader cannot start ball play");
        bool tossing=false;
        for(int i=0;i<2000;i++)
        {
            var free=SocialCandidates(true);
            ballGame.Advance(1f/60,pet,BallParticipants(),free,new[]{ball},PauseForSocial);
            if(ballGame.Stage=="Toss" && ballGame.Active) { tossing=true; break; }
        }
        if(!tossing) throw new Exception("The garden's actual candidate selector never started shared ball play");
        ballGame.Cancel(); moriLeisure.Cancel(friendArt);
        friend.Position=new(22,0,15); ImportedChao.ClearActionPause(friendArt);
        var quick=new ChaoGreeting(); ImportedChao.StandForAction(friendArt);
        ImportedChao.MovePose(friendArt,0,false,false,false,false,false,false);
        quick.Begin(friendArt); friend.Rotation=Vector3.Zero;
        bool ready=false; float footRange=0,firstFoot=0;
        for(int i=0;i<30;i++)
        {
            ready=quick.Step(friend,friendArt,friend.Position+Vector3.Forward,1f/60);
            var foot=(Node3D)friendArt.FindChild("FootL",true,false);
            if(i==0)firstFoot=foot.Rotation.X;
            footRange=Mathf.Max(footRange,Mathf.Abs(firstFoot-foot.Rotation.X));
        }
        if(!ready || footRange<.05f) throw new Exception("Gift greeting is too slow or turns without stepping");
        ImportedChao.ApplyDevelopment(friendArt,new PetState { Run=0 });
        for(int i=0;i<25;i++)
        {
            ImportedChao.MovePose(friendArt,i*.15f,true,false,false,false,false,false);
            var foot=(Node3D)friendArt.FindChild("FootL",true,false);
            if(Mathf.Abs(foot.Rotation.X)>.001f) throw new Exception("Crawling feet still kick up and down");
        }
        ImportedChao.StandForAction(friendArt); ImportedChao.ApplyDevelopment(friendArt,pet);
        ImportedChao.MovePose(friendArt,0,false,false,false,false,false,false);
        var paw=(Node3D)friendArt.FindChild("PaddleL",true,false); var size=paw.Scale;
        var drums=world.GetNode<ToyCar>("Drums"); drums.CancelPlay(friend);
        if(!drums.BeginPlay(friend,new PetState { Fun=20 })) throw new Exception("Drums cannot start");
        for(int i=0;i<100;i++)
        {
            drums.Advance(1f/60); drums.PlayPose(friend,friendArt);
            if(paw.Scale.DistanceTo(size)>.001f) throw new Exception("Instrument grip enlarges the Chao's paw");
        }
        drums.CancelPlay(friend); world.RemoveChild(guest); guest.QueueFree();
        ImportedChao.ClearActionPause(friendArt);
        GD.Print("INTERACTION_OK: real shared-play selection, nearest gifts, quick stepping turn, crawl feet, drum paw size");
    }
}
