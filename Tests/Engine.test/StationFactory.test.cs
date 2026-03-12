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

    private static StationFactory CreateFactory(StationFactoryOptions? options = null)
    {
        return new StationFactory(options ?? new StationFactoryOptions());
    }

    private static string CreateTempLocationsFile(params object[] locations)
    {
        var json = JsonSerializer.Serialize(locations);
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(filePath, json);
        return filePath;
    }

    private static string CreateTempLocationsFile(int count)
    {
        var locations = Enumerable.Range(1, count)
            .Select(i => new
            {
                Name = $"Station {i}",
                Address = $"Address {i}",
                Latitude = 56.0 + (i * 0.001),
                Longitude = 10.0 + (i * 0.001),
            })
            .ToArray();

        return CreateTempLocationsFile(locations);
    }

    private static string CreateTempFileWithRawContent(string content)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(filePath, content);
        return filePath;
    }

    private static Dictionary<Socket, int> CountSockets(IEnumerable<Station> stations)
    {
        var counts = new Dictionary<Socket, int>();

        var chargingPointField = typeof(Charger).GetField(
            "_chargingpoint",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

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
    public void CreateStations_EmptyFile_ReturnsEmptyList()
    {
        var filePath = CreateTempLocationsFile();

        try
        {
            var factory = CreateFactory();

            var stations = factory.CreateStations(filePath);

            Assert.Empty(stations);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CreateStations_SingleLocation_ReturnsSingleStation()
    {
        var filePath = CreateTempLocationsFile(
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

            var stations = factory.CreateStations(filePath);

            var station = Assert.Single(stations);
            Assert.Equal("Only Station", station.Name);
            Assert.Equal("Only Address", station.Address);
            Assert.Equal(57.0, station.Position.Latitude);
            Assert.Equal(9.0, station.Position.Longitude);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CreateStations_ReturnsOneStationPerLocation()
    {
        var filePath = CreateTempLocationsFile(
            new
            {
                Name = "Station A",
                Address = "Address A",
                Latitude = 57.0,
                Longitude = 9.0,
            },
            new
            {
                Name = "Station B",
                Address = "Address B",
                Latitude = 57.1,
                Longitude = 9.1,
            },
            new
            {
                Name = "Station C",
                Address = "Address C",
                Latitude = 57.2,
                Longitude = 9.2,
            });

        try
        {
            var factory = CreateFactory();

            var stations = factory.CreateStations(filePath);

            Assert.Equal(3, stations.Count);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CreateStations_AssignsUniqueStationIds()
    {
        var filePath = CreateTempLocationsFile(
            new
            {
                Name = "Station A",
                Address = "Address A",
                Latitude = 57.0,
                Longitude = 9.0,
            },
            new
            {
                Name = "Station B",
                Address = "Address B",
                Latitude = 57.1,
                Longitude = 9.1,
            },
            new
            {
                Name = "Station C",
                Address = "Address C",
                Latitude = 57.2,
                Longitude = 9.2,
            });

        try
        {
            var factory = CreateFactory();

            var stations = factory.CreateStations(filePath);

            var ids = stations.Select(s => s.Id).ToList();
            var uniqueIds = ids.Distinct().ToList();

            Assert.Equal(ids.Count, uniqueIds.Count);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CreateStations_MapsLocationDataCorrectly()
    {
        var filePath = CreateTempLocationsFile(
            new
            {
                Name = "Test Station",
                Address = "Test Address",
                Latitude = 56.123,
                Longitude = 10.456,
            });

        try
        {
            var factory = CreateFactory();

            var stations = factory.CreateStations(filePath);
            var station = Assert.Single(stations);

            Assert.Equal("Test Station", station.Name);
            Assert.Equal("Test Address", station.Address);
            Assert.Equal(10.456, station.Position.Longitude);
            Assert.Equal(56.123, station.Position.Latitude);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CreateStations_PreservesInputOrder()
    {
        var filePath = CreateTempLocationsFile(
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

            var stations = factory.CreateStations(filePath);

            Assert.Equal(3, stations.Count);
            Assert.Equal("First", stations[0].Name);
            Assert.Equal("Second", stations[1].Name);
            Assert.Equal("Third", stations[2].Name);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CreateStations_EachStation_GetsAtLeastOneCharger()
    {
        var filePath = CreateTempLocationsFile(
            new
            {
                Name = "Station A",
                Address = "Address A",
                Latitude = 57.0,
                Longitude = 9.0,
            },
            new
            {
                Name = "Station B",
                Address = "Address B",
                Latitude = 57.1,
                Longitude = 9.1,
            },
            new
            {
                Name = "Station C",
                Address = "Address C",
                Latitude = 57.2,
                Longitude = 9.2,
            });

        try
        {
            var factory = CreateFactory();

            var stations = factory.CreateStations(filePath);

            Assert.All(stations, station => Assert.NotEmpty(station.Chargers));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CreateStations_DuplicateNamesAndAddresses_StillCreateDistinctStations()
    {
        var filePath = CreateTempLocationsFile(
            new
            {
                Name = "Same Name",
                Address = "Same Address",
                Latitude = 57.0,
                Longitude = 9.0,
            },
            new
            {
                Name = "Same Name",
                Address = "Same Address",
                Latitude = 57.1,
                Longitude = 9.1,
            });

        try
        {
            var factory = CreateFactory();

            var stations = factory.CreateStations(filePath);

            Assert.Equal(2, stations.Count);
            Assert.NotEqual(stations[0].Id, stations[1].Id);
            Assert.Equal("Same Name", stations[0].Name);
            Assert.Equal("Same Name", stations[1].Name);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CreateStations_MapsNegativeAndZeroCoordinatesCorrectly()
    {
        var filePath = CreateTempLocationsFile(
            new
            {
                Name = "Edge Station",
                Address = "Edge Address",
                Latitude = 0.0,
                Longitude = -3.5,
            });

        try
        {
            var factory = CreateFactory();

            var stations = factory.CreateStations(filePath);
            var station = Assert.Single(stations);

            Assert.Equal(0.0, station.Position.Latitude);
            Assert.Equal(-3.5, station.Position.Longitude);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CreateStations_AllStationsHaveRequiredFields()
    {
        var filePath = CreateTempLocationsFile(
            new
            {
                Name = "Station A",
                Address = "Address A",
                Latitude = 57.0,
                Longitude = 9.0,
            },
            new
            {
                Name = "Station B",
                Address = "Address B",
                Latitude = 57.1,
                Longitude = 9.1,
            });

        try
        {
            var factory = CreateFactory();

            var stations = factory.CreateStations(filePath);

            Assert.All(stations, station =>
            {
                Assert.False(string.IsNullOrWhiteSpace(station.Name));
                Assert.False(string.IsNullOrWhiteSpace(station.Address));
                Assert.NotEmpty(station.Chargers);
            });
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CreateStations_WithSameSeed_ProducesDeterministicResults()
    {
        var filePath = CreateTempLocationsFile(
            new
            {
                Name = "Station A",
                Address = "Address A",
                Latitude = 57.0,
                Longitude = 9.0,
            },
            new
            {
                Name = "Station B",
                Address = "Address B",
                Latitude = 57.1,
                Longitude = 9.1,
            });

        var options = new StationFactoryOptions
        {
            Seed = 42,
            UseDualChargingPoints = true,
            DualChargingPointProbability = 0.3,
        };

        try
        {
            var factory1 = CreateFactory(options);
            var factory2 = CreateFactory(options);

            var stations1 = factory1.CreateStations(filePath);
            var stations2 = factory2.CreateStations(filePath);

            Assert.Equal(stations1.Count, stations2.Count);

            for (int i = 0; i < stations1.Count; i++)
            {
                Assert.Equal(stations1[i].Id, stations2[i].Id);
                Assert.Equal(stations1[i].Name, stations2[i].Name);
                Assert.Equal(stations1[i].Address, stations2[i].Address);
                Assert.Equal(stations1[i].Position.Longitude, stations2[i].Position.Longitude);
                Assert.Equal(stations1[i].Position.Latitude, stations2[i].Position.Latitude);
                Assert.Equal(stations1[i].Chargers.Count, stations2[i].Chargers.Count);
            }
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CreateStations_WhenDualChargingPointsDisabled_StillCreatesChargers()
    {
        var filePath = CreateTempLocationsFile(
            new
            {
                Name = "Station A",
                Address = "Address A",
                Latitude = 57.0,
                Longitude = 9.0,
            });

        var options = new StationFactoryOptions
        {
            Seed = 42,
            UseDualChargingPoints = false,
            DualChargingPointProbability = 1.0,
        };

        try
        {
            var factory = CreateFactory(options);

            var stations = factory.CreateStations(filePath);
            var station = Assert.Single(stations);

            Assert.NotEmpty(station.Chargers);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CreateStations_DoesNotExceedConfiguredSocketCounts()
    {
        var filePath = CreateTempLocationsFile(100);

        var options = new StationFactoryOptions
        {
            Seed = 42,
            UseDualChargingPoints = false,
            DualChargingPointProbability = 0.0,
        };

        try
        {
            var factory = CreateFactory(options);

            var stations = factory.CreateStations(filePath);
            var socketCounts = CountSockets(stations);

            Assert.True(socketCounts.GetValueOrDefault(Socket.CHADEMO, 0) <= 167);
            Assert.True(socketCounts.GetValueOrDefault(Socket.Type2SocketOnly, 0) <= 4514);
            Assert.True(socketCounts.GetValueOrDefault(Socket.NACS, 0) <= 14);
            Assert.True(socketCounts.GetValueOrDefault(Socket.CCS, 0) <= 1472);
            Assert.True(socketCounts.GetValueOrDefault(Socket.Type2Tethered, 0) <= 1044);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CreateStations_UsesExactlyConfiguredSocketCounts()
    {
        var filePath = CreateTempLocationsFile(100);

        var options = new StationFactoryOptions
        {
            Seed = 42,
            UseDualChargingPoints = false,
            DualChargingPointProbability = 0.0,
        };

        try
        {
            var factory = CreateFactory(options);

            var stations = factory.CreateStations(filePath);
            var socketCounts = CountSockets(stations);

            Assert.Equal(167, socketCounts.GetValueOrDefault(Socket.CHADEMO, 0));
            Assert.Equal(4514, socketCounts.GetValueOrDefault(Socket.Type2SocketOnly, 0));
            Assert.Equal(14, socketCounts.GetValueOrDefault(Socket.NACS, 0));
            Assert.Equal(1472, socketCounts.GetValueOrDefault(Socket.CCS, 0));
            Assert.Equal(1044, socketCounts.GetValueOrDefault(Socket.Type2Tethered, 0));

            var totalChargers = stations.Sum(station => station.Chargers.Count);
            Assert.Equal(7211, totalChargers);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CreateStations_WithMoreLocationsThanChargers_ThrowsInvalidOperationException()
    {
        var filePath = CreateTempLocationsFile(7212);

        try
        {
            var factory = CreateFactory();

            var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateStations(filePath));
            Assert.Equal("Not enough chargers to give at least one to each station.", exception.Message);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void CreateStations_MissingFile_ThrowsFileNotFoundException()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        var factory = CreateFactory();

        Assert.Throws<FileNotFoundException>(() => factory.CreateStations(filePath));
    }

    [Fact]
    public void CreateStations_InvalidJson_ThrowsJsonException()
    {
        var filePath = CreateTempFileWithRawContent("{ invalid json }");

        try
        {
            var factory = CreateFactory();

            Assert.Throws<JsonException>(() => factory.CreateStations(filePath));
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}