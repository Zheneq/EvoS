using System;
using System.Collections.Generic;
using System.Numerics;
using EvoS.Framework.Assets;
using EvoS.Framework.Assets.Serialized.Behaviours;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.NetworkBehaviours;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.Unity;
using EvoS.Framework.Logging;

namespace EvoS.Framework.Misc
{
    [Serializable]
    [SerializedMonoBehaviour("ActorMovement")]
    public class ActorMovement : MonoBehaviour
    {
        private static int s_maxSquaresCanMoveToCacheCount = 4;
        public float m_brushTransitionAnimationSpeed;

        public float m_brushTransitionAnimationSpeedEaseTime = 0.4f;

        private Dictionary<BoardSquare, BoardSquarePathInfo> m_tempClosedSquares = new Dictionary<BoardSquare, BoardSquarePathInfo>();
        internal ActorData m_actor;

        private float m_moveTimeoutTime;

        private List<ActorMovement.SquaresCanMoveToCacheEntry> m_squaresCanMoveToCache;
        private HashSet<BoardSquare> m_squaresCanMoveTo;
        private HashSet<BoardSquare> m_squareCanMoveToWithQueuedAbility;
        private BoardSquarePathInfo m_gameplayPath;
        private BoardSquarePathInfo m_aestheticPath;

        public ActorMovement()
        {
        }

        public ActorMovement(AssetFile assetFile, StreamReader stream)
        {
            DeserializeAsset(assetFile, stream);
        }

        public override void Awake()
        {
            m_actor = GetComponent<ActorData>();
            this.m_squaresCanMoveToCache = new List<ActorMovement.SquaresCanMoveToCacheEntry>();
            m_squaresCanMoveTo = new HashSet<BoardSquare>();
            m_squareCanMoveToWithQueuedAbility = new HashSet<BoardSquare>();
        }

        public BoardSquare GetTravelBoardSquare()
        {
            BoardSquare boardSquare = null;
            if (m_gameplayPath != null)
                boardSquare = m_gameplayPath.square;
            return boardSquare ?? m_actor.method_74();
        }

        public void UpdateSquaresCanMoveTo()
        {
            float maxMoveDist = this.m_actor.RemainingHorizontalMovement;
            float innerMoveDist = this.m_actor.RemainingMovementWithQueuedAbility;
            BoardSquare squareToStartFrom = this.m_actor.MoveFromBoardSquare;
            // removed Options_UI client-side stuff
            if (!FirstTurnMovement.CanWaypoint() && this.m_actor == GameFlowData.activeOwnedActorData)
            {
                maxMoveDist = this.CalculateMaxHorizontalMovement(false, false);
                innerMoveDist = this.CalculateMaxHorizontalMovement(true, false);
                squareToStartFrom = this.m_actor.InitialMoveStartSquare;
            }
            this.GetSquaresCanMoveTo_InnerOuter(squareToStartFrom, maxMoveDist, innerMoveDist, out this.m_squaresCanMoveTo, out this.m_squareCanMoveToWithQueuedAbility);
            if (Board != null)  // for client?
            {
                Board.MarkForUpdateValidSquares(true);
            }
            if (this.m_actor == GameFlowData.activeOwnedActorData)
            {
                LineData component = this.m_actor.GetComponent<LineData>();
                if (component != null)
                {
                    component.OnCanMoveToSquaresUpdated();
                }
            }
        }

        public bool CanMoveToBoardSquare(int x, int y)
        {
            bool result = false;
            BoardSquare square = Board.GetBoardSquare(x, y);
            if (square != null)
            {
                result = this.CanMoveToBoardSquare(square);
            }
            return result;
        }

        public bool CanMoveToBoardSquare(BoardSquare dest)
        {
            return this.m_squaresCanMoveTo.Contains(dest);
        }

        public BoardSquare GetClosestMoveableSquareTo(BoardSquare selectedSquare, bool alwaysIncludeMoverSquare = true)
        {
            BoardSquare bestSquare = alwaysIncludeMoverSquare ? this.m_actor.GetCurrentBoardSquare() : null;
            float bestDistance = alwaysIncludeMoverSquare ? bestSquare.HorizontalDistanceOnBoardTo(selectedSquare) : 100000f;
            if (selectedSquare != null)
            {
                using (HashSet<BoardSquare>.Enumerator enumerator = this.m_squaresCanMoveTo.GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        BoardSquare curSquare = enumerator.Current;
                        float curDistance = curSquare.HorizontalDistanceOnBoardTo(selectedSquare);
                        if (curDistance <= bestDistance)
                        {
                            bestDistance = curDistance;
                            bestSquare = curSquare;
                        }
                    }
                    return bestSquare;
                }
            }
            Log.Print(LogType.Error, "Trying to find the closest moveable square to a null square.  Code error-- tell Danny.");
            return bestSquare;
        }
        public BoardSquarePathInfo BuildPathTo(BoardSquare src, BoardSquare dest)
        {
            float maxHorizontalMovement = this.CalculateMaxHorizontalMovement(false, false);
            return this.BuildPathTo(src, dest, maxHorizontalMovement, false, null);
        }

