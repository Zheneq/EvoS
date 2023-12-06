using EvoS.Framework.DataAccess.Daos;
using System.Collections.Generic;
using System;
using EvoS.Framework.Network.Static;
using static EvoS.Framework.DataAccess.Daos.TrustWarDao;

namespace EvoS.Framework.DataAccess.Mock
{
    internal class TrustWarMockDao : TrustWarDao
    {
        public TrustWarDaoEntry Find()
        {
            return new TrustWarDaoEntry() { Omni = 0, Evos = 0, Warbotics = 0 };
        }

        public void Save(TrustWarDaoEntry entry)
        {
        }
    }
}