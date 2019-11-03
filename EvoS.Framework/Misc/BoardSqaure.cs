using System;
using System.Numerics;
using EvoS.Framework.Assets;
using EvoS.Framework.Assets.Serialized;
using EvoS.Framework.Assets.Serialized.Behaviours;
using EvoS.Framework.Network.NetworkBehaviours;
using EvoS.Framework.Network.Unity;
using Newtonsoft.Json;

namespace EvoS.Framework.Misc
{
    [Serializable]
    [SerializedMonoBehaviour("BoardSquare")]
    public class BoardSquare : MonoBehaviour
    {
        private GridPosProp _gridPosProp;
        private GridPos _pos = new GridPos(-1, -1, 0);
        public SerializedComponent m_LOSHighlightObj;
        [JsonIgnore] public SerializedArray<Vector3> m_vertices;
        public const float s_LoSHeightOffset = 1.6f;
        private GameObject m_occupant;
        private ActorData m_occupantActor;
        private ThinCover.CoverType[] m_thinCoverTypes = new ThinCover.CoverType[4];

        public int BrushRegion { get; set; }
        public int X => _pos.X;
        public int Y => _pos.Y;
        public int Height => _pos.Height;
        public GridPos GridPos => _pos;
        public float worldX => _pos.worldX;
        public float worldY => _pos.worldY;

        public GameObject occupant
        {
            get => m_occupant;
            set
            {
                m_occupant = value;
                m_occupantActor = m_occupant?.GetComponent<ActorData>();
            }
        }

        public ActorData OccupantActor
        {
            get => m_occupantActor;
            set => occupant = value?.gameObject;
        }

        public BoardSquare()
        {
        }

        public ThinCover.CoverType method_0(ActorCover.CoverDirections coverDirections_0)
        {
            return m_thinCoverTypes[(int) coverDirections_0];
        }

        public void SetThinCover(ActorCover.CoverDirections squareSide, ThinCover.CoverType coverType)
        {
            m_thinCoverTypes[(int) squareSide] = coverType;
        }

        public bool method_1()
        {
            foreach (var coverType in m_thinCoverTypes)
            {
                if (coverType != ThinCover.CoverType.None)
                    return true;
            }

            return false;
        }

        public bool method_2()
        {
            foreach (var coverType in m_thinCoverTypes)
            {
                if (coverType == ThinCover.CoverType.Full)
                    return true;
            }

            return false;
        }

        public GridPos method_3()
        {
            return _pos;
        }

        public bool method_4()
        {
            return BrushRegion != -1;
        }

        public BoardSquare(AssetFile assetFile, StreamReader stream)
        {
            DeserializeAsset(assetFile, stream);
        }

        public Vector3 method_13()
        {
            return new Vector3(worldX, Height, worldY);
        }

        public Vector3 ToVector3()
        {
            return new Vector3(_pos.worldX, Height, _pos.worldY);
        }

        public void ReevaluateSquare()
        {
            _pos.X = _gridPosProp.m_x;
            _pos.Y = _gridPosProp.m_y;
            _pos.Height = _gridPosProp.m_height;
        }

        public override void Awake()
        {
            BrushRegion = -1;
            for (var index = 0; index < m_thinCoverTypes.Length; ++index)
                m_thinCoverTypes[index] = ThinCover.CoverType.None;
        }

        public float HorizontalDistanceOnBoardTo(BoardSquare other)
        {
            float a = Mathf.Abs(X - other.X);
            float b = Mathf.Abs(Y - other.Y);
            float num = Mathf.Min(a, b);
            return (float) (Mathf.Max(a, b) - (double) num + num * 1.5);
        }

        public override void DeserializeAsset(AssetFile assetFile, StreamReader stream)
        {
            _gridPosProp = new GridPosProp(assetFile, stream);
            m_LOSHighlightObj = new SerializedComponent(assetFile, stream);
            m_vertices = new SerializedArray<Vector3>(assetFile, stream);
        }

        public override string ToString()
        {
            return $"{nameof(BoardSquare)}>(" +
                   $"{nameof(_gridPosProp)}: {_gridPosProp}" +
//                   $"{nameof(m_LOSHighlightObj)}: {m_LOSHighlightObj}, " +
//                   $"{nameof(m_vertices)}: [{string.Join(", ", m_vertices)}], " +
                   ")";
        }

        public string ToPositionString()
        {
            return $"(X={X}, Y={Y})";
        }

        public enum CornerType : byte
        {
            LowerLeft,
            LowerRight,
            UpperRight,
            UpperLeft
        }

        public enum VisibilityFlags : byte
        {
            Self = 1,
            Team = 2,
            Objective = 4,
            Revealed = 8
        }
    }
}
