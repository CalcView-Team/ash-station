using Content.Shared.DeviceLinking.Systems;
using Content.Shared.Power;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeviceLinking.Components;

/// <summary>
/// An electro-relay machine that is itself a length of power cable. When switched on - by a linked signal source or
/// from its interface - it severs its own powernet for the selected cable types, breaking the line that runs through
/// it without removing any cabling. A passive device: it needs no power and holds its switch state until flipped back.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CableRelayComponent : Component
{
    /// <summary>
    /// The sink port that toggles the relay when it receives a signal.
    /// </summary>
    [DataField]
    public ProtoId<SinkPortPrototype> TriggerPort = "Trigger";

    /// <summary>
    /// Maps each cable type the relay can sever to the node it owns for that voltage. The relay's own
    /// <see cref="Content.Server.Power.Nodes.RelayCableNode"/>s are what bridge the cables on either side, so
    /// disabling one breaks that voltage's line at the relay.
    /// </summary>
    [DataField]
    public Dictionary<CableType, string> CableNodes = new()
    {
        { CableType.HighVoltage, "hv" },
        { CableType.MediumVoltage, "mv" },
        { CableType.Apc, "lv" },
    };

    /// <summary>
    /// Which cable types the relay severs while active. Configurable from the interface. Defaults to HV and MV;
    /// low voltage is opt-in so the relay doesn't cut station APC lines by surprise.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<CableType> AffectedTypes = new() { CableType.HighVoltage, CableType.MediumVoltage };

    /// <summary>
    /// Whether the player has switched the relay on. The affected nodes are always re-derived from this flag, so the
    /// relay can never end up out of sync with the cables it bridges.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Severed;
}
