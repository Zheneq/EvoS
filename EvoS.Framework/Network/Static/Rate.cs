using System;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    [EvosMessage(157)]
    public struct Rate
    {
        public Rate(double amount, TimeSpan period)
        {
            Amount = amount;
            Period = period;
        }

        public static implicit operator Rate(string rate)
        {
            string[] array = rate.Split(new[]
            {
                " per "
            }, StringSplitOptions.RemoveEmptyEntries);
            if (array.Length != 2)
            {
                throw new Exception("Failed to parse rate");
            }

            return new Rate(double.Parse(array[0]), TimeSpan.Parse(array[1]));
        }

        public override string ToString()
        {
            return string.Format("{0} per {1}", Amount, Period);
        }

        public double AmountPerSecond
        {
            get
            {
                if (Period == TimeSpan.Zero)
                {
                    return 0.0;
                }

                return Amount / Period.TotalSeconds;
            }
        }

        public double Amount;

        public TimeSpan Period;
    }
}
