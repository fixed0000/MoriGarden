using Godot;

namespace Mori;

internal static class ChaoSounds
{
    private static readonly AudioStreamWav Bite=Make(540,380,.12f,true);
    private static readonly AudioStreamWav Mood=Make(880,1440,.18f,false);
    private static readonly AudioStreamWav Rattle=Render(.19f,(t,noise)=>
    {
        float taps=0;
        for (int i=0;i<8;i++)
        {
            float after=t-i*.017f-(i%3)*.003f;
            if (after<0) continue;
            taps+=Mathf.Exp(-after*(150+i*12))*
                (.58f*noise+.42f*Mathf.Sin(Mathf.Tau*(2400+i*173)*after));
        }
        return taps*.42f;
    });
    private static readonly AudioStreamWav Flute=Render(.84f,(t,noise)=>
    {
        int note=Mathf.Min(2,(int)(t/.28f));
        float beat=t-note*.28f;
        float hz=note==0 ? 784 : note==1 ? 988 : 880;
        float envelope=Mathf.SmoothStep(0,.045f,beat)*(1-Mathf.SmoothStep(.17f,.28f,beat));
        return envelope*(.50f*Mathf.Sin(Mathf.Tau*hz*beat)+.10f*Mathf.Sin(Mathf.Tau*hz*beat*2)+.045f*noise);
    });
    private static readonly AudioStreamWav Voice=Render(.36f,(t,noise)=>
    {
        float glide=Mathf.Tau*(410*t+330*t*t+Mathf.Sin(t*16)*.18f);
        float envelope=Mathf.SmoothStep(0,.038f,t)*(1-Mathf.SmoothStep(.19f,.36f,t));
        return envelope*(.45f*Mathf.Sin(glide)+.19f*Mathf.Sin(glide*2)+.11f*Mathf.Sin(glide*3)+.025f*noise);
    });
    private static readonly AudioStreamWav Guitar=Render(1.20f,(t,n)=>
    {
        float beat=t%.30f, hz=new float[]{164.81f,196,220,196}[(int)(t/.30f)%4];
        float pluck=0;
        for(int harmonic=1;harmonic<=12;harmonic++)
        {
            float pick=Mathf.Sin(harmonic*.72f);
            float detune=1+.0015f*Mathf.Sin(beat*31);
            pluck+=pick*Mathf.Sin(Mathf.Tau*hz*harmonic*detune*beat)*Mathf.Exp(-beat*(3.0f+harmonic*1.2f))/Mathf.Pow(harmonic,.82f);
        }
        float attack=1-Mathf.Exp(-beat*950);
        float amp=Mathf.Tanh((pluck+n*.13f*Mathf.Exp(-beat*130))*3.2f)*.52f;
        return attack*amp*(1-Mathf.SmoothStep(.25f,.30f,beat));
    });
    private static readonly AudioStreamWav Synth=Render(1.20f,(t,n)=>
    {
        // A struck-string piano envelope: bright hammer transient and decaying partials.
        float beat=t%.30f,hz=new float[]{523.25f,659.25f,783.99f,659.25f}[(int)(t/.30f)%4];
        float tone=0;
        for(int harmonic=1;harmonic<=9;harmonic++)
        {
            float inharmonic=Mathf.Sqrt(1+.00012f*harmonic*harmonic);
            tone+=Mathf.Cos(Mathf.Tau*hz*harmonic*inharmonic*beat)*Mathf.Exp(-beat*(2.4f+harmonic*2.8f))/Mathf.Pow(harmonic,1.6f);
        }
        return (1-Mathf.Exp(-beat*1400))*(tone*.46f+n*.08f*Mathf.Exp(-beat*150))*(1-Mathf.SmoothStep(.27f,.30f,beat));
    });
    private static readonly AudioStreamWav Ball=Render(.14f,(t,n)=>Mathf.Exp(-t*34)*(.7f*Mathf.Sin(Mathf.Tau*(170*t-230*t*t))+.30f*n));
    private static readonly AudioStreamWav Angry=Render(.55f,(t,n)=>Mathf.Sin(Mathf.Pi*t/.55f)*(.38f*Mathf.Sin(Mathf.Tau*(350*t+10*Mathf.Sin(t*20)))+.08f*n));
    private static readonly AudioStreamWav Sniff=Render(.32f,(t,n)=>Mathf.Sin(Mathf.Pi*t/.32f)*(.45f*n+.18f*Mathf.Sin(Mathf.Tau*630*t)));
    private static readonly AudioStreamWav Breath=SleepSound(false);
    private static readonly AudioStreamWav Snore=SleepSound(true);

