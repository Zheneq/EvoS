using EvoS.Framework.Network.Unity;
using System;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    public class AbilityGameSummary
    {
        public int ActionType;
        public string AbilityClassName;
        public string AbilityName;

        // reactor
        public string ModName;
        // rogues
        // public string GearName;

        public int CastCount;
        public int TauntCount;
        public int TotalTargetsHit;
        public int TotalDamage;
        public int TotalHealing;
        public int TotalAbsorb;
        public int TotalPotentialAbsorb;
        public int TotalEnergyGainOnSelf;
        public int TotalEnergyGainToOthers;
        public int TotalEnergyLossToOthers;

        // added in rogues
        public void Serialize(NetworkWriter writer)
        {
            writer.Write(ActionType);
            writer.Write(AbilityClassName);
            writer.Write(AbilityName);
        
            // custom
            writer.Write(ModName);
            // rogues
            // writer.Write(GearName);
        
            writer.Write(CastCount);
            writer.Write(TauntCount);
            writer.Write(TotalTargetsHit);
            writer.Write(TotalDamage);
            writer.Write(TotalHealing);
            writer.Write(TotalAbsorb);
            writer.Write(TotalPotentialAbsorb);
            writer.Write(TotalEnergyGainOnSelf);
            writer.Write(TotalEnergyGainToOthers);
            writer.Write(TotalEnergyLossToOthers);
        }

        // added in rogues
        public void Deserialize(NetworkReader reader)
        {
            ActionType = reader.ReadInt32();
            AbilityClassName = reader.ReadString();
            AbilityName = reader.ReadString();
        
            // custom
            ModName = reader.ReadString();
            // rogues
            // GearName = reader.ReadString();
        
            CastCount = reader.ReadInt32();
            TauntCount = reader.ReadInt32();
            TotalTargetsHit = reader.ReadInt32();
            TotalDamage = reader.ReadInt32();
            TotalHealing = reader.ReadInt32();
            TotalAbsorb = reader.ReadInt32();
            TotalPotentialAbsorb = reader.ReadInt32();
            TotalEnergyGainOnSelf = reader.ReadInt32();
            TotalEnergyGainToOthers = reader.ReadInt32();
            TotalEnergyLossToOthers = reader.ReadInt32();
        }
    }
}
