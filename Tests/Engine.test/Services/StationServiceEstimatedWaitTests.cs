namespace Engine.test.Services;

using Core.Charging;
using Core.Shared;
using Engine.Events;
using Engine.Services;
using Engine.test.Builders;
using Core.test.Builders;
using Engine.Vehicles;
using Xunit;
using Engine.Services.StationServiceHelpers;
using Engine.Utils;
using System.Collections.Generic;
using Core.Charging.ChargingModel;

public class ExpectedWaitTimeTests
{
    private static readonly ushort _stationId = 1;

    private static readonly uint _longJourneyMs = Time.MillisecondsPerDay;

    [Fact]
    public void ExpectedWaitTime_Single_WithEmptyQueue_ReturnsSimNow()
    {
        var (stationService, _, _, _) = BuildSingle();
        var simNow = new Time(100);
        var arrival = new Time(150);

        var result = stationService.ExpectedWaitTime(_stationId, simNow, arrival);

        Assert.Equal(simNow, result);
    }

    [Fact]
    public void ExpectedWaitTime_Dual_WithEmptyQueue_ReturnsSimNow()
    {
        var (stationService, _, _, _) = BuildDual();
        var simNow = new Time(100);
        var arrival = new Time(150);

        var result = stationService.ExpectedWaitTime(_stationId, simNow, arrival);

        Assert.Equal(simNow, result);
    }

