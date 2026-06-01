namespace Vellum.AI.Fuzzy
{
    // Funzione di appartenenza di una fuzzy set: dato un valore crisp restituisce
    // il grado di appartenenza in [0,1]. Forme classiche (triangolo, trapezio,
    // spalle) costruite via factory. Struct immutabile: nessuna alloc, copiabile.
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

        // Triangolo: 0 fino ad a, sale al picco in b, scende a 0 in c.
        public static MembershipFunction Triangle(float a, float b, float c)
            => new MembershipFunction(Shape.Triangle, a, b, c, 0f);

        // Trapezio: 0 fino ad a, sale ad 1 in b, resta 1 fino a c, scende a 0 in d.
        public static MembershipFunction Trapezoid(float a, float b, float c, float d)
            => new MembershipFunction(Shape.Trapezoid, a, b, c, d);

        // Spalla sinistra: 1 fino ad a, scende a 0 in b (set "basso/poco").
        public static MembershipFunction LeftShoulder(float a, float b)
            => new MembershipFunction(Shape.LeftShoulder, a, b, 0f, 0f);

        // Spalla destra: 0 fino ad a, sale ad 1 in b e resta 1 (set "alto/molto").
        public static MembershipFunction RightShoulder(float a, float b)
            => new MembershipFunction(Shape.RightShoulder, a, b, 0f, 0f);

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
