using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client.Atmos;
using Content.Client.Hands.Systems;
using Content.Shared.Atmos.Components;
using Content.Shared.Interaction;
using Content.Shared.RCD;
using Content.Shared.RCD.Components;
using Robust.Client.Placement;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.RCD;

/// <summary>
/// System for handling structure ghost placement in places where RCD can create objects.
/// </summary>
public sealed partial class RCDConstructionGhostSystem : EntitySystem
{
    private const string PlacementMode = nameof(AlignRCDConstruction);
    private const string PipeLayerPlacementMode = nameof(AlignAtmosPipeLayers);

    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IPlacementManager _placementManager = default!;
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private HandsSystem _hands = default!;

    private Direction _placementDirection = default;
    private AtmosPipeLayer _selectedPipeLayer = AtmosPipeLayer.Primary;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Only act on the newest predicted tick. Client systems are also ticked while prediction
        // resimulates past ticks, and network events raised during a resim get stamped with that
        // past tick, so the server logs a "Got late MsgEntity" warning for every one of them.
        // The placement manager isn't part of the simulation anyway, so resim ticks have nothing
        // new to tell us here.
        if (!_timing.IsFirstTimePredicted)
            return;

        // Get current placer data
        var placerEntity = _placementManager.CurrentPermission?.MobUid;
        var placerProto = _placementManager.CurrentPermission?.EntityType;
        var placerIsRCD = HasComp<RCDComponent>(placerEntity);

        // Exit if erasing or the current placer is not an RCD (build mode is active)
        if (_placementManager.Eraser || (placerEntity != null && !placerIsRCD))
            return;

        // Determine if player is carrying an RCD in their active hand
        if (_playerManager.LocalSession?.AttachedEntity is not { } player)
            return;

        var heldEntity = _hands.GetActiveItem(player);

        // Don't open the placement overlay for client-side RCDs.
        // This may happen when predictively spawning one in your hands.
        if (heldEntity != null && IsClientSide(heldEntity.Value))
            return;

        if (!TryComp<RCDComponent>(heldEntity, out var rcd))
        {
            // If the player was holding an RCD, but is no longer, cancel placement
            if (placerIsRCD)
                _placementManager.Clear();

            return;
        }
        var prototype = ProtoMan.Index(rcd.ProtoId);

        // Update the direction the RCD prototype based on the placer direction
        if (_placementDirection != _placementManager.Direction)
        {
            _placementDirection = _placementManager.Direction;
            RaiseNetworkEvent(new RCDConstructionGhostRotationEvent(GetNetEntity(heldEntity.Value), _placementDirection));
        }

        // Determine whether this operation places a layered atmos entity (pipe, pump, etc.).
        // If so, reuse the existing AlignAtmosPipeLayers placement mode, which already handles
        // mouse-based layer selection and the guide dots, instead of the plain RCD placement mode.
        var usesPipeLayers = TryGetPipeLayers(prototype, out var pipeLayers);
        var placementMode = usesPipeLayers ? PipeLayerPlacementMode : PlacementMode;

        // For layered placement, forward the currently hovered layer to the server.
        if (usesPipeLayers && heldEntity == placerEntity)
            UpdateSelectedPipeLayer(heldEntity.Value);

        // Determine whether the current placer already matches this operation.
        // AlignAtmosPipeLayers swaps the placer prototype between the base entity and its layer
        // variants, so treat any of those variants as "unchanged" to avoid recreating the placer.
        var placerMatches = heldEntity == placerEntity && placerProto != null &&
            (placerProto == prototype.Prototype ||
             (usesPipeLayers && pipeLayers!.AlternativePrototypes.Values.Any(v => v.Id == placerProto)));

        if (placerMatches)
            return;

        // Reset the tracked layer whenever we (re)create the placer for a new operation
        _selectedPipeLayer = AtmosPipeLayer.Primary;

        // Create a new placer
        var newObjInfo = new PlacementInformation
        {
            MobUid = heldEntity.Value,
            PlacementOption = placementMode,
            EntityType = prototype.Prototype,
            Range = (int)Math.Ceiling(SharedInteractionSystem.InteractionRange),
            IsTile = (prototype.Mode == RcdMode.ConstructTile),
            UseEditorContext = false,
        };

        _placementManager.Clear();
        _placementManager.BeginPlacing(newObjInfo);
    }

    /// <summary>
    /// Returns true if the operation builds a layered atmos entity (i.e. its target entity prototype
    /// has an <see cref="AtmosPipeLayersComponent"/> with more than one layer).
    /// </summary>
    private bool TryGetPipeLayers(RCDPrototype prototype, [NotNullWhen(true)] out AtmosPipeLayersComponent? pipeLayers)
    {
        pipeLayers = null;

        if (prototype.Mode != RcdMode.ConstructObject || prototype.Prototype == null)
            return false;

        if (!ProtoMan.TryIndex<EntityPrototype>(prototype.Prototype, out var entProto))
            return false;

        if (!entProto.TryComp<AtmosPipeLayersComponent>(out pipeLayers, _factory))
            return false;

        return pipeLayers.NumberOfPipeLayers > 1 && pipeLayers.AlternativePrototypes.Count > 0;
    }

    /// <summary>
    /// Reads the pipe layer that AlignAtmosPipeLayers has selected (encoded as the current placer
    /// prototype variant) and forwards it to the server when it changes.
    /// </summary>
    private void UpdateSelectedPipeLayer(EntityUid rcd)
    {
        var placerProto = _placementManager.CurrentPermission?.EntityType;

        if (placerProto == null)
            return;

        var layer = AtmosPipeLayer.Primary;

        if (ProtoMan.TryIndex<EntityPrototype>(placerProto, out var entProto) &&
            entProto.TryComp<AtmosPipeLayersComponent>(out var comp, _factory))
        {
            layer = comp.CurrentPipeLayer;
        }

        if (layer == _selectedPipeLayer)
            return;

        _selectedPipeLayer = layer;
        RaiseNetworkEvent(new RCDConstructionGhostLayerEvent(GetNetEntity(rcd), layer));
    }
}
