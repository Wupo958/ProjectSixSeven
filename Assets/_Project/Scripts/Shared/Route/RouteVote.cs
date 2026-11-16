using System;
using Unity.Collections;
using Unity.Netcode;

public struct RouteVote : INetworkSerializeByMemcpy, IEquatable<RouteVote>
{
    public ulong ClientId;
    public int Option;
    public FixedString32Bytes PlayerName;

    public bool Equals(RouteVote other)
    {
        return ClientId == other.ClientId
            && Option == other.Option
            && PlayerName.Equals(other.PlayerName);
    }

    public override bool Equals(object obj)
    {
        if (obj is RouteVote other)
        {
            return Equals(other);
        }

        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ClientId, Option, PlayerName.GetHashCode());
    }
}
