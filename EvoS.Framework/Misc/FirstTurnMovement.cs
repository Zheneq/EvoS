using System;
using System.Collections.Generic;
using System.Text;
using EvoS.Framework.Assets;
using EvoS.Framework.Assets.Serialized.Behaviours;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.NetworkBehaviours;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.Unity;
using EvoS.Framework.Logging;

namespace EvoS.Framework.Misc
{
	public class FirstTurnMovement : MonoBehaviour
	{
		private static FirstTurnMovement s_instance;
		public BoardRegion m_regionForTeamA;
		public BoardRegion m_regionForTeamB;
		public bool m_canWaypointOnFirstTurn;

		public FirstTurnMovement(AssetFile assetFile, StreamReader stream)
		{
			DeserializeAsset(assetFile, stream);
		}

		// Token: 0x060039C5 RID: 14789 RVA: 0x000337E1 File Offset: 0x000319E1
		//private void Awake()
		//{
		//	if (FirstTurnMovement.s_instance != null)
		//	{
		//		Debug.LogError("FirstTurnMovement is supposed to be a singleton class, but an instance already existed when it awoke.  Make sure there are not two instances of FirstTurnMovement in the scene.");
		//	}
		//	FirstTurnMovement.s_instance = this;
		//}

		// Token: 0x060039C6 RID: 14790 RVA: 0x00033800 File Offset: 0x00031A00
		//private void Start()
		//{
		//	this.m_regionForTeamA.Initialize();
		//	this.m_regionForTeamB.Initialize();
		//}

		//// Token: 0x060039C7 RID: 14791 RVA: 0x00033818 File Offset: 0x00031A18
		//public static FirstTurnMovement Get()
		//{
		//	return FirstTurnMovement.s_instance;
		//}

		public static bool CanActorMoveToSquare(ActorData actor, BoardSquare square)
		{
			// TODO ZHENEQ
			return true;

			//if (GameFlowData == null || GameFlowData.CurrentTurn > 1)
			//{
			//	return true;
			//}
			//if (actor == null)
			//{
			//	return false;
			//}
			//if (square == null)
			//{
			//	return false;
			//}
			//FirstTurnMovement firstTurnMovement = FirstTurnMovement.Get();
			//if (firstTurnMovement == null)
			//{
			//	return true;
			//}
			//if (actor.method_76() == Team.TeamA)
			//{
			//	return firstTurnMovement.m_regionForTeamA == null || !firstTurnMovement.m_regionForTeamA.HasNonZeroArea() || firstTurnMovement.m_regionForTeamA.Contains(square.x, square.y);
			//}
			//return actor.method_76() != Team.TeamB || firstTurnMovement.m_regionForTeamB == null || !firstTurnMovement.m_regionForTeamB.HasNonZeroArea() || firstTurnMovement.m_regionForTeamB.Contains(square.x, square.y);
		}

		//// Token: 0x060039C9 RID: 14793 RVA: 0x0014CE7C File Offset: 0x0014B07C
		//public static bool ForceShowSprintRange(ActorData actor)
		//{
		//	if (GameFlowData.Get() == null || GameFlowData.Get().CurrentTurn > 1)
		//	{
		//		return false;
		//	}
		//	FirstTurnMovement firstTurnMovement = FirstTurnMovement.Get();
		//	if (firstTurnMovement == null)
		//	{
		//		return false;
		//	}
		//	if (actor.method_76() == Team.TeamA)
		//	{
		//		return firstTurnMovement.m_regionForTeamA != null && firstTurnMovement.m_regionForTeamA.HasNonZeroArea();
		//	}
		//	return actor.method_76() == Team.TeamB && (firstTurnMovement.m_regionForTeamB != null && firstTurnMovement.m_regionForTeamB.HasNonZeroArea());
		//}

		public static bool CanWaypoint()
		{
			return FirstTurnMovement.s_instance == null ||
				FirstTurnMovement.s_instance.GetRestrictedMovementState() != FirstTurnMovement.RestrictedMovementState.Active ||
				FirstTurnMovement.s_instance.m_canWaypointOnFirstTurn;
		}

		//// Token: 0x060039CB RID: 14795 RVA: 0x0014CEFC File Offset: 0x0014B0FC
		//public void OnTurnTick()
		//{
		//	if (GameFlowData.Get() == null || GameFlowData.Get().CurrentTurn == 2)
		//	{
		//		List<ActorData> actors = GameFlowData.Get().GetActors();
		//		foreach (ActorData actorData in actors)
		//		{
		//			actorData.method_9().UpdateSquaresCanMoveTo();
		//		}
		//	}
		//}

		public FirstTurnMovement.RestrictedMovementState GetRestrictedMovementState()
		{
			if (GameFlowData != null)
			{
				if (GameFlowData.CurrentTurn > 1)
				{
					return FirstTurnMovement.RestrictedMovementState.Inactive;
				}
				if (GameFlowData.CurrentTurn == 1)
				{
					return FirstTurnMovement.RestrictedMovementState.Active;
				}
			}
			return FirstTurnMovement.RestrictedMovementState.Invalid;
		}


		// Token: 0x02000689 RID: 1673
		public enum RestrictedMovementState
		{
			// Token: 0x0400338A RID: 13194
			Invalid = -1,
			// Token: 0x0400338B RID: 13195
			Inactive,
			// Token: 0x0400338C RID: 13196
			Active
		}
	}
}
