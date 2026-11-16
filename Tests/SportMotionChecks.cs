using System;
using System.Linq;
using Mori;

internal static class SportMotionChecks
{
    internal static void Run(Action<bool,string> check)
    {
        check(SportMotions.All.Select(c=>c.Id).Distinct().Count()==SportMotions.All.Length,"Sport motion identifiers are distinct");
        check(SportMotions.All.All(c=>c.Source.Contains("mNN-Mwmah0k") && c.SourceEnd>c.SourceStart && c.Seconds>0),"Authored motions retain the actual GameCube source and bounded timing");
        check(SportMotions.All.All(c=>c.Status=="Approximate"),"An authored clip is not automatically labelled Verified");
        check(SportMotions.All.All(c=>c.Keys[0].Phase==0 && c.Keys[^1].Phase==1 && c.Keys.Zip(c.Keys.Skip(1)).All(p=>p.First.Phase<p.Second.Phase)),"Every key sequence has ordered initial and final poses");
        bool finite=true;foreach(var clip in SportMotions.All)foreach(float time in Enumerable.Range(0,1000).Select(i=>i*.0031f).Concat(new[]{-1,float.NaN,float.PositiveInfinity}))finite&=clip.At(time).Finite;
        check(finite,"Sampling, seeking and invalid time input never produce non-finite joint transforms");
        check(SportMotions.All.Where(c=>c.Loop).All(c=>c.Keys[0].Pose==c.Keys[^1].Pose && c.At(0)==c.At(c.Seconds)),"Looping sports cycles join without a pose discontinuity");
        check(SportMotions.All.Where(c=>!c.Loop).All(c=>c.At(c.Seconds)==c.At(c.Seconds+100)),"One-shot motions finish and hold their final pose");
        var punch=SportMotions.Find("karate_punch")!;check(punch.Keys.Any(k=>Math.Abs(k.Phase-KarateContact.ImpactSwing)<.0001f),"The authored strike includes the gameplay contact phase");
        var run=SportMotions.Find("race_run_normal")!;var a=run.At(0);var b=run.At(run.Seconds*.5f);
        check(a.LeftFoot.Offset.Z>0 && a.RightFoot.Offset.Z<0 && b.LeftFoot.Offset.Z<0 && b.RightFoot.Offset.Z>0,"The full running cycle exchanges the leading and trailing foot");
        check(SportMotions.Find("race_stop")==null && SportMotions.Find("karate_meditate")==null,"Unconfirmed motions have no fabricated reconstruction fallback");
    }
}