        public BoardSquarePathInfo BuildPathTo(BoardSquare src, BoardSquare dest, float maxHorizontalMovement, bool ignoreBarriers, List<BoardSquare> claimedSquares)
        {
            BoardSquarePathInfo currentPathInfo = null;
            if (!(src == null) && !(dest == null))
            {
                BuildNormalPathNodePool normalPathBuildScratchPool = Board.m_normalPathBuildScratchPool;
                normalPathBuildScratchPool.ResetAvailableNodeIndex();
                Vector3 tieBreakerDir = dest.ToVector3() - src.ToVector3();
                Vector3 tieBreakerPos = src.ToVector3();
                BuildNormalPathHeap normalPathNodeHeap = Board.m_normalPathNodeHeap;
                normalPathNodeHeap.Clear();
                normalPathNodeHeap.SetTieBreakerDirAndPos(tieBreakerDir, tieBreakerPos);
                this.m_tempClosedSquares.Clear();
                Dictionary<BoardSquare, BoardSquarePathInfo> tempClosedSquares = this.m_tempClosedSquares;
                BoardSquarePathInfo allocatedNode = normalPathBuildScratchPool.GetAllocatedNode();
                allocatedNode.square = src;
                normalPathNodeHeap.Insert(allocatedNode);
                List<BoardSquare> list = new List<BoardSquare>(8);
                bool diagonalMovementAllowed = GameplayData.m_diagonalMovement != GameplayData.DiagonalMovement.Disabled;
                bool flag2 = GameplayData.m_movementMaximumType != GameplayData.MovementMaximumType.CannotExceedMax;
                bool destClaimed = claimedSquares != null && claimedSquares.Contains(dest);
                while (!normalPathNodeHeap.IsEmpty())
                {
                    BoardSquarePathInfo heapTopPathInfo = normalPathNodeHeap.ExtractTop();
                    if (heapTopPathInfo.square == dest)
                    {
                        currentPathInfo = heapTopPathInfo;
                        break;
                    }
                    tempClosedSquares[heapTopPathInfo.square] = heapTopPathInfo;
                    list.Clear();
                    if (!diagonalMovementAllowed)
                    {
                        Board.getStraightAdjacentSquares(heapTopPathInfo.square.X, heapTopPathInfo.square.Y, ref list);
                    }
                    else
                    {
                        Board.getAllAdjacentSquares(heapTopPathInfo.square.X, heapTopPathInfo.square.Y, ref list);
                    }
                    for (int i = 0; i < list.Count; i++)
                    {
                        BoardSquare boardSquare = list[i];
                        bool diag = Board.AreDiagonallyAdjacent(heapTopPathInfo.square, boardSquare);
                        float cost;
                        if (diag)
                        {
                            cost = heapTopPathInfo.moveCost + 1.5f;
                        }
                        else
                        {
                            cost = heapTopPathInfo.moveCost + 1f;
                        }
                        bool flag5 = flag2 ? heapTopPathInfo.moveCost < maxHorizontalMovement : cost <= maxHorizontalMovement;
                        if (flag5 && this.CanCrossToAdjacentSquare(heapTopPathInfo.square, boardSquare, ignoreBarriers, (!diag) ? ActorMovement.DiagonalCalcFlag.NotDiagonal : ActorMovement.DiagonalCalcFlag.IsDiagonal) && FirstTurnMovement.CanActorMoveToSquare(this.m_actor, boardSquare))
                        {
                            BoardSquarePathInfo allocatedNode2 = normalPathBuildScratchPool.GetAllocatedNode();
                            allocatedNode2.square = boardSquare;
                            allocatedNode2.moveCost = cost;
                            if (claimedSquares != null && destClaimed && allocatedNode2.square == dest)
                            {
                                int num2 = 1;
                                BoardSquarePathInfo boardSquarePathInfo3 = heapTopPathInfo;
                                while (boardSquarePathInfo3 != null && claimedSquares.Contains(boardSquarePathInfo3.square))
                                {
                                    num2++;
                                    boardSquarePathInfo3 = boardSquarePathInfo3.prev;
                                }
                                allocatedNode2.m_expectedBackupNum = num2;
                            }
                            float squareSize = Board.squareSize;
                            if (!flag2)
                            {
                                allocatedNode2.heuristicCost = (boardSquare.transform.position - dest.transform.position).Length() / squareSize;
                            }
                            else
                            {
                                float num3 = (float)Mathf.Abs(boardSquare.X - dest.X);
                                float num4 = (float)Mathf.Abs(boardSquare.Y - dest.Y);
                                float num5 = num3 + num4 - 0.5f * Mathf.Min(num3, num4);
                                float heuristicCost = Mathf.Max(0f, num5 - 1.01f);
                                allocatedNode2.heuristicCost = heuristicCost;
                            }
                            allocatedNode2.prev = heapTopPathInfo;
                            bool flag6 = false;
                            if (tempClosedSquares.ContainsKey(allocatedNode2.square))
                            {
                                flag6 = (allocatedNode2.F_cost > tempClosedSquares[allocatedNode2.square].F_cost);
                            }
                            if (!flag6)
                            {
                                bool flag7 = false;
                                BoardSquarePathInfo boardSquarePathInfo4 = normalPathNodeHeap.TryGetNodeInHeapBySquare(allocatedNode2.square);
                                if (boardSquarePathInfo4 != null)
                                {
                                    flag7 = true;
                                    if (allocatedNode2.F_cost < boardSquarePathInfo4.F_cost)
                                    {
                                        normalPathNodeHeap.UpdatePriority(allocatedNode2);
                                    }
                                }
                                if (!flag7)
                                {
                                    normalPathNodeHeap.Insert(allocatedNode2);
                                }
                            }
                        }
                    }
                }
                if (currentPathInfo != null)
                {
                    while (currentPathInfo.prev != null)
                    {
                        currentPathInfo.prev.next = currentPathInfo;
                        currentPathInfo = currentPathInfo.prev;
                    }
                    currentPathInfo = currentPathInfo.Clone(null);
                    normalPathBuildScratchPool.ResetAvailableNodeIndex();
                }
                return currentPathInfo;
            }
            return currentPathInfo;
        }

