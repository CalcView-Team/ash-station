using Content.Shared.Atmos.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.RCD;

[Serializable, NetSerializable]
public sealed class RCDSystemMessage(ProtoId<RCDPrototype> protoId) : BoundUserInterfaceMessage
{
    public ProtoId<RCDPrototype> ProtoId = protoId;
}

[Serializable, NetSerializable]
public sealed class RCDConstructionGhostRotationEvent(NetEntity netEntity, Direction direction) : EntityEventArgs
{
    public readonly NetEntity NetEntity = netEntity;
    public readonly Direction Direction = direction;
}

/// <summary>
/// Sent by the client to update which atmos pipe layer the RCD/RPD will build on,
/// based on the position of the mouse cursor within the target tile.
/// </summary>
[Serializable, NetSerializable]
public sealed class RCDConstructionGhostLayerEvent(NetEntity netEntity, AtmosPipeLayer layer) : EntityEventArgs
{
    public readonly NetEntity NetEntity = netEntity;
    public readonly AtmosPipeLayer Layer = layer;
}

[Serializable, NetSerializable]
public enum RcdUiKey : byte
{
    Key
}
