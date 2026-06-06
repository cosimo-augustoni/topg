using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using MudBlazor;
using topg.Web.Quiz.Management;
using topg.Web.Templating.DomainObjects;

namespace topg.Web.Quiz.Execution;

public class QuizSession
{
    private readonly byte[] sessionSecret = RandomNumberGenerator.GetBytes(32);
    public required SessionId SessionId { get; init; }
    public required QuizExecution Quiz { get; init; }
    public BuzzerState BuzzerState { get; } = new();
    public TextInputState TextInputState { get; } = new();
    public TimerState TimerState { get; } = new();
    public SoundEffectManager SoundEffectManager { get; } = new();
    public List<Player> Players { get; } = [];

    public ControlDisplayState ControlDisplayState
    {
        get;
        set
        {
            field = value;
            SessionStateHasChanged();
        }
    }

    public bool IsInUse => SessionStateChanged?.GetInvocationList() is { Length: > 0 };

    public event AsyncEventHandler<SessionChangedEventArgs>? SessionStateChanged;

    /// <summary>
    /// Adds a player to the session if no player with the same name exists.
    /// </summary>
    /// <returns>Whether the player was added.</returns>
    public bool TryAddPlayer(string playerName, [NotNullWhen(true)] out string? playerId)
    {
        playerId = null;
        if (Players.Any(p => p.Name == playerName))
            return false;

        var nameBytes = Encoding.UTF8.GetBytes(playerName);
        var hmacBytes = HMACSHA256.HashData(sessionSecret, nameBytes);
        playerId = playerName + "." + Convert.ToHexString(hmacBytes);

        var player = new Player { Id = playerId, Name = playerName, Score = 0 };
        Players.Add(player);
        SessionStateHasChanged();
        return true;
    }

    public void SelectNextBoard()
    {
        if (Quiz.HasNextBoard)
        {
            Quiz.CurrentBoardId++;
            SessionStateHasChanged();
        }
    }

    public void SelectQuestion(Question question)
    {
        Quiz.CurrentQuestionId = question.Id;

        switch (question.AnswerType)
        {
            case AnswerType.Buzzer:
                ControlDisplayState = ControlDisplayState.Buzzer;
                break;
            case AnswerType.Text:
                ControlDisplayState = ControlDisplayState.Text;
                break;
        }

        SessionStateHasChanged();
    }

    public void UpdateQuestion<T>(T question, Action<T> action)
    {
        action(question);
        SessionStateHasChanged();
    }

    public void MarkCurrentQuestionAsAnswered()
    {
        Quiz.CurrentQuestion?.IsAnswered = true;
        Quiz.CurrentQuestionId = null;

        ControlDisplayState = ControlDisplayState.None;
        TextInputState.Clear();
        TextInputState.IsRevealed = false;
        BuzzerState.UnlockBuzzer();
        TimerState.Stop();

        SessionStateHasChanged();
    }

    public void AdjustPlayerScore(Player player, int points)
    {
        player.Score += points;
        var sound = points > 0 ? SoundEffect.Correct : SoundEffect.Incorrect;
        SoundEffectManager.PlaySound(sound);
        SessionStateHasChanged();
    }

    public void SetTimerDuration(int timerDuration)
    {
        TimerState.TimerDuration = timerDuration;
        SessionStateHasChanged();
    }

    public void ToggleTimer()
    {
        if (TimerState.IsRunning)
            TimerState.Stop();
        else
            TimerState.Start();

        SessionStateHasChanged();
    }

    public void RevealTextInput()
    {
        TextInputState.IsRevealed = true;
        SessionStateHasChanged();
    }

    public void HideTextInput()
    {
        TextInputState.IsRevealed = false;
        SessionStateHasChanged();
    }

    public void UpdateTextInput(Player player, string? text)
    {
        TextInputState.UpdateTextInput(player, text);
        SessionStateHasChanged();
    }

    public void ClearTextInputs()
    {
        TextInputState.Clear();
        SessionStateHasChanged();
    }

    public void Buzz(Player player)
    {
        if (BuzzerState.TrySetBuzzered(player))
        {
            SoundEffectManager.PlaySound(SoundEffect.Buzzer);
            TimerState.Stop();
            SessionStateHasChanged();
        }
    }

    public void LockBuzzer()
    {
        BuzzerState.LockBuzzer();
        SessionStateHasChanged();
    }

    public void UnlockBuzzer()
    {
        BuzzerState.UnlockBuzzer();
        SessionStateHasChanged();
    }

    private void SessionStateHasChanged()
    {
        SessionStateChanged?.Invoke(this, new SessionChangedEventArgs(SessionId));
    }

    public bool TryGetPlayer(string? playerSession, [NotNullWhen(true)] out Player? player)
    {
        player = Players.FirstOrDefault(p => p.Id == playerSession);
        return player != null;
    }
}

public class TimerState
{
    private int running = 0;

    public bool IsRunning => running == 1;
    public int TimerDuration { get; set; } = 10;

    public void Start()
    {
        Interlocked.Exchange(ref running, 1);
    }

    public void Stop()
    {
        Interlocked.Exchange(ref running, 0);
    }
}