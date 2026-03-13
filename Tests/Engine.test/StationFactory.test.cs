using System.Reflection;
using System.Text.Json;
using Core.Charging;
using Core.Shared;
using Engine.StationFactory;

public class StationFactoryTests
{
    public StationFactoryTests()
    {
        var csvPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Data", "energy_prices.csv"));

        Assert.True(File.Exists(csvPath), $"CSV not found at: {csvPath}");
        EnergyPrices.Initialize(csvPath);
    }

    private static StationFactory CreateFactory(
        StationFactoryOptions? options = null,
        int seed = 42)
    {
        return new StationFactory(options ?? new StationFactoryOptions(), seed);
    }

    private static FileInfo CreateTempLocationsFile(params object[] locations)
    {
        var json = JsonSerializer.Serialize(locations);
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(filePath, json);
        return new FileInfo(filePath);
    }

    private static FileInfo CreateTempLocationsFile(int count)
    {
        var locations = Enumerable.Range(1, count)
            .Select(i => (object)new
            {
                Name = $"Station {i}",
                Address = $"Address {i}",
                Latitude = 56.0 + (i * 0.001),
                Longitude = 10.0 + (i * 0.001),
            })
            .ToArray();

        return CreateTempLocationsFile(locations);
    }

    private static FileInfo CreateTempFileWithRawContent(string content)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(filePath, content);
        return new FileInfo(filePath);
    }

    private static Dictionary<Socket, int> CountSockets(IEnumerable<Station> stations)
    {
        var counts = new Dictionary<Socket, int>();

        var chargingPointField = typeof(Charger).GetField(
            "_chargingpoint",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(chargingPointField);

        foreach (var station in stations)
        {
            foreach (var charger in station.Chargers)
            {
                var chargingPoint = chargingPointField!.GetValue(charger) as IChargingPoint;
                Assert.NotNull(chargingPoint);

                foreach (var socket in chargingPoint!.GetSockets())
                {
                    if (!counts.TryAdd(socket, 1))
                    {
                        counts[socket]++;
                    }
                }
            }
        }

        return counts;
    }

    [Fact]
    public void Constructor_WhenTotalChargersIsZero_ThrowsArgumentOutOfRangeException()
    {
        var options = new StationFactoryOptions
        {
            TotalChargers = 0,
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => new StationFactory(options, 42));
    }

    [Fact]
    public void Constructor_WhenDualChargingPointProbabilityIsInvalid_ThrowsArgumentOutOfRangeException()
    {
        var options = new StationFactoryOptions
        {
            DualChargingPointProbability = 1.5,
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => new StationFactory(options, 42));
    }

    [Fact]
    public void Constructor_WhenSocketProbabilitiesAreEmpty_ThrowsArgumentException()
    {
        var options = new StationFactoryOptions
        {
            SocketProbabilities = new Dictionary<Socket, double>(),
        };

        Assert.Throws<ArgumentException>(() => new StationFactory(options, 42));
    }

    [Fact]
    public void Constructor_WhenSocketProbabilitiesDoNotSumToOne_ThrowsArgumentException()
    {
        var options = new StationFactoryOptions
        {
            SocketProbabilities = new Dictionary<Socket, double>
            {
                { Socket.CHADEMO, 0.5 },
                { Socket.CCS, 0.2 },
            },
        };

        Assert.Throws<ArgumentException>(() => new StationFactory(options, 42));
    }

    [Fact]
    public void CreateStations_EmptyFile_ReturnsEmptyList()
    {
        var file = CreateTempLocationsFile();

        try
        {
            var factory = CreateFactory();
            var stations = factory.CreateStations(file);

            Assert.Empty(stations);
        }
        finally
        {
            file.Delete();
        }
    }

    [Fact]
    public void CreateStations_SingleLocation_ReturnsSingleStationWithMappedProperties()
    {
        var file = CreateTempLocationsFile(
            new
            {
                Name = "Only Station",
                Address = "Only Address",
                Latitude = 57.0,
                Longitude = 9.0,
            });

        try
        {
            var factory = CreateFactory();
            var stations = factory.CreateStations(file);

            var station = Assert.Single(stations);
            Assert.Equal((ushort)1, station.Id);
            Assert.Equal("Only Station", station.Name);
            Assert.Equal("Only Address", station.Address);
            Assert.Equal(57.0, station.Position.Latitude);
            Assert.Equal(9.0, station.Position.Longitude);
            Assert.NotEmpty(station.Chargers);
        }
        finally
        {
            file.Delete();
        }
    }

    [Fact]
    public void CreateStations_MultipleLocations_ReturnsOneStationPerLocationInInputOrder()
    {
        var file = CreateTempLocationsFile(
            new
            {
                Name = "First",
                Address = "Address 1",
                Latitude = 57.0,
                Longitude = 9.0,
            },
            new
            {
                Name = "Second",
                Address = "Address 2",
                Latitude = 58.0,
                Longitude = 10.0,
            },
            new
            {
                Name = "Third",
                Address = "Address 3",
                Latitude = 59.0,
                Longitude = 11.0,
            });

        try
        {
            var factory = CreateFactory();
            var stations = factory.CreateStations(file);

            Assert.Equal(3, stations.Count);
            Assert.Equal("First", stations[0].Name);
            Assert.Equal("Second", stations[1].Name);
            Assert.Equal("Third", stations[2].Name);
            Assert.Equal((ushort)1, stations[0].Id);
            Assert.Equal((ushort)2, stations[1].Id);
            Assert.Equal((ushort)3, stations[2].Id);
        }
        finally
        {
            file.Delete();
        }
    }

    [Fact]
    public void CreateStations_WhenFileDoesNotExist_ThrowsFileNotFoundException()
    {
        var file = new FileInfo(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json"));
        var factory = CreateFactory();

        Assert.Throws<FileNotFoundException>(() => factory.CreateStations(file));
    }

    [Fact]
    public void CreateStations_WhenJsonIsInvalid_ThrowsJsonException()
    {
        var file = CreateTempFileWithRawContent("{ invalid json }");

        try
        {
            var factory = CreateFactory();

            Assert.Throws<JsonException>(() => factory.CreateStations(file));
        }
        finally
        {
            file.Delete();
        }
    }

    [Fact]
    public void CreateStations_WhenTotalChargersIsLessThanStationCount_ThrowsInvalidOperationException()
    {
        var file = CreateTempLocationsFile(5);

        var options = new StationFactoryOptions
        {
            TotalChargers = 4,
        };

        try
        {
            var factory = CreateFactory(options);

            var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateStations(file));
            Assert.Equal("Not enough chargers to give at least one to each station.", exception.Message);
        }
        finally
        {
            file.Delete();
        }
    }

    [Fact]
    public void CreateStations_AllStationsReceiveAtLeastOneCharger()
    {
        var file = CreateTempLocationsFile(10);

        var options = new StationFactoryOptions
        {
            TotalChargers = 25,
        };

        try
        {
            var factory = CreateFactory(options, seed: 123);
            var stations = factory.CreateStations(file);

            Assert.Equal(10, stations.Count);
            Assert.All(stations, station => Assert.NotEmpty(station.Chargers));
        }
        finally
        {
            file.Delete();
        }
    }

    [Fact]
    public void CreateStations_WithSameSeed_ProducesDeterministicSocketDistribution()
    {
        var file = CreateTempLocationsFile(8);

        var options = new StationFactoryOptions
        {
            TotalChargers = 40,
            UseDualChargingPoints = false,
            DualChargingPointProbability = 0.0,
        };

        try
        {
            var factory1 = CreateFactory(options, seed: 42);
            var factory2 = CreateFactory(options, seed: 42);

            var stations1 = factory1.CreateStations(file);
            var stations2 = factory2.CreateStations(file);

            Assert.Equal(stations1.Count, stations2.Count);
            Assert.Equal(
                stations1.Sum(s => s.Chargers.Count),
                stations2.Sum(s => s.Chargers.Count));

            var socketCounts1 = CountSockets(stations1);
            var socketCounts2 = CountSockets(stations2);

            Assert.Equal(socketCounts1, socketCounts2);
        }
        finally
        {
            file.Delete();
        }
    }
}