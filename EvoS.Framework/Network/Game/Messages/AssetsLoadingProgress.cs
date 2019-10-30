using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Network.Game.Messages
{
    [UNetMessage(new short[]{62}, new short[]{61})]
    public class AssetsLoadingProgress : MessageBase
    {
        public long AccountId;
        public int PlayerId;
        public byte TotalLoadingProgress;
        public byte LevelLoadingProgress;
        public byte CharacterLoadingProgress;
        public byte VfxLoadingProgress;
        public byte SpawningProgress;

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt64((ulong) AccountId);
            writer.WritePackedUInt32((uint) PlayerId);
            writer.WritePackedUInt32(TotalLoadingProgress);
            writer.WritePackedUInt32(LevelLoadingProgress);
            writer.WritePackedUInt32(CharacterLoadingProgress);
            writer.WritePackedUInt32(VfxLoadingProgress);
            writer.WritePackedUInt32(SpawningProgress);
        }

        public override void Deserialize(NetworkReader reader)
        {
            AccountId = (long) reader.ReadPackedUInt64();
            PlayerId = (int) reader.ReadPackedUInt32();
            TotalLoadingProgress = (byte) reader.ReadPackedUInt32();
            LevelLoadingProgress = (byte) reader.ReadPackedUInt32();
            CharacterLoadingProgress = (byte) reader.ReadPackedUInt32();
            VfxLoadingProgress = (byte) reader.ReadPackedUInt32();
            SpawningProgress = (byte) reader.ReadPackedUInt32();
        }

        public override string ToString()
        {
            return $"{nameof(AssetsLoadingProgress)}(" +
                   $"{nameof(AccountId)}: {AccountId}, " +
                   $"{nameof(PlayerId)}: {PlayerId}, " +
                   $"Total: {TotalLoadingProgress}, " +
                   $"Level: {LevelLoadingProgress}, " +
                   $"Character: {CharacterLoadingProgress}, " +
                   $"Vfx: {VfxLoadingProgress}, " +
                   $"Spawning: {SpawningProgress}" +
                   ")";
        }
    }
}
