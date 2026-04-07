using System;

namespace Pinta.Foundation;

partial class UserBlendOps
{
	[Serializable]
	public sealed class GlowBlendOp : UserBlendOp
	{
		public static string StaticName => "Glow";

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

			int fB = BlendChannel (lhs.B, rhs.B);
			int fG = BlendChannel (lhs.G, rhs.G);
			int fR = BlendChannel (lhs.R, rhs.R);

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

		private static int BlendChannel (int lhsC, int rhsC)
		{
			if (lhsC == 255) return 255;
			int i = (255 - lhsC) * 3;
			uint M = FoundationUtility.MasTable[i];
			uint A = FoundationUtility.MasTable[i + 1];
			uint S = FoundationUtility.MasTable[i + 2];
			int result = (int) (((rhsC * rhsC * M) + A) >> (int) S);
			return Math.Min (255, result);
		}
	}
}
