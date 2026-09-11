using Raisin.Audio;
using Xunit;

namespace Raisin.Audio.Tests;

/// <summary>The stored form of a spoken alert: one wording for one session, one for several.</summary>
public class SpeechPhraseTests
{
    [Fact]
    public void BothHalvesRoundTrip()
    {
        var phrase = SpeechPhrase.Parse("{subject} needs you||{count} sessions need you");

        Assert.Equal("{subject} needs you", phrase.Singular);
        Assert.Equal("{count} sessions need you", phrase.Plural);
        Assert.Equal("{subject} needs you||{count} sessions need you", phrase.Compose());
    }

    [Fact]
    public void APhraseWithNoPluralIsJustTheOneWording()
    {
        var phrase = SpeechPhrase.Parse("someone needs you");

        Assert.Equal("someone needs you", phrase.Singular);
        Assert.Equal("", phrase.Plural);
        Assert.Equal("someone needs you", phrase.Compose());   // no trailing separator
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankIsEmpty(string? text)
    {
        Assert.True(SpeechPhrase.Parse(text).IsEmpty);
    }

    [Fact]
    public void ASeparatorInsideThePluralDoesNotSplitAgain()
    {
        var phrase = SpeechPhrase.Parse("one||two||three");

        Assert.Equal("one", phrase.Singular);
        Assert.Equal("two||three", phrase.Plural);
    }
}

/// <summary>Turning a window's worth of events into one thing to say.</summary>
public class SpeechBatchTests
{
    private static readonly SpeechPhrase Waiting =
        new("{subject} needs you", "{count} sessions need you");

    private static SpeechRequest From(string session, SpeechPhrase? phrase = null) =>
        new(phrase ?? Waiting, session, Tokens("RaisinTerminal"));

    private static IReadOnlyDictionary<string, string> Tokens(string project) =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["project"] = project };

    [Fact]
    public void OneSession_IsNamed()
    {
        Assert.Equal("RT 2 needs you.", SpeechBatch.Compose([From("RT 2")]));
    }

    [Fact]
    public void SeveralSessions_AreCounted()
    {
        var text = SpeechBatch.Compose([From("RT 2"), From("RT 3"), From("SR 1")]);

        Assert.Equal("3 sessions need you.", text);
    }

    [Fact]
    public void TheSameSessionTwiceInOneWindow_IsOneEvent()
    {
        // A status that flickers must not inflate the count.
        Assert.Equal("RT 2 needs you.", SpeechBatch.Compose([From("RT 2"), From("RT 2")]));
    }

    [Fact]
    public void DifferentEvents_EachGetTheirOwnSentence()
    {
        var finished = new SpeechPhrase("{subject} finished", "{count} sessions finished");

        var text = SpeechBatch.Compose([From("RT 2"), From("RT 3", finished)]);

        Assert.Equal("RT 2 needs you. RT 3 finished.", text);
    }

    [Fact]
    public void WithNoPluralWording_EachIsSaidSeparately()
    {
        // Losing the detail would be worse than a longer sentence.
        var noPlural = new SpeechPhrase("{subject} needs you", "");

        var text = SpeechBatch.Compose([From("RT 2", noPlural), From("RT 3", noPlural)]);

        Assert.Equal("RT 2 needs you. RT 3 needs you.", text);
    }

    [Fact]
    public void ThePluralCanNameTheSessionsToo()
    {
        var listing = new SpeechPhrase("{subject} needs you", "{subject} need you");

        var requests = new[] { From("RT 2"), From("RT 3"), From("SR 1") }
            .Select(r => r with { Phrase = listing });

        var text = SpeechBatch.Compose(requests);

        Assert.Equal("RT 2, RT 3 and SR 1 need you.", text);
    }

    [Fact]
    public void ClaudesGlyphIsNotReadOutAsTheWordAsterisk()
    {
        // Claude titles its sessions "✳ SR2 4"; U+2733 is EIGHT SPOKED ASTERISK, and a
        // synthesiser says "asterisk" before every session name.
        var text = SpeechBatch.Compose([From("✳ SR2 4")]);

        Assert.Equal("SR2 4 needs you.", text);
    }

