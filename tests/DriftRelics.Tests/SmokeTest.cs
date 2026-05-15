using Xunit;

namespace DriftRelics.Tests;

public class SmokeTest
{
    [Fact]
    public void Runner_Is_Wired_Up()
    {
        Assert.Equal(4, 2 + 2);
    }
}
