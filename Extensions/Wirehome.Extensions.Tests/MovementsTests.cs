﻿using Moq;
using System;
using System.Reactive.Linq;
using System.Collections.Generic;
using Microsoft.Reactive.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wirehome.Contracts.Sensors;
using Wirehome.Contracts.Logging;
using Wirehome.Contracts.Environment;
using Wirehome.Extensions.Messaging.Core;
using Wirehome.Contracts.Core;
using Wirehome.Contracts.Components;
using Wirehome.Contracts.Components.Commands;
using Force.DeepCloner;
using System.Reactive;
using Wirehome.Motion.Model;
using Wirehome.Motion;

namespace Wirehome.Extensions.Tests
{
    //                                             STAIRCASE [S]
    //  ________________________________________<_    __________________________
    // |        |                |                       |                      |
    // |        |                  [HL]   HALLWAY        |                      |
    // |   B    |                |<         [H]          |<                     |
    // |   A                     |___   ______           |       BADROOM        |
    // |   L    |                |            |                    [D]          |
    // |   C    |                |            |          |                      |
    // |   O    |                |            |          |______________________|
    // |   N    |   LIVINGROOM  >|            |          |<                     |
    // |   Y    |      [L]       |  BATHROOM  |   [HT]                          |
    // |        |                |     [B]   >|___v  ____|                      |
    // |  [Y]   |                |            |          |       KITCHEN        |
    // |        |                |            |  TOILET  |         [K]          |
    // |        |                |            |    [T]   |                      |
    // |_______v|________________|____________|_____v____|______________________|
    //
    // LEGEND: v/< - Motion Detector

    [TestClass]
    public class MovementsTests : ReactiveTest
    {
        [TestMethod]
        public void MoveInRoomShouldTurnOnLight()
        {
            var (service, motionEvents, scheduler, lampDictionary, dateTime) = SetupEnviroment(null,
              OnNext(Time.Tics(500), new MotionEnvelope(ToiletId)),
              OnNext(Time.Tics(1500), new MotionEnvelope(KitchenId)),
              OnNext(Time.Tics(2000), new MotionEnvelope(LivingroomId))
            );

            service.Start();
            scheduler.AdvanceToEnd(motionEvents);
            
            Assert.AreEqual(true, lampDictionary[ToiletId].IsTurnedOn);
            Assert.AreEqual(true, lampDictionary[KitchenId].IsTurnedOn);
            Assert.AreEqual(true, lampDictionary[LivingroomId].IsTurnedOn);
        }

        [TestMethod]
        public void MoveInRoomShouldTurnOnLightOnWhenWorkinghoursAreDaylight()
        {
            var (service, motionEvents, scheduler, lampDictionary, dateTime) = SetupEnviroment(new AreaDescriptor { WorkingTime = WorkingTime.DayLight },
              OnNext(Time.Tics(500), new MotionEnvelope(ToiletId)),
              OnNext(Time.Tics(1500), new MotionEnvelope(KitchenId)),
              OnNext(Time.Tics(2000), new MotionEnvelope(LivingroomId))
            );

            Mock.Get(dateTime).Setup(x => x.Time).Returns(TimeSpan.FromHours(12));

            service.Start();
            scheduler.AdvanceToEnd(motionEvents);

            Assert.AreEqual(true, lampDictionary[ToiletId].IsTurnedOn);
            Assert.AreEqual(true, lampDictionary[KitchenId].IsTurnedOn);
            Assert.AreEqual(true, lampDictionary[LivingroomId].IsTurnedOn);
        }

