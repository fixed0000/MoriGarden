using Godot;

namespace Mori;

internal static class SwimAudio
{
    private static AudioStreamWav? stream, impactStream;

    public static AudioStreamPlayer3D Attach(Node3D actor,bool impact=false)
    {
        var sound=impact ? impactStream ??= MakeSplash(true) : stream ??= MakeSplash(false);
        var player=new AudioStreamPlayer3D { Stream=sound, VolumeDb=impact ? -5 : -13, MaxDistance=impact ? 24 : 14,
            UnitSize=2, Name=impact ? "WaterImpact" : "SwimSplash" };
        actor.AddChild(player);
        return player;
    }

    public static void Paddle(AudioStreamPlayer3D audio,float dt,bool swimming,ref float cooldown)
    {
        cooldown=Mathf.Max(0,cooldown-dt);
        if (!swimming || cooldown>0) return;
        audio.PitchScale=audio.PitchScale>.98f ? .93f : 1.07f;
        audio.Play();
        cooldown=.31f;
    }

    public static void Wade(AudioStreamPlayer3D audio,float distance,bool wet,ref float stride)
    {
        if (!wet) { stride=.28f; return; }
        stride+=distance;
        if (distance<=.001f || stride<.47f) return;
        stride-=.47f;
        audio.PitchScale=audio.PitchScale<1 ? 1.12f : .91f;
        audio.Play();
    }

    private static AudioStreamWav MakeSplash(bool impact)
    {
        const int rate=22050;
        byte[] bytes=new byte[(impact ? rate*3/5 : rate/5)*2];
        var noise=new System.Random(59);
        float smooth=0;
        for (int i=0;i<bytes.Length/2;i++)
        {
            float t=(float)i/rate;
            smooth=Mathf.Lerp(smooth,(float)noise.NextDouble()*2-1,.21f);
            float envelope=Mathf.Clamp(t/.008f,0,1)*Mathf.Exp(-t*(impact ? 9 : 21));
            float plop=impact ? Mathf.Sin(Mathf.Tau*(147*t-118*t*t))*.31f :
                Mathf.Sin(Mathf.Tau*(340*t-490*t*t))*.19f;
            short value=(short)((smooth+plop)*envelope*14000);
            bytes[2*i]=(byte)value;
            bytes[2*i+1]=(byte)(value>>8);
        }
        return new AudioStreamWav { MixRate=rate, Format=AudioStreamWav.FormatEnum.Format16Bits,Data=bytes };
    }
}
