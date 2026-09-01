using System.Collections.Generic;

public enum DismantleVfxSource
{
    Hand,
    Deck,
    Void,
    Unknown,
}

public readonly struct DismantleVfxEvent
{
    public DismantleVfxEvent(int sequence, EntityState entity, CardData card, DismantleVfxSource source)
    {
        Sequence = sequence;
        Entity = entity;
        Card = card;
        Source = source;
    }

    public int Sequence { get; }
    public EntityState Entity { get; }
    public CardData Card { get; }
    public DismantleVfxSource Source { get; }
}

public sealed class DismantleVfxQueue
{
    private readonly List<DismantleVfxEvent> pending = new List<DismantleVfxEvent>();
    private int nextSequence;

    public IReadOnlyList<DismantleVfxEvent> Pending => pending;
    public bool HasPending => pending.Count > 0;

    public void Enqueue(EntityState entity, CardData card, DismantleVfxSource source)
    {
        if (entity == null || card == null)
            return;

        pending.Add(new DismantleVfxEvent(nextSequence++, entity, card, source));
    }

    public List<DismantleVfxEvent> ConsumePending()
    {
        var result = new List<DismantleVfxEvent>(pending);
        pending.Clear();
        return result;
    }

    public void Clear()
    {
        pending.Clear();
        nextSequence = 0;
    }
}