    public static void Play(Node3D owner,string cue,float start=0)
    {
        var audio=owner.GetNodeOrNull<AudioStreamPlayer3D>("ChaoCue");
        if (audio==null)
        {
            audio=new AudioStreamPlayer3D { Name="ChaoCue",VolumeDb=-11,MaxDistance=15 };
            owner.AddChild(audio);
        }
        audio.Stream=cue switch
        {
            "guitar"=>Guitar,"synth"=>Synth,"ball"=>Ball,"angry"=>Angry,"sniff"=>Sniff,"bite"=>Bite,"rattle"=>Rattle,"flute"=>Flute,"voice"=>Voice,
            "breath"=>Breath,"snore"=>Snore,_=>Mood
        };
        audio.PitchScale=cue=="voice" ? Mathf.Lerp(.91f,1.23f,GD.Randf()) : 1;
        audio.VolumeDb=cue is "breath" or "snore" ? -19 : -11;
        audio.Play(start);
    }

    private static AudioStreamWav SleepSound(bool snore) => Render(.82f,(t,noise)=>
    {
        float envelope=Mathf.Pow(Mathf.Sin(Mathf.Pi*t/.82f),1.6f);
        float buzz=snore ? Mathf.Sin(Mathf.Tau*(109*t+18*t*t))*.29f+
            Mathf.Sin(Mathf.Tau*214*t)*.12f : 0;
        return envelope*(buzz+noise*(snore ? .13f : .20f));
    });

    private static AudioStreamWav Render(float duration,System.Func<float,float,float> signal)
    {
        const int rate=22050;
        byte[] data=new byte[(int)(rate*duration)*2];
        var random=new System.Random(911);
        float previous=0;
        for (int i=0;i<data.Length/2;i++)
        {
            float noise=(float)random.NextDouble()*2-1;
            float sample=signal((float)i/rate,(noise-previous)*.55f);
            previous=noise;
            short value=(short)(Mathf.Clamp(sample,-1,1)*13000);
            data[2*i]=(byte)value;
            data[2*i+1]=(byte)(value>>8);
        }
        return new AudioStreamWav { MixRate=rate,Format=AudioStreamWav.FormatEnum.Format16Bits,Data=data };
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

internal sealed class ChaoVocal
{
    private float delay=Mathf.Lerp(10f,23f,GD.Randf());
    private bool wasSleeping;

    public void Step(Node3D art,PetState chao,bool sleeping,float dt)
    {
        if (!chao.CanAct) return;
        if (sleeping!=wasSleeping)
        {
            wasSleeping=sleeping;
            delay=sleeping ? .8f : Mathf.Lerp(8f,14f,GD.Randf());
        }
        delay-=dt;
        if (delay>0) return;
        if (sleeping)
        {
            bool snores=chao.Glutton || chao.Aggressiveness>=25;
            ChaoSounds.Play(art,snores ? "snore" : "breath");
            delay=snores ? Mathf.Lerp(3.5f,5f,GD.Randf()) : Mathf.Lerp(5f,8f,GD.Randf());
        }
        else
        {
            ChaoSounds.Play(art,"voice");
            delay=Mathf.Lerp(19f,39f,GD.Randf())-Mathf.Clamp(chao.Curiosity*.07f,-4,6);
        }
    }
}
