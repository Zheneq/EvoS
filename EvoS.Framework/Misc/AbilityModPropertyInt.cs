using System;

namespace EvoS.Framework.Misc
{
    [Serializable]
    public class AbilityModPropertyInt
    {
        public float value;
        public ModOp operation;

        public override string ToString()
        {
            return $"{nameof(AbilityModPropertyInt)}>(" +
                   $"{nameof(value)}: {value}, " +
                   $"{nameof(operation)}: {operation}, " +
                   ")";
        }

        public int GetModifiedValue(int input)
        {
            switch (operation)
            {
                case ModOp.Add:
                    return input + (int) Math.Round(value);
                case ModOp.Override:
                    return (int) Math.Round(value);
                case ModOp.MultiplyAndFloor:
                    return (int) Math.Floor(input * value);
                case ModOp.MultiplyAndCeil:
                    return (int) Math.Ceiling(input * value);
                case ModOp.MultiplyAndRound:
                    return (int) Math.Round(input * value);
                default:
                    return input;
            }
        }

        public void CopyValuesFrom(AbilityModPropertyInt other)
        {
            value = other.value;
            operation = other.operation;
        }

        public enum ModOp
        {
            Ignore,
            Add,
            Override,
            MultiplyAndFloor,
            MultiplyAndCeil,
            MultiplyAndRound
        }
    }
}
