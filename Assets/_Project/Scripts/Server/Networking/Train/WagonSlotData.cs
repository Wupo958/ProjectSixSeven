using System;
using Unity.Netcode;
 
[Serializable]
public struct WagonSlotData : INetworkSerializable, IEquatable<WagonSlotData>
{
    public int Front;
    public int Back;
 
    public WagonSlotData(int front, int back)
    {
        Front = front;
        Back = back;
    }
 
    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref Front);
        s.SerializeValue(ref Back);
    }
 
    public bool Equals(WagonSlotData other) => Front == other.Front && Back == other.Back;
}
