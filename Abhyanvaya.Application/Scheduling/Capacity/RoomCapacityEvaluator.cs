namespace Abhyanvaya.Application.Scheduling.Capacity;

/// <summary>
/// AI-SCHED-CAP Prompt 3A — Result of room-fit evaluation (PlacementSize vs effective room capacity).
/// </summary>
public readonly record struct RoomCapacityEvaluation(
    bool IsEvaluable,
    bool IsExceeded,
    int RoomCapacity,
    decimal EffectiveCapacity,
    decimal MarginPercent,
    PlacementSizeResolution Placement)
{
    public static RoomCapacityEvaluation NotEvaluable(PlacementSizeResolution placement) =>
        new(false, false, 0, 0m, 0m, placement);
}

/// <summary>
/// Authoritative room-capacity semantics for ConflictEngine and SoftValidation.
/// EffectiveCapacity = Room.Capacity × (1 − margin%/100); exceeded when PlacementSize &gt; EffectiveCapacity.
/// </summary>
public interface IRoomCapacityEvaluator
{
    decimal ComputeEffectiveCapacity(int roomCapacity, decimal marginPercent);

    RoomCapacityEvaluation Evaluate(
        int roomCapacity,
        decimal marginPercent,
        PlacementSizeResolution placement);
}

/// <summary>Shared room-capacity implementation — no DB access; no TG capacity merge.</summary>
public sealed class RoomCapacityEvaluator : IRoomCapacityEvaluator
{
    public static RoomCapacityEvaluator Instance { get; } = new();

    public decimal ComputeEffectiveCapacity(int roomCapacity, decimal marginPercent)
    {
        var margin = marginPercent < 0m ? 0m : marginPercent;
        return roomCapacity * (1m - (margin / 100m));
    }

    public RoomCapacityEvaluation Evaluate(
        int roomCapacity,
        decimal marginPercent,
        PlacementSizeResolution placement)
    {
        if (!placement.HasValue)
            return RoomCapacityEvaluation.NotEvaluable(placement);

        var effective = ComputeEffectiveCapacity(roomCapacity, marginPercent);
        var exceeded = placement.Value > effective;
        return new RoomCapacityEvaluation(
            IsEvaluable: true,
            IsExceeded: exceeded,
            RoomCapacity: roomCapacity,
            EffectiveCapacity: effective,
            MarginPercent: marginPercent < 0m ? 0m : marginPercent,
            Placement: placement);
    }
}
