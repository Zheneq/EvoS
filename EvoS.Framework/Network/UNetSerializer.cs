using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EvoS.Framework.Logging;
using EvoS.Framework.Network.Unity;
using Newtonsoft.Json;

namespace EvoS.Framework.Network
{
    public delegate void UNetMessageDelegate(MessageBase msg);

    public class UNetSerializer
    {
        public static readonly Dictionary<short, Type> ServerTypesById = new Dictionary<short, Type>();
        public static readonly Dictionary<short, Type> ClientTypesById = new Dictionary<short, Type>();

        public static readonly Dictionary<Type, HashSet<short>> ServerIdsByType =
            new Dictionary<Type, HashSet<short>>();

        public static readonly Dictionary<Type, HashSet<short>> ClientIdsByType =
            new Dictionary<Type, HashSet<short>>();

        private readonly Dictionary<short, UNetMessageDelegate>
            _handlers = new Dictionary<short, UNetMessageDelegate>();

        public void RegisterHandler(short msgId, UNetMessageDelegate handler)
        {
            if (!_handlers.TryAdd(msgId, handler))
            {
                throw new UNetSerializerTypeException(
                    $"Cannot map {msgId} to {handler}, as it is already mapped to {_handlers[msgId]}");
            }
        }

        static UNetSerializer()
        {
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes()
                .Concat(Assembly.GetEntryAssembly().GetTypes()))
            {
                var attribute = type.GetCustomAttribute<UNetMessageAttribute>();
                if (attribute == null)
                    continue;

                foreach (var msgId in attribute.ClientMsgIds)
                {
                    AddType(type, msgId, "Client", ClientTypesById, ClientIdsByType);
                }

                foreach (var msgId in attribute.ServerMsgIds)
                {
                    AddType(type, msgId, "Server", ServerTypesById, ServerIdsByType);
                }
            }

            Log.Print(LogType.Game,
                $"Loaded {ClientIdsByType.Count} client {(ClientIdsByType.Count == 1 ? "type" : "types")} and {ServerIdsByType.Count} server {(ServerIdsByType.Count == 1 ? "type" : "types")}");
        }

        public void ProcessUNetMessages(byte[] rawPacket)
        {
            var unetBytes = UNetMessage.Deserialize(rawPacket);

            var reader = new NetworkReader(unetBytes);
            while (reader.Position < unetBytes.Length)
            {
                var msg = new NetworkMessage();
                msg.msgSeqNum = reader.ReadUInt32();
                var msgSize = reader.ReadUInt16();
                msg.msgType = reader.ReadInt16();
                var buffer = reader.ReadBytes(msgSize);
                msg.reader = new NetworkReader(buffer);

                ProcessUnetMessage(msg);
            }
        }

        public virtual void ProcessUnetMessage(NetworkMessage msg)
        {
            var deserialized = Deserialize(msg, null);

            if (deserialized != null)
            {
                if (_handlers.ContainsKey(deserialized.msgType))
                {
                    Log.Print(LogType.Game,
                        $"{deserialized.GetType().Name} - {JsonConvert.SerializeObject(deserialized)}");
                    _handlers[deserialized.msgType].Invoke(deserialized);
                }
                else
                {
                    Log.Print(LogType.Warning,
                        $"Unhandled {msg.msgType}:{deserialized.GetType().Name} - {JsonConvert.SerializeObject(deserialized)}");
                }
            }
        }

        public MessageBase Deserialize(NetworkMessage msg, Component context, bool fromClient = true)
        {
            var buffer = msg.reader.ReadBytes(msg.reader.Length);
            msg.reader.SeekZero();

            Type type;
            if (fromClient)
                ClientTypesById.TryGetValue(msg.msgType, out type);
            else
                ServerTypesById.TryGetValue(msg.msgType, out type);

            if (type == null)
            {
                Log.Print(LogType.Warning,
                    $"Received unknown message seq={msg.msgSeqNum}, size={msg.reader.Length}, type={msg.msgType}\n\t{Convert.ToBase64String(buffer)}");
                return null;
            }

            try
            {
                var readMessage = (MessageBase) Activator.CreateInstance(type);
                readMessage.msgType = msg.msgType;
                readMessage.msgSeqNum = msg.msgSeqNum;
                readMessage.Deserialize(msg.reader, context);
                return readMessage;
            }
            catch (Exception e)
            {
                Log.Print(LogType.Error, e);
                Log.Print(LogType.Error,
                    $"Failed parsing message seq={msg.msgSeqNum}, size={msg.reader.Length}, type={msg.msgType}\n\t{Convert.ToBase64String(buffer)}");
                return null;
            }
        }

        private static void AddType(Type messageType, short typeId, string mappingType,
            Dictionary<short, Type> typesById,
            Dictionary<Type, HashSet<short>> idsByType)
        {
            if (!idsByType.TryAdd(messageType, new HashSet<short> {typeId}) && !idsByType[messageType].Add(typeId))
            {
                throw new UNetSerializerTypeException(
                    $"Cannot map {mappingType} {messageType} to {typeId}, as it is already mapped to {idsByType[messageType]}");
            }

            if (!typesById.TryAdd(typeId, messageType) && typesById[typeId] != messageType)
            {
                throw new UNetSerializerTypeException(
                    $"Cannot map {mappingType} {typeId} to {messageType}, as it is already mapped to {typesById[typeId]}");
            }
        }

        public static UNetSerializer Instance { get; } = new UNetSerializer();
    }
}
