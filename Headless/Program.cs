// ChargingSimulator.cs
// Standalone runnable demo of the ChargingIntegrator.
// Compile: dotnet-script ChargingSimulator.cs
//      or: create a console project, drop this file in, run dotnet run

using System;
using System.Collections.Generic;
using System.Diagnostics;

// ─────────────────────────────────────────────────────────────
// CHARGING CURVE
// ─────────────────────────────────────────────────────────────

/// <summary>
/// Any charging curve implements this. Swap in a different curve
/// without touching the integrator.
/// </summary>
public interface IChargingCurve
{
    /// <summary>
    /// Returns [0,1] — how much of its allocated power the car can
    /// actually absorb at this SoC. 1.0 = full power, 0.5 = half, etc.
    /// </summary>
    double PowerFraction(double soc);
}

/// <summary>
/// Default curve with three regions:
///   soc &lt; 0.1  → ramp up   (0.5 → 1.0)
///   soc &lt; 0.8  → full power (1.0)
///   soc >= 0.8 → taper off  (1.0 → 0.2)
/// </summary>
public class DefaultChargingCurve : IChargingCurve
{
    public double PowerFraction(double soc)
    {
        if (soc < 0.1) return 0.5 + (5.0 * soc);
        if (soc < 0.8) return 1.0;
        return Math.Max(0.2, 1.0 - 3.0 * (soc - 0.8));
    }
}

/// <summary>
/// A curve that tapers more aggressively — starts tapering at 70% SoC
/// and drops to 0.1 at full charge. Models older or smaller battery packs.
/// </summary>
public class AggressiveTaperCurve : IChargingCurve
{
    public double PowerFraction(double soc)
    {
        if (soc < 0.1) return 0.4 + (4.0 * soc);
        if (soc < 0.7) return 1.0;
        return Math.Max(0.1, 1.0 - (3.0 * (soc - 0.7)));
    }
}


// ─────────────────────────────────────────────────────────────
// DATA
// ─────────────────────────────────────────────────────────────

/// <summary>
/// A car currently connected to a connector with everything needed
/// to plan or update its charging session.
/// </summary>
public record ConnectedCar(
    int CarId,
    double CurrentSoC,    // SoC right now, at this moment
    double TargetSoC,     // SoC the car wants to reach
    double CapacityKWh,   // battery size
    IChargingCurve Curve);        // this car's own charging curve

/// <summary>
/// Returned by all integrator methods.
///
/// SocA / SocB              — SoC each car reached at the end of the run.
/// FinishTimeA/B            — simulation timestamp (seconds) when that car hit TargetSoC.
///                            null if the car did not finish during this run.
/// EnergyDeliveredKWhA/B    — exact energy delivered to each car during this run.
/// DurationSeconds          — wall time covered by this integration run.
/// Utilization(maxKW)       — fraction of maxKW actually delivered over the run. 1.0 = full power the whole time.
/// </summary>
public record IntegrationResult(double SocA,
                                double SocB,
                                uint? FinishTimeA,
                                uint? FinishTimeB,
                                double EnergyDeliveredKWhA,
                                double EnergyDeliveredKWhB,
                                double DurationSeconds)
{
    public double TotalEnergyKWh => EnergyDeliveredKWhA + EnergyDeliveredKWhB;

    /// <summary>
    /// Fraction of maxKW actually delivered averaged over the run duration.
    /// Accumulate TotalEnergyKWh and DurationSeconds across multiple results
    /// to get utilization over a longer period than a single sub-session.
    /// </summary>
    public double Utilization(double maxKW)
    {
        if (DurationSeconds <= 0) return 0.0;
        double maxPossibleKWh = maxKW * (DurationSeconds / 3600.0);
        return TotalEnergyKWh / maxPossibleKWh;
    }
}


// ─────────────────────────────────────────────────────────────
// POWER ALLOCATION
// ─────────────────────────────────────────────────────────────

