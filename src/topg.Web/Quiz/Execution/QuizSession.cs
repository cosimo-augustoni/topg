using topg.Web.Client.Shared;
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
    public TimeProvider TimeProvider { private get; init; } = TimeProvider.System;
    public TimerState TimerState { get; } = new();
    public BonusPointsState BonusPointsState { get; } = new();
    public SoundEffectManager SoundEffectManager { get; } = new();
    public Player? ActivePlayer { get; private set; }
    public TurnDirection TurnDirection { get; private set; } = TurnDirection.Forward;
    public LinkedList<Player> Players { get; } = [];

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

    public bool TryAddPlayer(string playerName, [NotNullWhen(true)] out string? playerId)
    {
        playerId = null;
        // The player id is derived from the name, so it has to be normalized before the id is created.
        playerName = playerName.Trim();
        if (playerName.Length == 0)
            return false;

        Player player;
        // Two players joining at once must not both pass the duplicate check.
        lock (Players)
        {
            if (Players.Any(p => string.Equals(p.Name, playerName, StringComparison.OrdinalIgnoreCase)))
                return false;

            var nameBytes = Encoding.UTF8.GetBytes(playerName);
            var hmacBytes = HMACSHA256.HashData(sessionSecret, nameBytes);
            playerId = playerName + "." + Convert.ToHexString(hmacBytes);

            player = new Player { Id = playerId, Name = playerName, Score = 0 };
            Players.AddLast(player);
        }
        ActivePlayer ??= player;
        SessionStateHasChanged();
        return true;
    }

    private void AdvanceActivePlayer()
    {
        if (ActivePlayer == null)
            return;

        var activePlayerNode = Players.Find(ActivePlayer);
        var nextPlayerNode = TurnDirection == TurnDirection.Forward
            ? activePlayerNode?.Next ?? Players.First
            : activePlayerNode?.Previous ?? Players.Last;
        SetActivePlayer(nextPlayerNode?.Value);
    }

    public void SetActivePlayer(Player? player)
    {
        ActivePlayer = player;
        SessionStateHasChanged();
    }

    public void SetTurnDirection(TurnDirection turnDirection)
    {
        TurnDirection = turnDirection;
        SessionStateHasChanged();
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
        TextInputState.IsLocked = false;

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
        CloseCurrentQuestion();

        AdvanceActivePlayer();

        SessionStateHasChanged();
    }

    public void ReturnToBoard()
    {
        // The question stays selectable, so it has to look untouched when it is opened again.
        Quiz.CurrentQuestion?.ResetDisplayState();
        CloseCurrentQuestion();

        SessionStateHasChanged();
    }

    private void CloseCurrentQuestion()
    {
        Quiz.CurrentQuestionId = null;

        ControlDisplayState = ControlDisplayState.None;
        TextInputState.Clear();
        TextInputState.IsRevealed = false;
        TextInputState.IsLocked = false;
        BuzzerState.UnlockBuzzer();
        TimerState.Stop();
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
            TimerState.Start(TimeProvider, SessionStateHasChanged);

        SessionStateHasChanged();
    }

    public void SetBonusPoints(bool isEnabled, int lastQuestions)
    {
        BonusPointsState.IsEnabled = isEnabled;
        BonusPointsState.LastQuestions = Math.Max(1, lastQuestions);
        SessionStateHasChanged();
    }

    // Derived on every read rather than stored, so changing the setting or answering a question updates all tiles at
    // once, and answered questions fall back to their normal points.
    public bool HasBonus(Question question) =>
        BonusPointsState.IsEnabled
        && !question.IsAnswered
        && Quiz.CurrentBoard.UnansweredCount <= BonusPointsState.LastQuestions;

    public int PointsFor(Question question) =>
        HasBonus(question)
            ? (int)Math.Round(question.Points * BonusPointsState.Multiplier, MidpointRounding.AwayFromZero)
            : question.Points;

    public int QuestionsUntilBonus =>
        Math.Max(0, Quiz.CurrentBoard.UnansweredCount - BonusPointsState.LastQuestions);

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
        // A debounced keystroke can arrive after the host locked the inputs, so the lock is enforced here and not only in the player's UI.
        if (TextInputState.IsLocked)
            return;

        TextInputState.UpdateTextInput(player, text);
        SessionStateHasChanged();
    }

    public void LockTextInputs()
    {
        TextInputState.IsLocked = true;
        SessionStateHasChanged();
    }

    public void UnlockTextInputs()
    {
        TextInputState.IsLocked = false;
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
