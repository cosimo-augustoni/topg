using System.Collections.Concurrent;
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

        public JoinResult Join(SessionId sessionId, string playerName, out string? playerId)
        {
            playerId = null;
            if (!Sessions.TryGetValue(sessionId, out var session))
                return JoinResult.GameNotFound;

            return session.TryAddPlayer(playerName, out playerId) ? JoinResult.Joined : JoinResult.NameTaken;
        }
    }
}
