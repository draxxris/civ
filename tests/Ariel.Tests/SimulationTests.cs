using Ariel.Game;
using Xunit;

namespace Ariel.Tests;

public class SimulationTests
{
    [Fact]
    public void SameSeedSameMapAndHash()
    {
        Assert.Equal(TestGame.Sim(5).ComputeHash(), TestGame.Sim(5).ComputeHash());
        Assert.NotEqual(TestGame.Sim(5).ComputeHash(), TestGame.Sim(6).ComputeHash());
    }
}
