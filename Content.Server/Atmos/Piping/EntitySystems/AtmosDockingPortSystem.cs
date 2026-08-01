using System.Diagnostics.CodeAnalysis;
using Content.Server.Atmos.Piping.Components;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Shuttles.Events;

namespace Content.Server.Atmos.Piping.EntitySystems;

/// <summary>
///     Joins the pipe nets of two docked grids together, so long as both docking ports
///     have an <see cref="AtmosDockingPortComponent"/>.
/// </summary>
public sealed partial class AtmosDockingPortSystem : EntitySystem
{
    [Dependency] private NodeContainerSystem _nodeContainer = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AtmosDockingPortComponent, DockEvent>(OnDock);
        SubscribeLocalEvent<AtmosDockingPortComponent, UndockEvent>(OnUndock);
    }

    private void OnDock(Entity<AtmosDockingPortComponent> ent, ref DockEvent args)
    {
        // Raised on both docks, and both are already linked by this point, so only do the work once.
        if (args.DockA.Owner != ent.Owner)
            return;

        if (!TryGetPorts(args.DockA.Owner, args.DockB.Owner, out var portA, out var portB))
            return;

        portA.AddAlwaysReachable(portB);
        portB.AddAlwaysReachable(portA);
    }

    private void OnUndock(Entity<AtmosDockingPortComponent> ent, ref UndockEvent args)
    {
        if (args.DockA.Owner != ent.Owner)
            return;

        if (!TryGetPorts(args.DockA.Owner, args.DockB.Owner, out var portA, out var portB))
            return;

        portA.RemoveAlwaysReachable(portB);
        portB.RemoveAlwaysReachable(portA);
    }

    /// <summary>
    ///     Gets the pipe nodes of both ends of a dock, if both ends are actually atmos docking ports.
    /// </summary>
    private bool TryGetPorts(
        EntityUid uidA,
        EntityUid uidB,
        [NotNullWhen(true)] out PipeNode? portA,
        [NotNullWhen(true)] out PipeNode? portB)
    {
        portA = null;
        portB = null;

        // Docking a port to a plain airlock is legal, it just doesn't move any gas.
        return TryComp<AtmosDockingPortComponent>(uidA, out var compA)
            && TryComp<AtmosDockingPortComponent>(uidB, out var compB)
            && _nodeContainer.TryGetNode(uidA, compA.NodeName, out portA)
            && _nodeContainer.TryGetNode(uidB, compB.NodeName, out portB);
    }
}
