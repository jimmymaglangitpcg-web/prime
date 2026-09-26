using Prime.Domain.DomainServices;
using Shouldly;
using Xunit;

namespace Prime.Domain.Tests.DomainServices;

/// <summary>Remittance totals by mode (docs/analysis/collection.md §4.7): change comes out of the modes that allow it. DEMO amounts.</summary>
public class TenderNettingTests
{
    private static readonly Guid Cash = Guid.NewGuid();
    private static readonly Guid Check = Guid.NewGuid();

    [Fact]
    public void Change_IsTakenFromCash()
    {
        var net = TenderNetting.Net([new(Check, 1_000m, false), new(Cash, 1_000m, true)], 200m);

        net.ShouldBe([(Check, 1_000m), (Cash, 800m)]);
    }

    [Fact]
    public void NoChange_LeavesTendersAsTheyAre_AndMergesTheSameMode()
    {
        var net = TenderNetting.Net([new(Cash, 300m, true), new(Cash, 200.50m, true)], 0m);

        net.ShouldBe([(Cash, 500.50m)]);
    }

    [Fact]
    public void Change_SpreadsOverSeveralCashTenders_InOrder()
    {
        var net = TenderNetting.Net([new(Cash, 100m, true), new(Cash, 100m, true), new(Check, 50m, false)], 150m);

        net.ShouldBe([(Cash, 50m), (Check, 50m)]);
    }

    [Fact]
    public void ChangeBeyondCash_OrNegative_IsRefused()
    {
        Should.Throw<InvalidOperationException>(() => TenderNetting.Net([new(Check, 1_000m, false)], 1m));
        Should.Throw<ArgumentOutOfRangeException>(() => TenderNetting.Net([new(Cash, 1m, true)], -1m));
    }
}
