namespace Clothesline.Core.Physics;

/// <summary>
/// 二阶阻尼弹簧运动解析解模拟器（无累积漂移、高刷自适应物理引擎）
/// </summary>
public sealed class SpringMotion
{
    private readonly double _frequency;
    private readonly double _decay;

    public double Position { get; set; }
    public double Velocity { get; set; }
    public double Target { get; set; }

    public bool Settled => Math.Abs(Position - Target) < 0.005 && Math.Abs(Velocity) < 0.01;

    /// <summary>
    /// 初始化二阶阻尼谐振子
    /// </summary>
    /// <param name="response">响应周期 (秒)，越小硬度越高、运动越快</param>
    /// <param name="damping">阻尼比 (0~1: 欠阻尼产生回弹震荡; 1: 临界阻尼无回弹)</param>
    /// <param name="initial">初始位置</param>
    public SpringMotion(double response, double damping, double initial)
    {
        double natural = 2 * Math.PI / Math.Max(0.01, response);
        _decay = natural * damping;
        _frequency = natural * Math.Sqrt(Math.Max(0.0001, 1 - damping * damping));
        Snap(initial);
    }

    /// <summary>
    /// 强制瞬间对齐到目标值，速度归零
    /// </summary>
    public void Snap(double value)
    {
        Position = Target = value;
        Velocity = 0;
    }

    /// <summary>
    /// 推进物理时间步长（秒）
    /// </summary>
    public void Step(double seconds)
    {
        if (seconds <= 0) return;

        double offset = Position - Target;
        double sine = Math.Sin(_frequency * seconds);
        double cosine = Math.Cos(_frequency * seconds);
        double envelope = Math.Exp(-_decay * seconds);
        double coefficient = (Velocity + _decay * offset) / _frequency;

        Position = Target + envelope * (offset * cosine + coefficient * sine);
        Velocity = envelope * (Velocity * cosine - (_decay * coefficient + _frequency * offset) * sine);

        if (Settled)
        {
            Snap(Target);
        }
    }
}
