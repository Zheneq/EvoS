using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.Unity;
using System.Collections.Generic;
using System.Numerics;

namespace EvoS.Framework.Network.NetworkBehaviours
{
	public class Barrier
	{
		// Token: 0x0600391F RID: 14623 RVA: 0x00149AB0 File Offset: 0x00147CB0
		public Barrier(int guid, string name, Vector3 center, Vector3 facingDir, float width, bool bidirectional, BlockingRules blocksVision, BlockingRules blocksAbilities, BlockingRules blocksMovement, BlockingRules blocksPositionTargeting, bool considerAsCover, int maxDuration, ActorData owner, List<GameObject> barrierSequencePrefabs = null, bool playSequences = true, GameplayResponseForActor onEnemyMovedThrough = null, GameplayResponseForActor onAllyMovedThrough = null, int maxHits = -1, bool endOnCasterDeath = false, SequenceSource parentSequenceSource = null, Team barrierTeam = Team.Invalid)
		{
			this.InitBarrier(guid, name, center, facingDir, width, bidirectional, blocksVision, blocksAbilities, blocksMovement, BlockingRules.ForNobody, blocksPositionTargeting, considerAsCover, maxDuration, owner, barrierSequencePrefabs, playSequences, onEnemyMovedThrough, onAllyMovedThrough, maxHits, endOnCasterDeath, parentSequenceSource, barrierTeam);
		}

		// Token: 0x04003327 RID: 13095
		private string m_name;

		// Token: 0x04003328 RID: 13096
		private Vector3 m_center;

		// Token: 0x04003329 RID: 13097
		private Vector3 m_endpoint1;

		// Token: 0x0400332A RID: 13098
		private Vector3 m_endpoint2;

		// Token: 0x0400332B RID: 13099
		private Vector3 m_facingDir;

		// Token: 0x0400332C RID: 13100
		private bool m_bidirectional;

		// Token: 0x0400332D RID: 13101
		private bool m_makeClientGeo;

		// Token: 0x0400332E RID: 13102
		private GameObject m_generatedClientGeometry;

		// Token: 0x0400332F RID: 13103
		private Team m_team;

		// Token: 0x04003330 RID: 13104
		private ActorData m_owner;

		// Token: 0x04003331 RID: 13105
		//public SpoilsSpawnData m_spoilsSpawnOnEnemyMovedThrough;

		// Token: 0x04003332 RID: 13106
		//public SpoilsSpawnData m_spoilsSpawnOnAllyMovedThrough;

		// Token: 0x04003333 RID: 13107
		public bool m_removeAtTurnEndIfEnemyMovedThrough;

		// Token: 0x04003334 RID: 13108
		public bool m_removeAtTurnEndIfAllyMovedThrough;

		// Token: 0x04003335 RID: 13109
		public bool m_removeAtPhaseEndIfEnemyMovedThrough;

		// Token: 0x04003336 RID: 13110
		public bool m_removeAtPhaseEndIfAllyMovedThrough;

		// Token: 0x04003337 RID: 13111
		public AbilityPriority m_customEndPhase = AbilityPriority.INVALID;

		// Token: 0x04003338 RID: 13112
		public bool m_removeAtPhaseEndIfCasterKnockedBack;

		// Token: 0x04003339 RID: 13113
		private int m_maxHits;

		// Token: 0x0400333A RID: 13114
		// public EffectDuration m_time;

		// Token: 0x0400333B RID: 13115
		public int m_guid;

		// Token: 0x0400333D RID: 13117
		public List<Sequence> m_barrierSequences;

		// Token: 0x0400333E RID: 13118
		private List<GameObject> m_barrierSequencePrefabs;

		// Token: 0x0400333F RID: 13119
		private bool m_playSequences;

		// Token: 0x04003345 RID: 13125
		private bool m_considerAsCover;

		// Token: 0x170002EC RID: 748
		// (get) Token: 0x06003920 RID: 14624 RVA: 0x0003325E File Offset: 0x0003145E
		// (set) Token: 0x06003921 RID: 14625 RVA: 0x00033266 File Offset: 0x00031466
		public string Name
		{
			get
			{
				return this.m_name;
			}
			private set
			{
				this.m_name = value;
			}
		}

		// Token: 0x170002ED RID: 749
		// (get) Token: 0x06003922 RID: 14626 RVA: 0x0003326F File Offset: 0x0003146F
		// (set) Token: 0x06003923 RID: 14627 RVA: 0x00033277 File Offset: 0x00031477
		public ActorData Caster
		{
			get
			{
				return this.m_owner;
			}
			private set
			{
				this.m_owner = value;
			}
		}

