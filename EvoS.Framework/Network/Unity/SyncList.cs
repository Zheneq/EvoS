using System.Collections;
using System.Collections.Generic;
using EvoS.Framework.Logging;

namespace EvoS.Framework.Network.Unity
{
    public abstract class SyncList<T> : IList<T>, ICollection<T>, IEnumerable<T>, IEnumerable
    {
        private List<T> m_Objects = new List<T>();
        private NetworkBehaviour m_Behaviour;
        private int m_CmdHash;
        private SyncListChanged m_Callback;

        public int Count => m_Objects.Count;

        public bool IsReadOnly => false;

        public SyncListChanged Callback
        {
            get => m_Callback;
            set => m_Callback = value;
        }

        public abstract void SerializeItem(NetworkWriter writer, T item);

        public abstract T DeserializeItem(NetworkReader reader);

        public void InitializeBehaviour(NetworkBehaviour beh, int cmdHash)
        {
            m_Behaviour = beh;
            m_CmdHash = cmdHash;
        }

        private void SendMsg(Operation op, int itemIndex, T item)
        {
            if (m_Behaviour == null)
            {
                Log.Print(LogType.Error, "SyncList not initialized");
                return;
            }

            var component = m_Behaviour.GetComponent<NetworkIdentity>();
            if (component == null)
            {
                Log.Print(LogType.Error, "SyncList no NetworkIdentity");
                return;
            }

            var writer = new NetworkWriter();
            writer.StartMessage(9);
            writer.Write(component.netId);
            writer.WritePackedUInt32((uint) m_CmdHash);
            writer.Write((byte) op);
            writer.WritePackedUInt32((uint) itemIndex);
            SerializeItem(writer, item);
            writer.FinishMessage();
            m_Behaviour.NetworkServer.SendWriterToReady(component.gameObject, writer, m_Behaviour.GetNetworkChannel());

            m_Callback?.Invoke(op, itemIndex);
        }

        private void SendMsg(Operation op, int itemIndex)
        {
            SendMsg(op, itemIndex, default);
        }
        
        public void HandleMsg(NetworkReader reader)
        {
            var num = (Operation) reader.ReadByte();
            var index = (int) reader.ReadPackedUInt32();
            var obj = DeserializeItem(reader);
            switch (num)
            {
                case Operation.OP_ADD:
                    m_Objects.Add(obj);
                    break;
                case Operation.OP_CLEAR:
                    m_Objects.Clear();
                    break;
                case Operation.OP_INSERT:
                    m_Objects.Insert(index, obj);
                    break;
                case Operation.OP_REMOVE:
                    m_Objects.Remove(obj);
                    break;
                case Operation.OP_REMOVEAT:
                    m_Objects.RemoveAt(index);
                    break;
                case Operation.OP_SET:
                case Operation.OP_DIRTY:
                    m_Objects[index] = obj;
                    break;
            }

            if (m_Callback == null)
                return;
            m_Callback(num, index);
        }

        internal void AddInternal(T item)
        {
            m_Objects.Add(item);
        }

        public void Add(T item)
        {
            m_Objects.Add(item);
            SendMsg(Operation.OP_ADD, m_Objects.Count - 1, item);
        }

        public void Clear()
        {
            m_Objects.Clear();
            SendMsg(Operation.OP_CLEAR, 0);
        }

        public bool Contains(T item)
        {
            return m_Objects.Contains(item);
        }

        public void CopyTo(T[] array, int index)
        {
            m_Objects.CopyTo(array, index);
        }

        public int IndexOf(T item)
        {
            return m_Objects.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            m_Objects.Insert(index, item);
            SendMsg(Operation.OP_INSERT, index, item);
        }

        public bool Remove(T item)
        {
            bool flag = m_Objects.Remove(item);
            if (flag)
                SendMsg(Operation.OP_REMOVE, 0, item);
            return flag;
        }

        public void RemoveAt(int index)
        {
            m_Objects.RemoveAt(index);
            SendMsg(Operation.OP_REMOVEAT, index);
        }

        public void Dirty(int index)
        {
            SendMsg(Operation.OP_DIRTY, index, m_Objects[index]);
        }

        public T this[int i]
        {
            get => m_Objects[i];
            set
            {
                bool flag;
                if (m_Objects[i] == null)
                {
                    if (value == null)
                        return;
                    flag = true;
                }
                else
                    flag = !m_Objects[i].Equals(value);

                m_Objects[i] = value;
                if (!flag)
                    return;
                SendMsg(Operation.OP_SET, i, value);
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return m_Objects.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public delegate void SyncListChanged(Operation op, int itemIndex);

        public enum Operation
        {
            OP_ADD,
            OP_CLEAR,
            OP_INSERT,
            OP_REMOVE,
            OP_REMOVEAT,
            OP_SET,
            OP_DIRTY
        }
    }
}