        public void BuildSquaresCanMoveTo_InnerOuter(BoardSquare squareToStartFrom, float maxHorizontalMovement, float innerMaxMove, out HashSet<BoardSquare> eligibleSquares, out HashSet<BoardSquare> innerSquares)
        {
            eligibleSquares = new HashSet<BoardSquare>();
            innerSquares = new HashSet<BoardSquare>();
            if (squareToStartFrom != null)
            {
                if (maxHorizontalMovement != 0f)
                {
                    eligibleSquares.Add(squareToStartFrom);
                    if (innerMaxMove > 0f)
                    {
                        innerSquares.Add(squareToStartFrom);
                    }
                    LinkedList<ActorMovement.BoardSquareMovementInfo> linkedList = new LinkedList<ActorMovement.BoardSquareMovementInfo>();
                    HashSet<BoardSquare> hashSet = new HashSet<BoardSquare>();
                    ActorMovement.BoardSquareMovementInfo value;
                    value.square = squareToStartFrom;
                    value.cost = 0f;
                    value.prevCost = 0f;
                    linkedList.AddLast(value);
                    bool cannotExceedMaxMovement = GameplayData != null && GameplayData.m_movementMaximumType == GameplayData.MovementMaximumType.CannotExceedMax;
                    while (linkedList.Count > 0)
                    {
                        ActorMovement.BoardSquareMovementInfo value2 = linkedList.First.Value;
                        BoardSquare square = value2.square;
                        int x = square.X;
                        int y = square.Y;
                        for (int i = -1; i <= 1; i++)
                        {
                            for (int j = -1; j <= 1; j++)
                            {
                                if (i != 0 || j != 0)
                                {
                                    BoardSquare square2 = Board.GetBoardSquare(x + i, y + j);
                                    if (square2 != null && !hashSet.Contains(square2))
                                    {
                                        bool diagAdjacent = Board.AreDiagonallyAdjacent(square, square2);
                                        float cost;
                                        if (diagAdjacent)
                                        {
                                            cost = value2.cost + 1.5f;
                                        }
                                        else
                                        {
                                            cost = value2.cost + 1f;
                                        }
                                        bool flag3 = cannotExceedMaxMovement ? cost <= maxHorizontalMovement : value2.cost < maxHorizontalMovement;
                                        if (flag3 && this.CanCrossToAdjacentSquare(square, square2, false, (!diagAdjacent) ? ActorMovement.DiagonalCalcFlag.NotDiagonal : ActorMovement.DiagonalCalcFlag.IsDiagonal))
                                        {
                                            ActorMovement.BoardSquareMovementInfo value3;
                                            value3.square = square2;
                                            value3.cost = cost;
                                            value3.prevCost = value2.cost;
                                            bool flag4 = false;
                                            LinkedListNode<ActorMovement.BoardSquareMovementInfo> linkedListNode = linkedList.First;
                                            while (linkedListNode != linkedList.Last)
                                            {
                                                ActorMovement.BoardSquareMovementInfo value4 = linkedListNode.Value;
                                                if (!(value4.square == square2))
                                                {
                                                    linkedListNode = linkedListNode.Next;
                                                }
                                                else
                                                {
                                                    flag4 = true;
                                                    if (value4.cost > cost)
                                                    {
                                                        linkedListNode.Value = value3;
                                                    }
                                                    else if (value4.cost == cost && value3.prevCost < value4.prevCost)
                                                    {
                                                        linkedListNode.Value = value3;
                                                    }
                                                    if (!flag4 && FirstTurnMovement.CanActorMoveToSquare(this.m_actor, square2))
                                                    {
                                                        linkedList.AddLast(value3);
                                                    }
                                                    goto IL_231;
                                                }
                                            }
                                            if (!flag4 && FirstTurnMovement.CanActorMoveToSquare(this.m_actor, square2))
                                            {
                                                linkedList.AddLast(value3);
                                            }
                                        }
                                    }
                                }
                            IL_231:;
                            }
                        }
                        if (MovementUtils.CanStopOnSquare(square) && SinglePlayerManager.IsDestinationAllowed(this.m_actor, square, true) && FirstTurnMovement.CanActorMoveToSquare(this.m_actor, square))
                        {
                            if (!eligibleSquares.Contains(square))
                            {
                                eligibleSquares.Add(square);
                            }
                            if (innerMaxMove > 0f && !innerSquares.Contains(square))
                            {
                                bool valid = cannotExceedMaxMovement ? value2.cost <= innerMaxMove : value2.prevCost < innerMaxMove;
                                if (valid)
                                {
                                    innerSquares.Add(square);
                                }
                            }
                        }
                        hashSet.Add(square);
                        linkedList.RemoveFirst();
                    }
                    return;
                }
            }
        }

