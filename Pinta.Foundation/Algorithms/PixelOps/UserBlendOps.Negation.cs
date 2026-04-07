using System;

namespace Pinta.Foundation;

partial class UserBlendOps
{
	[Serializable]
	public sealed class NegationBlendOp : UserBlendOp
	{
		public static string StaticName => "Negation";

		public override ColorBgra Apply (in ColorBgra lhs, in ColorBgra rhs)
			=> ApplyStatic (lhs, rhs);

		public static ColorBgra ApplyStatic (in ColorBgra lhs, in ColorBgra rhs)
		{
			int lhsA = lhs.A;
			int rhsA = rhs.A;
			int y = lhsA * (255 - rhsA) + 0x80;
			y = ((y >> 8) + y) >> 8;
			int totalA = y + rhsA;

			if (totalA == 0) return ColorBgra.FromUInt32 (0);

			int fB = 255 - Math.Abs (255 - lhs.B - rhs.B);
			int fG = 255 - Math.Abs (255 - lhs.G - rhs.G);
			int fR = 255 - Math.Abs (255 - lhs.R - rhs.R);

			int x = lhsA * rhsA + 0x80;
			x = ((x >> 8) + x) >> 8;
			int z = rhsA - x;

			int masIndex = totalA * 3;
			uint taM = FoundationUtility.MasTable[masIndex];
			uint taA = FoundationUtility.MasTable[masIndex + 1];
			uint taS = FoundationUtility.MasTable[masIndex + 2];

			uint b = (uint) ((((long) (lhs.B * y + rhs.B * z + fB * x) * taM) + taA) >> (int) taS);
			uint g = (uint) ((((long) (lhs.G * y + rhs.G * z + fG * x) * taM) + taA) >> (int) taS);
			uint r = (uint) ((((long) (lhs.R * y + rhs.R * z + fR * x) * taM) + taA) >> (int) taS);

			int a = lhsA * (255 - rhsA) + 0x80;
			a = ((a >> 8) + a) >> 8;
			a += rhsA;

			return ColorBgra.FromUInt32 (b + (g << 8) + (r << 16) + ((uint) a << 24));
		}
	}
}
