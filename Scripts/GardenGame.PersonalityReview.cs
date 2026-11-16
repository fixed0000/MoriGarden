using System;
using System.Linq;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private float personalityReviewTime;
    private int personalityReviewShot=-1;
    private bool personalityReviewReady,personalityInspectReady;
    private int personalityReviewFrame;
    private ImportedChao[] personalityModels=Array.Empty<ImportedChao>();
    private readonly ChaoMotion[] reviewMotions={new(),new(),new()};
    private readonly float[] reviewGaits=new float[3];
    private readonly System.Collections.Generic.HashSet<string> personalityFrames=new();
    private int blinkReviewStage;
    private void SetupPersonalityGarden()
    {
        SetupGreenhouseDemo(true);SwitchZone(GardenZone.Garden,restoring:true);
        if(pet.GardenEggs.FirstOrDefault()?.Chao is {} shy){shy.Sociability=15;shy.Initiative=80;}
        friend.Position=new(1,0,10);player.Position=new(5,0,13);petZone=GardenZone.Garden;friend.Visible=true;pet.Energy=90;
        int index=0;foreach(var resident in world.GetChildren().OfType<GardenEgg>().Where(e=>e.Chao!=null))
        {resident.Position=new(2.3f+index*1.3f,0,10);index++;}
        personalityModels=new[]{(ImportedChao)friendArt}.Concat(world.GetChildren().OfType<GardenEgg>().Where(e=>e.Chao!=null).Select(e=>(ImportedChao)e.Art)).ToArray();
        AdvanceChaoMemories(.02f);paused=pausedByFocus=false;portalCooldown=4;
    }
    private void InspectPersonality()
    {
        if(personalityInspectReady)return;personalityInspectReady=true;
        var observed=SaveStore.LoadSlot(ProjectSettings.GlobalizePath("user://discovery-review/pet.json"));
        if(observed?.History.Events.Count>0)
        {
            pet=observed;petZone=GardenZone.Garden;carrying=false;SwitchZone(GardenZone.Garden,restoring:true);
            friend.Position=new(1,0,10);player.Position=new(5,0,13);friend.Visible=true;
            int index=0;foreach(var resident in world.GetChildren().OfType<GardenEgg>().Where(e=>e.Chao!=null)){resident.Position=new(2.3f+index*1.3f,0,10);index++;}
            AdvanceChaoMemories(.02f);paused=pausedByFocus=false;portalCooldown=4;
        }
        else SetupPersonalityGarden();
        testMode=false;
        Notify("Personality demo · Tab: Chao card · Memories · Three different personalities",12);
        cameraRig.Yaw=0;cameraRig.Pitch=.30f;cameraRig.Distance=6;cameraRig.Follow(player.Position,0,true);
    }
    private void ReviewPersonality(float delta)
    {
        personalityReviewTime+=Mathf.Min(delta,.1f);
        if(!personalityReviewReady){personalityReviewReady=true;SetupPersonalityGarden();mainName.Hide();}
        UpdateUi();
        float blink=personalityModels[0].BlinkAmount;
        if(personalityReviewTime<24 && (blinkReviewStage==0 && blink>.1f || blinkReviewStage==1 && blink>.85f || blinkReviewStage==2 && blink>.1f && blink<.5f))
            PersonalityShot("blink-"+blinkReviewStage++);
        // The first sequence is a visual rig check, followed by locomotion/rest clips at gameplay distance.
        int expressionIndex=(int)(personalityReviewTime/2.4f);
        if(expressionIndex<10)
        {
            camera.GlobalPosition=friend.Position+new Vector3(.45f,.34f,.78f);camera.LookAt(friend.Position+Vector3.Up*.25f);
            if(personalityReviewTime%2.4f>2 && personalityReviewShot!=expressionIndex)
            {personalityReviewShot=expressionIndex;PersonalityShot("face-"+((ChaoExpression)expressionIndex).ToString().ToLowerInvariant());}
        }
        else
        {
            var center=personalityModels.Select(m=>((Node3D)m.GetParent()).Position).Aggregate(Vector3.Zero,(a,b)=>a+b)/personalityModels.Length;
            camera.GlobalPosition=center+new Vector3(4.8f,2.0f,5.5f);camera.LookAt(center+Vector3.Up*.25f);
            int clip=(int)((personalityReviewTime-24)/5);
            float phase=(personalityReviewTime-24)%5;
            for(int frame=0;frame<3;frame++)
            {
                string key=$"motion-{clip}-frame-{frame}";
                if(phase>1+frame*1.25f && personalityFrames.Add(key))PersonalityShot(key);
            }
            int shot=10+clip;
            if((personalityReviewTime-24)%5>3.8f && personalityReviewShot!=shot)
            {personalityReviewShot=shot;PersonalityShot("motion-"+clip);}
        }
        if(personalityReviewTime>49)
        {
            // Memories shown here come from actual visits during this safe inspection, not invented adventures.
            showStats=true;OpenMemories();UpdateUi();
            if(personalityReviewTime>50){PersonalityShot("memories");GD.Print("PERSONALITY_REVIEW_OK: ten facial states, normal-camera gait/turn/stop, sleep/wake/swim poses, individual blinking and memory card captured");GetTree().Quit();}
        }
    }
    private void PersonalityShot(string name)=>GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://artifacts/personality-"+name+".png"));
    private void ReviewPersonalityPhysics(float dt)
    {
        if(!personalityReviewReady)return;
        personalityReviewFrame++;float time=personalityReviewTime;
        foreach(var model in personalityModels)
        {
            model.GetNode<EmotionOrb>("MoodOrb").WaitForPetting();ImportedChao.CloseEyes(model,false);ImportedChao.SmileEyes(model,false);ImportedChao.HalfEyes(model,false);
            ImportedChao.MovePose(model,0,false,false,false,false,false,false);
        }
        if(time<24)
        {
            var expression=(ChaoExpression)Math.Min(9,(int)(time/2.4f));
            foreach(var model in personalityModels){ImportedChao.Express(model,expression);ImportedChao.Attend(model,((Node3D)model.GetParent()).Position+new Vector3(.25f,.35f,2),80);}
        }
        else
        {
            float t=time-24;int clip=(int)(t/5);float phase=t%5;
            foreach(var model in personalityModels)
            {
                var body=(Node3D)model.GetParent();int index=Array.IndexOf(personalityModels,model);
                if(clip==0)
                {
                    var state=index==0 ? pet : pet.GardenEggs[index-1].Chao!;
                    var direction=phase<3 ? Vector3.Back.Rotated(Vector3.Up,Mathf.SmoothStep(1.5f,2.5f,phase)*.8f)*4 : Vector3.Zero;
                    bool walking=reviewMotions[index].Step((CharacterBody3D)body,direction,state,false,false,dt,false,out _);
                    reviewGaits[index]+=reviewMotions[index].GroundSpeed*dt*9;
                    ImportedChao.MovePose(model,reviewGaits[index],walking,false,false,false,false,false);
                }
                else if(clip==1)
                {
                    var runner=new PetState{Run=850};
                    bool running=reviewMotions[index].Step((CharacterBody3D)body,Vector3.Back*8,runner,true,false,dt,false,out _);
                    reviewGaits[index]+=reviewMotions[index].GroundSpeed*dt*9;
                    ImportedChao.MovePose(model,reviewGaits[index],running,false,false,reviewMotions[index].Sprinting,false,false);
                }
                else if(clip==2){if(phase<1.5f)ImportedChao.LieDown(model,phase/1.5f,50);else ImportedChao.SleepPose(model,phase-1.5f,50);}
                else if(clip==3)ImportedChao.StandUpPose(model,Quaternion.FromEuler(new(-Mathf.Pi/2,0,0)),Vector3.Up*.1f,Mathf.Min(phase/1.5f,1));
                else if(clip==4)
                {
                    body.Position=GardenArt.Pond+new Vector3(2+index*.8f,-.04f,0);
                    ImportedChao.MovePose(model,t*10,true,true,index!=1,false,false,false);
                }
            }
        }
    }
}
