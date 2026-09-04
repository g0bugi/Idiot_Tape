using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace IdiotTape.Gameplay.Tests
{

    public sealed class GameplaySlideTransitionTests
    {

        private static readonly Type ActiveNoteType = typeof(GameplaySession).GetNestedType(
            "ActiveNote", BindingFlags.NonPublic);
        private static readonly Type ContactStateType = typeof(GameplaySession).GetNestedType(
            "ContactState", BindingFlags.NonPublic);
        private GameObject sessionObject;
        private GameplaySession session;
        private PrototypeChart chart;
        private object activeNote;

        [SetUp]
        public void SetUp()
        {

            sessionObject = new GameObject("Slide Transition Session Test");
            sessionObject.SetActive(false);
            session = sessionObject.AddComponent<GameplaySession>();
            SetField(session, "inputRouter", sessionObject.AddComponent<GameplayInputRouter>());
            chart = ScriptableObject.CreateInstance<PrototypeChart>();
            JsonUtility.FromJsonOverwrite(
                "{\"laneCount\":8,\"tempoSections\":[{\"startTime\":0.0," +
                "\"startBar\":1,\"beatsPerMinute\":120.0,\"beatsPerBar\":4,\"beatUnit\":4}]}", chart);
            SetField(session, "chart", chart);
            activeNote = CreateActiveNote();

        }

        [TearDown]
        public void TearDown()
        {

            UnityEngine.Object.DestroyImmediate(sessionObject);
            UnityEngine.Object.DestroyImmediate(chart);

        }

        [TestCase(1.9d)]
        [TestCase(2d)]
        [TestCase(2.1d)]
        public void TransitionAcceptsTimestampedEarlyOnTimeAndLateArrival(double arrivalTime)
        {

            object contact = AddContact(1, 2, 1d);
            Record(contact, 6, arrivalTime);

            Assert.That(Validate(2d, Math.Max(2d, arrivalTime), out bool pending), Is.True);
            Assert.That(pending, Is.False);
            Assert.That(GetProperty<int>(activeNote, "ValidatedSlideTransitionIndex"), Is.EqualTo(0));
            Assert.That(GetProperty<object>(contact, "Owner"), Is.SameAs(activeNote));

        }

        [Test]
        public void WrongDestinationStaysPendingAtDeadlineAndFailsAfterIt()
        {

            AddContact(1, 4, 1d);
            double deadline = 2d + new JudgementSettings().GoodWindowSeconds;

            Assert.That(Validate(2d, deadline, out bool pending), Is.False);
            Assert.That(pending, Is.True);
            Assert.That(Validate(2d, deadline + 0.001d, out pending), Is.False);
            Assert.That(pending, Is.False);

        }

        [Test]
        public void ArrivalAfterDeadlineCannotBeInterpolatedBackIntoWindow()
        {

            object contact = AddContact(1, 2, 1d);
            Record(contact, 6, 2.2d);

            Assert.That(Validate(2d, 2.3d, out bool pending), Is.False);
            Assert.That(pending, Is.False);

        }

        [Test]
        public void LaneHoldNeedsNoDiagonalTraceBetweenNodes()
        {

            object contact = AddContact(1, 2, 1d);
            Assert.That(Validate(1.5d, 1.5d, out bool pending), Is.True);
            Assert.That(pending, Is.False);
            Record(contact, 6, 2d);
            Assert.That(Validate(2d, 2d, out pending), Is.True);
            Assert.That(Validate(2.5d, 2.5d, out pending), Is.True);

        }

        [Test]
        public void ContactGapDuringTransferWaitsForEligibleReplacementArrival()
        {

            object previous = AddContact(1, 2, 1d);
            Record(previous, 2, 1.9d, false);
            Assert.That(Validate(2d, 2d, out bool pending), Is.False);
            Assert.That(pending, Is.True);
            object replacement = AddContact(2, 6, 2.1d);

            Assert.That(Validate(2d, 2.1d, out pending), Is.True);
            Assert.That(pending, Is.False);
            Assert.That(GetProperty<object>(replacement, "Owner"), Is.SameAs(activeNote));

        }

        [Test]
        public void UnrelatedSustainedOwnerCannotSupplySlideDestination()
        {

            object contact = AddContact(1, 2, 1d);
            object unrelatedNote = CreateActiveNote();
            SetProperty(contact, "Owner", unrelatedNote);
            Record(contact, 6, 1.9d);

            Assert.That(Validate(2d, 2.2d, out bool pending), Is.False);
            Assert.That(pending, Is.False);
            Assert.That(GetProperty<object>(contact, "Owner"), Is.SameAs(unrelatedNote));

        }

        [Test]
        public void ClearingCurrentOwnerDoesNotMakeAnOwnedHistoricalSampleEligible()
        {

            object contact = AddContact(1, 2, 1d);
            SetProperty(contact, "Owner", CreateActiveNote());
            Record(contact, 6, 1.9d);
            Record(contact, 6, 2.2d, false);
            SetProperty(contact, "Owner", null);

            Assert.That(Validate(2d, 2.2d, out bool pending), Is.False);
            Assert.That(pending, Is.False);

        }

        [Test]
        public void FreeHistoricalArrivalRemainsValidWithoutStealingItsCurrentOwner()
        {

            object contact = AddContact(1, 2, 1d);
            Record(contact, 6, 1.9d);
            object unrelatedNote = CreateActiveNote();
            Record(contact, 5, 2.2d);
            SetProperty(contact, "Owner", unrelatedNote);

            Assert.That(Validate(2d, 2.2d, out bool pending), Is.True);
            Assert.That(pending, Is.False);
            Assert.That(GetProperty<object>(contact, "Owner"), Is.SameAs(unrelatedNote));
            Assert.That(GetProperty<object>(activeNote, "PrimaryContactId"), Is.Null);

        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReusedContactIdPreservesPastArrivalAndItsOwnership(bool previouslyOwned)
        {

            object contact = AddContact(1, 2, 1d);

            if (previouslyOwned)
            {

                SetProperty(contact, "Owner", CreateActiveNote());

            }

            Record(contact, 6, 1.9d);
            Record(contact, 6, 1.95d, false);
            SetProperty(contact, "Owner", null);
            Invoke(session, "BeginContact", 1, PlayfieldGeometry.GetLaneCenterNormalized(5, 8), 2.2d);

            Assert.That(GetField<IDictionary>(session, "contacts")[1], Is.SameAs(contact));
            Assert.That(Validate(2d, 2.2d, out bool pending), Is.EqualTo(!previouslyOwned));
            Assert.That(pending, Is.False);

        }

        [Test]
        public void LaneHoldUsesTheLastObservedPositionInsteadOfFutureInterpolation()
        {

            object contact = AddContact(1, 2, 1d);
            Record(contact, 6, 2d);

            Assert.That(Validate(1.5d, 2d, out bool pending), Is.True);
            Assert.That(pending, Is.False);

        }

        [Test]
        public void FutureArrivalCannotRepairAnEarlierWrongLaneHold()
        {

            object contact = AddContact(1, 6, 1d);
            Record(contact, 2, 1.6d);

            Assert.That(Validate(1.5d, 1.6d, out bool pending), Is.False);
            Assert.That(pending, Is.False);

        }

        [TestCase(0, true)]
        [TestCase(1, false)]
        public void FinalLaneChangeGraceAppliesOnlyToNormalSlideEnd(int endBehavior, bool expectedSuccess)
        {

            activeNote = CreateActiveNote(endBehavior);
            AddContact(1, 5, 3.1d);

            Assert.That(Validate(3d, 3.1d, out bool pending), Is.EqualTo(expectedSuccess));
            Assert.That(pending, Is.False);

        }

        [TestCase(false)]
        [TestCase(true)]
        public void TransitionResolutionAwardsNodeAndCoincidentTickOnce(bool frameHitch)
        {

            CreateHud();
            SetProperty(activeNote, "NextQuarterCheckTime", 1.875d);
            SetProperty(activeNote, "NextHalfRewardTime", 2d);
            object contact = AddContact(1, 2, 1d);

            if (!frameHitch)
            {

                Invoke(session, "UpdateSlide", activeNote, 2d);
                Assert.That(GetField<int>(session, "score"), Is.Zero);
                Assert.That(GetProperty<double>(activeNote, "NextQuarterCheckTime"), Is.EqualTo(1.875d));
                Assert.That(GetProperty<double>(activeNote, "NextHalfRewardTime"), Is.EqualTo(2d));
                Assert.That(GetProperty<int>(activeNote, "NextSlideNodeIndex"), Is.Zero);

            }

            Record(contact, 6, 2.08d);
            Invoke(session, "UpdateSlide", activeNote, 2.09d);
            Assert.That(GetField<int>(session, "score"), Is.EqualTo(2000));
            Assert.That(GetField<int>(session, "combo"), Is.EqualTo(2));
            Assert.That(GetProperty<int>(activeNote, "NextSlideNodeIndex"), Is.EqualTo(1));
            Assert.That(GetProperty<double>(activeNote, "NextQuarterCheckTime"), Is.EqualTo(2.125d));
            Assert.That(GetProperty<double>(activeNote, "NextHalfRewardTime"), Is.EqualTo(2.25d));

            Invoke(session, "UpdateSlide", activeNote, 2.13d);
            Assert.That(GetField<int>(session, "score"), Is.EqualTo(2000));
            Assert.That(GetField<int>(session, "combo"), Is.EqualTo(2));
            Assert.That(GetProperty<double>(activeNote, "NextQuarterCheckTime"), Is.EqualTo(2.25d));

        }

        [Test]
        public void TerminalPrerequisitesResolvePendingNodeAndCoincidentRewardsFirst()
        {

            CreateHud();
            activeNote = CreateActiveNote(1);
            SetProperty(activeNote, "NextQuarterCheckTime", 1.875d);
            SetProperty(activeNote, "NextHalfRewardTime", 2d);
            object contact = AddContact(1, 2, 1d);

            Assert.That((bool)Invoke(session, "ResolveSlideBeforeTerminalFlick", activeNote, 2d), Is.False);
            Assert.That(GetField<int>(session, "score"), Is.Zero);
            Assert.That(GetProperty<int>(activeNote, "NextSlideNodeIndex"), Is.Zero);

            Record(contact, 6, 2.08d);
            Assert.That((bool)Invoke(session, "ResolveSlideBeforeTerminalFlick", activeNote, 2.09d), Is.True);
            Assert.That(GetField<int>(session, "score"), Is.EqualTo(2000));
            Assert.That(GetProperty<int>(activeNote, "NextSlideNodeIndex"), Is.EqualTo(1));
            Assert.That(GetProperty<double>(activeNote, "NextHalfRewardTime"), Is.EqualTo(2.25d));

        }

        [Test]
        public void TerminalPrerequisitesRejectInputBeforePenultimateNodeTime()
        {

            activeNote = CreateActiveNote(1);

            Assert.That((bool)Invoke(session, "ResolveSlideBeforeTerminalFlick", activeNote, 1.99d), Is.False);
            Assert.That(GetProperty<int>(activeNote, "NextSlideNodeIndex"), Is.Zero);
            Assert.That(GetField<int>(session, "score"), Is.Zero);

        }

        [Test]
        public void TerminalPrerequisitesRejectSkippingThePenultimateDestination()
        {

            CreateHud();
            activeNote = CreateActiveNote(1, true);
            SetProperty(activeNote, "NextQuarterCheckTime", 1.875d);
            SetProperty(activeNote, "NextHalfRewardTime", 2d);
            object contact = AddContact(1, 2, 1d);
            Record(contact, 5, 2.05d);

            Assert.That((bool)Invoke(session, "ResolveSlideBeforeTerminalFlick", activeNote, 2.2d), Is.False);
            Assert.That(GetProperty<bool>(activeNote, "HasFailed"), Is.True);
            Assert.That(GetField<int>(session, "score"), Is.Zero);

        }

        [Test]
        public void TerminalMotionCannotFinishAfterPrerequisitesHandOffItsContact()
        {

            CreateHud();
            activeNote = CreateActiveNote(1, true);
            SetProperty(activeNote, "NextQuarterCheckTime", 1.875d);
            SetProperty(activeNote, "NextHalfRewardTime", 2d);
            object motionContact = AddContact(1, 2, 1d);
            SetProperty(motionContact, "Owner", activeNote);
            SetProperty(activeNote, "PrimaryContactId", (int?)1);
            Record(motionContact, 6, 2.9d);
            Record(motionContact, 5, 3d);
            object replacement = AddContact(2, 6, 1.95d);

            Invoke(session, "EvaluateFlick", activeNote, motionContact, 3d, JudgementGrade.Perfect);

            Assert.That(GetProperty<bool>(activeNote, "HasFailed"), Is.False);
            Assert.That(GetProperty<object>(motionContact, "Owner"), Is.Null);
            Assert.That(GetProperty<object>(replacement, "Owner"), Is.SameAs(activeNote));
            Assert.That(GetProperty<int?>(activeNote, "PrimaryContactId"), Is.EqualTo(2));
            Assert.That(GetField<int>(session, "score"), Is.EqualTo(5000));
            Assert.That(GetField<int>(session, "combo"), Is.EqualTo(5));

        }

        [Test]
        public void LifecycleReleaseUsesItsExplicitTimeWithoutErasingStationaryOwnershipHistory()
        {

            object contact = AddContact(1, 6, 1.9d);
            object previousOwner = CreateActiveNote();
            SetProperty(contact, "Owner", previousOwner);
            SetProperty(previousOwner, "PrimaryContactId", (int?)1);

            Invoke(session, "ReleaseOwnedContacts", previousOwner, (double?)1.95d);

            Assert.That(Invoke(contact, "GetOwnerAtTime", 1.94d), Is.SameAs(previousOwner));
            Assert.That(Invoke(contact, "GetOwnerAtTime", 1.95d), Is.Null);
            Assert.That(GetProperty<object>(contact, "Owner"), Is.Null);
            Assert.That(GetProperty<object>(previousOwner, "PrimaryContactId"), Is.Null);

        }

        [Test]
        public void TerminalMotionDefersItsQuarterRewardUntilSuccessfulCompletion()
        {

            object contact = CreateTerminalMotionCase();
            CreateFeedbackPresenter();
            float originX = PlayfieldGeometry.GetLaneCenterNormalized(6, 8);
            Invoke(contact, "Record", originX - 0.85f / 8f, 1.98d, true);
            Invoke(session, "EvaluateFlick", activeNote, contact, 2.1d, JudgementGrade.Perfect);

            Invoke(session, "UpdateSlide", activeNote, 2.01d);
            Assert.That(GetProperty<bool>(activeNote, "HasFailed"), Is.False);
            Assert.That(GetProperty<bool>(activeNote, "TerminalFlickCompleted"), Is.False);
            Assert.That(GetProperty<double>(activeNote, "NextQuarterCheckTime"), Is.EqualTo(2d));
            Assert.That(GetProperty<double>(activeNote, "NextHalfRewardTime"), Is.EqualTo(2d));
            Assert.That(GetField<int>(session, "score"), Is.Zero);

            Record(contact, 5, 2.02d);
            Invoke(session, "EvaluateFlick", activeNote, contact, 2.1d, JudgementGrade.Perfect);
            Assert.That(GetProperty<bool>(activeNote, "HasFailed"), Is.False);
            Assert.That(GetProperty<bool>(activeNote, "TerminalFlickCompleted"), Is.True);
            Assert.That(GetProperty<double>(activeNote, "NextQuarterCheckTime"), Is.EqualTo(2.125d));
            Assert.That(GetProperty<double>(activeNote, "NextHalfRewardTime"), Is.EqualTo(2.25d));
            Assert.That(GetField<int>(session, "score"), Is.EqualTo(2000));
            Assert.That(GetField<int>(session, "combo"), Is.EqualTo(2));

            Invoke(session, "UpdateSlide", activeNote, 2.03d);
            Assert.That(GetField<int>(session, "score"), Is.EqualTo(2000));

        }

        [Test]
        public void TerminalMotionTimeoutFailsOnceWithoutPayingItsDeferredReward()
        {

            object contact = CreateTerminalMotionCase();
            float originX = PlayfieldGeometry.GetLaneCenterNormalized(6, 8);
            Invoke(contact, "Record", originX - 0.85f / 8f, 1.98d, true);
            Invoke(session, "UpdateSlide", activeNote, 2.01d);
            Assert.That(GetProperty<bool>(activeNote, "HasFailed"), Is.False);
            SetField(session, "combo", 7);

            Invoke(session, "UpdateSlide", activeNote, 2.25d);
            Assert.That(GetProperty<bool>(activeNote, "HasFailed"), Is.True);
            Assert.That(GetProperty<bool>(activeNote, "TerminalFlickCompleted"), Is.False);
            Assert.That(GetField<int>(session, "score"), Is.Zero);
            Assert.That(GetField<int>(session, "combo"), Is.Zero);

            GameplayHud hud = GetField<GameplayHud>(session, "hud");
            object judgementAnimation = GetField<object>(hud, "judgementAnimation");
            SetField(judgementAnimation, "elapsed", 0.12f);
            Invoke(session, "UpdateSlide", activeNote, 2.3d);
            Assert.That(GetField<float>(judgementAnimation, "elapsed"), Is.EqualTo(0.12f),
                "An already failed terminal flick must not restart its Miss feedback.");
            Assert.That(GetField<int>(session, "score"), Is.Zero);

        }

        [Test]
        public void WrongDirectionStillFailsWhileTerminalQuarterChecksArePending()
        {

            object contact = CreateTerminalMotionCase();
            Invoke(session, "UpdateSlide", activeNote, 2.01d);
            Assert.That(GetProperty<bool>(activeNote, "HasFailed"), Is.False);
            float originX = PlayfieldGeometry.GetLaneCenterNormalized(6, 8);
            Invoke(contact, "Record", originX + 0.3f / 8f, 2.02d, true);

            Invoke(session, "EvaluateFlick", activeNote, contact, 2.1d, JudgementGrade.Perfect);

            Assert.That(GetProperty<bool>(activeNote, "HasFailed"), Is.True);
            Assert.That(GetProperty<bool>(activeNote, "TerminalFlickCompleted"), Is.False);
            Assert.That(GetField<int>(session, "score"), Is.Zero);

        }

        private object CreateTerminalMotionCase()
        {

            CreateHud();
            ChartNote note = JsonUtility.FromJson<ChartNote>(
                "{\"hitTime\":1.0,\"laneIndex\":2,\"noteType\":2,\"slideEndBehavior\":1," +
                "\"slideNodes\":[{\"time\":1.5,\"laneIndex\":6},{\"time\":2.1,\"laneIndex\":5}]}");
            RuntimeNoteView view = sessionObject.AddComponent<RuntimeNoteView>();
            activeNote = Activator.CreateInstance(ActiveNoteType, note, view, true);
            SetProperty(activeNote, "HasStarted", true);
            SetProperty(activeNote, "StartGrade", JudgementGrade.Perfect);
            SetProperty(activeNote, "NextSlideNodeIndex", 1);
            SetProperty(activeNote, "NextQuarterCheckTime", 1.875d);
            SetProperty(activeNote, "NextHalfRewardTime", 2d);
            object contact = AddContact(1, 6, 1.5d);
            SetProperty(contact, "Owner", activeNote);
            SetProperty(activeNote, "PrimaryContactId", (int?)1);
            return contact;

        }

        private void CreateFeedbackPresenter()
        {

            GameObject presenterObject = new("Slide End Feedback Test");
            presenterObject.transform.SetParent(sessionObject.transform, false);
            PlayfieldPresenter presenter = presenterObject.AddComponent<PlayfieldPresenter>();
            GameObject effectObject = new("Slide End Hit Effect");
            effectObject.transform.SetParent(presenterObject.transform, false);
            HitEffectView effect = effectObject.AddComponent<HitEffectView>();
            effect.Initialize(null);
            GameObject reactionObject = new("Slide End Line Reaction");
            reactionObject.transform.SetParent(presenterObject.transform, false);
            JudgementLineReaction reaction = reactionObject.AddComponent<JudgementLineReaction>();
            reaction.Initialize(null, 8f, -5f, 0f);
            SetField(presenter, "hitEffectPool", new[] { effect });
            SetField(presenter, "lineReactionPool", new[] { reaction });
            SetField(session, "presenter", presenter);

        }

        private object CreateActiveNote(int endBehavior = 0, bool withView = false)
        {

            ChartNote note = JsonUtility.FromJson<ChartNote>(
                "{\"hitTime\":1.0,\"laneIndex\":2,\"noteType\":2," +
                $"\"slideEndBehavior\":{endBehavior}," +
                "\"slideNodes\":[{\"time\":2.0,\"laneIndex\":6},{\"time\":3.0,\"laneIndex\":5}]} ");
            RuntimeNoteView view = withView ? sessionObject.AddComponent<RuntimeNoteView>() : null;
            object result = Activator.CreateInstance(ActiveNoteType, note, view, true);
            SetProperty(result, "HasStarted", true);
            SetProperty(result, "StartGrade", JudgementGrade.Perfect);
            return result;

        }

        private object AddContact(int id, int lane, double time)
        {

            float normalizedX = PlayfieldGeometry.GetLaneCenterNormalized(lane, chart.LaneCount);
            object contact = Activator.CreateInstance(ContactStateType, id, normalizedX, time);
            IDictionary contacts = GetField<IDictionary>(session, "contacts");
            contacts.Add(id, contact);
            return contact;

        }

        private static void Record(object contact, int lane, double time, bool isDown = true)
        {

            Invoke(contact, "Record", PlayfieldGeometry.GetLaneCenterNormalized(lane, 8), time, isDown);

        }

        private bool Validate(double checkTime, double songTime, out bool pending)
        {

            object[] arguments = { activeNote, checkTime, songTime, false };
            bool success = (bool)Invoke(session, "TryValidateSlideCheck", arguments);
            pending = (bool)arguments[3];
            return success;

        }

        private void CreateHud()
        {

            GameObject hudObject = new("Slide Reward Test HUD");
            hudObject.transform.SetParent(sessionObject.transform, false);
            GameplayHud hud = hudObject.AddComponent<GameplayHud>();

            foreach (string fieldName in new[] { "scoreText", "comboText", "judgementText", "instrumentText" })
            {

                FieldInfo field = typeof(GameplayHud).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                GameObject textObject = new(fieldName, typeof(RectTransform));
                textObject.transform.SetParent(hudObject.transform, false);
                field.SetValue(hud, textObject.AddComponent(field.FieldType));

            }

            Invoke(hud, "Awake");
            SetField(session, "hud", hud);

        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {

            return target.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(target, arguments);

        }

        private static T GetField<T>(object target, string fieldName)
        {

            return (T)target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);

        }

        private static void SetField(object target, string fieldName, object value)
        {

            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);

        }

        private static T GetProperty<T>(object target, string propertyName)
        {

            return (T)target.GetType().GetProperty(propertyName).GetValue(target);

        }

        private static void SetProperty(object target, string propertyName, object value)
        {

            target.GetType().GetProperty(propertyName).SetValue(target, value);

        }

    }

}
