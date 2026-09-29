using LogisticsFleetOperations.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace LogisticsFleetOperations.Api.Controllers;

/// <summary>Typed error body for rejected writes (schedule overlap 409, invalid status transition 400, etc).</summary>
public sealed record ApiError(string Error, string Message);

internal static class ErrorMapping
{
    public static ActionResult ToActionResult(this Exception ex) => ex switch
    {
        NotFoundException e => new NotFoundObjectResult(new ApiError("not_found", e.Message)),
        ConflictException e => new ConflictObjectResult(new ApiError("conflict", e.Message)),
        BusinessRuleException e => new BadRequestObjectResult(new ApiError("business_rule_violation", e.Message)),
        _ => throw ex,
    };
}