/// <summary>
/// At a single moment in time, computes how many kW each car receives.
///
/// Rules:
///   1. Nominal split is 50/50
///   2. If a car cannot absorb its full share due to taper,
///      the surplus goes to the other car
///   3. Neither car ever exceeds maxKW * PowerFraction(soc) — hard ceiling
///
/// Example:
///   maxKW=100, Car A soc=0.20, Car B soc=0.85
///   Nominal: 50 kW each
///   Car B PowerFraction(0.85)=0.55 → absorbs 27.5 kW → surplus=22.5 kW
///   Car A gets 50+22.5=72.5 kW  (within ceiling 100*1.0=100 kW)
///   Result: A=72.5 kW, B=27.5 kW, total=100 kW ✓
/// </summary>
public static class PowerAllocator
{
    public static (double PowerA, double PowerB) Allocate(
        double maxKW,
        double socA,
        IChargingCurve curveA,
        double? socB,      // null = no second car
        IChargingCurve? curveB)
    {
        if (socB is null)
            return (maxKW * curveA.PowerFraction(socA), 0.0);

        double nominal = maxKW / 2.0;
        double ceilA = nominal * curveA.PowerFraction(socA);
        double ceilB = nominal * curveB!.PowerFraction(socB.Value);
        double surplusA = nominal - ceilA;
        double surplusB = nominal - ceilB;

        double finalA = Math.Min(ceilA + Math.Max(0, surplusB), maxKW * curveA.PowerFraction(socA));
        double finalB = Math.Min(ceilB + Math.Max(0, surplusA), maxKW * curveB!.PowerFraction(socB.Value));

        return (finalA, finalB);
    }
}


// ─────────────────────────────────────────────────────────────
// INTEGRATOR
// ─────────────────────────────────────────────────────────────

/// <summary>
/// Integrates charging forward in time using small fixed steps.
/// All timestamps are in seconds to match EventScheduler (uint).
///
/// Two use cases:
///
///   PlanSession — called when charger state changes (connect/disconnect).
///                 Runs until both cars reach TargetSoC.
///                 Use FinishTimeA/B from result to schedule departure events.
///
///   UpdateSoC   — called just BEFORE replanning when a state change interrupts
///                 an ongoing session. Runs for exactly elapsedSeconds.
///                 Update car batteries with SocA/SocB before calling PlanSession.
/// </summary>
public class ChargingIntegrator
{
    private readonly uint _stepSeconds;

    public ChargingIntegrator(uint stepSeconds = 30)
    {
        _stepSeconds = stepSeconds;
    }

    // ── Plan: two cars ───────────────────────────────────────

    public IntegrationResult PlanSession(
        uint simNow, double maxKW, ConnectedCar carA, ConnectedCar carB)
        => Integrate(simNow, maxKW, carA, carB, runUntilSeconds: null);

    // ── Plan: one car ────────────────────────────────────────

    public IntegrationResult PlanSession(
        uint simNow, double maxKW, ConnectedCar carA)
        => Integrate(simNow, maxKW, carA, carB: null, runUntilSeconds: null);

    // ── Update: two cars ─────────────────────────────────────

    public IntegrationResult UpdateSoC(
        uint simNow, double maxKW, ConnectedCar carA, ConnectedCar carB, uint elapsedSeconds)
        => Integrate(simNow, maxKW, carA, carB, runUntilSeconds: elapsedSeconds);

    // ── Update: one car ──────────────────────────────────────

    public IntegrationResult UpdateSoC(
        uint simNow, double maxKW, ConnectedCar carA, uint elapsedSeconds)
        => Integrate(simNow, maxKW, carA, carB: null, runUntilSeconds: elapsedSeconds);


    // ─────────────────────────────────────────────────────────
    // CORE LOOP
    // ─────────────────────────────────────────────────────────

