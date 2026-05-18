namespace Sodalis.Core;

public interface IGameContext
{
    Guid GameId { get; }
    bool IsResolved { get; }
}