        public HashSet<BoardSquare> BuildSquaresCanMoveTo(BoardSquare squareToStartFrom, float maxHorizontalMovement)
        {
            HashSet<BoardSquare> hashSet = new HashSet<BoardSquare>();
            if (!(squareToStartFrom == null))
            {
                if (maxHorizontalMovement != 0f)
                {
                    hashSet.Add(squareToStartFrom);
                    LinkedList<ActorMovement.BoardSquareMovementInfo> linkedList = new LinkedList<ActorMovement.BoardSquareMovementInfo>();
                    HashSet<BoardSquare> hashSet2 = new HashSet<BoardSquare>();
                    ActorMovement.BoardSquareMovementInfo value;
                    value.square = squareToStartFrom;
                    value.cost = 0f;
                    value.prevCost = 0f;
                    linkedList.AddLast(value);
                    List<BoardSquare> list = new List<BoardSquare>(8);
                    while (linkedList.Count > 0)
                    {
                        ActorMovement.BoardSquareMovementInfo value2 = linkedList.First.Value;
                        BoardSquare square = value2.square;
                        list.Clear();
                        if (GameplayData != null && GameplayData.m_diagonalMovement == GameplayData.DiagonalMovement.Disabled)
                        {
                            Board.getStraightAdjacentSquares(square.X, square.Y, ref list);
                        }
                        else
                        {
                            Board.getAllAdjacentSquares(square.X, square.Y, ref list);
                        }
                        for (int i = 0; i < list.Count; i++)
                        {
                            BoardSquare boardSquare = list[i];
                            if (!hashSet2.Contains(boardSquare))
                            {
                                bool diag = Board.AreDiagonallyAdjacent(square, boardSquare);
                                float num;
                                if (diag)
                                {
                                    num = value2.cost + 1.5f;
                                }
                                else
                                {
                                    num = value2.cost + 1f;
                                }
                                bool flag2;
                                if (GameplayData != null && GameplayData.m_movementMaximumType == GameplayData.MovementMaximumType.CannotExceedMax)
                                {
                                    flag2 = (num <= maxHorizontalMovement);
                                }
                                else
                                {
                                    flag2 = (value2.cost < maxHorizontalMovement);
                                }
                                if (flag2)
                                {
                                    ActorMovement.BoardSquareMovementInfo value3;
                                    value3.square = boardSquare;
                                    value3.cost = num;
                                    value3.prevCost = value2.cost;
                                    bool flag3 = false;
                                    LinkedListNode<ActorMovement.BoardSquareMovementInfo> linkedListNode = linkedList.First;
                                    while (linkedListNode != linkedList.Last)
                                    {
                                        ActorMovement.BoardSquareMovementInfo value4 = linkedListNode.Value;
                                        if (!(value4.square == boardSquare))
                                        {
                                            linkedListNode = linkedListNode.Next;
                                        }
                                        else
                                        {
                                            flag3 = true;
                                            if (value4.cost > num)
                                            {
                                                linkedListNode.Value = value3;
                                            }
                                            if (!flag3 && this.CanCrossToAdjacentSquare(square, boardSquare, false, (!diag) ? ActorMovement.DiagonalCalcFlag.NotDiagonal : ActorMovement.DiagonalCalcFlag.IsDiagonal) && FirstTurnMovement.CanActorMoveToSquare(this.m_actor, boardSquare))
                                            {
                                                linkedList.AddLast(value3);
                                            }
                                            goto IL_202;
                                        }
                                    }
                                    if (!flag3 && this.CanCrossToAdjacentSquare(square, boardSquare, false, (!diag) ? ActorMovement.DiagonalCalcFlag.NotDiagonal : ActorMovement.DiagonalCalcFlag.IsDiagonal) && FirstTurnMovement.CanActorMoveToSquare(this.m_actor, boardSquare))
                                    {
                                        linkedList.AddLast(value3);
                                    }
                                }
                            }
                        IL_202:;
                        }
                        if (!hashSet.Contains(square) && MovementUtils.CanStopOnSquare(square) && SinglePlayerManager.IsDestinationAllowed(this.m_actor, square, true) && FirstTurnMovement.CanActorMoveToSquare(this.m_actor, square))
                        {
                            hashSet.Add(square);
                        }
                        hashSet2.Add(square);
                        linkedList.RemoveFirst();
                    }
                    return hashSet;
                }
            }
            return hashSet;
        }

