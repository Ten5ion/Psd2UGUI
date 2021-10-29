namespace PDNWrapper
{
    internal enum MeasurementUnit
    {
        Pixel = 1,
        Inch = 2,
        Centimeter = 3
    }

    internal enum LayerBlendMode
    {
        Normal = 0,         // 正常
        Multiply = 1,       // 正片叠底
        Additive = 2,       // 线性减淡（添加）
        ColorBurn = 3,      // 颜色加深
        ColorDodge = 4,     // 颜色减淡
        Reflect = 5,
        Glow = 6,
        Overlay = 7,        // 叠加
        Difference = 8,
        Negation = 9,
        Lighten = 10,       // 变亮
        Darken = 11,        // 变暗
        Screen = 12,        // 滤色
        Xor = 13
    }
}