		// Token: 0x06003924 RID: 14628 RVA: 0x00033280 File Offset: 0x00031480
		public Vector3 GetCenterPos()
		{
			return this.m_center;
		}

		// Token: 0x06003925 RID: 14629 RVA: 0x00033288 File Offset: 0x00031488
		public Vector3 GetEndPos1()
		{
			return this.m_endpoint1;
		}

		// Token: 0x06003926 RID: 14630 RVA: 0x00033290 File Offset: 0x00031490
		public Vector3 GetEndPos2()
		{
			return this.m_endpoint2;
		}

		// Token: 0x06003927 RID: 14631 RVA: 0x00033298 File Offset: 0x00031498
		public Team GetBarrierTeam()
		{
			return this.m_team;
		}

		// Token: 0x06003928 RID: 14632 RVA: 0x000332A0 File Offset: 0x000314A0
		private bool UnlimitedHits()
		{
			return this.m_maxHits < 0;
		}

		// Token: 0x170002EE RID: 750
		// (get) Token: 0x06003929 RID: 14633 RVA: 0x000332AB File Offset: 0x000314AB
		// (set) Token: 0x0600392A RID: 14634 RVA: 0x000332B3 File Offset: 0x000314B3
		public SequenceSource BarrierSequenceSource { get; protected set; }

		// Token: 0x170002EF RID: 751
		// (get) Token: 0x0600392B RID: 14635 RVA: 0x000332BC File Offset: 0x000314BC
		// (set) Token: 0x0600392C RID: 14636 RVA: 0x000332C4 File Offset: 0x000314C4
		public BlockingRules BlocksVision { get; private set; }

		// Token: 0x170002F0 RID: 752
		// (get) Token: 0x0600392D RID: 14637 RVA: 0x000332CD File Offset: 0x000314CD
		// (set) Token: 0x0600392E RID: 14638 RVA: 0x000332D5 File Offset: 0x000314D5
		public BlockingRules BlocksAbilities { get; private set; }

		// Token: 0x170002F1 RID: 753
		// (get) Token: 0x0600392F RID: 14639 RVA: 0x000332DE File Offset: 0x000314DE
		// (set) Token: 0x06003930 RID: 14640 RVA: 0x000332E6 File Offset: 0x000314E6
		public BlockingRules BlocksMovement { get; private set; }

		// Token: 0x170002F2 RID: 754
		// (get) Token: 0x06003931 RID: 14641 RVA: 0x000332EF File Offset: 0x000314EF
		// (set) Token: 0x06003932 RID: 14642 RVA: 0x000332F7 File Offset: 0x000314F7
		public BlockingRules BlocksMovementOnCrossover { get; private set; }

		// Token: 0x170002F3 RID: 755
		// (get) Token: 0x06003933 RID: 14643 RVA: 0x00033300 File Offset: 0x00031500
		// (set) Token: 0x06003934 RID: 14644 RVA: 0x00033308 File Offset: 0x00031508
		public BlockingRules BlocksPositionTargeting { get; private set; }

		// Token: 0x170002F4 RID: 756
		// (get) Token: 0x06003935 RID: 14645 RVA: 0x00033311 File Offset: 0x00031511
		// (set) Token: 0x06003936 RID: 14646 RVA: 0x00033319 File Offset: 0x00031519
		public bool ConsiderAsCover
		{
			get
			{
				return this.m_considerAsCover;
			}
			set
			{
				this.m_considerAsCover = value;
			}
		}

