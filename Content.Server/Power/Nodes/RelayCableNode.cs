using Content.Server.NodeContainer.EntitySystems;

namespace Content.Server.Power.Nodes;

/// <summary>
/// A <see cref="CableNode"/> an electro-relay can switch off to break its powernet at this tile without removing
/// the cable. The relay owns one per voltage it can sever.
/// </summary>
[DataDefinition]
public sealed partial class RelayCableNode : CableNode
{
    /// <summary>
    /// While disabled the node refuses to connect, breaking the powernet here while the relay physically stays.
    /// </summary>
    /// <remarks>
    /// If you change this, you must manually call <see cref="NodeGroupSystem.QueueReflood"/> to update the
    /// node connections.
    /// </remarks>
    [DataField]
    public bool Enabled { get; set; } = true;

    public override bool Connectable(IEntityManager entMan, TransformComponent? xform = null)
    {
        return Enabled && base.Connectable(entMan, xform);
    }
}
