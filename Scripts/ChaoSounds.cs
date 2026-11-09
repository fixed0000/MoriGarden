using Godot;

namespace Mori;

internal static class ChaoSounds
{
    private static readonly AudioStreamWav Bite=Make(540,380,.12f,true);
    private static readonly AudioStreamWav Mood=Make(880,1440,.18f,false);
    private static readonly AudioStreamWav Rattle=Make(170,130,.105f,true);

    public static void Play(Node3D owner,string cue)
    {
        var audio=owner.GetNodeOrNull<AudioStreamPlayer3D>("ChaoCue");
        if (audio==null)
        {
            audio=new AudioStreamPlayer3D { Name="ChaoCue",VolumeDb=-11,MaxDistance=15 };
            owner.AddChild(audio);
        }
        audio.Stream=cue=="bite" ? Bite : cue=="rattle" ? Rattle : Mood;
        audio.Play();
    }

    private static AudioStreamWav Make(float start,float end,float duration,bool noisy)
    {
        const int rate=22050;
        byte[] data=new byte[(int)(rate*duration)*2];
        var random=new System.Random(204);
        for (int i=0;i<data.Length/2;i++)
        {
            float t=(float)i/rate;
            float fade=Mathf.Min(1,t*250)*Mathf.Pow(Mathf.Max(0,1-t/duration),2);
            float tone=Mathf.Sin(Mathf.Tau*(start*t+(end-start)*t*t/(2*duration)));
            float noise=noisy ? ((float)random.NextDouble()*2-1)*.62f : 0;
            short sample=(short)((tone*.45f+noise)*fade*8400);
            data[2*i]=(byte)sample;
            data[2*i+1]=(byte)(sample>>8);
        }
        return new AudioStreamWav { MixRate=rate,Format=AudioStreamWav.FormatEnum.Format16Bits,Data=data };
    }
}
