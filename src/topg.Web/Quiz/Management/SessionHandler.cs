using System.Diagnostics.CodeAnalysis;
using topg.Web.Quiz.Execution;
using topg.Web.Templating.DomainObjects;

namespace topg.Web.Quiz.Management
{
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
}
