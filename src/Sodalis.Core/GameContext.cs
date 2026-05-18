namespace Sodalis.Core;

public sealed class GameContext : IGameContext
{
    public Guid GameId { get; private set; }
    public bool IsResolved { get; private set; }

    public void SetGameId(Guid gameId)
    {
        if (IsResolved)
            throw new InvalidOperationException("GameContext.GameId is already set for this request.");

        GameId = gameId;
        IsResolved = true;
    }
}
