using System;
using System.Collections.Generic;
using System.Reflection;
using EvoS.Framework.Network.Unity;
using log4net;

namespace CentralServer.BridgeServer
{
    public static class BridgeMessageSerializer
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(BridgeMessageSerializer));
        
        private static readonly List<Type> BridgeMessageTypes = new List<Type>
        {
            typeof(RegisterGameServerRequest),
            typeof(RegisterGameServerResponse),
            typeof(LaunchGameRequest),
            typeof(JoinGameServerRequest),
            null, // typeof(JoinGameAsObserverRequest),
            typeof(ShutdownGameRequest),
            typeof(AdminShutdownGameRequest), // Custom AdminShutdownGame
            typeof(DisconnectPlayerRequest),
            typeof(ReconnectPlayerRequest),
            null, // typeof(MonitorHeartbeatResponse),
            typeof(ServerGameSummaryNotification),
            typeof(PlayerDisconnectedNotification),
            typeof(ServerGameMetricsNotification),
            typeof(ServerGameStatusNotification),
            typeof(MonitorHeartbeatNotification),
            typeof(LaunchGameResponse),
            typeof(JoinGameServerResponse),
            null, // typeof(JoinGameAsObserverResponse),
            typeof(ReconnectPlayerResponse),
        };

        private static List<Type> GetMessageTypes()
        {
            return BridgeMessageTypes;
        }

        public static short GetMessageType(AllianceMessageBase msg)
        {
            short num = (short)GetMessageTypes().IndexOf(msg.GetType());
            if (num < 0)
            {
                log.Error($"Message type {msg.GetType().Name} is not in the MonitorGameServerInsightMessages MessageTypes list and doesnt have a type");
            }

            return num;
        }

        public static AllianceMessageBase DeserializeMessage(byte[] data, out int callbackId)
        {
            NetworkReader networkReader = new NetworkReader(data);
            short messageType = networkReader.ReadInt16();
            callbackId = networkReader.ReadInt32();
            List<Type> messageTypes = GetMessageTypes();
            if (messageType >= messageTypes.Count)
            {
                log.Error($"Unknown bridge message type {messageType}");
                throw new NullReferenceException();
            }

            Type type = messageTypes[messageType];
            return Deserialize(type, networkReader);
        }
        
        public static byte[] SerializeMessage(short msgType, AllianceMessageBase msg, int originalCallbackId = 0)
        {
            NetworkWriter networkWriter = new NetworkWriter();
            networkWriter.Write(msgType);
            networkWriter.Write(originalCallbackId);
            msg.Serialize(networkWriter);
            return networkWriter.ToArray();
        }

        private static AllianceMessageBase Deserialize(Type type, NetworkReader reader)
        {
            if (!typeof(AllianceMessageBase).IsAssignableFrom(type))
            {
                throw new Exception("Class " + type + " is not AllianceMessageBase");
            }
            ConstructorInfo constructor = type.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
            {
                throw new Exception("No default constructor in class " + type);
            }
            AllianceMessageBase o = (AllianceMessageBase)constructor.Invoke(Array.Empty<object>());
            o.Deserialize(reader);
            return o;
        }
    }
}