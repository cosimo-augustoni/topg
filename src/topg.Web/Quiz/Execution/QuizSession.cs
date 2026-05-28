using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using topg.Web.Quiz.Management;

namespace topg.Web.Quiz.Execution;

public class QuizSession
{
    private readonly byte[] sessionSecret = RandomNumberGenerator.GetBytes(32);
    public required SessionId SessionId { get; init; }
    public required QuizExecution Quiz { get; init; }
    public BuzzerState BuzzerState { get; } = new();
    public SoundEffectManager SoundEffectManager { get; } = new();
    public List<Player> Players { get; } = [];
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

    public void SelectQuestion(Question question)
    {
        Quiz.CurrentQuestionId = question.Template.Id;
        SessionStateHasChanged();
    }

    public void MarkCurrentQuestionAsAnswered()
    {
        Quiz.CurrentQuestion?.IsAnswered = true;
        Quiz.CurrentQuestionId = null;
        SessionStateHasChanged();
    }

    public void AdjustPlayerScore(Player player, int points)
    {
        player.Score += points;
        SessionStateHasChanged();
    }

    public void Buzz(Player player)
    {
        if (BuzzerState.TrySetBuzzered(player))
        {
            SoundEffectManager.PlaySound(SoundEffect.Buzzer);
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