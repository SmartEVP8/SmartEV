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
    public Time EstimateChargerWaitTime(ChargerBase chargerBase, Time simNow)
    {
        return chargerBase switch
        {
            SingleCharger singleCharger => EstimateSingleChargerWaitTime(
                singleCharger,
                simNow),

            DualCharger dualCharger => EstimateDualChargerWaitTime(
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
        SingleCharger singleCharger,
        Time simNow)
    {
        var availableAt = GetSingleChargerAvailable(singleCharger, simNow);

        foreach (var (_, queuedEv) in singleCharger.Queue)
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
    DualCharger dualCharger,
    Time simNow)
    {
        var queue = new Queue<ConnectedEV>(dualCharger.Queue.Select(x => x.EV));

        var currentTime = simNow;

        var evA = dualCharger.SessionA is null
            ? null
            : dualCharger.SessionA.EV with
            {
                CurrentSoC = dualCharger.SessionA.GetCurrentSoC(simNow)
            };

        var evB = dualCharger.SessionB is null
            ? null
            : dualCharger.SessionB.EV with
            {
                CurrentSoC = dualCharger.SessionB.GetCurrentSoC(simNow)
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
        SingleCharger singleCharger,
        Time simNow)
    {
        if (singleCharger.Session is null)
            return simNow;

        var activeSession = singleCharger.Session;

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
