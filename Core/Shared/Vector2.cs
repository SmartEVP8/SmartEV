namespace Core.Shared
{
    /// <summary>
    /// A simple 2D vector struct for representing positions and performing basic vector operations.
    /// </summary>
    /// <param name="X">The first component of the vector</param>
    /// <param name="Y">The second component of the vector</param>
    public readonly record struct Vec2(double X, double Y)
    {
        /// <summary>
        /// Defines the subtraction operator for Vec2, allowing you to subtract one Vec2 from another.
        /// </summary>
        /// <param name="a">The first vector.</param>
        /// <param name="b">The second vector.</param>
        /// <returns>The result of subtracting vector b from vector a.</returns>
        public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);

        /// <summary>Computes the dot product given 2 vectors.</summary>
        /// <param name="other">Another vec2.</param>
        /// <returns>Scalar.</returns>
        public double Dot(Vec2 other) => (X * other.X) + (Y * other.Y);

        /// <summary>
        /// Gets the squared length of the vector, which is more efficient than computing
        /// the actual length because it avoids the square root operation.
        /// </summary>
        public double LengthSq => Dot(this);
    }
}