        public HashSet<BoardSquare> CheckSquareCanMoveToCache(BoardSquare squareToStartFrom, float maxHorizontalMovement)
        {
            HashSet<BoardSquare> result = null;
            int squaresCanMoveToSinglePlayerState = -1;
            if (SinglePlayerManager != null && this.m_actor.SpawnerId == -1)
            {
                squaresCanMoveToSinglePlayerState = SinglePlayerManager.GetCurrentScriptIndex();
            }
            int squaresCanMoveToBarrierState = -1;
            if (BarrierManager != null)
            {
                squaresCanMoveToBarrierState = BarrierManager.GetMovementStateChangesFor(this.m_actor);
            }
            FirstTurnMovement.RestrictedMovementState squaresCanMoveToFirstTurnState = FirstTurnMovement.RestrictedMovementState.Invalid;
            if (FirstTurnMovement != null)
            {
                squaresCanMoveToFirstTurnState = FirstTurnMovement.GetRestrictedMovementState();
            }
            else
            {
                // ZHENEQ
                Log.Print(LogType.Error, "FirstTurnMovement is null!");
            }
            ActorMovement.SquaresCanMoveToCacheEntry squaresCanMoveToCacheEntry = new ActorMovement.SquaresCanMoveToCacheEntry();
            squaresCanMoveToCacheEntry.m_squaresCanMoveToOrigin = squareToStartFrom;
            squaresCanMoveToCacheEntry.m_squaresCanMoveToHorizontalAllowed = maxHorizontalMovement;
            squaresCanMoveToCacheEntry.m_squaresCanMoveToSinglePlayerState = squaresCanMoveToSinglePlayerState;
            squaresCanMoveToCacheEntry.m_squaresCanMoveToBarrierState = squaresCanMoveToBarrierState;
            squaresCanMoveToCacheEntry.m_squaresCanMoveToFirstTurnState = squaresCanMoveToFirstTurnState;
            int num = 0;
            ActorMovement.SquaresCanMoveToCacheEntry item = null;
            for (int i = 0; i < this.m_squaresCanMoveToCache.Count; i++)
            {
                ActorMovement.SquaresCanMoveToCacheEntry squaresCanMoveToCacheEntry2 = this.m_squaresCanMoveToCache[i];
                if (squaresCanMoveToCacheEntry2.m_squaresCanMoveToOrigin == squaresCanMoveToCacheEntry.m_squaresCanMoveToOrigin && squaresCanMoveToCacheEntry2.m_squaresCanMoveToHorizontalAllowed == squaresCanMoveToCacheEntry.m_squaresCanMoveToHorizontalAllowed && squaresCanMoveToCacheEntry2.m_squaresCanMoveToSinglePlayerState == squaresCanMoveToCacheEntry.m_squaresCanMoveToSinglePlayerState && squaresCanMoveToCacheEntry2.m_squaresCanMoveToBarrierState == squaresCanMoveToCacheEntry.m_squaresCanMoveToBarrierState && squaresCanMoveToCacheEntry2.m_squaresCanMoveToFirstTurnState == squaresCanMoveToCacheEntry.m_squaresCanMoveToFirstTurnState)
                {
                    result = squaresCanMoveToCacheEntry2.m_squaresCanMoveTo;
                    item = squaresCanMoveToCacheEntry2;
                    num = i;
                    break;
                }
            }
            if (num != 0)
            {
                this.m_squaresCanMoveToCache.RemoveAt(num);
                this.m_squaresCanMoveToCache.Insert(0, item);
            }
            return result;
        }

        public void AddToSquareCanMoveToCache(BoardSquare squareToStartFrom, float maxHorizontalMovement, HashSet<BoardSquare> squaresCanMoveTo)
        {
            int squaresCanMoveToBarrierState = -1;
            int squaresCanMoveToSinglePlayerState = -1;
            FirstTurnMovement.RestrictedMovementState squaresCanMoveToFirstTurnState = FirstTurnMovement.RestrictedMovementState.Invalid;
            if (SinglePlayerManager != null && this.m_actor.SpawnerId == -1)
            {
                squaresCanMoveToSinglePlayerState = SinglePlayerManager.GetCurrentScriptIndex();
            }
            if (BarrierManager != null)
            {
                squaresCanMoveToBarrierState = BarrierManager.GetMovementStateChangesFor(this.m_actor);
            }
            if (FirstTurnMovement != null)
            {
                squaresCanMoveToFirstTurnState = FirstTurnMovement.GetRestrictedMovementState();
            }
            else
            {
                // ZHENEQ
                Log.Print(LogType.Error, "FirstTurnMovement is null!");
            }
            ActorMovement.SquaresCanMoveToCacheEntry squaresCanMoveToCacheEntry = new ActorMovement.SquaresCanMoveToCacheEntry();
            squaresCanMoveToCacheEntry.m_squaresCanMoveToOrigin = squareToStartFrom;
            squaresCanMoveToCacheEntry.m_squaresCanMoveToHorizontalAllowed = maxHorizontalMovement;
            squaresCanMoveToCacheEntry.m_squaresCanMoveToSinglePlayerState = squaresCanMoveToSinglePlayerState;
            squaresCanMoveToCacheEntry.m_squaresCanMoveToBarrierState = squaresCanMoveToBarrierState;
            squaresCanMoveToCacheEntry.m_squaresCanMoveToFirstTurnState = squaresCanMoveToFirstTurnState;
            squaresCanMoveToCacheEntry.m_squaresCanMoveTo = squaresCanMoveTo;
            if (this.m_squaresCanMoveToCache.Count >= ActorMovement.s_maxSquaresCanMoveToCacheCount)
            {
                this.m_squaresCanMoveToCache.RemoveAt(this.m_squaresCanMoveToCache.Count - 1);
            }
            this.m_squaresCanMoveToCache.Insert(0, squaresCanMoveToCacheEntry);
        }

