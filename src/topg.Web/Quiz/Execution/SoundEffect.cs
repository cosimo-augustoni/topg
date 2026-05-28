using topg.Web.Quiz.Utils;

namespace topg.Web.Quiz.Execution;

public record SoundEffect : Enumeration<SoundEffect>
{
    public static SoundEffect Buzzer = new("buzzer", "buzzer.mp3");
    public static SoundEffect Correct = new("correct", "correct.mp3");
    public static SoundEffect Incorrect = new("incorrect", "incorrect.mp3");

    private SoundEffect(string Id, string DisplayName) : base(Id, DisplayName)
    {
    }
}