        [TestMethod]
        public void MoveInRoomShouldNotTurnOnLightOnNightWhenWorkinghoursAreDaylight()
        {
            var (service, motionEvents, scheduler, lampDictionary, dateTime) = SetupEnviroment(new AreaDescriptor { WorkingTime = WorkingTime.DayLight },
              OnNext(Time.Tics(500), new MotionEnvelope(ToiletId)),
              OnNext(Time.Tics(1500), new MotionEnvelope(KitchenId)),
              OnNext(Time.Tics(2000), new MotionEnvelope(LivingroomId))
            );

            Mock.Get(dateTime).Setup(x => x.Time).Returns(TimeSpan.FromHours(21));

            service.Start();
            scheduler.AdvanceToEnd(motionEvents);

            Assert.AreEqual(false, lampDictionary[ToiletId].IsTurnedOn);
            Assert.AreEqual(false, lampDictionary[KitchenId].IsTurnedOn);
            Assert.AreEqual(false, lampDictionary[LivingroomId].IsTurnedOn);
        }

        [TestMethod]
        public void MoveInRoomShouldNotTurnOnLightOnDaylightWhenWorkinghoursIsNight()
        {
            var (service, motionEvents, scheduler, lampDictionary, dateTime) = SetupEnviroment(new AreaDescriptor { WorkingTime = WorkingTime.AfterDusk },
              OnNext(Time.Tics(500), new MotionEnvelope(ToiletId)),
              OnNext(Time.Tics(1500), new MotionEnvelope(KitchenId)),
              OnNext(Time.Tics(2000), new MotionEnvelope(LivingroomId))
            );
            Mock.Get(dateTime).Setup(x => x.Time).Returns(TimeSpan.FromHours(12));

            service.Start();
            scheduler.AdvanceToEnd(motionEvents);

            Assert.AreEqual(false, lampDictionary[ToiletId].IsTurnedOn);
            Assert.AreEqual(false, lampDictionary[KitchenId].IsTurnedOn);
            Assert.AreEqual(false, lampDictionary[LivingroomId].IsTurnedOn);
        }

        [TestMethod]
        public void MoveInRoomShouldNotTurnOnLightWhenAutomationIsDisabled()
        {
            var (service, motionEvents, scheduler, lampDictionary, dateTime) = SetupEnviroment(null,
              OnNext(Time.Tics(500), new MotionEnvelope(ToiletId))
            );

            service.DisableAutomation(ToiletId);
            service.Start();
            scheduler.AdvanceToEnd(motionEvents);

            Assert.AreEqual(false, lampDictionary[ToiletId].IsTurnedOn);
        }

        [TestMethod]
        public void MoveInRoomShouldTurnOnLightWhenAutomationIsReEnabled()
        {
            var (service, motionEvents, scheduler, lampDictionary, dateTime) = SetupEnviroment(null,
              OnNext(Time.Tics(500), new MotionEnvelope(ToiletId)),
              OnNext(Time.Tics(2500), new MotionEnvelope(ToiletId))
            );

            service.DisableAutomation(ToiletId);
            motionEvents.Subscribe(x => service.EnableAutomation(ToiletId));
            service.Start();
            scheduler.AdvanceToEnd(motionEvents);

            Assert.AreEqual(true, lampDictionary[ToiletId].IsTurnedOn);
        }

        [TestMethod]
        public void AnalyzeMoveShouldCountPeopleNumberInRoom()
        {
            var (service, motionEvents, scheduler, lampDictionary, dateTime) = SetupEnviroment(null,
              OnNext(Time.Tics(500), new MotionEnvelope(ToiletId)),
              OnNext(Time.Tics(1500), new MotionEnvelope(HallwayToiletId)),
              OnNext(Time.Tics(2000), new MotionEnvelope(KitchenId)),
              OnNext(Time.Tics(2500), new MotionEnvelope(LivingroomId)),
              OnNext(Time.Tics(3000), new MotionEnvelope(HallwayLivingroomId)),
              OnNext(Time.Tics(3500), new MotionEnvelope(HallwayToiletId)),
              OnNext(Time.Tics(4000), new MotionEnvelope(KitchenId))

            );

            service.Start();
            scheduler.AdvanceJustAfterEnd(motionEvents);

            Assert.AreEqual(2, service.GetCurrentNumberOfPeople(KitchenId));

        }