    [Theory]
    [InlineData("✳ SR2 4", "SR2 4")]
    [InlineData("* build", "build")]
    [InlineData("  RT 2  ", "RT 2")]
    [InlineData("(dev)", "dev")]
    [InlineData("RT-2", "RT-2")]           // punctuation inside a name is left alone
    [InlineData("✳", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void DecorationIsStrippedFromEitherEndOfAName(string? name, string expected)
    {
        Assert.Equal(expected, SpeechBatch.SpokenName(name));
    }

    [Fact]
    public void ListedNamesAreCleanedToo()
    {
        var listing = new SpeechPhrase("{subject} needs you", "{subject} need you");

        var requests = new[] { From("✳ RT 2"), From("✳ RT 3") }
            .Select(r => r with { Phrase = listing });

        Assert.Equal("RT 2 and RT 3 need you.", SpeechBatch.Compose(requests));
    }

    [Fact]
    public void EmptyPhrasesContributeNothing()
    {
        Assert.Equal("", SpeechBatch.Compose([From("RT 2", SpeechPhrase.Empty)]));
        Assert.Equal("", SpeechBatch.Compose([]));
    }

    [Fact]
    public void ARequestThatCountedForItself_FillsThatNumber()
    {
        // The startup tally: one request, seven sessions. Left to the batch's own counting this
        // would come out singular - "1 session open" - which is the wrong number and the wrong
        // half of the phrase.
        var phrase = new SpeechPhrase("{count} session open", "{count} sessions open");

        var text = SpeechBatch.Compose([new SpeechRequest(phrase, null, null, Count: 7)]);

        Assert.Equal("7 sessions open.", text);
    }

    [Fact]
    public void ARequestCountingOneSession_StaysSingular()
    {
        var phrase = new SpeechPhrase("{count} session open", "{count} sessions open");

        var text = SpeechBatch.Compose([new SpeechRequest(phrase, null, null, Count: 1)]);

        Assert.Equal("1 session open.", text);
    }

    [Fact]
    public void PerSessionAlerts_StillCountThemselves()
    {
        // The default count of one is what makes the ordinary path unchanged: three sessions
        // each speaking for themselves are still three.
        var text = SpeechBatch.Compose([From("RT 2"), From("RT 3"), From("SR 1")]);

        Assert.Contains("3 sessions need you", text);
    }

    [Fact]
    public void PlaceholdersAreFilled()
    {
        var phrase = new SpeechPhrase("{project}: {subject} needs you", "");

        Assert.Equal("RaisinTerminal: RT 2 needs you.", SpeechBatch.Compose([From("RT 2", phrase)]));
    }

    [Fact]
    public void AnUnfilledPlaceholderDoesNotLeaveDoubledSpaces()
    {
        var phrase = new SpeechPhrase("{project} {subject} needs you", "");

        var text = SpeechBatch.Compose([new SpeechRequest(phrase, "RT 2", null)]);

        Assert.Equal("RT 2 needs you.", text);
    }

    [Theory]
    [InlineData("RT 2 needs you", "RT 2 needs you.")]
    [InlineData("RT 2 needs you.", "RT 2 needs you.")]
    [InlineData("is anyone there?", "is anyone there?")]
    public void SentencesEndInPunctuation(string template, string expected)
    {
        // A synthesiser runs two clauses together without it.
        Assert.Equal(expected, SpeechBatch.Fill(template, "RT 2", null, 1));
    }
}

/// <summary>
/// The engine contract the rest of the stack relies on. These pin behaviour that a UI freeze
/// taught us the hard way: auditioning a voice while a long narration was playing locked the
/// window, because SelectVoice and the Volume and Rate setters block while speech is in
/// progress and the call was being made on the UI thread.
/// </summary>
public class SpeechEngineContractTests
{
    [Fact]
    public void SpeakIsDeclaredToNotBlockTheCaller()
    {
        // A guard on the shape rather than the behaviour: the real engine needs SAPI and a
        // long utterance to demonstrate the freeze, neither of which belongs in a unit test.
        // What can be checked is that nothing on the UI path calls the synthesiser directly.
        var source = File.ReadAllText(RepoFile(@"Raisin.Audio\Speech\SpeechEngine.cs"));

        var speakBody = source[source.IndexOf("public long Speak(")..source.IndexOf("private void SpeakOffThread")];

        Assert.Contains("Task.Run", speakBody);
        Assert.DoesNotContain("SelectVoice", speakBody);
        Assert.DoesNotContain(".Volume =", speakBody);
        Assert.DoesNotContain(".Rate =", speakBody);
    }

    [Fact]
    public void StopIsAlsoOffTheCallersThread()
    {
        var source = File.ReadAllText(RepoFile(@"Raisin.Audio\Speech\SpeechEngine.cs"));

        var stopBody = source[source.IndexOf("public void Stop()")..source.IndexOf("private static void SelectVoice")];

        Assert.Contains("Task.Run", stopBody);
    }

    [Fact]
    public void SpeakingCancelsWhatIsAlreadyBeingSaid()
    {
        // Otherwise an audition queues behind a screenful of narration.
        var source = File.ReadAllText(RepoFile(@"Raisin.Audio\Speech\SpeechEngine.cs"));

        var body = source[source.IndexOf("private void SpeakOffThread")..source.IndexOf("public void Stop()")];

        Assert.True(body.IndexOf("SpeakAsyncCancelAll") < body.IndexOf("SpeakAsync(prompt)"),
            "SpeakOffThread must cancel before it queues, or a new utterance waits out the old.");
    }

    [Fact]
    public void AnUtteranceIsNumberedBeforeItIsSpoken()
    {
        // The caller has to be handed the id before the utterance can report anything, or the
        // first progress event beats the assignment and is taken for someone else's.
        var source = File.ReadAllText(RepoFile(@"Raisin.Audio\Speech\SpeechEngine.cs"));

        var speakBody = source[source.IndexOf("public long Speak(")..source.IndexOf("private void SpeakOffThread")];

        Assert.True(speakBody.IndexOf("Interlocked.Increment") < speakBody.IndexOf("Task.Run"),
            "Speak must number the utterance before handing it off, and return that number.");
    }

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "RaisinLibraries.slnx")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, relative);
    }
}

