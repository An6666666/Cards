public interface IEnemyCooldownProvider
{
    int CooldownSlotCount { get; }
    int GetCooldownTurnsRemaining(int slotIndex);
}