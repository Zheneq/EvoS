using EvoS.Framework.Constants.Enums;
using System;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Unity;
using Newtonsoft.Json;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    [EvosMessage(779, typeof(LobbySessionInfo))]
    public class LobbySessionInfo
    {
        public long AccountId;
        public string UserName;
        public string BuildVersion;
        public string ProtocolVersion;
        public long SessionToken;
        public long ReconnectSessionToken;
        public string ProcessCode;
        public ProcessType ProcessType;
        public string ConnectionAddress;
        public string Handle;
        public bool IsBinary;
        public string FakeEntitlements;
        public Region Region;
        public string LanguageCode;

        public void Serialize(NetworkWriter writer)
        {
            writer.Write(AccountId);
            writer.Write(UserName);
            writer.Write(BuildVersion);
            writer.Write(ProtocolVersion);
            writer.Write(SessionToken);
            writer.Write(ReconnectSessionToken);
            writer.Write(ProcessCode);
            writer.Write((short)ProcessType);
            writer.Write(ConnectionAddress);
            writer.Write(Handle);
            writer.Write(IsBinary);
            writer.Write(FakeEntitlements);
            writer.Write((short)Region);
            writer.Write(LanguageCode);
        }

        public void Deserialize(NetworkReader reader)
        {
            AccountId = reader.ReadInt64();
            UserName = reader.ReadString();
            BuildVersion = reader.ReadString();
            ProtocolVersion = reader.ReadString();
            SessionToken = reader.ReadInt64();
            ReconnectSessionToken = reader.ReadInt64();
            ProcessCode = reader.ReadString();
            ProcessType = (ProcessType)reader.ReadInt16();
            ConnectionAddress = reader.ReadString();
            Handle = reader.ReadString();
            IsBinary = reader.ReadBoolean();
            FakeEntitlements = reader.ReadString();
            Region = (Region)reader.ReadInt16();
            LanguageCode = reader.ReadString();
        }

        [JsonIgnore]
        public string Name
        {
            get
            {
                if (!Handle.IsNullOrEmpty())
                {
                    return $"{Handle} [{AccountId} {SessionToken:x}]";
                }

                if (SessionToken != 0)
                {
                    return $"[{AccountId} {SessionToken:x}]";
                }

                if (!ProcessCode.IsNullOrEmpty())
                {
                    return ProcessCode;
                }

                return "unknown";
            }
        }


        public override string ToString()
        {
            return Name;
        }
    }
}