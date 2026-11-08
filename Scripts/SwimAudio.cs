using Godot;

namespace Mori;

internal static class SwimAudio
{
    private static AudioStreamWav? stream;

    public static AudioStreamPlayer3D Attach(Node3D actor)
    {
        stream ??= MakeSplash();
        var player=new AudioStreamPlayer3D { Stream=stream, VolumeDb=-13, MaxDistance=14,
            UnitSize=2, Name="SwimSplash" };
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

    private static AudioStreamWav MakeSplash()
    {
        const int rate=22050;
        byte[] bytes=new byte[rate/5*2];
        var noise=new System.Random(59);
        float smooth=0;
        for (int i=0;i<bytes.Length/2;i++)
        {
            float t=(float)i/rate;
            smooth=Mathf.Lerp(smooth,(float)noise.NextDouble()*2-1,.21f);
            float envelope=Mathf.Clamp(t/.008f,0,1)*Mathf.Exp(-t*21);
            float plop=Mathf.Sin(Mathf.Tau*(340*t-490*t*t))*.19f;
            short value=(short)((smooth+plop)*envelope*14000);
            bytes[2*i]=(byte)value;
            bytes[2*i+1]=(byte)(value>>8);
        }
        return new AudioStreamWav { MixRate=rate, Format=AudioStreamWav.FormatEnum.Format16Bits,Data=bytes };
    }
}