    [Fact]
    public void ExpectedWaitTime_Single_WithActiveAndQueuedEVs_MatchesHandlerEstimate()
    {
        var (stationService, _, evStore, handler) = BuildSingle();
        var simNow = new Time(100);
        var arrival = new Time(150);

        evStore.TryAllocate((index, ref ev) => ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 0.2f)), out var ev1);
        evStore.TryAllocate((index, ref ev) => ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 0.3f)), out var ev2);

        stationService.HandleArrivalAtStation(new ArriveAtStation(ev1, _stationId, 0.8, simNow));
        stationService.HandleArrivalAtStation(new ArriveAtStation(ev2, _stationId, 0.9, simNow));

        var expected = handler.EstimateWaitTime(simNow).AvailableAt + simNow;
        var result = stationService.ExpectedWaitTime(_stationId, simNow, arrival);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExpectedWaitTime_Dual_WithActiveAndQueuedEVs_MatchesHandlerEstimate()
    {
        var (stationService, _, evStore, handler) = BuildDual();
        var simNow = new Time(100);
        var arrival = new Time(150);

        evStore.TryAllocate((index, ref ev) => ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 0.2f)), out var ev1);
        evStore.TryAllocate((index, ref ev) => ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 0.3f)), out var ev2);
        evStore.TryAllocate((index, ref ev) => ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 0.4f)), out var ev3);

        stationService.HandleArrivalAtStation(new ArriveAtStation(ev1, _stationId, 0.8, simNow));
        stationService.HandleArrivalAtStation(new ArriveAtStation(ev2, _stationId, 0.8, simNow));
        stationService.HandleArrivalAtStation(new ArriveAtStation(ev3, _stationId, 0.9, simNow));

        var expected = handler.EstimateWaitTime(simNow).AvailableAt + simNow;
        var result = stationService.ExpectedWaitTime(_stationId, simNow, arrival);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExpectedWaitTime_Single_WithOneReservationBeforeArrival_IncludesReservation()
    {
        var (stationService, _, evStore, handler) = BuildSingle();
        var simNow = Time.From(day: 0, hour: 8);
        var arrival = Time.From(day: 0, hour: 10);

        evStore.TryAllocate((index, ref ev) => ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 0.2f), originalDuration: _longJourneyMs), out var activeEv);
        evStore.TryAllocate((index, ref ev) => ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 0.2f)), out var resEv);
        var battery = evStore.Get(resEv).Battery;

        stationService.HandleArrivalAtStation(new ArriveAtStation(activeEv, _stationId, 0.8, simNow));
        stationService.HandleReservation(new Reservation(resEv, Time.From(day: 0, hour: 9), 0.2, 0.8), _stationId);

        var chargerFreeAfterActive = handler.EstimateWaitTime(simNow).AvailableAt + simNow;
        var reservedEV = new ConnectedEV(resEv, 0.2, 0.8, battery.MaxCapacityKWh, battery.MaxChargeRateKW, chargerFreeAfterActive);
        var expected = handler.EstimateWaitTime(chargerFreeAfterActive, [reservedEV]).AvailableAt + chargerFreeAfterActive;

        var result = stationService.ExpectedWaitTime(_stationId, simNow, arrival);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExpectedWaitTime_Dual_WithOneReservationBeforeArrival_IncludesReservation()
    {
        var (stationService, _, evStore, handler) = BuildDual();
        var simNow = Time.From(day: 0, hour: 8);
        var arrival = Time.From(day: 0, hour: 10);

        evStore.TryAllocate((index, ref ev) => ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 0.2f), originalDuration: _longJourneyMs), out var activeEv1);
        evStore.TryAllocate((index, ref ev) => ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 0.3f), originalDuration: _longJourneyMs), out var activeEv2);
        evStore.TryAllocate((index, ref ev) => ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 0.2f)), out var resEv);
        var battery = evStore.Get(resEv).Battery;

        stationService.HandleArrivalAtStation(new ArriveAtStation(activeEv1, _stationId, 0.8, simNow));
        stationService.HandleArrivalAtStation(new ArriveAtStation(activeEv2, _stationId, 0.8, simNow));
        stationService.HandleReservation(new Reservation(resEv, Time.From(day: 0, hour: 9), 0.2, 0.8), _stationId);

        var chargerFreeAfterActive = handler.EstimateWaitTime(simNow).AvailableAt + simNow;
        var reservedEV = new ConnectedEV(resEv, 0.2, 0.8, battery.MaxCapacityKWh, battery.MaxChargeRateKW, chargerFreeAfterActive);
        var expected = handler.EstimateWaitTime(chargerFreeAfterActive, [reservedEV]).AvailableAt + chargerFreeAfterActive;

        var result = stationService.ExpectedWaitTime(_stationId, simNow, arrival);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExpectedWaitTime_Single_WithReservationExactlyAtArrivalTime_IncludesReservation()
    {
        var (stationService, _, evStore, handler) = BuildSingle();
        var simNow = Time.From(day: 0, hour: 8);
        var arrival = Time.From(day: 0, hour: 10);

        evStore.TryAllocate((index, ref ev) => ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 0.2f)), out var resEv);
        var battery = evStore.Get(resEv).Battery;

        stationService.HandleReservation(new Reservation(resEv, arrival, 0.2, 0.8), _stationId);

        var chargerFreeInitially = handler.EstimateWaitTime(simNow).AvailableAt + simNow;
        var reservedEV = new ConnectedEV(resEv, 0.2, 0.8, battery.MaxCapacityKWh, battery.MaxChargeRateKW, chargerFreeInitially);
        var expected = handler.EstimateWaitTime(chargerFreeInitially, [reservedEV]).AvailableAt + chargerFreeInitially;

        var result = stationService.ExpectedWaitTime(_stationId, simNow, arrival);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExpectedWaitTime_Single_WithReservationAfterArrivalTime_IgnoresReservation()
    {
        var (stationService, _, evStore, _) = BuildSingle();
        var simNow = Time.From(day: 0, hour: 8);
        var arrival = Time.From(day: 0, hour: 10);

        evStore.TryAllocate((index, ref ev) => ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 0.2f)), out var resEv);

        stationService.HandleReservation(new Reservation(resEv, Time.From(day: 0, hour: 12), 0.2, 0.8), _stationId);

        var result = stationService.ExpectedWaitTime(_stationId, simNow, arrival);

        Assert.Equal(simNow, result);
    }

    [Fact]
    public void ExpectedWaitTime_Single_WithMultipleReservations_AccumulatesChain()
    {
        var (stationService, _, evStore, handler) = BuildSingle();
        var simNow = Time.From(day: 0, hour: 8);
        var arrival = Time.From(day: 0, hour: 12);

        evStore.TryAllocate((_, ref ev) => ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 0.2f)), out var resEv1);
        evStore.TryAllocate((_, ref ev) => ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 0.2f)), out var resEv2);

        stationService.HandleReservation(new Reservation(resEv1, Time.From(day: 0, hour: 9), 0.2, 0.8), _stationId);
        stationService.HandleReservation(new Reservation(resEv2, Time.From(day: 0, hour: 10), 0.2, 0.8), _stationId);

        var expectedQueue = new[]
        {
            ToConnectedEV(resEv1, 0.2),
            ToConnectedEV(resEv2, 0.2),
        };

        var expected = handler.EstimateWaitTime(simNow, expectedQueue).AvailableAt + simNow;
        var result = stationService.ExpectedWaitTime(_stationId, simNow, arrival);

        Assert.Equal(expected, result);

        ConnectedEV ToConnectedEV(int id, double soc) => new(
            id, soc, 0.8,
            evStore.Get(id).Battery.MaxCapacityKWh,
            evStore.Get(id).Battery.MaxChargeRateKW,
            simNow);
    }

    [Fact]
    public void ExpectedWaitTime_Dual_WithMultipleReservations_AccumulatesChain()
    {
        var (stationService, _, evStore, handler) = BuildDual();
        var simNow = Time.From(day: 0, hour: 8);
        var arrival = Time.From(day: 0, hour: 14);

        evStore.TryAllocate((_, ref ev) => ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 0.2f)), out var resEv1);
        evStore.TryAllocate((_, ref ev) => ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 0.3f)), out var resEv2);
        evStore.TryAllocate((_, ref ev) => ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 0.4f)), out var resEv3);

        stationService.HandleReservation(new Reservation(resEv1, Time.From(day: 0, hour: 10, minute: 0), 0.2, 0.8), _stationId);
        stationService.HandleReservation(new Reservation(resEv2, Time.From(day: 0, hour: 10, minute: 20), 0.3, 0.8), _stationId);
        stationService.HandleReservation(new Reservation(resEv3, Time.From(day: 0, hour: 10, minute: 40), 0.4, 0.8), _stationId);

        var expectedQueue = new[]
        {
            ToConnectedEV(resEv1, 0.2),
            ToConnectedEV(resEv2, 0.3),
            ToConnectedEV(resEv3, 0.4),
        };

        var expected = handler.EstimateWaitTime(simNow, expectedQueue).AvailableAt + simNow;
        var result = stationService.ExpectedWaitTime(_stationId, simNow, arrival);

        Assert.Equal(expected, result);

        ConnectedEV ToConnectedEV(int id, double soc) => new(
            id, soc, 0.8,
            evStore.Get(id).Battery.MaxCapacityKWh,
            evStore.Get(id).Battery.MaxChargeRateKW,
            simNow);
    }

    [Fact]
    public void ExpectedWaitTime_TwoChargersWithUnequalLoad_ReturnsShorterWait()
    {
        var charger1 = CoreTestData.SingleCharger(1, maxPowerKW: 150);
        var charger2 = CoreTestData.SingleCharger(2, maxPowerKW: 150);
        var station = CoreTestData.Station(1, chargers: [charger1, charger2]);
        var scheduler = new EventScheduler();
        var evStore = new EVStore(10);
        var stationService = EngineTestData.StationService(
            new Dictionary<ushort, Station> { [_stationId] = station }, scheduler, evStore);

        var simNow = new Time(100);
        var arrival = new Time(150);

        evStore.TryAllocate((index, ref ev) => ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 0.2f)), out var activeEv);
        stationService.HandleArrivalAtStation(new ArriveAtStation(activeEv, _stationId, 0.8, simNow));

        var result = stationService.ExpectedWaitTime(_stationId, simNow, arrival);
        Assert.Equal(simNow, result);
    }

    [Fact]
    public void ExpectedWaitTime_TwoChargersWithDifferentPower_ReturnsFromFasterCharger()
    {
        var slowCharger = CoreTestData.SingleCharger(1, maxPowerKW: 50);
        var fastCharger = CoreTestData.SingleCharger(2, maxPowerKW: 150);
        var station = CoreTestData.Station(1, chargers: [slowCharger, fastCharger]);
        var scheduler = new EventScheduler();
        var evStore = new EVStore(10);
        var stationService = EngineTestData.StationService(
            new Dictionary<ushort, Station> { [_stationId] = station }, scheduler, evStore);

        var simNow = new Time(100);
        var arrival = new Time(150);

        evStore.TryAllocate((index, ref ev) => ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 0.2f)), out var ev1);
        evStore.TryAllocate((index, ref ev) => ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 0.2f)), out var ev2);

        stationService.HandleArrivalAtStation(new ArriveAtStation(ev1, _stationId, 0.8, simNow));
        stationService.HandleArrivalAtStation(new ArriveAtStation(ev2, _stationId, 0.8, simNow));

        var fastHandler = stationService.GetChargerHandler(fastCharger.Id);
        var expectedFromFastCharger = fastHandler.EstimateWaitTime(simNow).AvailableAt + simNow;

        var result = stationService.ExpectedWaitTime(_stationId, simNow, arrival);

        Assert.Equal(expectedFromFastCharger, result);
    }

    [Fact]
    public void ExpectedWaitTime_Single_EVAlreadyAtTargetSoC_ChargerBecomesImmediatelyFree()
    {
        var (stationService, _, evStore, _) = BuildSingle();
        var simNow = new Time(100);
        var arrival = new Time(150);

        evStore.TryAllocate((index, ref ev) => ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 0.8f)), out var fullEv);
        stationService.HandleArrivalAtStation(new ArriveAtStation(fullEv, _stationId, 0.8, simNow));

        var result = stationService.ExpectedWaitTime(_stationId, simNow, arrival);
        Assert.Equal(simNow, result);
    }

    private static (StationService stationService, EventScheduler scheduler, EVStore evStore, SingleChargerHandler handler) BuildSingle(ushort maxPowerKW = 150)
    {
        var charger = CoreTestData.SingleCharger(1, maxPowerKW: maxPowerKW);
        var station = CoreTestData.Station(1, chargers: [charger]);
        var scheduler = new EventScheduler();
        var evStore = new EVStore(10);
        var service = EngineTestData.StationService(new Dictionary<ushort, Station> { [_stationId] = station }, scheduler, evStore);
        var handler = service.GetChargerHandler(charger.Id) as SingleChargerHandler
            ?? throw new SkillissueException();
        return (service, scheduler, evStore, handler);
    }

    private static (StationService stationService, EventScheduler scheduler, EVStore evStore, DualChargerHandler handler) BuildDual(ushort maxPowerKW = 200)
    {
        var charger = CoreTestData.DualCharger(1, maxPowerKW: maxPowerKW);
        var station = CoreTestData.Station(1, chargers: [charger]);
        var scheduler = new EventScheduler();
        var evStore = new EVStore(10);
        var service = EngineTestData.StationService(new Dictionary<ushort, Station> { [_stationId] = station }, scheduler, evStore);
        var handler = service.GetChargerHandler(charger.Id) as DualChargerHandler
            ?? throw new SkillissueException();
        return (service, scheduler, evStore, handler);
    }
}
