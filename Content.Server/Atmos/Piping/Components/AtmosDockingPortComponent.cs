using Content.Server.Atmos.Piping.EntitySystems;

namespace Content.Server.Atmos.Piping.Components;

/// <summary>
///     Links this entity's pipe node with the pipe node of whatever it docks to,
///     letting gas flow between the pipe networks of two docked grids.
///     Requires <see cref="Content.Server.Shuttles.Components.DockingComponent"/> and a
///     <see cref="Content.Server.NodeContainer.Nodes.PipeNode"/> named <see cref="NodeName"/>.
/// </summary>
/// <seealso cref="AtmosDockingPortSystem"/>
[RegisterComponent]
[Access(typeof(AtmosDockingPortSystem))]
public sealed partial class AtmosDockingPortComponent : Component
{
    /// <summary>
    ///     Name of the pipe node to link up on docking.
    /// </summary>
    [DataField]
    public string NodeName = "pipe";
}
