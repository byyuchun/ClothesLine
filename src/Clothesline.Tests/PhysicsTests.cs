using Clothesline.Core.Physics;
using Xunit;

namespace Clothesline.Tests;

public class PhysicsTests
{
    [Fact]
    public void SpringMotion_Step_ShouldConvergeToTarget()
    {
        // 初始在 -300，目标在 0
        var spring = new SpringMotion(response: 0.42, damping: 0.82, initial: -300)
        {
            Target = 0
        };

        Assert.False(spring.Settled);

        // 模拟以 60 FPS 推进 1.5 秒
        double dt = 1.0 / 60.0;
        for (int i = 0; i < 90; i++)
        {
            spring.Step(dt);
        }

        // 验证收敛到目标 0
        Assert.True(spring.Settled);
        Assert.Equal(0, spring.Position, precision: 2);
        Assert.Equal(0, spring.Velocity, precision: 2);
    }
}
