namespace topg.Web.Quiz.Execution;

public class SoundEffectArgs(SoundEffect soundEffect) : EventArgs
{
    public SoundEffect SoundEffect { get; } = soundEffect;
}