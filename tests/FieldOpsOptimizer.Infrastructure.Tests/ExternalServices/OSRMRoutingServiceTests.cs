using FieldOpsOptimizer.Application.Common.Interfaces;
using FieldOpsOptimizer.Domain.ValueObjects;
using FieldOpsOptimizer.Infrastructure.ExternalServices;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;

namespace FieldOpsOptimizer.Infrastructure.Tests.ExternalServices;

public class OSRMRoutingServiceTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly Mock<ILogger<OSRMRoutingService>> _mockLogger;
    private readonly OSRMOptions _options;
    private readonly OSRMRoutingService _service;

    public OSRMRoutingServiceTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _mockLogger = new Mock<ILogger<OSRMRoutingService>>();
        _options = new OSRMOptions { BaseUrl = "http://localhost:5000" };

        var httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _service = new OSRMRoutingService(httpClient, _mockLogger.Object, Options.Create(_options));
    }

    [Fact]
    public async Task GetDistanceMatrixAsync_WithValidCoordinates_ShouldReturnMatrix()
    {
        // Arrange
        var coordinates = new List<Coordinate>
        {
            new Coordinate(40.7128, -74.0060), // NYC
            new Coordinate(34.0522, -118.2437), // LA
            new Coordinate(41.8781, -87.6298)  // Chicago
        };

        var responseJson = JsonSerializer.Serialize(new
        {
            code = "Ok",
            distances = new[] { new[] { 0.0, 1000000.0, 500000.0 }, new[] { 1000000.0, 0.0, 800000.0 }, new[] { 500000.0, 800000.0, 0.0 } },
            durations = new[] { new[] { 0.0, 10000.0, 5000.0 }, new[] { 10000.0, 0.0, 8000.0 }, new[] { 5000.0, 8000.0, 0.0 } }
        });

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _service.GetDistanceMatrixAsync(coordinates);

        // Assert
        result.Should().NotBeNull();
        result.Distances[0, 1].Should().BeApproximately(1000.0, 0.01); // Converted to km
        result.Durations[0, 1].Should().Be(10000.0);
        result.Coordinates.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetDistanceMatrixAsync_WithLessThanTwoCoordinates_ShouldThrowException()
    {
        // Arrange
        var coordinates = new List<Coordinate> { new Coordinate(40.7128, -74.0060) };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.GetDistanceMatrixAsync(coordinates));
    }

    [Fact]
    public async Task GetDistanceMatrixAsync_WithOSRMError_ShouldThrowException()
    {
        // Arrange
        var coordinates = new List<Coordinate>
        {
            new Coordinate(40.7128, -74.0060),
            new Coordinate(34.0522, -118.2437)
        };

        var responseJson = JsonSerializer.Serialize(new
        {
            code = "InvalidInput",
            message = "Invalid coordinates"
        });

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.GetDistanceMatrixAsync(coordinates));
    }

    [Fact]
    public async Task GetNavigationRouteAsync_WithValidCoordinates_ShouldReturnRoute()
    {
        // Arrange
        var start = new Coordinate(40.7128, -74.0060);
        var end = new Coordinate(34.0522, -118.2437);

        var responseJson = JsonSerializer.Serialize(new
        {
            code = "Ok",
            routes = new[]
            {
                new
                {
                    distance = 1000000.0,
                    duration = 10000.0,
                    geometry = new
                    {
                        coordinates = new[] { new[] { -74.0060, 40.7128 }, new[] { -118.2437, 34.0522 } }
                    },
                    legs = new[]
                    {
                        new
                        {
                            distance = 1000000.0,
                            duration = 10000.0,
                            steps = new[]
                            {
                                new
                                {
                                    distance = 500000.0,
                                    duration = 5000.0,
                                    maneuver = new
                                    {
                                        instruction = "Head west",
                                        location = new[] { -74.0060, 40.7128 }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        });

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _service.GetNavigationRouteAsync(start, end);

        // Assert
        result.Should().NotBeNull();
        result.Distance.Should().BeApproximately(1000.0, 0.01);
        result.Duration.Should().Be(TimeSpan.FromSeconds(10000));
        result.Path.Should().HaveCount(2);
        result.Instructions.Should().NotBeEmpty();
        result.Instructions.First().Text.Should().Be("Head west");
    }

    [Fact]
    public async Task GetNavigationRouteAsync_WithNoRoutes_ShouldThrowException()
    {
        // Arrange
        var start = new Coordinate(40.7128, -74.0060);
        var end = new Coordinate(34.0522, -118.2437);

        var responseJson = JsonSerializer.Serialize(new
        {
            code = "Ok",
            routes = Array.Empty<object>()
        });

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.GetNavigationRouteAsync(start, end));
    }

    [Fact]
    public async Task OptimizeRouteAsync_WithSimpleRequest_ShouldOptimizeRoute()
    {
        // Arrange
        var request = new OptimizeRouteRequest(
            new Coordinate(40.7128, -74.0060),
            new List<RouteWaypoint>
            {
                new RouteWaypoint(new Coordinate(34.0522, -118.2437), "LA", TimeSpan.FromMinutes(30)),
                new RouteWaypoint(new Coordinate(41.8781, -87.6298), "Chicago", TimeSpan.FromMinutes(20))
            },
            null,
            false);

        var responseJson = JsonSerializer.Serialize(new
        {
            code = "Ok",
            distances = new[] 
            { 
                new[] { 0.0, 1000000.0, 500000.0 }, 
                new[] { 1000000.0, 0.0, 800000.0 }, 
                new[] { 500000.0, 800000.0, 0.0 } 
            },
            durations = new[] 
            { 
                new[] { 0.0, 10000.0, 5000.0 }, 
                new[] { 10000.0, 0.0, 8000.0 }, 
                new[] { 5000.0, 8000.0, 0.0 } 
            }
        });

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _service.OptimizeRouteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.OptimizedWaypoints.Should().HaveCount(2);
        result.TotalDistance.Should().BeGreaterThan(0);
        result.TotalDuration.Should().BeGreaterThan(TimeSpan.Zero);
        result.Segments.Should().NotBeEmpty();
    }

    [Fact]
    public async Task OptimizeRouteAsync_WithRoundTrip_ShouldReturnToStart()
    {
        // Arrange
        var request = new OptimizeRouteRequest(
            new Coordinate(40.7128, -74.0060),
            new List<RouteWaypoint>
            {
                new RouteWaypoint(new Coordinate(34.0522, -118.2437), "LA", TimeSpan.FromMinutes(30))
            },
            null,
            true);

        var responseJson = JsonSerializer.Serialize(new
        {
            code = "Ok",
            distances = new[] { new[] { 0.0, 1000000.0 }, new[] { 1000000.0, 0.0 } },
            durations = new[] { new[] { 0.0, 10000.0 }, new[] { 10000.0, 0.0 } }
        });

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _service.OptimizeRouteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Segments.Should().HaveCount(2); // To waypoint and back
    }

    [Fact]
    public async Task OptimizeRouteAsync_WithEndLocation_ShouldIncludeEndLocation()
    {
        // Arrange
        var endLocation = new Coordinate(42.3601, -71.0589); // Boston
        var request = new OptimizeRouteRequest(
            new Coordinate(40.7128, -74.0060),
            new List<RouteWaypoint>
            {
                new RouteWaypoint(new Coordinate(34.0522, -118.2437), "LA", TimeSpan.FromMinutes(30))
            },
            endLocation,
            false);

        var responseJson = JsonSerializer.Serialize(new
        {
            code = "Ok",
            distances = new[] 
            { 
                new[] { 0.0, 1000000.0, 300000.0 }, 
                new[] { 1000000.0, 0.0, 800000.0 }, 
                new[] { 300000.0, 800000.0, 0.0 } 
            },
            durations = new[] 
            { 
                new[] { 0.0, 10000.0, 3000.0 }, 
                new[] { 10000.0, 0.0, 8000.0 }, 
                new[] { 3000.0, 8000.0, 0.0 } 
            }
        });

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _service.OptimizeRouteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Segments.Should().NotBeEmpty();
    }

    [Fact]
    public async Task OptimizeRouteAsync_WithServiceTimes_ShouldIncludeInDuration()
    {
        // Arrange
        var serviceTime = TimeSpan.FromMinutes(45);
        var request = new OptimizeRouteRequest(
            new Coordinate(40.7128, -74.0060),
            new List<RouteWaypoint>
            {
                new RouteWaypoint(new Coordinate(34.0522, -118.2437), "LA", serviceTime)
            },
            null,
            false);

        var responseJson = JsonSerializer.Serialize(new
        {
            code = "Ok",
            distances = new[] { new[] { 0.0, 1000000.0 }, new[] { 1000000.0, 0.0 } },
            durations = new[] { new[] { 0.0, 10000.0 }, new[] { 10000.0, 0.0 } }
        });

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        // Act
        var result = await _service.OptimizeRouteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.TotalDuration.Should().BeGreaterThanOrEqualTo(serviceTime);
    }

    // Helper methods
    private void SetupHttpResponse(HttpStatusCode statusCode, string content)
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });
    }
}
