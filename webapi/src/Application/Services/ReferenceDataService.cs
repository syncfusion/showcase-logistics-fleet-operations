using LogisticsFleetOperations.Application.Abstractions;
using LogisticsFleetOperations.Application.Dtos;

namespace LogisticsFleetOperations.Application.Services;

public sealed class ReferenceDataService(IReadRepository repository)
{
    public async Task<List<VehicleDto>> GetVehiclesAsync(CancellationToken ct) =>
        (await repository.GetVehiclesAsync(ct)).Select(Mapping.ToDto).ToList();

    public async Task<List<DriverDto>> GetDriversAsync(CancellationToken ct) =>
        (await repository.GetDriversAsync(ct)).Select(Mapping.ToDto).ToList();

    public async Task<List<DepotDto>> GetDepotsAsync(CancellationToken ct) =>
        (await repository.GetDepotsAsync(ct)).Select(Mapping.ToDto).ToList();

    public async Task<MetaDto> GetMetaAsync(CancellationToken ct)
    {
        var meta = await repository.GetMetaAsync(ct);
        return meta is null
            ? new MetaDto("unseeded", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
            : new MetaDto(meta.DatasetVersion, meta.ScenarioClockReference, meta.SeededAt);
    }
}
