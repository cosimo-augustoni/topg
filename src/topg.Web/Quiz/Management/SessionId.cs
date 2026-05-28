using System.Security.Cryptography;

namespace topg.Web.Quiz.Management;

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