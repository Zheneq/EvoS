using System;
using System.Numerics;
using System.Collections.Generic;
using EvoS.Framework.Logging;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Unity;
using EvoS.Framework.Network.NetworkBehaviours;

namespace EvoS.Framework.Misc
{
	public class SinglePlayerManager : NetworkBehaviour
	{
		// STUB

		private static string m_uniqueNetworkHash = "FFFFFF24601";
		private static SinglePlayerManager s_instance;
		// [SyncVar(hook = "HookSetCurrentScriptIndex")]
		private int m_currentScriptIndex;
		// [SyncVar(hook = "HookSetCanEndTurn")]
		private bool m_canEndTurn = true;
		private bool m_clientCanEndTurn = true;
		private GameObject m_advanceDestinationsHighlight;
		private bool m_pausedTimer;
		private bool m_decisionTimerForceOff;
		private bool m_lockInCancelButtonForceOff;
		private bool m_lockinPhaseDisplayForceOff;
		private bool m_lockinPhaseTextForceOff;
		private bool m_lockinPhaseColorForceOff;
		private bool m_notificationPanelForceOff;
		private bool[] m_teamPlayerIconForceOff = new bool[5];
		private bool[] m_enemyPlayerIconForceOff = new bool[5];
		// [HideInInspector]
		private bool m_errorTriggered;
		private int m_lastTutorialTextState = -1;
		private int m_lastTutorialCameraState = -1;
		private static int kRpcRpcPlayScriptedChat = 884030896;

		public static bool IsDestinationAllowed(ActorData mover, BoardSquare square, bool settingWaypoints = true)
		{
			return true;
			// TODO ZHENEQ
			//if (SinglePlayerManager.s_instance == null)
			//{
			//	return true;
			//}
			//if (SinglePlayerManager.s_instance.GetCurrentState() == null)
			//{
			//	return true;
			//}
			//if (mover.SpawnerId != -1)
			//{
			//	return true;
			//}
			//SinglePlayerState currentState = SinglePlayerManager.s_instance.GetCurrentState();
			//bool flag = currentState.m_allowedDestinations.m_quads.Count == 0 || currentState.m_allowedDestinations.method_0().Contains(square);
			//bool flag2 = !currentState.m_onlyAllowWaypointMovement || settingWaypoints;
			//return flag && flag2;
		}

		public int GetCurrentScriptIndex()
		{
			return this.m_currentScriptIndex;
		}

	}
}
