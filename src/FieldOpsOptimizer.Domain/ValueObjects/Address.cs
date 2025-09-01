namespace FieldOpsOptimizer.Domain.ValueObjects;

public record Address(
    string Street,
    string? Unit,
    string City,
    string State,
    string PostalCode,
    string Country = "US",
    Coordinate? Coordinate = null)
{
    public string FormattedAddress => Unit switch
    {
        null or "" => $"{Street}, {City}, {State} {PostalCode}",
        _ => $"{Street} {Unit}, {City}, {State} {PostalCode}"
    };
    
    public bool HasCoordinates => Coordinate != null;
}
