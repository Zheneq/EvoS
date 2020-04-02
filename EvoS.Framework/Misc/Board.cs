using System;
using System.Collections.Generic;
using EvoS.Framework.Assets;
using EvoS.Framework.Assets.Serialized;
using EvoS.Framework.Assets.Serialized.Behaviours;
using EvoS.Framework.Network.NetworkBehaviours;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Misc
{
    [Serializable]
    [SerializedMonoBehaviour("Board")]
    public class Board : MonoBehaviour
    {
        public SerializedComponent m_LOSHighlightsParent;
        public bool m_showLOS;
        public float m_squareSize = 1f;
        public int m_baselineHeight = -1;
        public SerializedComponent m_losLookup;
        private BoardSquare[,] _boardSquares;

        private int m_lowestPositiveHeight = 9999;
        private int m_lastValidGuidedHeight = 99999;
        public HashSet<BoardSquare> m_highlightedBoardSquares = new HashSet<BoardSquare>();
        private bool m_needToUpdateValidSquares = true;
        private int m_maxX;
        private int m_maxY;
        private int m_maxHeight;

//        private MeshCollider m_cameraGuideMeshCollider;
        internal BuildNormalPathNodePool m_normalPathBuildScratchPool;
        internal BuildNormalPathHeap m_normalPathNodeHeap;
        public float squareSize => m_squareSize;

        public int BaselineHeight
        {
            get
            {
                if (m_baselineHeight >= 0)
                    return m_baselineHeight;
                return m_lowestPositiveHeight;
            }
        }

        public float LosCheckHeight => BaselineHeight + BoardSquare.s_LoSHeightOffset;

        public Board()
        {
        }

        public Board(AssetFile assetFile, StreamReader stream)
        {
            DeserializeAsset(assetFile, stream);
        }

        public override void Awake()
        {
            enabled = false;
//            GameEventManager.Get().AddListener((IGameEventListener) this, GameEventManager.EventType.GameFlowDataStarted);
//            GameObject gameObject = GameObject.Find("Camera Guide Mesh");
//            if (gameObject != null)
//                this.m_cameraGuideMeshCollider = gameObject.GetComponent<MeshCollider>();
            ReevaluateBoard();
            m_showLOS = true;
//            Board.s_squareSizeStatic = m_squareSize;
//            Board.BaselineHeightStatic = BaselineHeight;
            this.m_normalPathBuildScratchPool = new BuildNormalPathNodePool();
            this.m_normalPathNodeHeap = new BuildNormalPathHeap(60);
        }

        public void SetThinCover(
            int x,
            int y,
            ActorCover.CoverDirections side,
            ThinCover.CoverType coverType)
        {
            _boardSquares[x, y].SetThinCover(side, coverType);
        }

        public int method_3()
        {
            return m_maxX;
        }

        public int method_4()
        {
            return m_maxY;
        }

        public BoardSquare method_5(float float_0, float float_1)
        {
            BoardSquare boardSquare = null;
            int index1 = Mathf.RoundToInt(float_0 / squareSize);
            int index2 = Mathf.RoundToInt(float_1 / squareSize);
            if (index1 >= 0 && index1 < method_3() && (index2 >= 0 && index2 < method_4()))
                boardSquare = _boardSquares[index1, index2];
            return boardSquare;
        }

        public BoardSquare method_9(Transform transform_0)
        {
            BoardSquare boardSquare = null;
            if (transform_0 != null)
                boardSquare = method_5(transform_0.position.X, transform_0.position.Z);
            return boardSquare;
        }

        public BoardSquare method_10(int int_0, int int_1)
        {
            BoardSquare boardSquare = null;
            if (int_0 >= 0 && int_0 < method_3() && (int_1 >= 0 && int_1 < method_4()))
                boardSquare = _boardSquares[int_0, int_1];
            return boardSquare;
        }

        private BoardSquare GetSquare(int x, int y)
        {
            return GetBoardSquare(x, y);
        }

        public void getStraightAdjacentSquares(int x, int y, ref List<BoardSquare> outList)
        {
            method_12(x, y, ref outList);
        }

        public void method_12(int int_0, int int_1, ref List<BoardSquare> list_0)
        {
            if (list_0 == null)
            {
                list_0 = new List<BoardSquare>(4);
            }
            if (this.GetSquare(int_0 + 1, int_1) != null)
            {
                list_0.Add(this.GetSquare(int_0 + 1, int_1));
            }
            if (this.GetSquare(int_0 - 1, int_1) != null)
            {
                list_0.Add(this.GetSquare(int_0 - 1, int_1));
            }
            if (this.GetSquare(int_0, int_1 + 1) != null)
            {
                list_0.Add(this.GetSquare(int_0, int_1 + 1));
            }
            if (this.GetSquare(int_0, int_1 - 1) != null)
            {
                list_0.Add(this.GetSquare(int_0, int_1 - 1));
            }
        }

        public void getDiagonalAdjacentSquares(int x, int y, ref List<BoardSquare> outList)
        {
            method_13(x, y, ref outList);
        }

        public void method_13(int int_0, int int_1, ref List<BoardSquare> list_0)
        {
            if (list_0 == null)
            {
                list_0 = new List<BoardSquare>(4);
            }
            if (this.GetSquare(int_0 + 1, int_1 + 1) != null)
            {
                list_0.Add(this.GetSquare(int_0 + 1, int_1 + 1));
            }
            if (this.GetSquare(int_0 + 1, int_1 - 1) != null)
            {
                list_0.Add(this.GetSquare(int_0 + 1, int_1 - 1));
            }
            if (this.GetSquare(int_0 - 1, int_1 + 1) != null)
            {
                list_0.Add(this.GetSquare(int_0 - 1, int_1 + 1));
            }
            if (this.GetSquare(int_0 - 1, int_1 - 1) != null)
            {
                list_0.Add(this.GetSquare(int_0 - 1, int_1 - 1));
            }
        }

        public void getAllAdjacentSquares(int x, int y, ref List<BoardSquare> outList)
        {
            method_14(x, y, ref outList);
        }

        public void method_14(int int_0, int int_1, ref List<BoardSquare> list_0)
        {
            if (list_0 == null)
            {
                list_0 = new List<BoardSquare>(8);
            }
            this.method_12(int_0, int_1, ref list_0);
            this.method_13(int_0, int_1, ref list_0);
        }

        public bool areAdjacent(BoardSquare a, BoardSquare b)
        {
            return method_17(a, b);
        }

        public bool method_17(BoardSquare boardSquare_0, BoardSquare boardSquare_1)
        {
            var flag1 = boardSquare_0.X != boardSquare_1.X || boardSquare_0.Y != boardSquare_1.Y;
            var flag2 = boardSquare_0.X >= boardSquare_1.X - 1 && boardSquare_0.X <= boardSquare_1.X + 1;
            var flag3 = boardSquare_0.Y >= boardSquare_1.Y - 1 && boardSquare_0.Y <= boardSquare_1.Y + 1;
            if (flag1 && flag2)
                return flag3;
            return false;
        }


        public bool method_18(BoardSquare boardSquare_0, BoardSquare boardSquare_1)
        {
            return boardSquare_0.X == boardSquare_1.X &&
                   (boardSquare_0.Y == boardSquare_1.Y + 1 || boardSquare_0.Y == boardSquare_1.Y - 1) ||
                   boardSquare_0.Y == boardSquare_1.Y &&
                   (boardSquare_0.X == boardSquare_1.X + 1 || boardSquare_0.X == boardSquare_1.X - 1);
        }

        public bool AreDiagonallyAdjacent(BoardSquare a, BoardSquare b)
        {
            return method_19(a, b);
        }

        public bool method_19(BoardSquare boardSquare_0, BoardSquare boardSquare_1)
        {
            if (method_17(boardSquare_0, boardSquare_1) && boardSquare_0.X != boardSquare_1.X)
                return boardSquare_0.Y != boardSquare_1.Y;
            return false;
        }

        public List<BoardSquare> method_21(BoardSquare boardSquare_0, BoardSquare boardSquare_1)
        {
            var boardSquareList = new List<BoardSquare>();
            if (boardSquare_0 == null || boardSquare_1 == null) return boardSquareList;

            var num1 = Mathf.Min(boardSquare_0.X, boardSquare_1.X);
            var num2 = Mathf.Max(boardSquare_0.X, boardSquare_1.X);
            var num3 = Mathf.Min(boardSquare_0.Y, boardSquare_1.Y);
            var num4 = Mathf.Max(boardSquare_0.Y, boardSquare_1.Y);
            for (var int_1 = num3; int_1 <= num4; ++int_1)
            {
                for (var int_0 = num1; int_0 <= num2; ++int_0)
                {
                    var boardSquare = method_10(int_0, int_1);
                    boardSquareList.Add(boardSquare);
                }
            }

            return boardSquareList;
        }

        public void ReevaluateBoard()
        {
            m_maxX = 0;
            m_maxY = 0;
            m_maxHeight = 0;
            m_lastValidGuidedHeight = 99999;
            m_lowestPositiveHeight = 99999;
            foreach (var child in transform)
            {
                var component = child.GetComponent<BoardSquare>();
                component.ReevaluateSquare();
                if (component.Height > 0 && component.Height < m_lowestPositiveHeight)
                    m_lowestPositiveHeight = component.Height;
                if (component.Height > m_maxHeight)
                    m_maxHeight = component.Height;
                if (component.X + 1 > m_maxX)
                    m_maxX = component.X + 1;
                if (component.Y + 1 > m_maxY)
                    m_maxY = component.Y + 1;
            }

            _boardSquares = new BoardSquare[m_maxX, m_maxY];
            foreach (var child in transform)
            {
                var component = child.GetComponent<BoardSquare>();
                if (component != null)
                    _boardSquares[component.X, component.Y] = component;
            }
        }

        public void MarkForUpdateValidSquares(bool value = true)
        {
            m_needToUpdateValidSquares = value;
        }

        public override void DeserializeAsset(AssetFile assetFile, StreamReader stream)
        {
            m_LOSHighlightsParent = new SerializedComponent(assetFile, stream);
            m_showLOS = stream.ReadBoolean();
            stream.AlignTo();
            m_squareSize = stream.ReadSingle();
            m_baselineHeight = stream.ReadInt32();
            m_losLookup = new SerializedComponent(assetFile, stream);
        }

        public override string ToString()
        {
            return $"{nameof(Board)}>(" +
                   $"{nameof(m_LOSHighlightsParent)}: {m_LOSHighlightsParent}, " +
                   $"{nameof(m_showLOS)}: {m_showLOS}, " +
                   $"{nameof(m_squareSize)}: {m_squareSize}, " +
                   $"{nameof(m_baselineHeight)}: {m_baselineHeight}, " +
                   $"{nameof(m_losLookup)}: {m_losLookup}, " +
                   ")";
        }

        public BoardSquare GetBoardSquare(int x, int y)
        {
            if (x >= 0 && x < m_maxX && (y >= 0 && y < m_maxY))
                return _boardSquares[x, y];
            return null;
        }

        public BoardSquare GetBoardSquare(GridPos gridPos_0) // 11
        {
            var boardSquare = (BoardSquare) null;
            if (gridPos_0.X >= 0 && gridPos_0.X < method_3() && (gridPos_0.Y >= 0 && gridPos_0.Y < method_4()))
                boardSquare = _boardSquares[gridPos_0.X, gridPos_0.Y];
            return boardSquare;
        }
    }
}
