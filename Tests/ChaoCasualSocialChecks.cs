using System;
using System.Linq;
using System.Text.Json;
using Mori;

internal static class ChaoCasualSocialChecks
{
    internal static void Run(Action<bool,string> check)
    {
        PetState Pet()=>new(){Personality=new(),PersonalitySchemaVersion=1,Fullness=80,Energy=80,SocialNeed=60,Joy=60};
        var root=Pet();root.EnsureStatisticsTree();var peer=Pet();peer.EnsureStatisticsTree();
        var r=new ChaoCasualSocialRuntime();
        bool Begin(bool x=true,bool y=true,float distance=1,float height=0)=>r.Begin(root,"main",root,x,"peer",peer,y,distance,height);
        check(!Begin() && root.SocialLinks.Count==0,"Casual A no introduction or relationship manufactured");
        var link=root.SocialLink("main","peer",true)!;link.Met=link.Introduced=true;
        check(Begin(),"Casual B known neutral pair starts");r.Cancel("fixture");r.Clock(20);
        link.SetScoreFrom("peer",-1);check(!Begin(),"Casual C recipient directional dislike excluded");link.SetScoreFrom("peer",0);link.SetScoreFrom("main",-1);check(!Begin(),"Casual C initiator dislike excluded");link.SetScoreFrom("main",0);
        check(!Begin(y:false),"Casual D sleeping/protected recipient readiness");
        peer.Energy=34;check(!Begin(),"Casual E recipient energy");peer.Energy=80;root.Energy=34;check(!Begin(),"Casual E initiator energy");root.Energy=80;
        root.SocialNeed=24;check(!Begin(),"Casual F initiator SocialNeed threshold");root.SocialNeed=60;peer.SocialNeed=14;check(!Begin(),"Casual F recipient SocialNeed threshold");peer.SocialNeed=60;
        check(!Begin(distance:2.21f) && !Begin(distance:.44f),"Casual G safe nearby distance bounds");check(!Begin(height:.17f),"Casual H height bounds");
        check(!Begin(y:false),"Casual I rejected physical eligibility cannot reserve a pair");
        root.Fullness=5;check(!Begin(),"Casual critical hunger excluded");root.Fullness=80;
        check(Begin(),"Casual O successful synchronous pair reservation after a failed candidate");check(!Begin(),"Casual W single active executor forbids overlapping participants");
        string persistent=JsonSerializer.Serialize(new{root.History,root.ActivityHabits,root.SocialLinks,root.Personality,root.Joy,PeerHistory=peer.History,PeerHabits=peer.ActivityHabits});
        float xNeed=root.SocialNeed,yNeed=peer.SocialNeed;int steps=0;
        while(r.Active && steps++<1000)r.Advance(.02f,.8f);
        check(!r.Active && r.Successful==1 && r.Elapsed is >=3 and <7 && root.SocialNeed==xNeed-3 && peer.SocialNeed==yNeed-3,"Casual P one successful physical-timing completion relieves exactly three per participant");
        r.Advance(100,.8f);check(r.Successful==1 && root.SocialNeed==xNeed-3,"Casual R duplicate end cannot award twice");
        check(JsonSerializer.Serialize(new{root.History,root.ActivityHabits,root.SocialLinks,root.Personality,root.Joy,PeerHistory=peer.History,PeerHabits=peer.ActivityHabits})==persistent,"Casual S/T/U/V relationships, memories, habits, thirteen traits and Joy unchanged");
        check(root.SocialSelection.LastCompletedPartner=="peer" && peer.SocialSelection.LastCompletedPartner=="main","Casual successful partner marker preserves existing repeat preference");
        check(r.PairRemaining("main","peer")==180 && r.PairRemaining("peer","main")==180 && r.ActorRemaining("main")==60,"Casual L/M stable unordered pair and actor cooldowns");
        r.Clock(81);check(!Begin(),"Casual pair cooldown outlasts actor cooldown");r.Clock(201);check(Begin(),"Casual pair eligible after active-time interval");
        xNeed=root.SocialNeed;r.Cancel("pickup");check(root.SocialNeed==xNeed && r.Cancelled==2 && r.ActorRemaining("peer")==15 && r.PairRemaining("peer","main")==15,"Casual N/Q cancel gives no relief and short retry cooldown");
        r.Clock(217);check(Begin(),"Casual cancellation eventually releases both actors");
        var old=root;var updated=JsonSerializer.Deserialize<PetState>(JsonSerializer.Serialize(root))!;root=updated;r.Rebind("main",updated);xNeed=updated.SocialNeed;float oldNeed=old.SocialNeed;
        while(r.Active)r.Advance(.02f,.8f);
        check(updated.SocialNeed==xNeed-3 && old.SocialNeed==oldNeed,"Casual X runtime rebind uses current state without replay");
        string saved=JsonSerializer.Serialize(updated);check(!saved.Contains("GreetingCooldown") && !saved.Contains("CurrentConversation") && !saved.Contains("CasualSocial"),"Casual Y no saved social-executor fields");
        check(link.Met && link.Introduced && root.SocialLinks.Count==1,"Casual Z first-introduction flags remain untouched");
        var timeout=new ChaoCasualSocialRuntime();check(timeout.Begin(root,"main",root,true,"peer",peer,true,2,0),"Casual timeout fixture starts");for(int i=0;i<300;i++)timeout.Advance(.02f,2);
        check(!timeout.Active && timeout.Cancelled==1 && timeout.LastCancellationReason=="Approach timeout","Casual blocked approach cannot last more than three seconds");
        var hard=new ChaoCasualSocialRuntime();hard.Begin(root,"main",root,true,"peer",peer,true,1,0);hard.Advance(10.1f,1,prepared:false);check(!hard.Active && hard.Cancelled==1,"Casual rising/preparation obeys hard ten-second limit");
        var selected=new[]{new SocialPartnerCandidate("white",1),new SocialPartnerCandidate("black",1)};
        foreach(string mode in new[]{"Neutral","Preferred","Unavailable"})
        {
            var pet=Pet();var white=pet.SocialLink("main","white",true)!;white.Met=white.Introduced=true;white.SetScoreFrom("main",mode=="Neutral" ? 0 : 65);
            var black=pet.SocialLink("main","black",true)!;black.Met=black.Introduced=true;black.SetScoreFrom("main",0);
            if(mode!="Neutral")pet.History.Events.Add(new(){Kind=ChaoMemoryKind.Pleasant,Source="real-prior-fixture",Area="garden",Description="Past joint activity.",Importance=1,Valence=.65f,Participants=new[]{"white"}});
            int count=0;var candidates=selected.Select(c=>c with{Available=c.Id!="white" || mode!="Unavailable"}).ToArray();
            for(int seed=0;seed<10000;seed++)
            {pet.SocialSelection=new(new Random(seed));if(pet.SocialSelection.Select(pet,"main",pet,candidates,"CasualGreeting")=="white")count++;}
            var scores=ChaoSocialPartnerScorer.Candidates(pet,"main",pet,candidates);double expected=scores[0].FinalWeight/scores.Sum(s=>(double)s.FinalWeight);
            check(Math.Abs(count/10000d-expected)<.015 && (mode=="Unavailable" ? count==0 : count is >4000 and <6500),"Casual J/K 10000 independent weighted choices "+mode+": "+count+" expected "+expected.ToString("F4"));
            if(mode=="Preferred")
            {pet.SocialSelection.LastCompletedPartner="white";check(ChaoSocialPartnerScorer.Candidates(pet,"main",pet,candidates).Single(s=>s.Id=="white").RecentMultiplier==.85f,"Casual existing repeat penalty remains active");}
        }
    }
}