    private IntegrationResult Integrate(
        uint simNow,
        double maxKW,
        ConnectedCar carA,
        ConnectedCar? carB,
        uint? runUntilSeconds)
    {
        bool dual = carB is not null;
        double socA = carA.CurrentSoC;
        double socB = carB?.CurrentSoC ?? 0.0;
        double? finishA = null;
        double? finishB = null;
        double energyA = 0.0;
        double energyB = 0.0;
        uint t = 0;

        while (true)
        {
            bool aFinished = finishA.HasValue || socA >= carA.TargetSoC;
            bool bFinished = !dual || finishB.HasValue || socB >= carB!.TargetSoC;

            if (runUntilSeconds.HasValue && t >= runUntilSeconds.Value) break;
            if (!runUntilSeconds.HasValue && aFinished && bFinished) break;

            uint step = runUntilSeconds.HasValue
                                   ? Math.Min(_stepSeconds, runUntilSeconds.Value - t)
                                   : _stepSeconds;
            double stepHours = step / 3600.0;

            // Finished cars pass their TargetSoC so the other car gets freed power
            var (powerA, powerB) = PowerAllocator.Allocate(
                maxKW,
                aFinished ? carA.TargetSoC : socA,
                carA.Curve,
                dual ? (bFinished ? carB!.TargetSoC : socB) : null,
                carB?.Curve);

            if (!aFinished)
            {
                double deltaEnergy = powerA * stepHours;
                energyA += deltaEnergy;
                socA += deltaEnergy / carA.CapacityKWh;
                if (socA >= carA.TargetSoC) { socA = carA.TargetSoC; finishA = simNow + t; }
            }

            if (dual && !bFinished)
            {
                double deltaEnergy = powerB * stepHours;
                energyB += deltaEnergy;
                socB += deltaEnergy / carB!.CapacityKWh;
                if (socB >= carB.TargetSoC) { socB = carB.TargetSoC; finishB = simNow + t; }
            }

            t += step;
        }

        return new IntegrationResult(
            SocA: socA,
            SocB: socB,
            FinishTimeA: finishA.HasValue ? (uint)finishA.Value : null,
            FinishTimeB: finishB.HasValue ? (uint)finishB.Value : null,
            EnergyDeliveredKWhA: energyA,
            EnergyDeliveredKWhB: energyB,
            DurationSeconds: t);
    }
}


// ─────────────────────────────────────────────────────────────
// DEMO PROGRAM
// ─────────────────────────────────────────────────────────────

public static class Program
{
    private static readonly IChargingCurve DefaultCurve = new DefaultChargingCurve();
    private static readonly ChargingIntegrator Integrator = new ChargingIntegrator(stepSeconds: 30);

    public static void Main()
    {
        const int Runs = 10_000;

        // Print one full run first so output is visible
        ScenarioA_SingleCar();
        ScenarioB_TwoCarsSimultaneous();
        ScenarioC_SecondCarArivesMidSession();
        ScenarioD_OneCarDepartsMidSession();
        ScenarioE_TenWaves();

        // Benchmark each scenario
        Console.WriteLine();
        Separator($"BENCHMARK — {Runs:N0} runs per scenario");

        Benchmark("Scenario A — Single car", Runs, ScenarioA_SingleCar);
        Benchmark("Scenario B — Two cars simultaneous", Runs, ScenarioB_TwoCarsSimultaneous);
        Benchmark("Scenario C — Second car arrives mid-session", Runs, ScenarioC_SecondCarArivesMidSession);
        Benchmark("Scenario D — One car departs mid-session", Runs, ScenarioD_OneCarDepartsMidSession);
        Benchmark("Scenario E — 10 waves of arrivals/departures", Runs, ScenarioE_TenWaves);
    }

    static void Benchmark(string label, int runs, Action scenario)
    {
        // Suppress console output during benchmark runs
        var originalOut = Console.Out;
        Console.SetOut(System.IO.TextWriter.Null);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < runs; i++)
            scenario();
        sw.Stop();

        Console.SetOut(originalOut);