/// <summary>
/// The batching policy: collect over a window, say it once, and never talk over yourself.
/// </summary>
public class SpeechAnnouncerTests
{
    private sealed class FakeEngine : ISpeechEngine
    {
        public bool IsSpeaking { get; set; }
        public IReadOnlyList<string> AvailableVoices => ["Test"];
        public List<string> Spoken { get; } = [];

        public long Speak(string text, string? voice, double volume, double rate)
        {
            Spoken.Add(text);
            return Spoken.Count;
        }
        public void Stop() => IsSpeaking = false;

        public event Action<long, int, int>? Progress;
        public event Action<long>? Finished;
        public void RaiseProgress(int offset, int length, long utterance = 1) =>
            Progress?.Invoke(utterance, offset, length);

        public void RaiseFinished(long utterance = 1) => Finished?.Invoke(utterance);
    }

    /// <summary>Holds the scheduled callback so a test decides when the window closes.</summary>
    private sealed class ManualClock
    {
        private readonly List<Action> _pending = [];

        public int Scheduled => _pending.Count;

        public void Schedule(TimeSpan _, Action action) => _pending.Add(action);

        public void CloseWindow()
        {
            var due = _pending.ToList();
            _pending.Clear();
            foreach (var action in due) action();
        }
    }

    private static readonly SpeechPhrase Waiting =
        new("{subject} needs you", "{count} sessions need you");

    private static (SpeechAnnouncer Announcer, FakeEngine Engine, ManualClock Clock) Build()
    {
        var engine = new FakeEngine();
        var clock = new ManualClock();
        var announcer = new SpeechAnnouncer(
            engine,
            () => new SpeechOptions(null, 1, 0, TimeSpan.FromSeconds(1.5)),
            clock.Schedule);

        return (announcer, engine, clock);
    }

    [Fact]
    public void NothingIsSaidBeforeTheWindowCloses()
    {
        var (announcer, engine, _) = Build();

        announcer.Announce(new SpeechRequest(Waiting, "RT 2", null));

        Assert.Empty(engine.Spoken);
    }

    [Fact]
    public void TheWindowClosingSaysItOnce()
    {
        var (announcer, engine, clock) = Build();

        announcer.Announce(new SpeechRequest(Waiting, "RT 2", null));
        clock.CloseWindow();

        Assert.Equal(["RT 2 needs you."], engine.Spoken);
    }

    [Fact]
    public void ABurstBecomesOneSentence()
    {
        // The whole point: five sessions at once is one announcement, not five.
        var (announcer, engine, clock) = Build();

        foreach (var name in new[] { "RT 2", "RT 3", "SR 1" })
            announcer.Announce(new SpeechRequest(Waiting, name, null));

        clock.CloseWindow();

        Assert.Equal(["3 sessions need you."], engine.Spoken);
    }

    [Fact]
    public void ABurstOpensOnlyOneWindow()
    {
        var (announcer, _, clock) = Build();

        announcer.Announce(new SpeechRequest(Waiting, "RT 2", null));
        announcer.Announce(new SpeechRequest(Waiting, "RT 3", null));

        Assert.Equal(1, clock.Scheduled);
    }

