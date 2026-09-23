using Prime.Domain.Common;
using Shouldly;
using Xunit;

namespace Prime.Domain.Tests.Common;

public class EntityTests
{
    private sealed class TestEntity : Entity;

    [Fact]
    public void NewEntity_HasNonEmptyId()
    {
        var entity = new TestEntity();

        entity.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void TwoEntities_WithDifferentIds_AreNotEqual()
    {
        var a = new TestEntity();
        var b = new TestEntity();

        a.ShouldNotBe(b);
    }
}
