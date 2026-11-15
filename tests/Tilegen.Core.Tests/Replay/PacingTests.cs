using CsCheck;
using Tilegen.Core.Replay;
using Xunit;

namespace Tilegen.Core.Tests.Replay;

/// <summary>
/// <see cref="Pacing.Steps"/>: how many run steps an elapsed time is worth at the frame interval
/// (design D10), and the checks on its arguments.
/// </summary>
public class PacingTests
{
    private static readonly TimeSpan Interval = Playback.FrameInterval;

    [Fact]
    public void Zero_elapsed_time_gives_no_steps_and_keeps_the_carry()
    {
        var carry = TimeSpan.FromMilliseconds(100);

        var paced = Pacing.Steps(carry, TimeSpan.Zero);

        Assert.Equal(new Paced(0, carry), paced);
    }

    [Fact]
    public void Less_than_an_interval_gives_no_steps_and_carries_the_time()
    {
        var paced = Pacing.Steps(TimeSpan.Zero, Interval - TimeSpan.FromTicks(1));

        Assert.Equal(new Paced(0, Interval - TimeSpan.FromTicks(1)), paced);
    }

    [Fact]
    public void Exactly_one_interval_gives_one_step_and_no_carry()
    {
        var paced = Pacing.Steps(TimeSpan.Zero, Interval);

        Assert.Equal(new Paced(1, TimeSpan.Zero), paced);
    }

    [Fact]
    public void Two_and_a_half_intervals_give_two_steps_and_half_an_interval()
    {
        var paced = Pacing.Steps(TimeSpan.Zero, Interval * 2.5);

        Assert.Equal(new Paced(2, Interval / 2), paced);
    }

    [Fact]
    public void An_existing_carry_adds_up_with_the_elapsed_time()
    {
        var carry = TimeSpan.FromMilliseconds(200);

        var paced = Pacing.Steps(carry, TimeSpan.FromMilliseconds(100));

        Assert.Equal(new Paced(1, TimeSpan.FromMilliseconds(50)), paced);
    }

    [Fact]
    public void A_long_elapsed_time_takes_every_step_it_is_owed_at_once()
    {
        var paced = Pacing.Steps(TimeSpan.Zero, TimeSpan.FromMinutes(1));

        Assert.Equal(new Paced(240, TimeSpan.Zero), paced);
    }

    [Fact]
    public void A_negative_carry_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Pacing.Steps(TimeSpan.FromTicks(-1), TimeSpan.Zero));
    }

    [Fact]
    public void A_carry_of_a_whole_interval_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Pacing.Steps(Interval, TimeSpan.Zero));
    }

    [Fact]
    public void A_negative_elapsed_time_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Pacing.Steps(TimeSpan.Zero, TimeSpan.FromTicks(-1)));
    }

    [Fact]
    public void An_elapsed_time_worth_more_steps_than_an_int_holds_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Pacing.Steps(TimeSpan.Zero, TimeSpan.MaxValue));
    }

    [Fact]
    public void Splitting_elapsed_time_over_updates_gives_the_same_total_steps_and_final_carry_as_one_update()
    {
        var ticks = Interval.Ticks;

        // Elapsed times up to a few intervals, with whole intervals and one tick either side of
        // them picked often.
        var elapsed = Gen.Frequency<long>(
                (4, Gen.Long[0, 3 * ticks]),
                (1, Gen.Int[0, 4].Select(n => n * ticks)),
                (1, Gen.Int[1, 4].Select(n => (n * ticks) - 1)),
                (1, Gen.Int[0, 4].Select(n => (n * ticks) + 1)))
            .Select(TimeSpan.FromTicks);

        Gen.Select(Gen.Long[0, ticks - 1].Select(TimeSpan.FromTicks), elapsed.Array[1, 12]).Sample(
            (startCarry, parts) =>
            {
                var total = parts.Aggregate(TimeSpan.Zero, (sum, part) => sum + part);
                var once = Pacing.Steps(startCarry, total);

                var carry = startCarry;
                var steps = 0;
                foreach (var part in parts)
                {
                    var paced = Pacing.Steps(carry, part);
                    steps += paced.Steps;
                    carry = paced.Carry;
                }

                Assert.Equal(once, new Paced(steps, carry));
            },
            iter: 1000);
    }
}
