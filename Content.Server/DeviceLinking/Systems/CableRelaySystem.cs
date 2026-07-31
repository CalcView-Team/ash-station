using Content.Server.NodeContainer.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Power.Nodes;
using Content.Shared.Construction.Components;
using Content.Shared.DeviceLinking.Components;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DeviceLinking.Systems;
using Content.Shared.NodeContainer;
using Content.Shared.Popups;
using Content.Shared.Timing;
using Robust.Shared.Map.Components;

namespace Content.Server.DeviceLinking.Systems;

/// <summary>
/// Server side of <see cref="CableRelayComponent"/>: severs the relay's own cable nodes to match its switch, handles
/// signal toggling, and refuses to anchor over a cable that would bridge the break. Unanchoring isolates the nodes
/// on its own, so the powernet break tracks the switch.
/// </summary>
public sealed class CableRelaySystem : SharedCableRelaySystem
{
    [Dependency] private DeviceLinkSystem _deviceLink = default!;
    [Dependency] private NodeContainerSystem _nodeContainer = default!;
    [Dependency] private NodeGroupSystem _nodeGroup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CableRelayComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<CableRelayComponent, SignalReceivedEvent>(OnSignalReceived);
        SubscribeLocalEvent<CableRelayComponent, AnchorAttemptEvent>(OnAnchorAttempt);
        SubscribeLocalEvent<CableRelayComponent, AnchorStateChangedEvent>(OnAnchorStateChanged);
    }

    private void OnInit(Entity<CableRelayComponent> ent, ref ComponentInit args)
    {
        _deviceLink.EnsureSinkPorts(ent, ent.Comp.TriggerPort);
        ApplyCables(ent);
    }

    private void OnSignalReceived(Entity<CableRelayComponent> ent, ref SignalReceivedEvent args)
    {
        if (args.Port != ent.Comp.TriggerPort)
            return;

        if (TryComp<UseDelayComponent>(ent, out var useDelay) && !_useDelay.TryResetDelay((ent, useDelay), true))
            return;

        Toggle(ent);
    }

    private void OnAnchorAttempt(Entity<CableRelayComponent> ent, ref AnchorAttemptEvent args)
    {
        if (args.Cancelled || !HasCableOnTile(ent))
            return;

        _popup.PopupEntity(Loc.GetString("cable-relay-anchor-blocked"), ent, args.User);
        args.Cancel();
    }

    private void OnAnchorStateChanged(Entity<CableRelayComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored || !HasCableOnTile(ent))
            return;

        _popup.PopupEntity(Loc.GetString("cable-relay-anchor-blocked"), ent);
        _transform.Unanchor(ent, Transform(ent));
    }

    /// <summary>
    /// Re-derives every node the relay owns from its switch and refloods the powernet. Idempotent.
    /// </summary>
    protected override void ApplyCables(Entity<CableRelayComponent> ent)
    {
        if (!TryComp<NodeContainerComponent>(ent, out var nodeContainer))
            return;

        foreach (var (type, nodeName) in ent.Comp.CableNodes)
        {
            if (!_nodeContainer.TryGetNode<RelayCableNode>(nodeContainer, nodeName, out var node))
                continue;

            var shouldConnect = !(ent.Comp.Severed && ent.Comp.AffectedTypes.Contains(type));
            SetNodeEnabled(node, shouldConnect);
        }
    }

    private void SetNodeEnabled(RelayCableNode node, bool enabled)
    {
        if (node.Enabled == enabled)
            return;

        node.Enabled = enabled;
        _nodeGroup.QueueReflood(node);
    }

    private bool HasCableOnTile(Entity<CableRelayComponent> ent)
    {
        var xform = Transform(ent);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return false;

        var indices = _map.TileIndicesFor((grid, gridComp), xform.Coordinates);
        var enumerator = _map.GetAnchoredEntitiesEnumerator(grid, gridComp, indices);
        while (enumerator.MoveNext(out var other))
        {
            if (other != ent.Owner && HasComp<CableComponent>(other))
                return true;
        }

        return false;
    }
}
