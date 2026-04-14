namespace Engine.Services.StationServiceHelpers;

using Core.Charging;
using Core.Charging.ChargingModel;
using Core.Shared;
using Engine.Utils;


/// <summary>
/// Estimates the projected waiting time for an arriving EV at a charger.
/// The estimator simulates both the currently active charging sessions and
/// the EVs already waiting in the charger queue.
/// </summary>
public class ProjectedWaitTimeEstimator(ChargingIntegrator chargingIntegrator)
{
    private readonly ChargingIntegrator _chargingIntegrator = chargingIntegrator;

    /// <summary>
    /// Estimates the waiting time for an arriving EV at the given charger.
    /// </summary>
    public Time EstimateChargerWaitTime(ChargerState chargerState, Time simNow)
    {
        return chargerState.Charger switch
        {
            SingleCharger singleCharger => EstimateSingleChargerWaitTime(
                chargerState,
                singleCharger,
                simNow),

            DualCharger dualCharger => EstimateDualChargerWaitTime(
                chargerState,
                dualCharger,
                simNow),

            _ => throw new SkillissueException(
                "Do we have a third type of charger? :O")
        };
    }

    /// <summary>
    /// Estimates wait time for a single charger by simulating:
    /// 1. the active charging session, and
    /// 2. all queued EVs ahead of the arriving EV.
    /// </summary>
    private Time EstimateSingleChargerWaitTime(
        ChargerState chargerState,
        SingleCharger singleCharger,
        Time simNow)
    {
        var availableAt = GetSingleChargerAvailable(chargerState, singleCharger, simNow);

        foreach (var (_, queuedEv) in chargerState.Queue)
        {
            var result = _chargingIntegrator.IntegrateSingleToCompletion(
                availableAt,
                singleCharger.MaxPowerKW,
                singleCharger,
                queuedEv);

            availableAt = result.FinishTimeA
                          ?? throw new InvalidOperationException(
                              $"Queued EV {queuedEv.EVId} did not produce a finish time.");
        }

        return availableAt > simNow ? availableAt - simNow : new Time(0);
    }

    /// <summary>
    /// Estimates wait time for a dual charger by simulating the whole dual system:
    /// current active sessions plus queued EVs ahead of the arriving EV.
    /// </summary>
    private Time EstimateDualChargerWaitTime(
    ChargerState chargerState,
    DualCharger dualCharger,
    Time simNow)
    {
        var queue = new Queue<ConnectedEV>(chargerState.Queue.Select(x => x.Item2));

        var currentTime = simNow;

        ConnectedEV? evA = chargerState.SessionA is null
            ? null
            : chargerState.SessionA.EV with
            {
                CurrentSoC = chargerState.SessionA.GetCurrentSoC(simNow)
            };

        ConnectedEV? evB = chargerState.SessionB is null
            ? null
            : chargerState.SessionB.EV with
            {
                CurrentSoC = chargerState.SessionB.GetCurrentSoC(simNow)
            };

        while (true)
        {
            if (evA is null && queue.Count > 0)
                evA = queue.Dequeue();

            if (evB is null && queue.Count > 0)
                evB = queue.Dequeue();

            if (queue.Count == 0 && (evA is null || evB is null))
                return currentTime > simNow ? currentTime - simNow : new Time(0);

            if (evA is null && evB is null)
                return new Time(0);

            if (evA is not null && evB is not null)
            {
                var result = _chargingIntegrator.IntegrateDualToCompletion(
                    currentTime,
                    dualCharger.MaxPowerKW,
                    dualCharger,
                    evA,
                    evB);

                var finishA = result.FinishTimeA
                    ?? throw new InvalidOperationException(
                        $"EV {evA.EVId} on side A did not produce a finish time.");

                var finishB = result.FinishTimeB
                    ?? throw new InvalidOperationException(
                        $"EV {evB.EVId} on side B did not produce a finish time.");

                if (finishA <= finishB)
                {
                    currentTime = finishA;

                    evB = evB with
                    {
                        CurrentSoC = result.BSoCWhenAFinish
                    };

                    evA = null;
                }
                else
                {
                    currentTime = finishB;

                    evA = evA with
                    {
                        CurrentSoC = result.ASoCWhenBFinish
                    };

                    evB = null;
                }

                continue;
            }

            continue;
        }
    }

    /// <summary>
    /// Computes when a single charger becomes free from its currently active session.
    /// </summary>
    private Time GetSingleChargerAvailable(
        ChargerState chargerState,
        SingleCharger singleCharger,
        Time simNow)
    {
        if (chargerState.SessionA is null)
            return simNow;

        var activeSession = chargerState.SessionA;

        var activeEv = activeSession.EV with
        {
            CurrentSoC = activeSession.GetCurrentSoC(simNow)
        };

        var result = _chargingIntegrator.IntegrateSingleToCompletion(
            simNow,
            singleCharger.MaxPowerKW,
            singleCharger,
            activeEv);

        return result.FinishTimeA
               ?? throw new InvalidOperationException(
                   $"Active EV {activeEv.EVId} did not produce a finish time.");
    }
}
