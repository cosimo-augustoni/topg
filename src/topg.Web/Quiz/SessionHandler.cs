using System.Security.Cryptography;
using topg.Web.Templating.DomainObjects;

namespace topg.Web.Quiz
{
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
                Quiz = template
            });

            return sessionId;
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

    public class QuizSession()
    {
        public required SessionId SessionId { get; init; }
        public required QuizTemplate Quiz { get; init; }
    }
}