        [TestMethod]
        public void AnalyzeMoveShouldCountPeopleNumberInHouse()
        {
            var (service, motionEvents, scheduler, lampDictionary, dateTime) = SetupEnviroment(null,
              OnNext(Time.Tics(500), new MotionEnvelope(ToiletId)),
              OnNext(Time.Tics(1500), new MotionEnvelope(HallwayToiletId)),
              OnNext(Time.Tics(2000), new MotionEnvelope(KitchenId)),
              OnNext(Time.Tics(2500), new MotionEnvelope(LivingroomId)),
              OnNext(Time.Tics(3000), new MotionEnvelope(HallwayLivingroomId))

            );

            service.Start();
            scheduler.AdvanceJustAfterEnd(motionEvents, 2000);

            Assert.AreEqual(2, service.NumberOfPersonsInHouse);

        }

        [TestMethod]
        public void WhenLeaveFromOnePersonRoomWithNoConfusionShouldTurnOffLightImmediately()
        {
            var (service, motionEvents, scheduler, lampDictionary, dateTime) = SetupEnviroment(null,
              OnNext(Time.Tics(500), new MotionEnvelope(ToiletId)),
              OnNext(Time.Tics(1500), new MotionEnvelope(HallwayToiletId))
            );

            service.Start();
            scheduler.AdvanceJustAfterEnd(motionEvents);

            Assert.AreEqual(false, lampDictionary[ToiletId].IsTurnedOn);
        }


        [TestMethod]
        public void WhenLeaveFromOnePersonRoomWithConfusionShouldTurnOffImmediatelyWhenConfusionResolved()
        {
            
            var (service, motionEvents, scheduler, lampDictionary, dateTime) = SetupEnviroment(null,
                  // T->HT vs K->HT 
                  OnNext(Time.Tics(500), new MotionEnvelope(ToiletId)),
                  OnNext(Time.Tics(1000), new MotionEnvelope(KitchenId)),
                  OnNext(Time.Tics(1500), new MotionEnvelope(HallwayToiletId)),
                  OnNext(Time.Tics(2000), new MotionEnvelope(HallwayLivingroomId)),
                  // Move in K cancels K->HT 
                  OnNext(Time.Tics(3000), new MotionEnvelope(KitchenId))
            );

            service.Start();
            scheduler.AdvanceToEnd(motionEvents);

            Assert.AreEqual(false, lampDictionary[ToiletId].IsTurnedOn);
        }


        [TestMethod]
        public void WhenLeaveFromRoomWithNoConfusionShouldTurnOffLightAfterSomeTime()
        {
            var (service, motionEvents, scheduler, lampDictionary, dateTime) = SetupEnviroment(null,
              OnNext(Time.Tics(500), new MotionEnvelope(KitchenId)),
              OnNext(Time.Tics(1500), new MotionEnvelope(HallwayToiletId))
            );
            
            service.Start();

            scheduler.AdvanceJustAfterEnd(motionEvents);
            Assert.AreEqual(true, lampDictionary[KitchenId].IsTurnedOn);
            scheduler.AdvanceTo(Time.Tics(2500));
            Assert.AreEqual(false, lampDictionary[KitchenId].IsTurnedOn);
        }


        [TestMethod]
        public void WhenNoMoveInRoomShouldTurnOffAfterTurnOffTimeout()
        {
            var (service, motionEvents, scheduler, lampDictionary, dateTime) = SetupEnviroment(null,
                OnNext(Time.Tics(500), new MotionEnvelope(KitchenId))
            );

            service.Start();
            var area = service.GetAreaDescriptor(KitchenId);

            scheduler.AdvanceJustAfterEnd(motionEvents);
            Assert.AreEqual(true, lampDictionary[KitchenId].IsTurnedOn);
            scheduler.AdvanceTo(area.TurnOffTimeout);
            Assert.AreEqual(true, lampDictionary[KitchenId].IsTurnedOn);
            scheduler.AdvanceJustAfter(area.TurnOffTimeout);
            Assert.AreEqual(false, lampDictionary[KitchenId].IsTurnedOn);
        }
      
        #region Setup