		// Token: 0x06003937 RID: 14647 RVA: 0x00149AF8 File Offset: 0x00147CF8
		private void InitBarrier(int guid, string name, Vector3 center, Vector3 facingDir, float width, bool bidirectional, BlockingRules blocksVision, BlockingRules blocksAbilities, BlockingRules blocksMovement, BlockingRules blocksMovementOnCrossover, BlockingRules blocksPositionTargeting, bool considerAsCover, int maxDuration, ActorData owner, List<GameObject> barrierSequencePrefabs, bool playSequences, GameplayResponseForActor onEnemyMovedThrough, GameplayResponseForActor onAllyMovedThrough, int maxHits, bool endOnCasterDeath, SequenceSource parentSequenceSource, Team barrierTeam)
		{
			this.m_guid = guid;
			this.m_name = name;
			this.m_center = center;
			this.m_facingDir = facingDir;
			this.m_bidirectional = bidirectional;
			Vector3 a = Vector3.Normalize(Vector3.Cross(facingDir, VectorUtils.up));
			float d = width * 1; // TODO ZHENEQ width * Board.getBoard().squareSize;
			this.m_endpoint1 = center + a * d / 2f;
			this.m_endpoint2 = center - a * d / 2f;
			this.BlocksVision = blocksVision;
			this.BlocksAbilities = blocksAbilities;
			this.BlocksMovement = blocksMovement;
			this.BlocksMovementOnCrossover = blocksMovementOnCrossover;
			this.BlocksPositionTargeting = blocksPositionTargeting;
			this.m_considerAsCover = considerAsCover;
			this.m_owner = owner;
			if (this.m_owner != null)
			{
				this.m_team = this.m_owner.method_76();
			}
			else
			{
				this.m_team = barrierTeam;
			}
			// this.m_time = new EffectDuration();
			// this.m_time.duration = maxDuration;
			this.m_barrierSequencePrefabs = barrierSequencePrefabs;
			this.m_playSequences = (playSequences && this.m_barrierSequencePrefabs != null);
			this.m_barrierSequences = new List<Sequence>();
			if (this.m_playSequences)
			{
				this.BarrierSequenceSource = new SequenceSource(null, null, false, parentSequenceSource, null);
			}
			this.m_maxHits = maxHits;
		}

		public bool CanBeSeenThroughBy(ActorData viewer)
		{
			return !this.IsBlocked(viewer, this.BlocksVision);
		}

		//public bool CanBeShotThroughBy(ActorData shooter)
		//{
		//	return BarrierManager.Get().SuppressingAbilityBlocks() || !this.IsBlocked(shooter, this.BlocksAbilities);
		//}

		public bool CanBeMovedThroughBy(ActorData mover)
		{
			return !this.IsBlocked(mover, this.BlocksMovement);
		}

		public bool CanMoveThroughAfterCrossoverBy(ActorData mover)
		{
			return !this.IsBlocked(mover, this.BlocksMovementOnCrossover);
		}

		public bool IsPositionTargetingBlockedFor(ActorData caster)
		{
			return this.IsBlocked(caster, this.BlocksPositionTargeting);
		}

		private bool IsBlocked(ActorData actor, BlockingRules rules)
		{
			bool result;
			switch (rules)
			{
				case BlockingRules.ForNobody:
					result = false;
					break;
				case BlockingRules.ForEnemies:
					result = (actor == null || actor.method_76() != this.m_team);
					break;
				case BlockingRules.ForEverybody:
					result = true;
					break;
				default:
					result = false;
					break;
			}
			return result;
		}

		//public virtual void OnStart(bool delayVisionUpdate, out List<ActorData> visionUpdaters)
		//{
		//	visionUpdaters = new List<ActorData>();
		//	if (NetworkClient.active && this.m_makeClientGeo)
		//	{
		//		float squareSize = Board.getBoard().squareSize;
		//		Vector3 a = this.m_endpoint2 - this.m_endpoint1;
		//		bool flag = Mathf.Abs(a.z) > Mathf.Abs(a.x);
		//		Vector3 vector = this.m_endpoint1 + 0.5f * a;
		//		this.m_generatedClientGeometry = GameObject.CreatePrimitive(PrimitiveType.Cube);
		//		this.m_generatedClientGeometry.transform.position = new Vector3(vector.x, 1.5f * squareSize, vector.z);
		//		if (flag)
		//		{
		//			this.m_generatedClientGeometry.transform.localScale = new Vector3(0.25f, 2f * squareSize, a.magnitude);
		//		}
		//		else
		//		{
		//			this.m_generatedClientGeometry.transform.localScale = new Vector3(a.magnitude, 2f * squareSize, 0.25f);
		//		}
		//	}
		//}

		//public virtual void OnEnd()
		//{
		//	if (NetworkServer.active)
		//	{
		//		foreach (Sequence sequence in this.m_barrierSequences)
		//		{
		//			if (sequence != null)
		//			{
		//				sequence.MarkForRemoval();
		//			}
		//		}
		//	}
		//	if (NetworkClient.active && this.m_makeClientGeo)
		//	{
		//		if (this.m_generatedClientGeometry != null)
		//		{
		//			UnityEngine.Object.DestroyObject(this.m_generatedClientGeometry);
		//		}
		//		this.m_generatedClientGeometry = null;
		//	}
		//}

