namespace FieldOpsOptimizer.Domain.ValueObjects;

public record Address
{
    public string Street { get; init; } = string.Empty;
    public string? Unit { get; init; }
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string Country { get; init; } = "US";
    public Coordinate? Coordinate { get; init; }
    
    // Parameterless constructor for EF Core
    private Address() { }
    
    // Factory method for creating instances
    public Address(
        string street,
        string? unit,
        string city,
        string state,
        string postalCode,
        string country = "US",
        Coordinate? coordinate = null)
    {
        Street = street;
        Unit = unit;
        City = city;
        State = state;
        PostalCode = postalCode;
        Country = country;
        Coordinate = coordinate;
    }
    
    public string FormattedAddress => Unit switch
    {
        null or "" => $"{Street}, {City}, {State} {PostalCode}",
        _ => $"{Street} {Unit}, {City}, {State} {PostalCode}"
    };
    
    public bool HasCoordinates => Coordinate != null;
}
