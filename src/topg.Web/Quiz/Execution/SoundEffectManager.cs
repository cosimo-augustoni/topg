namespace topg.Web.Quiz.Execution;

public class SoundEffectManager
{
    public event AsyncEventHandler<SoundEffectArgs>? SoundEffectRequested;

    public void PlaySound(SoundEffect soundEffect)
    {
        SoundEffectRequested?.Invoke(this, new SoundEffectArgs(soundEffect));
    }
}