        public HashSet<BoardSquare> GetSquaresCanMoveTo(BoardSquare squareToStartFrom, float maxHorizontalMovement)
        {
            HashSet<BoardSquare> hashSet = this.CheckSquareCanMoveToCache(squareToStartFrom, maxHorizontalMovement);
            if (hashSet != null)
            {
                return hashSet;
            }
            hashSet = this.BuildSquaresCanMoveTo(squareToStartFrom, maxHorizontalMovement);
            this.AddToSquareCanMoveToCache(squareToStartFrom, maxHorizontalMovement, hashSet);
            return hashSet;
        }

        public void GetSquaresCanMoveTo_InnerOuter(BoardSquare squareToStartFrom, float maxMoveDist, float innerMoveDist, out HashSet<BoardSquare> outMaxMoveSquares, out HashSet<BoardSquare> outInnerMoveSquares)
        {
            HashSet<BoardSquare> MaxMoveSquares = this.CheckSquareCanMoveToCache(squareToStartFrom, maxMoveDist);
            HashSet<BoardSquare> InnerMoveSquares = this.CheckSquareCanMoveToCache(squareToStartFrom, innerMoveDist);
            if (MaxMoveSquares == null && InnerMoveSquares == null)
            {
                this.BuildSquaresCanMoveTo_InnerOuter(squareToStartFrom, maxMoveDist, innerMoveDist, out MaxMoveSquares, out InnerMoveSquares);
                this.AddToSquareCanMoveToCache(squareToStartFrom, maxMoveDist, MaxMoveSquares);
                this.AddToSquareCanMoveToCache(squareToStartFrom, innerMoveDist, InnerMoveSquares);
            }
            else if (MaxMoveSquares == null)
            {
                HashSet<BoardSquare> hashSet3;
                this.BuildSquaresCanMoveTo_InnerOuter(squareToStartFrom, maxMoveDist, 0f, out MaxMoveSquares, out hashSet3);
                this.AddToSquareCanMoveToCache(squareToStartFrom, maxMoveDist, MaxMoveSquares);
            }
            else if (InnerMoveSquares == null)
            {
                HashSet<BoardSquare> hashSet4;
                this.BuildSquaresCanMoveTo_InnerOuter(squareToStartFrom, innerMoveDist, 0f, out InnerMoveSquares, out hashSet4);
                this.AddToSquareCanMoveToCache(squareToStartFrom, innerMoveDist, InnerMoveSquares);
            }
            outMaxMoveSquares = MaxMoveSquares;
            outInnerMoveSquares = InnerMoveSquares;
        }

        public bool CanCrossToAdjacentSquare(BoardSquare src, BoardSquare dest, bool ignoreBarriers, ActorMovement.DiagonalCalcFlag diagonalFlag = ActorMovement.DiagonalCalcFlag.Unknown)
        {
            return this.CanCrossToAdjacentSingleSquare(src, dest, ignoreBarriers, true, diagonalFlag);
        }

        private bool CanCrossToAdjacentSingleSquare(BoardSquare src, BoardSquare dest, bool ignoreBarriers, bool knownAdjacent = false, ActorMovement.DiagonalCalcFlag diagonalFlag = ActorMovement.DiagonalCalcFlag.Unknown)
        {
            if (dest == null || !dest.isBaselineHeight())
            {
                return false;
            }
            if (src.GetCoverType(VectorUtils.GetCoverDirection(src, dest)) == ThinCover.CoverType.Full)
            {
                return false;
            }
            if (!ignoreBarriers && BarrierManager != null && BarrierManager.IsMovementBlocked(this.m_actor, src, dest))
            {
                return false;
            }
            if (!knownAdjacent && !Board.areAdjacent(src, dest))
            {
                return false;
            }
            bool flag = true;
            if (diagonalFlag == ActorMovement.DiagonalCalcFlag.IsDiagonal || (diagonalFlag == ActorMovement.DiagonalCalcFlag.Unknown && Board.AreDiagonallyAdjacent(src, dest)))
            {
                BoardSquare square = Board.GetBoardSquare(src.X, dest.Y);
                BoardSquare square2 = Board.GetBoardSquare(dest.X, src.Y);
                if (flag)
                {
                    flag &= this.CanCrossToAdjacentSingleSquare(src, square, ignoreBarriers, true, ActorMovement.DiagonalCalcFlag.NotDiagonal);
                }
                if (flag)
                {
                    flag &= this.CanCrossToAdjacentSingleSquare(src, square2, ignoreBarriers, true, ActorMovement.DiagonalCalcFlag.NotDiagonal);
                }
                if (flag)
                {
                    flag &= this.CanCrossToAdjacentSingleSquare(square, dest, ignoreBarriers, true, ActorMovement.DiagonalCalcFlag.NotDiagonal);
                }
                if (flag)
                {
                    flag &= this.CanCrossToAdjacentSingleSquare(square2, dest, ignoreBarriers, true, ActorMovement.DiagonalCalcFlag.NotDiagonal);
                }
            }
            return flag;
        }

