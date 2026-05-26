using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using topg.Web.Templating.DomainObjects;

namespace topg.Web.Quiz
{
    public delegate Task AsyncEventHandler<in T>(object sender, T e) where T : EventArgs;

    public class SessionHandler
    {
        public const string PlayerSessionStorageId = "playerSession";

        public Dictionary<SessionId, QuizSession> Sessions { get; } = new();

        public SessionId CreateSession(QuizTemplate template)
        {
            SessionId sessionId;
            do
            {
                sessionId = SessionId.Create();
            } while (this.Sessions.ContainsKey(sessionId));

            this.Sessions.Add(sessionId, new QuizSession
            {
                SessionId = sessionId,
                Quiz = new QuizExecution(template),
            });

            return sessionId;
        }

        /// <summary>
        /// Tries to join an existing session.
        /// </summary>
        /// <param name="sessionId">Id of the session.</param>
        /// <param name="playerName">Name under which the player wants to join. Doubles as identifier.</param>
        /// <param name="playerId">Id of the player when the Join was successful</param>
        /// <returns>Whether the join was successful.</returns>
        public bool Join(SessionId sessionId, string playerName, [NotNullWhen(true)] out string? playerId)
        {
            if (Sessions.TryGetValue(sessionId, out var session))
                return session.TryAddPlayer(playerName, out playerId);

            playerId = null;
            return false;
        }
    }

    public record QuizExecution
    {
        public string Name { get; }
        private readonly List<Board> Boards;

        public long? CurrentQuestionId { get; set; }
        public Question? CurrentQuestion => CurrentQuestionId == null ? null : CurrentBoard.Questions.Single(q => q.Template.Id == CurrentQuestionId);

        public int CurrentBoardId = 0;
        public Board CurrentBoard => Boards[CurrentBoardId];

        public QuizExecution(QuizTemplate template)
        {
            Name = template.Name;
            Boards = template.Boards.Select(b => new Board
            {
                Order = b.Order,
                Questions = b.Questions.Select(q => new Question
                {
                    Template = q,
                    IsAnswered = false,
                }).ToList()
            }).ToList();
        }
    }

    public class Board
    {
        public int Order { get; init; }
        public required List<Question> Questions { get; init; }
    }

    public class Question
    {
        public required Templating.DomainObjects.Question Template { get; set; }
        public bool IsAnswered { get; set; }
    }

    public record SessionId(string Key)
    {
        public static SessionId Create()
        {
            var idBytes = new byte[4];
            RandomNumberGenerator.Create().GetBytes(idBytes);
            var id = ((BitConverter.ToInt32(idBytes) & int.MaxValue) % 10000).ToString("0000");
            return new SessionId(id);
        }
    }

    public class QuizSession
    {
        private readonly byte[] sessionSecret = RandomNumberGenerator.GetBytes(32);
        public required SessionId SessionId { get; init; }
        public required QuizExecution Quiz { get; init; }
        public string Input { get; private set; } = string.Empty;
        public List<Player> Players { get; } = [];
        public bool IsInUse => this.SessionStateChanged?.GetInvocationList().Length > 0;

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

        /// <summary>
        /// Updates the shared input field and notifies all subscribers.
        /// </summary>
        public void UpdateInput(string value)
        {
            Input = value;
            SessionStateHasChanged();
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

        private void SessionStateHasChanged()
        {
            SessionStateChanged?.Invoke(this, new SessionChangedEventArgs(SessionId));
        }

        public bool IsUserInSession(string? playerSession)
        {
            return Players.Any(p => p.Id == playerSession);
        }

        
    }

    public class SessionChangedEventArgs(SessionId sessionId) : EventArgs;

    public record Player
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required int Score { get; set; }
    }
}