		public bool CanAffectVision()
		{
			return this.BlocksVision == BlockingRules.ForEnemies || this.BlocksVision == BlockingRules.ForEverybody;
		}

		public bool CanAffectMovement()
		{
			return this.BlocksMovement == BlockingRules.ForEnemies || this.BlocksMovement == BlockingRules.ForEverybody;
		}

		public bool CrossingBarrier(Vector3 src, Vector3 dest)
		{
			bool flag = VectorUtils.IsPointInLaser(src, this.m_endpoint1, this.m_endpoint2, 0.001f);
			bool flag2 = VectorUtils.IsPointInLaser(dest, this.m_endpoint1, this.m_endpoint2, 0.001f);
			bool result;
			if (flag)
			{
				result = false;
			}
			else if (!flag2 && VectorUtils.OnSameSideOfLine(src, dest, this.m_endpoint1, this.m_endpoint2))
			{
				result = false;
			}
			else if (!flag2 && VectorUtils.OnSameSideOfLine(this.m_endpoint1, this.m_endpoint2, src, dest))
			{
				result = false;
			}
			else if (this.m_bidirectional)
			{
				result = true;
			}
			else
			{
				Vector3 lhs = src - this.m_center;
				float num = Vector3.Dot(lhs, this.m_facingDir);
				result = (num > 0f);
			}
			return result;
		}

		public bool CrossingBarrierForVision(Vector3 src, Vector3 dest)
		{
			return this.SegmentsIntersectForVision(src, dest, this.m_endpoint1, this.m_endpoint2);
		}

		private bool SegmentsIntersectForVision(Vector3 startA, Vector3 endA, Vector3 startB, Vector3 endB)
		{
			return Barrier.PointsAreCounterClockwise(startA, startB, endB) != Barrier.PointsAreCounterClockwise(endA, startB, endB) && Barrier.PointsAreCounterClockwise(startA, endA, startB) != Barrier.PointsAreCounterClockwise(startA, endA, endB);
		}

		private static bool PointsAreCounterClockwise(Vector3 a, Vector3 b, Vector3 c)
		{
			return (c.Z - a.Z) * (b.X - a.X) > (b.Z - a.Z) * (c.X - a.X);
		}

		//public Vector3 GetIntersectionPoint(Vector3 src, Vector3 dest)
		//{
		//	Vector3 vector = dest - src;
		//	Vector3 directionOfSecond = this.m_endpoint2 - this.m_endpoint1;
		//	bool flag;
		//	Vector3 vector2 = VectorUtils.GetLineLineIntersection(src, vector, this.m_endpoint1, directionOfSecond, out flag);
		//	if (flag)
		//	{
		//		vector2.y = src.y;
		//		Vector3 normalized = (-vector).normalized;
		//		vector2 += normalized * 0.05f;
		//	}
		//	return vector2;
		//}

		public Vector3 GetCollisionNormal(Vector3 incomingDir)
		{
			if (this.m_bidirectional && Vector3.Dot(incomingDir, this.m_facingDir) > 0f)
			{
				return -this.m_facingDir;
			}
			return this.m_facingDir;
		}

		public Vector3 GetFacingDir()
		{
			return this.m_facingDir;
		}

		internal static BarrierSerializeInfo BarrierToSerializeInfo(Barrier barrier)
		{
			BarrierSerializeInfo barrierSerializeInfo = new BarrierSerializeInfo();
			barrierSerializeInfo.m_guid = barrier.m_guid;
			barrierSerializeInfo.m_center = barrier.m_center;
			barrierSerializeInfo.m_widthInWorld = (barrier.m_endpoint1 - barrier.m_endpoint2).Length();
			barrierSerializeInfo.m_facingHorizontalAngle = VectorUtils.HorizontalAngle_Deg(barrier.m_facingDir);
			barrierSerializeInfo.m_bidirectional = barrier.m_bidirectional;
			barrierSerializeInfo.m_blocksVision = (sbyte)barrier.BlocksVision;
			barrierSerializeInfo.m_blocksAbilities = (sbyte)barrier.BlocksAbilities;
			barrierSerializeInfo.m_blocksMovement = (sbyte)barrier.BlocksMovement;
			barrierSerializeInfo.m_blocksMovementOnCrossover = (sbyte)barrier.BlocksMovementOnCrossover;
			barrierSerializeInfo.m_blocksPositionTargeting = (sbyte)barrier.BlocksPositionTargeting;
			barrierSerializeInfo.m_considerAsCover = barrier.m_considerAsCover;
			barrierSerializeInfo.m_team = (sbyte)barrier.m_team;
			int ownerIndex = ActorData.s_invalidActorIndex;
			if (barrier.m_owner != null)
			{
				ownerIndex = barrier.m_owner.ActorIndex;
			}
			barrierSerializeInfo.m_ownerIndex = ownerIndex;
			barrierSerializeInfo.m_makeClientGeo = barrier.m_makeClientGeo;
			return barrierSerializeInfo;
		}