        public float CalculateMaxHorizontalMovement(bool forcePostAbility = false, bool calculateAsIfSnared = false)
        {
            float num = 0f;
            if (!this.m_actor.IsDead())
            {
                num = (float)this.m_actor.getActorStats().GetModifiedStatInt(StatType.Movement_Horizontal);
                AbilityData abilityData = this.m_actor.GetAbilityData();
                if (abilityData != null)
                {
                    if (abilityData.GetQueuedAbilitiesAllowMovement())
                    {
                        float num2;
                        if (forcePostAbility)
                        {
                            num2 = -1f * this.m_actor.method_29();
                        }
                        else
                        {
                            num2 = abilityData.GetQueuedAbilitiesMovementAdjust();
                        }
                        num += num2;
                        num = this.GetAdjustedMovementFromBuffAndDebuff(num, forcePostAbility, calculateAsIfSnared);
                    }
                    else
                    {
                        num = 0f;
                    }
                }
                else
                {
                    num = this.GetAdjustedMovementFromBuffAndDebuff(num, forcePostAbility, calculateAsIfSnared);
                }
                num = Mathf.Clamp(num, 0f, 99f);
            }
            return num;
        }

        public float GetAdjustedMovementFromBuffAndDebuff(float movement, bool forcePostAbility, bool calculateAsIfSnared = false)
        {
            float result = movement;
            ActorStatus actorStatus = this.m_actor.method_11();
            if (actorStatus.HasStatus(StatusType.RecentlySpawned, true))
            {
                result += (float)GameplayData.m_recentlySpawnedBonusMovement;
            }
            if (actorStatus.HasStatus(StatusType.RecentlyRespawned, true))
            {
                result += (float)GameplayData.m_recentlyRespawnedBonusMovement;
            }
            List<StatusType> queuedAbilitiesOnRequestStatuses = this.m_actor.GetAbilityData().GetQueuedAbilitiesOnRequestStatuses();
            bool debuffSuppressed = actorStatus.HasStatus(StatusType.MovementDebuffSuppression, true) || queuedAbilitiesOnRequestStatuses.Contains(StatusType.MovementDebuffSuppression);
            bool unstoppable = actorStatus.IsMovementDebuffImmune(true) || queuedAbilitiesOnRequestStatuses.Contains(StatusType.MovementDebuffImmunity) || queuedAbilitiesOnRequestStatuses.Contains(StatusType.Unstoppable);
            bool debuffNotAffected = !debuffSuppressed && !unstoppable;
            bool cantSprintUnlessUnstoppable = actorStatus.HasStatus(StatusType.CantSprint_UnlessUnstoppable, true) || queuedAbilitiesOnRequestStatuses.Contains(StatusType.CantSprint_UnlessUnstoppable);
            bool cantSprintAtAll = actorStatus.HasStatus(StatusType.CantSprint_Absolute, true) || queuedAbilitiesOnRequestStatuses.Contains(StatusType.CantSprint_Absolute);
            bool cantSprint = (cantSprintUnlessUnstoppable && debuffNotAffected) || cantSprintAtAll;
            if ((debuffNotAffected && actorStatus.HasStatus(StatusType.Rooted, true)) || actorStatus.HasStatus(StatusType.AnchoredNoMovement, true))
            {
                result = 0f;
            }
            else if (debuffNotAffected && actorStatus.HasStatus(StatusType.CrippledMovement, true))
            {
                result = Mathf.Clamp(result, 0f, 1f);
            }
            else
            {
                if (cantSprint && !forcePostAbility && this.m_actor.GetAbilityData() != null && this.m_actor.GetAbilityData().GetQueuedAbilitiesMovementAdjustType() == Ability.MovementAdjustment.FullMovement)
                {
                    result -= this.m_actor.GetAbilityMovementCost();
                }
                bool snared = actorStatus.HasStatus(StatusType.Snared, true) || queuedAbilitiesOnRequestStatuses.Contains(StatusType.Snared);
                bool hasted = actorStatus.HasStatus(StatusType.Hasted, true) || queuedAbilitiesOnRequestStatuses.Contains(StatusType.Hasted);
                if ((debuffNotAffected && snared && !hasted) || calculateAsIfSnared)
                {
                    float snaredMult;
                    int snaredHalfMoveAdjust;
                    int snaredFullMoveAdjust;
                    ActorMovement.CalcSnaredMovementAdjustments(out snaredMult, out snaredHalfMoveAdjust, out snaredFullMoveAdjust);
                    if (forcePostAbility)
                    {
                        result = Mathf.Clamp(result + (float)snaredHalfMoveAdjust, 0f, 99f);
                    }
                    else
                    {
                        int snaredAdjustment = snaredFullMoveAdjust;
                        if (this.m_actor.GetAbilityData() != null)
                        {
                            Ability.MovementAdjustment queuedAbilitiesMovementAdjustType = this.m_actor.GetAbilityData().GetQueuedAbilitiesMovementAdjustType();
                            if (queuedAbilitiesMovementAdjustType == Ability.MovementAdjustment.ReducedMovement)
                            {
                                snaredAdjustment = snaredHalfMoveAdjust;
                            }
                        }
                        result = Mathf.Clamp(result + (float)snaredAdjustment, 0f, 99f);
                    }
                    result *= snaredMult;
                    result = MovementUtils.RoundToNearestHalf(result);
                }
                else if (hasted && (!debuffNotAffected || !snared))
                {
                    float hastedMult;
                    int hastedHalfMoveAdjustment;
                    int hastedFullMoveAdjustment;
                    ActorMovement.CalcHastedMovementAdjustments(out hastedMult, out hastedHalfMoveAdjustment, out hastedFullMoveAdjustment);
                    if (forcePostAbility)
                    {
                        result = Mathf.Clamp(result + (float)hastedHalfMoveAdjustment, 0f, 99f);
                    }
                    else
                    {
                        int hastedAdjustment = hastedFullMoveAdjustment;
                        if (this.m_actor.GetAbilityData() != null)
                        {
                            Ability.MovementAdjustment queuedAbilitiesMovementAdjustType = this.m_actor.GetAbilityData().GetQueuedAbilitiesMovementAdjustType();
                            if (queuedAbilitiesMovementAdjustType == Ability.MovementAdjustment.ReducedMovement)
                            {
                                hastedAdjustment = hastedHalfMoveAdjustment;
                            }
                        }
                        result = Mathf.Clamp(result + (float)hastedAdjustment, 0f, 99f);
                    }
                    result *= hastedMult;
                    result = MovementUtils.RoundToNearestHalf(result);
                }
            }
            return result;
        }

