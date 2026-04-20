namespace Core.Vehicles;

using Core.Helper;
using Core.Vehicles.Configs;

/// <summary>
/// Provides the list of all supported EV models.
/// </summary>
public static class EVModels
{
    static EVModels()
    {
        var total = 0f;
        foreach (var model in Models)
            total += model.SpawnChance;

        if (MathF.Abs(total - 100f) > 0.01f)
            throw Log.Error(0, 0, new InvalidOperationException($"SpawnChance values must sum to 100, but sum is {total:F3}."));
    }

    /// <summary>
    /// Array of all 100 different EV models.
    /// </summary>
    public static readonly EVConfig[] Models =
    [
        new("Tesla Model Y",         8.241f, "Large Crossover",   new BatteryConfig(250, 75), 158),
        new("Tesla Model 3",         6.703f, "Sedan",             new BatteryConfig(175, 60), 135),
        new("Volkswagen ID.4",       6.584f, "Compact Crossover", new BatteryConfig(135, 77), 173),
        new("Skoda Enyaq iV",        5.638f, "Compact Crossover", new BatteryConfig(135, 77), 169),
        new("Volkswagen ID.3",       4.896f, "Hatchback",         new BatteryConfig(170, 58), 162),
        new("Audi Q4 e-tron",        3.582f, "Compact Crossover", new BatteryConfig(135, 77), 174),
        new("Peugeot 208",           2.829f, "City Car",          new BatteryConfig(100, 51), 152),
        new("Mercedes-Benz EQA",     2.565f, "Compact Crossover", new BatteryConfig(100, 70), 168),
        new("Skoda Elroq",           1.995f, "Compact Crossover", new BatteryConfig(185, 77), 171),
        new("Polestar 2",            1.975f, "Sedan",             new BatteryConfig(135, 78), 166),
        new("Hyundai Kona",          1.974f, "Compact Crossover", new BatteryConfig(102, 65), 168),
        new("Citroen C3",            1.885f, "City Car",          new BatteryConfig(100, 44), 172),
        new("Cupra Born",            1.794f, "Hatchback",         new BatteryConfig(170, 58), 164),
        new("Mercedes-Benz EQB",     1.750f, "Compact Crossover", new BatteryConfig(100, 70), 180),
        new("Volvo XC40",            1.680f, "Compact Crossover", new BatteryConfig(150, 79), 190),
        new("Peugeot 2008",          1.587f, "Compact Crossover", new BatteryConfig(100, 51), 169),
        new("BMW iX1",               1.572f, "Compact Crossover", new BatteryConfig(130, 64), 163),
        new("Volkswagen ID.5",       1.509f, "Large Crossover",   new BatteryConfig(135, 77), 169),
        new("Volkswagen ID.7",       1.464f, "Sedan",             new BatteryConfig(200, 77), 162),
        new("BMW iX3",               1.354f, "Large Crossover",   new BatteryConfig(230, 109), 190),
        new("Kia Niro",              1.323f, "Compact Crossover", new BatteryConfig(85,  65), 170),
        new("Volkswagen ID. Buzz",   1.316f, "MPV",               new BatteryConfig(170, 82), 210),
        new("Renault Megane E-Tech", 1.313f, "Hatchback",         new BatteryConfig(130, 60), 158),
        new("Mini 3-dørs",           1.291f, "City Car",          new BatteryConfig(50,  54), 149),
        new("Hyundai Ioniq 5",       1.263f, "Hatchback",         new BatteryConfig(233, 73), 174),
        new("Ford Mustang Mach-E",   1.217f, "Compact Crossover", new BatteryConfig(150, 91), 205),
        new("Opel Corsa",            1.214f, "City Car",          new BatteryConfig(100, 51), 152),
        new("Nissan Ariya",          1.197f, "Compact Crossover", new BatteryConfig(130, 87), 190),
        new("Xpeng G6",              1.155f, "Sedan",             new BatteryConfig(270, 87), 170),
        new("Renault Scenic",        1.132f, "Compact Crossover", new BatteryConfig(130, 87), 175),
        new("BMW i4",                1.056f, "Sedan",             new BatteryConfig(205, 84), 155),
        new("Kia EV3",               1.024f, "Hatchback",         new BatteryConfig(101, 58), 170),
        new("Volvo EX30",            1.018f, "City Car",          new BatteryConfig(153, 51), 155),
        new("BMW i5",                1.001f, "Sedan",             new BatteryConfig(205, 84), 169),
        new("Ford Explorer",         0.975f, "Large Crossover",   new BatteryConfig(185, 77), 173),
        new("Renault R5",            0.963f, "City Car",          new BatteryConfig(100, 40), 154),
        new("Volvo EX40",            0.934f, "Compact Crossover", new BatteryConfig(150, 79), 190),
        new("Peugeot 3008",          0.923f, "Compact Crossover", new BatteryConfig(160, 97), 190),
        new("Kia EV6",               0.852f, "Hatchback",         new BatteryConfig(233, 74), 174),
        new("Cupra Tavascan",        0.843f, "Compact Crossover", new BatteryConfig(165, 77), 173),
        new("Mercedes-Benz CLA",     0.800f, "Sedan",             new BatteryConfig(208, 85), 145),
        new("Audi Q6 e-tron",        0.731f, "Large Crossover",   new BatteryConfig(270, 95), 190),
        new("Mercedes-Benz EQE",     0.715f, "Sedan",             new BatteryConfig(170, 96), 173),
        new("Xpeng G9",              0.707f, "Large Crossover",   new BatteryConfig(215, 92), 200),
        new("MG 4",                  0.671f, "City Car",          new BatteryConfig(117, 51), 169),
        new("Peugeot 5008",          0.658f, "Large Crossover",   new BatteryConfig(160, 97), 195),
        new("Fiat 500",              0.626f, "City Car",          new BatteryConfig(85,  42), 159),
        new("BYD Dolphin",           0.561f, "City Car",          new BatteryConfig(60,  44), 173),
        new("Cupra Terramar",        0.514f, "Compact Crossover", new BatteryConfig(165, 77), 180),
        new("Mercedes-Benz GLA",     0.513f, "Compact Crossover", new BatteryConfig(100, 70), 180),
        new("MG ZS",                 0.497f, "Compact Crossover", new BatteryConfig(76,  51), 180),
        new("Navor E5",              0.450f, "City Car",          new BatteryConfig(80,  42), 160),
        new("Polestar 4",            0.434f, "Sedan",             new BatteryConfig(200, 94), 175),
        new("Mercedes-Benz EQE SUV", 0.425f, "Large Crossover",   new BatteryConfig(170, 91), 205),
        new("Audi A6 e-tron",        0.424f, "Sedan",             new BatteryConfig(270, 95), 158),
        new("Hyundai Ioniq 6",       0.378f, "Sedan",             new BatteryConfig(233, 77), 149),
        new("Citroen C4",            0.361f, "City Car",          new BatteryConfig(100, 51), 156),
        new("Hyundai Inster",        0.350f, "City Car",          new BatteryConfig(85,  42), 153),
        new("BMW iX",                0.342f, "Large Crossover",   new BatteryConfig(195, 95), 200),
        new("Mazda MX-30",           0.323f, "Hatchback",         new BatteryConfig(50,  30), 170),
        new("Opel Mokka",            0.320f, "City Car",          new BatteryConfig(100, 51), 162),
        new("Mazda 6e",              0.309f, "Hatchback",         new BatteryConfig(150, 69), 162),
        new("Opel Grandland",        0.278f, "Compact Crossover", new BatteryConfig(160, 97), 200),
        new("Ford Capri",            0.258f, "Compact Crossover", new BatteryConfig(185, 77), 168),
        new("BYD Seal",              0.249f, "Hatchback",         new BatteryConfig(150, 83), 172),
        new("Zeekr 7X",              0.247f, "Compact Crossover", new BatteryConfig(200, 96), 190),
        new("Porsche Macan",         0.244f, "Large Crossover",   new BatteryConfig(270, 95), 190),
        new("BMW iX2",               0.238f, "Large Crossover",   new BatteryConfig(130, 64), 165),
        new("BYD Seal U",            0.213f, "Compact Crossover", new BatteryConfig(100, 82), 190),
        new("Honda e:Ny1",           0.199f, "Hatchback",         new BatteryConfig(78,  68), 185),
        new("BYD Sealion 7",         0.198f, "Large Crossover",   new BatteryConfig(150, 91), 200),
        new("Volvo C40",             0.186f, "Large Crossover",   new BatteryConfig(150, 79), 195),
        new("Toyota Urban Cruiser",  0.179f, "Compact Crossover", new BatteryConfig(100, 61), 170),
        new("BYD Atto 3",            0.176f, "Compact Crossover", new BatteryConfig(88,  60), 190),
        new("Mini Countryman",       0.174f, "Compact Crossover", new BatteryConfig(75,  64), 163),
        new("Renault R4",            0.168f, "Compact Crossover", new BatteryConfig(100, 40), 163),
        new("Audi Q8 e-tron",        0.160f, "Large Crossover",   new BatteryConfig(170, 106), 240),
        new("Kia EV9",               0.148f, "Large Crossover",   new BatteryConfig(233, 96), 240),
        new("Volvo EC40",            0.144f, "Large Crossover",   new BatteryConfig(150, 79), 195),
        new("Opel Frontera",         0.135f, "Compact Crossover", new BatteryConfig(100, 54), 170),
        new("Fisker Ocean",          0.132f, "Compact Crossover", new BatteryConfig(200, 113), 200),
        new("MG S5",                 0.123f, "Compact Crossover", new BatteryConfig(150, 74), 171),
        new("MG EHS",                0.122f, "Large Crossover",   new BatteryConfig(76,  61), 200),
        new("Hongqi E-HS9",          0.115f, "Large Crossover",   new BatteryConfig(150, 112), 250),
        new("MG Marvel R",           0.112f, "Large Crossover",   new BatteryConfig(76,  70), 200),
        new("Volvo EX90",            0.100f, "Large Crossover",   new BatteryConfig(250, 102), 220),
        new("Mini 5-dørs",           0.097f, "City Car",          new BatteryConfig(50,  54), 155),
        new("Leapmotor B10",         0.083f, "City Car",          new BatteryConfig(80,  43), 150),
        new("BYD Dolphin Surf",      0.080f, "City Car",          new BatteryConfig(60,  38), 160),
        new("Mini Aceman",           0.079f, "City Car",          new BatteryConfig(75,  54), 167),
        new("Xpeng P7",              0.077f, "Sedan",             new BatteryConfig(215, 93), 167),
        new("Porsche Taycan",        0.076f, "Sedan",             new BatteryConfig(320, 97), 166),
        new("Subaru Solterra",       0.075f, "Compact Crossover", new BatteryConfig(150, 71), 200),
        new("Lexus RZ",              0.071f, "Large Crossover",   new BatteryConfig(150, 71), 200),
        new("BMW i7",                0.068f, "Sedan",             new BatteryConfig(195, 102), 210),
        new("Citroen ë-C4 X",        0.068f, "City Car",          new BatteryConfig(100, 51), 152),
        new("BYD Tang",              0.064f, "Large Crossover",   new BatteryConfig(110, 109), 250),
        new("Mercedes-Benz EQV",     0.060f, "MPV",               new BatteryConfig(110, 90), 240),
        new("Hongqi EHS7",           0.060f, "Compact Crossover", new BatteryConfig(130, 95), 200),
        new("BYD Atto 2",            0.057f, "Compact Crossover", new BatteryConfig(80,  45), 160)
    ];
}
