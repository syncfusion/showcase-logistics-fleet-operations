using LogisticsFleetOperations.Application.Abstractions;
using LogisticsFleetOperations.Application.Dtos;
using LogisticsFleetOperations.Domain;

namespace LogisticsFleetOperations.Application.Services;

/// <summary>Triage state machine for shipment exceptions (Kanban + Grid backing service).</summary>
public sealed class ExceptionService(IReadRepository readRepository, IWriteRepository writeRepository)
{
    public async Task<List<ExceptionDto>> GetExceptionsAsync(string? status, string? severity, CancellationToken ct)
    {
        ExceptionStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<ExceptionStatus>(status, ignoreCase: true, out var value))
            {
                throw new BusinessRuleException($"Unknown exception status '{status}'.");
            }
            parsedStatus = value;
        }

        ExceptionSeverity? parsedSeverity = null;
        if (!string.IsNullOrWhiteSpace(severity))
        {
            if (!Enum.TryParse<ExceptionSeverity>(severity, ignoreCase: true, out var value))
            {
                throw new BusinessRuleException($"Unknown exception severity '{severity}'.");
            }
            parsedSeverity = value;
        }

        var exceptions = await readRepository.GetExceptionsAsync(parsedStatus, parsedSeverity, ct);
        var shipments = await readRepository.GetAllShipmentsAsync(ct);
        var customerLabelByShipmentId = shipments.ToDictionary(s => s.Id, s => s.CustomerLabel);
        return exceptions
            .Select(e => e.ToDto(customerLabelByShipmentId.GetValueOrDefault(e.ShipmentId, "Unknown customer")))
            .ToList();
    }

    /// <summary>
    /// Applies a triage transition. Rejects a skip from Open directly to Resolved (mandatory
    /// Investigating step) and rejects any transition on an already-Resolved exception.
    /// </summary>
    public async Task<ExceptionDto> UpdateStatusAsync(Guid id, string newStatus, CancellationToken ct)
    {
        if (!Enum.TryParse<ExceptionStatus>(newStatus, ignoreCase: true, out var target))
        {
            throw new BusinessRuleException($"Unknown exception status '{newStatus}'.");
        }

        var exception = await writeRepository.GetExceptionAsync(id, ct)
            ?? throw new NotFoundException($"Exception '{id}' not found.");

        if (exception.Status == target)
        {
            throw new BusinessRuleException($"Exception '{id}' is already '{target}'.");
        }

        if (exception.Status == ExceptionStatus.Resolved)
        {
            throw new BusinessRuleException($"Exception '{id}' is already resolved; use an explicit reopen action instead.");
        }

        if (!ExceptionTransitions.IsAllowed(exception.Status, target))
        {
            throw new BusinessRuleException(
                $"Cannot transition exception '{id}' from '{exception.Status}' to '{target}'.");
        }

        exception.Status = target;
        await writeRepository.SaveExceptionAsync(exception, ct);
        var shipments = await readRepository.GetAllShipmentsAsync(ct);
        var customerLabel = shipments.FirstOrDefault(s => s.Id == exception.ShipmentId)?.CustomerLabel ?? "Unknown customer";
        return exception.ToDto(customerLabel);
    }
}
