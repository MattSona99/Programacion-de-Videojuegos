namespace Vellum.AI.Fuzzy
{
    /// <summary>
    /// Membership function of a fuzzy set: given a crisp value it returns the
    /// membership degree in [0,1]. Classic shapes (triangle, trapezoid, shoulders)
    /// built via factory methods. Immutable struct: no allocations, copyable.
    /// </summary>
    public readonly struct MembershipFunction
    {
        private enum Shape { Triangle, Trapezoid, LeftShoulder, RightShoulder }

        private readonly Shape _shape;
        private readonly float _a, _b, _c, _d;

        private MembershipFunction(Shape shape, float a, float b, float c, float d)
        {
            _shape = shape;
            _a = a; _b = b; _c = c; _d = d;
        }

        /// <summary>Triangle: 0 up to a, rises to the peak at b, falls back to 0 at c.</summary>
        public static MembershipFunction Triangle(float a, float b, float c)
            => new MembershipFunction(Shape.Triangle, a, b, c, 0f);

        /// <summary>Trapezoid: 0 up to a, rises to 1 at b, stays 1 until c, falls to 0 at d.</summary>
        public static MembershipFunction Trapezoid(float a, float b, float c, float d)
            => new MembershipFunction(Shape.Trapezoid, a, b, c, d);

        /// <summary>Left shoulder: 1 up to a, falls to 0 at b (a "low/few" set).</summary>
        public static MembershipFunction LeftShoulder(float a, float b)
            => new MembershipFunction(Shape.LeftShoulder, a, b, 0f, 0f);

        /// <summary>Right shoulder: 0 up to a, rises to 1 at b and stays 1 (a "high/many" set).</summary>
        public static MembershipFunction RightShoulder(float a, float b)
            => new MembershipFunction(Shape.RightShoulder, a, b, 0f, 0f);

        /// <summary>Returns the membership degree in [0,1] of the crisp value <paramref name="x"/>.</summary>
        public float Evaluate(float x)
        {
            switch (_shape)
            {
                case Shape.Triangle:
                    if (x <= _a || x >= _c) return 0f;
                    if (x == _b) return 1f;
                    return x < _b ? (x - _a) / (_b - _a) : (_c - x) / (_c - _b);

                case Shape.Trapezoid:
                    if (x <= _a || x >= _d) return 0f;
                    if (x >= _b && x <= _c) return 1f;
                    return x < _b ? (x - _a) / (_b - _a) : (_d - x) / (_d - _c);

                case Shape.LeftShoulder:
                    if (x <= _a) return 1f;
                    if (x >= _b) return 0f;
                    return (_b - x) / (_b - _a);

                case Shape.RightShoulder:
                    if (x <= _a) return 0f;
                    if (x >= _b) return 1f;
                    return (x - _a) / (_b - _a);
            }
            return 0f;
        }
    }
}
