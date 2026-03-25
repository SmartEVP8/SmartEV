namespace Engine.test;

using System.Text.Json;
using Core.Charging;
using Core.Shared;
using Engine.StationFactory;

public class StationFactoryTest
{

    private static readonly EnergyPrices _energyPrices = new(new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "energy_prices.csv")));

    private static StationFactory CreateFactory(StationFactoryOptions? options = null, Random? random = null, FileInfo? file = null)
    {
        random ??= new Random();
        if (file is null)
        {
            throw new ArgumentNullException(nameof(file), "A FileInfo must be provided to create a StationFactory.");
        }

        return new StationFactory(options ?? new StationFactoryOptions(), random, _energyPrices, file);
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

    private static Dictionary<Socket, int> CountSockets(Dictionary<ushort, Station> stations)
    {
        var counts = new Dictionary<Socket, int>();

        foreach (var station in stations)
        {
            foreach (var charger in station.Value.Chargers)
            {
                foreach (var socket in charger.GetSockets())
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

        Assert.Throws<ArgumentOutOfRangeException>(() => new StationFactory(options, new Random(), _energyPrices, CreateTempLocationsFile(1)));
    }

    [Fact]
    public void Constructor_WhenDualChargingPointProbabilityIsInvalid_ThrowsArgumentOutOfRangeException()
    {
        var options = new StationFactoryOptions
        {
            DualChargingPointProbability = 1.5,
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => new StationFactory(options, new Random(), _energyPrices, CreateTempLocationsFile(1)));
    }

    [Fact]
    public void Constructor_WhenSocketProbabilitiesAreEmpty_ThrowsArgumentException()
    {
        var options = new StationFactoryOptions
        {
            SocketProbabilities = [],
        };

        Assert.Throws<ArgumentException>(() => new StationFactory(options, new Random(), _energyPrices, CreateTempLocationsFile(1)));
    }

    [Fact]
    public void Constructor_WhenSocketProbabilitiesDoNotSumToOne_ThrowsArgumentException()
    {
        var options = new StationFactoryOptions
        {
            SocketProbabilities = new Dictionary<Socket, double>
            {
                { Socket.CHADEMO, 0.5 },
                { Socket.CCS2, 0.2 },
            },
        };

        Assert.Throws<ArgumentException>(() => new StationFactory(options, new Random(), _energyPrices, CreateTempLocationsFile(1)));
    }

    [Fact]
    public void CreateStations_EmptyFile_ReturnsEmptyList()
    {
        var file = CreateTempLocationsFile();

        try
        {
            var factory = CreateFactory(file: file);
            Assert.Throws<InvalidOperationException>(() => factory.CreateStations());
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
            var factory = CreateFactory(file: file);
            var stations = factory.CreateStations();

            var station = Assert.Single(stations);
            Assert.Equal((ushort)1, station.Key);
            Assert.Equal("Only Station", station.Value.Name);
            Assert.Equal("Only Address", station.Value.Address);
            Assert.Equal(57.0, station.Value.Position.Latitude);
            Assert.Equal(9.0, station.Value.Position.Longitude);
            Assert.NotEmpty(station.Value.Chargers);
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
            var factory = CreateFactory(file: file);
            var stations = factory.CreateStations();

            Assert.Equal(3, stations.Count);
            Assert.Equal("First", stations[1].Name);
            Assert.Equal("Second", stations[2].Name);
            Assert.Equal("Third", stations[3].Name);
            Assert.Equal((ushort)1, stations[1].Id);
            Assert.Equal((ushort)2, stations[2].Id);
            Assert.Equal((ushort)3, stations[3].Id);
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
        var factory = CreateFactory(file: file);

        Assert.Throws<FileNotFoundException>(() => factory.CreateStations());
    }

    [Fact]
    public void CreateStations_WhenJsonIsInvalid_ThrowsJsonException()
    {
        var file = CreateTempFileWithRawContent("{ invalid json }");

        try
        {
            var factory = CreateFactory(file: file);

            Assert.Throws<JsonException>(() => factory.CreateStations());
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
            var factory = CreateFactory(options, file: file);

            var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateStations());
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
            var factory = CreateFactory(options, new Random(), file: file);
            var stations = factory.CreateStations();

            Assert.Equal(10, stations.Count);
            Assert.All(stations, station => Assert.NotEmpty(station.Value.Chargers));
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
            const int seed = 0;
            var factory1 = CreateFactory(options, new Random(seed), file: file);
            var factory2 = CreateFactory(options, new Random(seed), file: file);

            var stations1 = factory1.CreateStations();
            var stations2 = factory2.CreateStations();

            Assert.Equal(stations1.Count, stations2.Count);
            Assert.Equal(
                stations1.Sum(s => s.Value.Chargers.Count),
                stations2.Sum(s => s.Value.Chargers.Count));

            var socketCounts1 = CountSockets(stations1);
            var socketCounts2 = CountSockets(stations2);

            Assert.Equal(socketCounts1, socketCounts2);
        }
        finally
        {
            file.Delete();
        }
    }

    [Fact]
    public void CreateStations_WithMultiSocketChargers_ProducesSomeChargersWithMultipleSockets()
    {
        var file = CreateTempLocationsFile(10);

        var options = new StationFactoryOptions
        {
            TotalChargers = 50,
            AllowMultiSocketChargers = true,
            MultiSocketChargerProbability = 0.5,
        };

        try
        {
            var factory = CreateFactory(options, new Random(), file: file);
            var stations = factory.CreateStations();

            Assert.Equal(10, stations.Count);
            Assert.All(stations, station => Assert.NotEmpty(station.Value.Chargers));

            var hasMultiSocketCharger = stations
                .SelectMany(station => station.Value.Chargers)
                .Any(charger => charger.GetSockets().Length > 1);

            Assert.True(hasMultiSocketCharger, "Expected at least one charger to have multiple sockets.");
        }
        finally
        {
            file.Delete();
        }
    }

    [Fact]
    public void CreateStations_WhenMultiSocketProbabilityIsOne_AllChargersBecomeMultiSocket()
    {
        var file = CreateTempLocationsFile(6);

        var options = new StationFactoryOptions
        {
            TotalChargers = 18,
            AllowMultiSocketChargers = true,
            MultiSocketChargerProbability = 1.0,
            UseDualChargingPoints = false,
            DualChargingPointProbability = 0.0,
            SocketProbabilities = new Dictionary<Socket, double>
            {
                { Socket.CHADEMO, 0.5 },
                { Socket.CCS2, 0.5 },
            },
        };

        try
        {
            var factory = CreateFactory(options, new Random(), file: file);
            var stations = factory.CreateStations();

            var chargers = stations.SelectMany(station => station.Value.Chargers).ToList();

            Assert.NotEmpty(chargers);
            Assert.All(chargers, charger =>
                Assert.True(
                    charger.GetSockets().Length > 1,
                    "Expected every charger to have more than one socket when multi-socket probability is 1.0."));
        }
        finally
        {
            file.Delete();
        }
    }

    [Fact]
    public void CreateStations_WhenOnlyOneSocketTypeExists_DoesNotCreateMultiSocketChargers()
    {
        var file = CreateTempLocationsFile(6);

        var options = new StationFactoryOptions
        {
            TotalChargers = 18,
            AllowMultiSocketChargers = true,
            MultiSocketChargerProbability = 1.0,
            UseDualChargingPoints = false,
            DualChargingPointProbability = 0.0,
            SocketProbabilities = new Dictionary<Socket, double>
            {
                { Socket.CCS2, 1.0 },
            },
        };

        try
        {
            var factory = CreateFactory(options, new Random(), file);
            var stations = factory.CreateStations();

            var chargers = stations.SelectMany(station => station.Value.Chargers).ToList();

            Assert.NotEmpty(chargers);
            Assert.All(chargers, charger =>
                Assert.Single(charger.GetSockets()));
        }
        finally
        {
            file.Delete();
        }
    }

    private static readonly EnergyPrices _energyPrices = new(new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "energy_prices.csv")));

    private static StationFactory CreateFactory(StationFactoryOptions? options = null, Random? random = null)
    {
        random ??= new Random();
        return new StationFactory(options ?? new StationFactoryOptions(), random, _energyPrices);
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

    private static Dictionary<Socket, int> CountSockets(Dictionary<ushort, Station> stations)
    {
        var counts = new Dictionary<Socket, int>();

        foreach (var station in stations)
        {
            foreach (var charger in station.Value.Chargers)
            {
                foreach (var socket in charger.GetSockets())
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
}
