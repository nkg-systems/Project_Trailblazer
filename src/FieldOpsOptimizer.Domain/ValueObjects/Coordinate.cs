namespace FieldOpsOptimizer.Domain.ValueObjects;

public record Coordinate(double Latitude, double Longitude)
{
    public static Coordinate Zero => new(0, 0);
    
    public double DistanceToInKilometers(Coordinate other)
    {
        const double earthRadiusKm = 6371.0;
        
        var dLat = ToRadians(other.Latitude - Latitude);
        var dLon = ToRadians(other.Longitude - Longitude);
        
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(Latitude)) * Math.Cos(ToRadians(other.Latitude)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
                
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        
        return earthRadiusKm * c;
    }
    
    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
    
    public override string ToString() => $"{Latitude},{Longitude}";
}
