using LogisticsFleetOperations.Application.Abstractions;
using LogisticsFleetOperations.Application.Dtos;
using LogisticsFleetOperations.Domain;

namespace LogisticsFleetOperations.Application.Services;

public sealed class ShipmentService(IReadRepository repository, TimeProvider clock)
{
    public async Task<PagedResult<ShipmentDto>> GetShipmentsAsync(string? status, Guid? routeId, int page, int pageSize, CancellationToken ct)
    {
        ShipmentStatus? parsed = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<ShipmentStatus>(status, ignoreCase: true, out var value))
            {
                throw new BusinessRuleException($"Unknown shipment status '{status}'.");
            }
            parsed = value;
        }

        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 25 : pageSize;

        var (items, total) = await repository.GetShipmentsPagedAsync(parsed, routeId, page, pageSize, ct);
        var now = clock.GetUtcNow();
        return new PagedResult<ShipmentDto>(items.Select(s => s.ToDto(now)).ToList(), total, page, pageSize);
    }
}
