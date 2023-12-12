using EvoS.Framework.DataAccess.Daos;

namespace EvoS.Framework.DataAccess.Mock
{
    public class MiscMockDao: MiscDao
    {
        public MiscDao.Entry GetEntry(string key)
        {
            return null;
        }

        public void SaveEntry(MiscDao.Entry entry) { }
    }
}