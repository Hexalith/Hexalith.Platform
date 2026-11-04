
using System.Text.Json;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Sample.Counter.Events;
using Hexalith.EventStore.Sample.Counter.State;
using Hexalith.EventStore.Sample.Counter.Queries;

namespace Hexalith.EventStore.Sample.Counter.Projections;

/// <summary>
/// Projection handler for the Counter domain. Replays events from a
/// <see cref="ProjectionRequest"/> onto a fresh <see cref="CounterState"/>
/// and returns the current count as a <see cref="ProjectionResponse"/>.
/// Rebuild-from-scratch is the supported pattern under the full-replay
/// projection contract; the handler does not need to read or persist prior
/// projection state.
/// </summary>
public static class CounterProjectionHandler {
    /// <summary>
    /// Projects a sequence of events into a counter projection response.
    /// </summary>
    /// <param name="request">The projection request containing events to replay.</param>
    /// <returns>A projection response with type "counter" and the current count state.</returns>
    public static ProjectionResponse Project(ProjectionRequest request) {
        ArgumentNullException.ThrowIfNull(request);

        CounterState state = new();
        foreach (ProjectionEventDto? evt in request.Events ?? []) {
            if (evt is null) {
                continue;
            }

            ApplyEvent(state, evt.EventTypeName);
        }

        return new ProjectionResponse(
            "counter",
            JsonSerializer.SerializeToElement(new CounterStatusPayload(state.Count)))
        {
            NotificationGroupScope = "counter-status",
            NotificationMetadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["change-kind"] = "counter-projection-updated",
            },
        };
    }

    private static void ApplyEvent(CounterState state, string eventTypeName) {
        if (string.IsNullOrEmpty(eventTypeName)) {
            return;
        }

        // EventTypeName may be short ("CounterIncremented") or fully qualified; suffix match handles both.
        if (eventTypeName.EndsWith(nameof(CounterIncremented), StringComparison.Ordinal)) {
            state.Apply(new CounterIncremented());
        }
        else if (eventTypeName.EndsWith(nameof(CounterDecremented), StringComparison.Ordinal)) {
            state.Apply(new CounterDecremented());
        }
        else if (eventTypeName.EndsWith(nameof(CounterReset), StringComparison.Ordinal)) {
            state.Apply(new CounterReset());
        }
        else if (eventTypeName.EndsWith(nameof(CounterClosed), StringComparison.Ordinal)) {
            state.Apply(new CounterClosed());
        }

        // Unknown events (CounterCannotGoNegative, AggregateTerminated): silently skipped.
        // These don't affect counter projection state.
    }
}
