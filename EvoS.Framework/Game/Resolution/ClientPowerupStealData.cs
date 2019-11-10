namespace EvoS.Framework.Game.Resolution
{
    public class ClientPowerupStealData
    {
        public int m_powerupGuid;
        public ClientPowerupResults m_powerupResults;

        public ClientPowerupStealData(int powerupGuid, ClientPowerupResults powerupResults)
        {
            this.m_powerupGuid = powerupGuid;
            this.m_powerupResults = powerupResults;
        }
    }
}