		//internal static Barrier CreateBarrierFromSerializeInfo(BarrierSerializeInfo info)
		//{
		//	BlockingRules blocksVision = (BlockingRules)info.m_blocksVision;
		//	BlockingRules blocksAbilities = (BlockingRules)info.m_blocksAbilities;
		//	BlockingRules blocksMovement = (BlockingRules)info.m_blocksMovement;
		//	BlockingRules blocksMovementOnCrossover = (BlockingRules)info.m_blocksMovementOnCrossover;
		//	BlockingRules blocksPositionTargeting = (BlockingRules)info.m_blocksPositionTargeting;
		//	ActorData owner = null;
		//	if (info.m_ownerIndex != ActorData.s_invalidActorIndex)
		//	{
		//		owner = GameFlowData.Get().FindActorByActorIndex(info.m_ownerIndex);
		//	}
		//	Vector3 facingDir = VectorUtils.AngleDegreesToVector(info.m_facingHorizontalAngle);
		//	float width = info.m_widthInWorld / Board.getBoard().squareSize;
		//	return new Barrier(info.m_guid, string.Empty, info.m_center, facingDir, width, info.m_bidirectional, blocksVision, blocksAbilities, blocksMovement, blocksPositionTargeting, info.m_considerAsCover, -1, owner, null, true, null, null, -1, false, null, Team.Invalid)
		//	{
		//		BlocksMovementOnCrossover = blocksMovementOnCrossover,
		//		m_makeClientGeo = info.m_makeClientGeo
		//	};
		//}

		//public virtual List<ServerClientUtils.SequenceStartData> GetSequenceStartDataList()
		//{
		//	List<ServerClientUtils.SequenceStartData> list = new List<ServerClientUtils.SequenceStartData>();
		//	if (this.m_barrierSequencePrefabs != null && this.m_playSequences)
		//	{
		//		Quaternion targetRotation = Quaternion.LookRotation(this.m_facingDir);
		//		ActorData[] targetActorArray = new ActorData[0];
		//		using (List<GameObject>.Enumerator enumerator = this.m_barrierSequencePrefabs.GetEnumerator())
		//		{
		//		IL_F8:
		//			while (enumerator.MoveNext())
		//			{
		//				GameObject gameObject = enumerator.Current;
		//				if (gameObject != null)
		//				{
		//					Sequence[] components = gameObject.GetComponents<Sequence>();
		//					bool flag = false;
		//					foreach (Sequence sequence in components)
		//					{
		//						if (sequence is OverwatchScanSequence || sequence is GroundLineSequence || sequence is ExoLaserHittingWallSequence)
		//						{
		//							flag = true;
		//						IL_A1:
		//							Sequence.IExtraSequenceParams[] extraParams = null;
		//							if (flag)
		//							{
		//								extraParams = new Sequence.IExtraSequenceParams[]
		//								{
		//								new GroundLineSequence.ExtraParams
		//								{
		//									startPos = this.m_endpoint2,
		//									endPos = this.m_endpoint1
		//								}
		//								};
		//							}
		//							ServerClientUtils.SequenceStartData item = new ServerClientUtils.SequenceStartData(gameObject, null, targetRotation, targetActorArray, this.m_owner, this.BarrierSequenceSource, extraParams);
		//							list.Add(item);
		//							goto IL_F8;
		//						}
		//					}
		//					goto IL_A1;
		//				}
		//			}
		//		}
		//	}
		//	return list;
		//}

		//public void DrawGizmos()
		//{
		//	Vector3 vector = new Vector3(0f, 0f, 0f);
		//	for (int i = 0; i < 3; i++)
		//	{
		//		vector += new Vector3(0f, 0.3f, 0f);
		//		Gizmos.color = Color.blue;
		//		Gizmos.DrawLine(this.m_endpoint1 + vector, this.m_endpoint2 + vector);
		//		Gizmos.color = Color.white;
		//		Gizmos.DrawLine(this.m_center + vector, this.m_center + this.m_facingDir + vector);
		//	}
		//}
	}
}
