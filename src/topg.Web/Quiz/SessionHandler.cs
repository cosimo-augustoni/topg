using System.Security.Cryptography;
using topg.Web.Templating.DomainObjects;

namespace topg.Web.Quiz
{
    public delegate Task AsyncEventHandler<in T>(object sender, T e) where T : EventArgs;

    public class SessionHandler
    {
        public Dictionary<SessionId, QuizSession> Sessions { get; } = new();

        public async Task<SessionId> CreateSessionAsync(QuizTemplate template)
        {
            SessionId sessionId;
            do
            {
                sessionId = SessionId.Create();
            } while (this.Sessions.ContainsKey(sessionId));

            this.Sessions.Add(sessionId, new QuizSession
            {
                SessionId = sessionId,
                Quiz = template,
            });

            return sessionId;
        }

        /// <summary>
        /// Tries to join an existing session.
        /// </summary>
        /// <param name="sessionId">Id of the session.</param>
        /// <param name="playerName">Name under which the player wants to join. Doubles as identifier.</param>
        /// <returns>Whether the join was successful.</returns>
        public bool Join(SessionId sessionId, string playerName)
        {
            if (Sessions.TryGetValue(sessionId, out var session))
                return session.AddPlayer(playerName);

            return false;
        }
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
        public required SessionId SessionId { get; init; }
        public required QuizTemplate Quiz { get; init; }
        public string Input { get; private set; } = string.Empty;
        public List<Player> Players { get; } = [];
        public bool IsInUse => this.SessionStateChanged?.GetInvocationList().Length > 0;

        public event AsyncEventHandler<SessionChangedEventArgs>? SessionStateChanged;

        /// <summary>
        /// Adds a player to the session if no player with the same name exists.
        /// </summary>
        /// <returns>Whether the player was added.</returns>
        public bool AddPlayer(string playerName)
        {
            if (Players.Any(p => p.Name == playerName))
                return false;

            var player = new Player { Name = playerName, Score = 0 };
            Players.Add(player);
            SessionStateChanged?.Invoke(this, new SessionChangedEventArgs(SessionId));
            return true;
        }

        /// <summary>
        /// Updates the shared input field and notifies all subscribers.
        /// </summary>
        public void UpdateInput(string value)
        {
            Input = value;
            SessionStateChanged?.Invoke(this, new SessionChangedEventArgs(SessionId));
        }
    }

    public class SessionChangedEventArgs(SessionId sessionId) : EventArgs;

    public record Player
    {
        public required string Name { get; init; }
        public required int Score { get; set; }
    }
}
