using System;
using Unity.Netcode;
 
[Serializable]
public struct WagonSlotData : INetworkSerializable, IEquatable<WagonSlotData>
{
    public int Front;
    public int Back;
    public bool Upgraded;

    public WagonSlotData(int front, int back)
    {
        Front = front;
        Back = back;
        Upgraded = false;
    }

    public WagonSlotData(int front, int back, bool upgraded)
    {
        Front = front;
        Back = back;
        Upgraded = upgraded;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref Front);
        s.SerializeValue(ref Back);
        s.SerializeValue(ref Upgraded);
    }

    public bool Equals(WagonSlotData other)
    {
        return Front == other.Front && Back == other.Back && Upgraded == other.Upgraded;
    }
}