    [Fact]
    public void ArrivingMidUtterance_WaitsForTheNextWindow()
    {
        var (announcer, engine, clock) = Build();
        engine.IsSpeaking = true;

        announcer.Announce(new SpeechRequest(Waiting, "RT 2", null));
        clock.CloseWindow();

        Assert.Empty(engine.Spoken);         // held rather than spoken over

        engine.IsSpeaking = false;
        clock.CloseWindow();

        Assert.Equal(["RT 2 needs you."], engine.Spoken);
    }

    [Fact]
    public void NothingIsHeldOverAfterASuccessfulFlush()
    {
        var (announcer, engine, clock) = Build();

        announcer.Announce(new SpeechRequest(Waiting, "RT 2", null));
        clock.CloseWindow();
        clock.CloseWindow();

        Assert.Single(engine.Spoken);
    }

    [Fact]
    public void ASecondBurstAfterTheFirstIsItsOwnSentence()
    {
        var (announcer, engine, clock) = Build();

        announcer.Announce(new SpeechRequest(Waiting, "RT 2", null));
        clock.CloseWindow();

        announcer.Announce(new SpeechRequest(Waiting, "RT 3", null));
        clock.CloseWindow();

        Assert.Equal(["RT 2 needs you.", "RT 3 needs you."], engine.Spoken);
    }

    [Fact]
    public void AWithdrawnAlertIsNeverSaid()
    {
        // The point of the whole thing: answered inside its own window, so it never reaches
        // the speakers at all.
        var (announcer, engine, clock) = Build();

        announcer.Announce(new SpeechRequest(Waiting, "RT 2", null));
        announcer.CancelPending("RT 2");
        clock.CloseWindow();

        Assert.Empty(engine.Spoken);
    }

    [Fact]
    public void WithdrawingOneSessionLeavesTheOthers()
    {
        var (announcer, engine, clock) = Build();

        foreach (var name in new[] { "RT 2", "RT 3", "SR 1" })
            announcer.Announce(new SpeechRequest(Waiting, name, null));

        announcer.CancelPending("RT 3");
        clock.CloseWindow();

        Assert.Equal(["2 sessions need you."], engine.Spoken);
    }

    [Fact]
    public void WithdrawingTheLastOneLeavesNothingScheduled()
    {
        // The window still closes; it just finds nothing, and must not hold itself open and
        // swallow the next alert.
        var (announcer, engine, clock) = Build();

        announcer.Announce(new SpeechRequest(Waiting, "RT 2", null));
        announcer.CancelPending("RT 2");
        clock.CloseWindow();

        announcer.Announce(new SpeechRequest(Waiting, "RT 3", null));
        clock.CloseWindow();

        Assert.Equal(["RT 3 needs you."], engine.Spoken);
    }

    [Fact]
    public void WithdrawingAfterTheWindowClosedIsTooLate()
    {
        // Honest about the limit: once it has been said, it has been said.
        var (announcer, engine, clock) = Build();

        announcer.Announce(new SpeechRequest(Waiting, "RT 2", null));
        clock.CloseWindow();
        announcer.CancelPending("RT 2");

        Assert.Equal(["RT 2 needs you."], engine.Spoken);
    }

    [Fact]
    public void WithdrawingWhileTalkingStillTakesItBack()
    {
        // Held over because an utterance was in progress, which is the longest a request ever
        // sits waiting - so it is also the longest it stays withdrawable.
        var (announcer, engine, clock) = Build();
        engine.IsSpeaking = true;

        announcer.Announce(new SpeechRequest(Waiting, "RT 2", null));
        clock.CloseWindow();
        announcer.CancelPending("RT 2");

        engine.IsSpeaking = false;
        clock.CloseWindow();

        Assert.Empty(engine.Spoken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void WithdrawingNothingInParticularWithdrawsNothing(string? session)
    {
        var (announcer, engine, clock) = Build();

        announcer.Announce(new SpeechRequest(Waiting, "RT 2", null));
        announcer.CancelPending(session);
        clock.CloseWindow();

        Assert.Equal(["RT 2 needs you."], engine.Spoken);
    }

    [Fact]
    public void AnEmptyPhraseNeverOpensAWindow()
    {
        var (announcer, _, clock) = Build();

        announcer.Announce(new SpeechRequest(SpeechPhrase.Empty, "RT 2", null));

        Assert.Equal(0, clock.Scheduled);
    }
}
