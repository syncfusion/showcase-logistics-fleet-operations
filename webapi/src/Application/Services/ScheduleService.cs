using LogisticsFleetOperations.Application.Abstractions;
using LogisticsFleetOperations.Application.Dtos;
using LogisticsFleetOperations.Domain;

namespace LogisticsFleetOperations.Application.Services;

/// <summary>Driver shift read model and the authenticated reschedule/reassign command, with conflict validation.</summary>
public sealed class ScheduleService(IReadRepository readRepository, IWriteRepository writeRepository)
{
    public async Task<List<ScheduleEntryDto>> GetScheduleAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)
    {
        var entries = await readRepository.GetScheduleAsync(from, to, ct);
        return entries.Select(Mapping.ToDto).ToList();
    }

    /// <summary>Rejects an overlap with another confirmed entry for the same driver (409).</summary>
    public async Task<ScheduleEntryDto> RescheduleAsync(Guid id, PatchScheduleRequest request, CancellationToken ct)
    {
        var entry = await writeRepository.GetScheduleEntryAsync(id, ct)
            ?? throw new NotFoundException($"Schedule entry '{id}' not found.");

        var candidate = new ScheduleEntry
        {
            Id = entry.Id,
            DriverId = entry.DriverId,
            ShiftStart = request.ShiftStart ?? entry.ShiftStart,
            ShiftEnd = request.ShiftEnd ?? entry.ShiftEnd,
            RouteId = request.RouteId ?? entry.RouteId,
            Status = ParseStatusOrDefault(request.Status, entry.Status),
        };

        if (candidate.ShiftStart >= candidate.ShiftEnd)
        {
            throw new BusinessRuleException("shiftStart must be before shiftEnd.");
        }

        if (candidate.Status == ScheduleEntryStatus.Confirmed)
        {
            var siblings = (await writeRepository.GetDriverScheduleEntriesAsync(entry.DriverId, ct))
                .Where(e => e.Id != entry.Id)
                .ToList();

            if (ScheduleRules.HasConflict(candidate, siblings))
            {
                throw new ConflictException(
                    $"Rescheduled shift overlaps another confirmed shift for driver '{entry.DriverId}'.");
            }
        }

        entry.ShiftStart = candidate.ShiftStart;
        entry.ShiftEnd = candidate.ShiftEnd;
        entry.RouteId = candidate.RouteId;
        entry.Status = candidate.Status;

        await writeRepository.SaveScheduleEntryAsync(entry, ct);
        return entry.ToDto();
    }

    private static ScheduleEntryStatus ParseStatusOrDefault(string? status, ScheduleEntryStatus fallback)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return fallback;
        }

        if (!Enum.TryParse<ScheduleEntryStatus>(status, ignoreCase: true, out var value))
        {
            throw new BusinessRuleException($"Unknown schedule status '{status}'.");
        }

        return value;
    }
}
