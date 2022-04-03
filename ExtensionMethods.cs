using System.Drawing;
using System.Windows;

namespace Anonymity
{
    static class ExtensionMethods
    {
        public static Vector ToVector(this PointF operand) => new Vector(operand.X, operand.Y);
        public static PointF ToPointF(this Vector operand) => new PointF((float)operand.X, (float)operand.Y);
    }
}