        double totalMs = sw.Elapsed.TotalMilliseconds;
        double avgUs = totalMs / runs * 1000.0;   // microseconds per run
        Console.WriteLine($"  {label,-42} avg: {avgUs,8:F2} µs  |  total: {totalMs,8:F1} ms");
    }

    // ── A: Single car, charges alone ─────────────────────────

    static void ScenarioA_SingleCar()
    {
        Separator("SCENARIO A: Single car charges alone");

        uint simNow = 0;
        double maxKW = 100.0;
        var car = new ConnectedCar(CarId: 1, CurrentSoC: 0.20, TargetSoC: 0.80, CapacityKWh: 75.0, Curve: DefaultCurve);

        PrintCharger(maxKW);
        PrintCar("Car 1", car);

        var result = Integrator.PlanSession(simNow, maxKW, car);

        Console.WriteLine();
        Console.WriteLine($"  Car 1 finishes at : {FmtTime(result.FinishTimeA!.Value)}");
        Console.WriteLine($"  Car 1 final SoC   : {FmtSoC(result.SocA)}");

        Console.WriteLine($"  Energy delivered  : {result.EnergyDeliveredKWhA:F1} kWh");
        Console.WriteLine($"  Avg actual power  : {result.EnergyDeliveredKWhA / (result.FinishTimeA!.Value / 3600.0):F1} kW  (max {maxKW} kW)");
        Console.WriteLine($"  Charger utiliz.   : {result.Utilization(maxKW) * 100:F1}%");
        Console.WriteLine();
        Console.WriteLine("  NOTE: Utilization < 100% because the car ramps up slowly");
        Console.WriteLine("  below 10% SoC and tapers off above 80% SoC.");
    }

    // ── B: Two cars connect simultaneously ───────────────────

    static void ScenarioB_TwoCarsSimultaneous()
    {
        Separator("SCENARIO B: Two cars connect simultaneously");

        uint simNow = 0;
        double maxKW = 100.0;
        var carA = new ConnectedCar(CarId: 1, CurrentSoC: 0.20, TargetSoC: 0.80, CapacityKWh: 75.0, Curve: DefaultCurve);
        var carB = new ConnectedCar(CarId: 2, CurrentSoC: 0.75, TargetSoC: 0.90, CapacityKWh: 60.0, Curve: DefaultCurve);

        PrintCharger(maxKW);
        PrintCar("Car 1", carA);
        PrintCar("Car 2", carB);

        var result = Integrator.PlanSession(simNow, maxKW, carA, carB);

        Console.WriteLine();
        Console.WriteLine($"  Car 1 finishes at : {FmtTime(result.FinishTimeA!.Value)}");
        Console.WriteLine($"  Car 2 finishes at : {FmtTime(result.FinishTimeB!.Value)}");

        uint lastFinish = Math.Max(result.FinishTimeA!.Value, result.FinishTimeB!.Value);
        Console.WriteLine($"  Total energy      : {result.TotalEnergyKWh:F1} kWh");
        Console.WriteLine($"  Charger utiliz.   : {result.Utilization(maxKW) * 100:F1}%");
        Console.WriteLine();
        Console.WriteLine("  NOTE: Car 2 is near taper so Car 1 gets more than 50 kW early.");
        Console.WriteLine("  Car 2 finishes first, then Car 1 gets full 100 kW until done.");
    }

    // ── C: Second car arrives while first is charging ────────

    static void ScenarioC_SecondCarArivesMidSession()
    {
        Separator("SCENARIO C: Car 2 arrives while Car 1 is already charging");

        uint simNow = 0;
        double maxKW = 100.0;
        var carA = new ConnectedCar(CarId: 1, CurrentSoC: 0.10, TargetSoC: 0.80, CapacityKWh: 75.0, Curve: DefaultCurve);

        PrintCharger(maxKW);
        PrintCar("Car 1 connects at t=0", carA);

        // ── Phase 1: Car 1 charges alone ──
        // Car 2 arrives at t=1800s (30 min)
        uint connectTime = 1800;

        var phase1 = Integrator.UpdateSoC(simNow, maxKW, carA, elapsedSeconds: connectTime);
        Console.WriteLine();
        Console.WriteLine($"  Car 2 connects at : {FmtTime(connectTime)}");
        Console.WriteLine($"  Car 1 SoC at that point: {FmtSoC(phase1.SocA)}  (was {FmtSoC(carA.CurrentSoC)})");

        // ── Phase 2: Both cars charge together ──
        var carAUpdated = carA with { CurrentSoC = phase1.SocA };
        var carB = new ConnectedCar(CarId: 2, CurrentSoC: 0.30, TargetSoC: 0.80, CapacityKWh: 60.0, Curve: DefaultCurve);

        PrintCar("Car 2 connects now", carB);

        var phase2 = Integrator.PlanSession(connectTime, maxKW, carAUpdated, carB);

        Console.WriteLine();
        Console.WriteLine($"  Car 1 finishes at : {FmtTime(phase2.FinishTimeA!.Value)}");
        Console.WriteLine($"  Car 2 finishes at : {FmtTime(phase2.FinishTimeB!.Value)}");
        Console.WriteLine();
        Console.WriteLine("  NOTE: Car 1 had a head start charging alone at full 100 kW.");
        Console.WriteLine("  Once Car 2 arrives the power splits, so Car 1 slows down.");
    }

    // ── D: One car departs, other gets full power ─────────────

    static void ScenarioD_OneCarDepartsMidSession()
    {
        Separator("SCENARIO D: Car 2 departs mid-session, Car 1 gets full power");

        uint simNow = 0;
        double maxKW = 100.0;
        var carA = new ConnectedCar(CarId: 1, CurrentSoC: 0.20, TargetSoC: 0.80, CapacityKWh: 75.0, Curve: DefaultCurve);
        var carB = new ConnectedCar(CarId: 2, CurrentSoC: 0.50, TargetSoC: 0.80, CapacityKWh: 60.0, Curve: DefaultCurve);

        PrintCharger(maxKW);
        PrintCar("Car 1", carA);
        PrintCar("Car 2", carB);

        // ── Phase 1: Both cars charge together ──
        // Car 2 departs early at t=2700s (45 min)
        uint departTime = 2700;

        var phase1 = Integrator.UpdateSoC(simNow, maxKW, carA, carB, elapsedSeconds: departTime);
        Console.WriteLine();
        Console.WriteLine($"  Car 2 departs at  : {FmtTime(departTime)}");
        Console.WriteLine($"  Car 1 SoC at that point: {FmtSoC(phase1.SocA)}  (was {FmtSoC(carA.CurrentSoC)})");
        Console.WriteLine($"  Car 2 SoC at that point: {FmtSoC(phase1.SocB)}  (was {FmtSoC(carB.CurrentSoC)})");

        // ── Phase 2: Car 1 charges alone with full power ──
        var carAUpdated = carA with { CurrentSoC = phase1.SocA };
        var phase2 = Integrator.PlanSession(departTime, maxKW, carAUpdated);

        Console.WriteLine();
        Console.WriteLine($"  Car 1 finishes at : {FmtTime(phase2.FinishTimeA!.Value)}");
        Console.WriteLine();
        Console.WriteLine("  NOTE: After Car 2 leaves, Car 1 gets full 100 kW and finishes faster");
        Console.WriteLine("  than it would have if Car 2 had stayed the whole time.");
    }


    // ── E: 10 waves — car arrives, second joins, first leaves, repeat ──

    static void ScenarioE_TenWaves()
    {
        Separator("SCENARIO E: 10 waves of arrivals and departures");

        const int Waves = 10;
        const double MaxKW = 100.0;

        uint simNow = 0;

        PrintCharger(MaxKW);
        Console.WriteLine($"  Waves             : {Waves}  (each wave = 2400s / 40 min)");
        Console.WriteLine($"  Event spacing     : new car every 1200s, resident departs 1200s later");
        Console.WriteLine();

        // Two different curves to demonstrate per-car curve support
        IChargingCurve curveA = DefaultCurve;
        IChargingCurve curveB = new AggressiveTaperCurve();  // newcomers use a different curve

        var resident = new ConnectedCar(CarId: 0, CurrentSoC: 0.10, TargetSoC: 0.90, CapacityKWh: 75.0, Curve: curveA);
        Console.WriteLine($"  t={FmtTime(simNow)}  Car  0 connects  SoC {FmtSoC(resident.CurrentSoC)} → {FmtSoC(resident.TargetSoC)}");

        // Accumulate energy and time across all sub-sessions for overall utilization
        double totalEnergyKWh = 0.0;
        double totalDurationSecs = 0.0;

        for (int wave = 1; wave <= Waves; wave++)
        {
            // ── Step 1: resident charges alone for 1200s, then newcomer arrives ──
            uint arrivalTime = simNow + 1200;
            var afterAlone = Integrator.UpdateSoC(simNow, MaxKW, resident, elapsedSeconds: 1200);
            totalEnergyKWh += afterAlone.TotalEnergyKWh;
            totalDurationSecs += afterAlone.DurationSeconds;
            resident = resident with { CurrentSoC = afterAlone.SocA };

            double newSoC = 0.10 + (wave % 5) * 0.08;
            double newCap = 60.0 + (wave % 3) * 15.0;
            var newcomer = new ConnectedCar(CarId: wave, CurrentSoC: newSoC,
                                             TargetSoC: 0.90, CapacityKWh: newCap, Curve: curveB);

            Console.WriteLine($"  t={FmtTime(arrivalTime)}  Car {wave,2} connects  " +
                              $"SoC {FmtSoC(newcomer.CurrentSoC)} → {FmtSoC(newcomer.TargetSoC)}  " +
                              $"| Resident Car {wave - 1,2} now at {FmtSoC(resident.CurrentSoC)}  " +
                              $"| utiliz so far: {totalEnergyKWh / (MaxKW * totalDurationSecs / 3600.0) * 100:F1}%");

            // ── Step 2: both charge together for 1200s, then resident departs ──
            uint departTime = arrivalTime + 1200;
            var afterDual = Integrator.UpdateSoC(arrivalTime, MaxKW, resident, newcomer, elapsedSeconds: 1200);
            totalEnergyKWh += afterDual.TotalEnergyKWh;
            totalDurationSecs += afterDual.DurationSeconds;

            Console.WriteLine($"  t={FmtTime(departTime)}  Car {wave - 1,2} departs    " +
                              $"SoC reached {FmtSoC(afterDual.SocA)}  " +
                              $"| Car {wave,2} now at {FmtSoC(afterDual.SocB)}  " +
                              $"| utiliz so far: {totalEnergyKWh / (MaxKW * totalDurationSecs / 3600.0) * 100:F1}%");

            resident = newcomer with { CurrentSoC = afterDual.SocB };
            simNow = departTime;
        }

        // Final car charges to completion
        var finalPlan = Integrator.PlanSession(simNow, MaxKW, resident);
        totalEnergyKWh += finalPlan.TotalEnergyKWh;
        totalDurationSecs += finalPlan.DurationSeconds;

        Console.WriteLine();
        Console.WriteLine($"  Final car finishes at    : {FmtTime(finalPlan.FinishTimeA!.Value)}");
        Console.WriteLine($"  Final car end SoC        : {FmtSoC(finalPlan.SocA)}");
        Console.WriteLine();
        Console.WriteLine($"  Total energy delivered   : {totalEnergyKWh:F2} kWh");
        Console.WriteLine($"  Total sim duration       : {FmtTime((uint)totalDurationSecs)}");
        Console.WriteLine($"  Max possible energy      : {MaxKW * totalDurationSecs / 3600.0:F2} kWh");
        Console.WriteLine($"  Overall charger utiliz.  : {totalEnergyKWh / (MaxKW * totalDurationSecs / 3600.0) * 100:F1}%");
    }


    // ─────────────────────────────────────────────────────────
    // FORMATTING HELPERS
    // ─────────────────────────────────────────────────────────

    static void Separator(string title)
    {
        int width = 62;
        int pad = (width - title.Length - 2) / 2;
        string line = new string('─', pad);
        Console.WriteLine();
        Console.WriteLine($"{line} {title} {line}");
    }

    static void PrintCharger(double maxKW) =>
        Console.WriteLine($"  Charger max power : {maxKW} kW");

    static void PrintCar(string label, ConnectedCar car) =>
        Console.WriteLine($"  {label,-24}: {car.CapacityKWh} kWh  |  SoC {FmtSoC(car.CurrentSoC)} → {FmtSoC(car.TargetSoC)}");

    static string FmtTime(uint seconds)
    {
        uint h = seconds / 3600;
        uint m = (seconds % 3600) / 60;
        uint s = seconds % 60;
        return $"{h:D2}h {m:D2}m {s:D2}s  ({seconds}s)";
    }

    static string FmtSoC(double soc) => $"{soc * 100:F1}%";
}