        private const string HallwayToiletId = "HallwayToilet";
        private const string HallwayLivingroomId = "HallwayLivingroom";
        private const string ToiletId = "Toilet";
        private const string LivingroomId = "Livingroom";
        private const string BathroomId = "Bathroom";
        private const string BadroomId = "Badroom";
        private const string KitchenId = "Kitchen";
        private const string BalconyId = "Balcony";
        private const string StaircaseId = "Staircase";
        private const int TIMER_DURATION = 20;

        public
        (
            LightAutomationService,
            ITestableObservable<MotionEnvelope>,
            TestScheduler,
            Dictionary<string, MotionLamp>,
            IDateTimeService
        )
        SetupEnviroment(AreaDescriptor areaDescription = null, params Recorded<Notification<MotionEnvelope>>[] messages)
        {
            AreaDescriptor area = areaDescription ?? new AreaDescriptor();
            
            var hallwayDetectorToilet = CreateMotionDetector(HallwayToiletId);
            var hallwayDetectorLivingRoom = CreateMotionDetector(HallwayLivingroomId);
            var toiletDetector = CreateMotionDetector(ToiletId);
            var livingRoomDetector = CreateMotionDetector(LivingroomId);
            var bathroomDetector = CreateMotionDetector(BathroomId);
            var badroomDetector = CreateMotionDetector(BadroomId);
            var kitchenDetector = CreateMotionDetector(KitchenId);
            var balconyDetector = CreateMotionDetector(BalconyId);
            var staircaseDetector = CreateMotionDetector(StaircaseId);

            var hallwayLampToilet = new MotionLamp();
            var hallwayLampLivingRoom = new MotionLamp();
            var toiletLamp = new MotionLamp();
            var livingRoomLamp = new MotionLamp();
            var bathroomLamp = new MotionLamp();
            var badroomLamp = new MotionLamp();
            var kitchenLamp = new MotionLamp();
            var balconyLamp = new MotionLamp();
            var staircaseLamp = new MotionLamp();

            var lampDictionary = new Dictionary<string, MotionLamp>
            {
                { HallwayToiletId, hallwayLampToilet },
                { HallwayLivingroomId, hallwayLampLivingRoom },
                { ToiletId, toiletLamp },
                { LivingroomId, livingRoomLamp },
                { BathroomId, bathroomLamp },
                { BadroomId, badroomLamp },
                { KitchenId, kitchenLamp },
                { BalconyId, balconyLamp },
                { StaircaseId, staircaseLamp }
            };
        
            var daylightService = Mock.Of<IDaylightService>();
            Mock.Get(daylightService).Setup(x => x.Sunrise).Returns(TimeSpan.FromHours(8));
            Mock.Get(daylightService).Setup(x => x.Sunset).Returns(TimeSpan.FromHours(20));

            var logService = Mock.Of<ILogService>();
            var eventAggregator = Mock.Of<IEventAggregator>();
            var dateTimeService = Mock.Of<IDateTimeService>();
            var scheduler = new TestScheduler();
            var concurrencyProvider = new TestConcurrencyProvider(scheduler);
            var motionConfigurationProvider = new MotionConfigurationProvider();
            var motionConfiguration = motionConfigurationProvider.GetConfiguration();

            var observableTimer = Mock.Of<IObservableTimer>();

            Mock.Get(observableTimer).Setup(x => x.GenerateTime(motionConfiguration.PeriodicCheckTime)).Returns(scheduler.CreateColdObservable(GenerateTestTime(TimeSpan.FromSeconds(TIMER_DURATION), motionConfiguration.PeriodicCheckTime)));

            var lightAutomation = new LightAutomationService(eventAggregator, daylightService, logService, concurrencyProvider, dateTimeService,  motionConfigurationProvider, observableTimer);

            var descriptors = new List<MotionDesctiptorInitializer>
            {
                new MotionDesctiptorInitializer(hallwayDetectorToilet.Id, new[] { hallwayDetectorLivingRoom.Id, kitchenDetector.Id, staircaseDetector.Id }, hallwayLampToilet, area.DeepClone()),
                new MotionDesctiptorInitializer(hallwayDetectorLivingRoom.Id, new[] { livingRoomDetector.Id, bathroomDetector.Id, hallwayDetectorToilet.Id }, hallwayLampLivingRoom, area.DeepClone()),
                new MotionDesctiptorInitializer(livingRoomDetector.Id, new[] { balconyDetector.Id, hallwayDetectorLivingRoom.Id }, livingRoomLamp, area.DeepClone()),
                new MotionDesctiptorInitializer(balconyDetector.Id, new[] { livingRoomDetector.Id }, balconyLamp, area.DeepClone()),
                new MotionDesctiptorInitializer(kitchenDetector.Id, new[] { hallwayDetectorToilet.Id }, kitchenLamp, area.DeepClone()),
                new MotionDesctiptorInitializer(bathroomDetector.Id, new[] { hallwayDetectorLivingRoom.Id }, bathroomLamp, area.DeepClone()),
                new MotionDesctiptorInitializer(badroomDetector.Id, new[] { hallwayDetectorLivingRoom.Id }, badroomLamp, area.DeepClone()),
                new MotionDesctiptorInitializer(staircaseDetector.Id, new[] { hallwayDetectorToilet.Id }, staircaseLamp, area.DeepClone())
            };

            var toiletArea = area.DeepClone();
            toiletArea.MaxPersonCapacity = 1;
            descriptors.Add(new MotionDesctiptorInitializer(toiletDetector.Id, new[] { hallwayDetectorToilet.Id }, toiletLamp, toiletArea));
            lightAutomation.RegisterDescriptors(descriptors);
            lightAutomation.Initialize();

            var motionEvents = scheduler.CreateColdObservable(messages);
            Mock.Get(eventAggregator).Setup(x => x.Observe<MotionEvent>()).Returns(motionEvents);

            return
            (
                lightAutomation,
                motionEvents,
                scheduler,
                lampDictionary,
                dateTimeService
            );
        }