        public static void CalcSnaredMovementAdjustments(out float mult, out int halfMoveAdjust, out int fullMoveAdjust)
        {
            // TODO ZHENEQ
            //if (!(GameplayMutators == null) && GameplayMutators.m_useSlowOverride)
            //{
            //    halfMoveAdjust = GameplayMutators.m_slowHalfMovementAdjustAmount;
            //    fullMoveAdjust = GameplayMutators.m_slowFullMovementAdjustAmount;
            //    mult = GameplayMutators.m_slowMovementMultiplier;
            //}
            //else
            //{
            //    halfMoveAdjust = GameWideData.m_slowHalfMovementAdjustAmount;
            //    fullMoveAdjust = GameWideData.m_slowFullMovementAdjustAmount;
            //    mult = GameWideData.m_slowMovementMultiplier;
            //}
            mult = .5f;
            halfMoveAdjust = 0;
            fullMoveAdjust = 0;
        }

        public static void CalcHastedMovementAdjustments(out float mult, out int halfMoveAdjustment, out int fullMoveAdjustment)
        {
            // TODO ZHENEQ
            //if (!(GameplayMutators.Get() == null) && GameplayMutators.Get().m_useHasteOverride)
            //{
            //    halfMoveAdjustment = GameplayMutators.Get().m_hasteHalfMovementAdjustAmount;
            //    fullMoveAdjustment = GameplayMutators.Get().m_hasteFullMovementAdjustAmount;
            //    mult = GameplayMutators.Get().m_hasteMovementMultiplier;
            //}
            //else
            //{
            //    halfMoveAdjustment = GameWideData.Get().m_hasteHalfMovementAdjustAmount;
            //    fullMoveAdjustment = GameWideData.Get().m_hasteFullMovementAdjustAmount;
            //    mult = GameWideData.Get().m_hasteMovementMultiplier;
            //}
            mult = 1.5f;
            halfMoveAdjustment = 0;
            fullMoveAdjustment = 0;
        }

        public override void DeserializeAsset(AssetFile assetFile, StreamReader stream)
        {
            m_brushTransitionAnimationSpeed = stream.ReadSingle(); // float32
            m_brushTransitionAnimationSpeedEaseTime = stream.ReadSingle(); // float32
        }

        public override string ToString()
        {
            return $"{nameof(ActorMovement)}(" +
                   $"{nameof(m_brushTransitionAnimationSpeed)}: {m_brushTransitionAnimationSpeed}, " +
                   $"{nameof(m_brushTransitionAnimationSpeedEaseTime)}: {m_brushTransitionAnimationSpeedEaseTime}, " +
                   ")";
        }

        public enum IdleType
        {
            Default,
            Special1
        }

        public class SquaresCanMoveToCacheEntry
        {
            public BoardSquare m_squaresCanMoveToOrigin;
            public float m_squaresCanMoveToHorizontalAllowed;
            public int m_squaresCanMoveToSinglePlayerState = -1;
            public int m_squaresCanMoveToBarrierState = -1;
            public FirstTurnMovement.RestrictedMovementState m_squaresCanMoveToFirstTurnState = FirstTurnMovement.RestrictedMovementState.Invalid;
            public HashSet<BoardSquare> m_squaresCanMoveTo;
        }

        private struct BoardSquareMovementInfo
        {
            public BoardSquare square;
            public float cost;
            public float prevCost;
        }

        public enum DiagonalCalcFlag
        {
            Unknown,
            IsDiagonal,
            NotDiagonal
        }
    }
}
