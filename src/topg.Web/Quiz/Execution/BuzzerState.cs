namespace topg.Web.Quiz.Execution;

public class BuzzerState
{
    private Player? buzzeredPlayer;
    private bool isLocked;
    public Player? BuzzeredPlayer => buzzeredPlayer;
    public bool IsLocked => isLocked || buzzeredPlayer != null;

    public bool TrySetBuzzered(Player player)
    {
        if (isLocked)
            return false;

        return Interlocked.CompareExchange(ref buzzeredPlayer, player, null) == null;
    }

    public void LockBuzzer()
    {
        Interlocked.Exchange(ref isLocked, true);
    }

    public void UnlockBuzzer()
    {
        Interlocked.Exchange(ref buzzeredPlayer, null);
        Interlocked.Exchange(ref isLocked, false);
    }
}