        public Recorded<Notification<DateTimeOffset>>[] GenerateTestTime(TimeSpan duration, TimeSpan frequency)
        {
            var time = new List<Recorded<Notification<DateTimeOffset>>>();
            var durationSoFar = TimeSpan.FromTicks(0);
            var dateSoFar = new DateTimeOffset(1, 1, 1, 0, 0, 0, TimeSpan.FromTicks(0));
            while (true)
            {
                durationSoFar = durationSoFar.Add(frequency);
                if (durationSoFar > duration) break;

                dateSoFar = dateSoFar.Add(frequency);
                time.Add(new Recorded<Notification<DateTimeOffset>>(durationSoFar.Ticks, Notification.CreateOnNext(dateSoFar)));
            }

            return time.ToArray();
        }

        private IMotionDetector CreateMotionDetector(string id)
        {
            var mockDetector = Mock.Of<IMotionDetector>();
            Mock.Get(mockDetector).Setup(x => x.Id).Returns(id);
            return mockDetector;
        }

        public class MotionEnvelope : MessageEnvelope<MotionEvent>
        {
            public MotionEnvelope(string motionUid) : base(new MotionEvent(motionUid))
            {
            }
        }

        public class MotionLamp : IComponent
        {
            
            public string Id => throw new NotImplementedException();

            public event EventHandler<ComponentFeatureStateChangedEventArgs> StateChanged;

            public bool IsTurnedOn { get; private set; }

            public void ExecuteCommand(ICommand command)
            {
                if (command is TurnOnCommand)
                {
                    IsTurnedOn = true;
                }
                else if (command is TurnOffCommand)
                {
                    IsTurnedOn = false;
                }
                else
                {
                    throw new NotSupportedException($"Not supported command {command}");
                }
            }

            public IComponentFeatureCollection GetFeatures()
            {
                throw new NotImplementedException();
            }

            public IComponentFeatureStateCollection GetState()
            {
                throw new NotImplementedException();
            }
        }
   
        #endregion
    }
}
