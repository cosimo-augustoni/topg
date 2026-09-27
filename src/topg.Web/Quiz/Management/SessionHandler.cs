using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using topg.Web.Quiz.Execution;
using topg.Web.Templating.DomainObjects;

namespace topg.Web.Quiz.Management
{
    public class SessionHandler
    {
        public ConcurrentDictionary<SessionId, QuizSession> Sessions { get; } = new();

        public SessionId CreateSession(QuizTemplate template)
        {
            SessionId sessionId;
            do
            {
                sessionId = SessionId.Create();
            } while (!Sessions.TryAdd(sessionId, new QuizSession
            {
                SessionId = sessionId,
                Quiz = new QuizExecution(template),
            }));

            return sessionId;
        }

        public bool Join(SessionId sessionId, string playerName, [NotNullWhen(true)] out string? playerId)
        {
            if (Sessions.TryGetValue(sessionId, out var session))
                return session.TryAddPlayer(playerName, out playerId);

            playerId = null;
            return false;
        }
    }
}
