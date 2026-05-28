namespace topg.Web.Quiz.Execution;

public class BuzzerState
{
    private Player? buzzeredPlayer;
    private int isLocked;
    public Player? BuzzeredPlayer => buzzeredPlayer;
    public bool IsLocked => isLocked == 1 || buzzeredPlayer != null;

    public bool TrySetBuzzered(Player player)
    {
        if (isLocked == 1)
            return false;

        return Interlocked.CompareExchange(ref buzzeredPlayer, player, null) == null;
    }

    public void LockBuzzer()
    {
        Interlocked.Exchange(ref isLocked, 1);
    }

    public void UnlockBuzzer()
    {
        Interlocked.Exchange(ref buzzeredPlayer, null);
        Interlocked.Exchange(ref isLocked, 0);
    }
}