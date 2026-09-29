namespace LogisticsFleetOperations.Application.Abstractions;

/// <summary>Thrown when a requested entity does not exist. API layer maps this to 404.</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

/// <summary>Thrown when a request conflicts with existing state (e.g. schedule overlap). API layer maps this to 409.</summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}

/// <summary>Thrown when a request violates a business rule (e.g. invalid status transition). API layer maps this to 400.</summary>
public sealed class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message) { }
}
