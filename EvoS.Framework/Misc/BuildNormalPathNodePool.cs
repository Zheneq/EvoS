using System;
using System.Collections.Generic;
using System.Text;

namespace EvoS.Framework.Misc
{
	public class BuildNormalPathNodePool
	{
		private List<BoardSquarePathInfo> m_allocatedInstances;
		private int m_nextEntryIndex;

		public BuildNormalPathNodePool()
		{
			this.m_allocatedInstances = new List<BoardSquarePathInfo>(450);
		}

		public BoardSquarePathInfo GetAllocatedNode()
		{
			if (this.m_nextEntryIndex < this.m_allocatedInstances.Count)
			{
				BoardSquarePathInfo boardSquarePathInfo = this.m_allocatedInstances[this.m_nextEntryIndex];
				this.InitNodeValues(boardSquarePathInfo);
				this.m_nextEntryIndex++;
				return boardSquarePathInfo;
			}
			BoardSquarePathInfo boardSquarePathInfo2 = new BoardSquarePathInfo();
			this.m_allocatedInstances.Add(boardSquarePathInfo2);
			this.m_nextEntryIndex++;
			return boardSquarePathInfo2;
		}

		private void InitNodeValues(BoardSquarePathInfo node)
		{
			node.ResetValuesToDefault();
		}

		public void ResetAvailableNodeIndex()
		{
			this.m_nextEntryIndex = 0;
		}

		public int GetNextAvailableIndex()
		{
			return this.m_nextEntryIndex;
		}
	}
}
