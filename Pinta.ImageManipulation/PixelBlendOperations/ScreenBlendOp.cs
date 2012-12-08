﻿/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) Rick Brewster, Tom Jackson, and past contributors.            //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Pinta.ImageManipulation.PixelBlendOperations
{
	public sealed class ScreenBlendOp : UserBlendOp
	{
		public static string StaticName { get { return "Screen"; } }
		public override ColorBgra Apply (ColorBgra lhs, ColorBgra rhs) { int lhsA; { lhsA = ((lhs).A); }; int rhsA; { rhsA = ((rhs).A); }; int y; { y = ((lhsA) * (255 - rhsA) + 0x80); y = ((((y) >> 8) + (y)) >> 8); }; int totalA = y + rhsA; uint ret; if (totalA == 0) { ret = 0; } else { int fB; int fG; int fR; { { fB = ((((rhs).B)) * (((lhs).B)) + 0x80); fB = ((((fB) >> 8) + (fB)) >> 8); }; fB = ((rhs).B) + ((lhs).B) - fB; }; { { fG = ((((rhs).G)) * (((lhs).G)) + 0x80); fG = ((((fG) >> 8) + (fG)) >> 8); }; fG = ((rhs).G) + ((lhs).G) - fG; }; { { fR = ((((rhs).R)) * (((lhs).R)) + 0x80); fR = ((((fR) >> 8) + (fR)) >> 8); }; fR = ((rhs).R) + ((lhs).R) - fR; }; int x; { x = ((lhsA) * (rhsA) + 0x80); x = ((((x) >> 8) + (x)) >> 8); }; int z = rhsA - x; int masIndex = totalA * 3; uint taM = masTable[masIndex]; uint taA = masTable[masIndex + 1]; uint taS = masTable[masIndex + 2]; uint b = (uint)(((((long)((((lhs).B * y) + ((rhs).B * z) + (fB * x)))) * taM) + taA) >> (int)taS); uint g = (uint)(((((long)((((lhs).G * y) + ((rhs).G * z) + (fG * x)))) * taM) + taA) >> (int)taS); uint r = (uint)(((((long)((((lhs).R * y) + ((rhs).R * z) + (fR * x)))) * taM) + taA) >> (int)taS); int a; { { a = ((lhsA) * (255 - (rhsA)) + 0x80); a = ((((a) >> 8) + (a)) >> 8); }; a += (rhsA); }; ret = b + (g << 8) + (r << 16) + ((uint)a << 24); }; return ColorBgra.FromUInt32 (ret); }
		public unsafe override void Apply (ColorBgra* src, ColorBgra* dst, int length) { while (length > 0) { int lhsA; { lhsA = ((*dst).A); }; int rhsA; { rhsA = ((*src).A); }; int y; { y = ((lhsA) * (255 - rhsA) + 0x80); y = ((((y) >> 8) + (y)) >> 8); }; int totalA = y + rhsA; uint ret; if (totalA == 0) { ret = 0; } else { int fB; int fG; int fR; { { fB = ((((*src).B)) * (((*dst).B)) + 0x80); fB = ((((fB) >> 8) + (fB)) >> 8); }; fB = ((*src).B) + ((*dst).B) - fB; }; { { fG = ((((*src).G)) * (((*dst).G)) + 0x80); fG = ((((fG) >> 8) + (fG)) >> 8); }; fG = ((*src).G) + ((*dst).G) - fG; }; { { fR = ((((*src).R)) * (((*dst).R)) + 0x80); fR = ((((fR) >> 8) + (fR)) >> 8); }; fR = ((*src).R) + ((*dst).R) - fR; }; int x; { x = ((lhsA) * (rhsA) + 0x80); x = ((((x) >> 8) + (x)) >> 8); }; int z = rhsA - x; int masIndex = totalA * 3; uint taM = masTable[masIndex]; uint taA = masTable[masIndex + 1]; uint taS = masTable[masIndex + 2]; uint b = (uint)(((((long)((((*dst).B * y) + ((*src).B * z) + (fB * x)))) * taM) + taA) >> (int)taS); uint g = (uint)(((((long)((((*dst).G * y) + ((*src).G * z) + (fG * x)))) * taM) + taA) >> (int)taS); uint r = (uint)(((((long)((((*dst).R * y) + ((*src).R * z) + (fR * x)))) * taM) + taA) >> (int)taS); int a; { { a = ((lhsA) * (255 - (rhsA)) + 0x80); a = ((((a) >> 8) + (a)) >> 8); }; a += (rhsA); }; ret = b + (g << 8) + (r << 16) + ((uint)a << 24); }; dst->Bgra = ret; ++dst; ++src; --length; } }
		public unsafe override void Apply (ColorBgra* lhs, ColorBgra* rhs, ColorBgra* dst, int length) { while (length > 0) { int lhsA; { lhsA = ((*lhs).A); }; int rhsA; { rhsA = ((*rhs).A); }; int y; { y = ((lhsA) * (255 - rhsA) + 0x80); y = ((((y) >> 8) + (y)) >> 8); }; int totalA = y + rhsA; uint ret; if (totalA == 0) { ret = 0; } else { int fB; int fG; int fR; { { fB = ((((*rhs).B)) * (((*lhs).B)) + 0x80); fB = ((((fB) >> 8) + (fB)) >> 8); }; fB = ((*rhs).B) + ((*lhs).B) - fB; }; { { fG = ((((*rhs).G)) * (((*lhs).G)) + 0x80); fG = ((((fG) >> 8) + (fG)) >> 8); }; fG = ((*rhs).G) + ((*lhs).G) - fG; }; { { fR = ((((*rhs).R)) * (((*lhs).R)) + 0x80); fR = ((((fR) >> 8) + (fR)) >> 8); }; fR = ((*rhs).R) + ((*lhs).R) - fR; }; int x; { x = ((lhsA) * (rhsA) + 0x80); x = ((((x) >> 8) + (x)) >> 8); }; int z = rhsA - x; int masIndex = totalA * 3; uint taM = masTable[masIndex]; uint taA = masTable[masIndex + 1]; uint taS = masTable[masIndex + 2]; uint b = (uint)(((((long)((((*lhs).B * y) + ((*rhs).B * z) + (fB * x)))) * taM) + taA) >> (int)taS); uint g = (uint)(((((long)((((*lhs).G * y) + ((*rhs).G * z) + (fG * x)))) * taM) + taA) >> (int)taS); uint r = (uint)(((((long)((((*lhs).R * y) + ((*rhs).R * z) + (fR * x)))) * taM) + taA) >> (int)taS); int a; { { a = ((lhsA) * (255 - (rhsA)) + 0x80); a = ((((a) >> 8) + (a)) >> 8); }; a += (rhsA); }; ret = b + (g << 8) + (r << 16) + ((uint)a << 24); }; dst->Bgra = ret; ++dst; ++lhs; ++rhs; --length; } }
		public static ColorBgra ApplyStatic (ColorBgra lhs, ColorBgra rhs) { int lhsA; { lhsA = ((lhs).A); }; int rhsA; { rhsA = ((rhs).A); }; int y; { y = ((lhsA) * (255 - rhsA) + 0x80); y = ((((y) >> 8) + (y)) >> 8); }; int totalA = y + rhsA; uint ret; if (totalA == 0) { ret = 0; } else { int fB; int fG; int fR; { { fB = ((((rhs).B)) * (((lhs).B)) + 0x80); fB = ((((fB) >> 8) + (fB)) >> 8); }; fB = ((rhs).B) + ((lhs).B) - fB; }; { { fG = ((((rhs).G)) * (((lhs).G)) + 0x80); fG = ((((fG) >> 8) + (fG)) >> 8); }; fG = ((rhs).G) + ((lhs).G) - fG; }; { { fR = ((((rhs).R)) * (((lhs).R)) + 0x80); fR = ((((fR) >> 8) + (fR)) >> 8); }; fR = ((rhs).R) + ((lhs).R) - fR; }; int x; { x = ((lhsA) * (rhsA) + 0x80); x = ((((x) >> 8) + (x)) >> 8); }; int z = rhsA - x; int masIndex = totalA * 3; uint taM = masTable[masIndex]; uint taA = masTable[masIndex + 1]; uint taS = masTable[masIndex + 2]; uint b = (uint)(((((long)((((lhs).B * y) + ((rhs).B * z) + (fB * x)))) * taM) + taA) >> (int)taS); uint g = (uint)(((((long)((((lhs).G * y) + ((rhs).G * z) + (fG * x)))) * taM) + taA) >> (int)taS); uint r = (uint)(((((long)((((lhs).R * y) + ((rhs).R * z) + (fR * x)))) * taM) + taA) >> (int)taS); int a; { { a = ((lhsA) * (255 - (rhsA)) + 0x80); a = ((((a) >> 8) + (a)) >> 8); }; a += (rhsA); }; ret = b + (g << 8) + (r << 16) + ((uint)a << 24); }; return ColorBgra.FromUInt32 (ret); }
		public override UserBlendOp CreateWithOpacity (int opacity) { return new ScreenBlendOpWithOpacity (opacity); }
		
		private sealed class ScreenBlendOpWithOpacity : UserBlendOp
		{
			private int opacity;
			
			private byte ApplyOpacity (byte a) { int r; { r = (a); }; { r = ((r) * (this.opacity) + 0x80); r = ((((r) >> 8) + (r)) >> 8); }; return (byte)r; }
			public static string StaticName { get { return "UserBlendOps." + "Screen" + "BlendOp.Name"; } }
			public override ColorBgra Apply (ColorBgra lhs, ColorBgra rhs) { int lhsA; { lhsA = ((lhs).A); }; int rhsA; { rhsA = ApplyOpacity ((rhs).A); }; int y; { y = ((lhsA) * (255 - rhsA) + 0x80); y = ((((y) >> 8) + (y)) >> 8); }; int totalA = y + rhsA; uint ret; if (totalA == 0) { ret = 0; } else { int fB; int fG; int fR; { { fB = ((((rhs).B)) * (((lhs).B)) + 0x80); fB = ((((fB) >> 8) + (fB)) >> 8); }; fB = ((rhs).B) + ((lhs).B) - fB; }; { { fG = ((((rhs).G)) * (((lhs).G)) + 0x80); fG = ((((fG) >> 8) + (fG)) >> 8); }; fG = ((rhs).G) + ((lhs).G) - fG; }; { { fR = ((((rhs).R)) * (((lhs).R)) + 0x80); fR = ((((fR) >> 8) + (fR)) >> 8); }; fR = ((rhs).R) + ((lhs).R) - fR; }; int x; { x = ((lhsA) * (rhsA) + 0x80); x = ((((x) >> 8) + (x)) >> 8); }; int z = rhsA - x; int masIndex = totalA * 3; uint taM = masTable[masIndex]; uint taA = masTable[masIndex + 1]; uint taS = masTable[masIndex + 2]; uint b = (uint)(((((long)((((lhs).B * y) + ((rhs).B * z) + (fB * x)))) * taM) + taA) >> (int)taS); uint g = (uint)(((((long)((((lhs).G * y) + ((rhs).G * z) + (fG * x)))) * taM) + taA) >> (int)taS); uint r = (uint)(((((long)((((lhs).R * y) + ((rhs).R * z) + (fR * x)))) * taM) + taA) >> (int)taS); int a; { { a = ((lhsA) * (255 - (rhsA)) + 0x80); a = ((((a) >> 8) + (a)) >> 8); }; a += (rhsA); }; ret = b + (g << 8) + (r << 16) + ((uint)a << 24); }; return ColorBgra.FromUInt32 (ret); }
			public unsafe override void Apply (ColorBgra* src, ColorBgra* dst, int length) { while (length > 0) { int lhsA; { lhsA = ((*dst).A); }; int rhsA; { rhsA = ApplyOpacity ((*src).A); }; int y; { y = ((lhsA) * (255 - rhsA) + 0x80); y = ((((y) >> 8) + (y)) >> 8); }; int totalA = y + rhsA; uint ret; if (totalA == 0) { ret = 0; } else { int fB; int fG; int fR; { { fB = ((((*src).B)) * (((*dst).B)) + 0x80); fB = ((((fB) >> 8) + (fB)) >> 8); }; fB = ((*src).B) + ((*dst).B) - fB; }; { { fG = ((((*src).G)) * (((*dst).G)) + 0x80); fG = ((((fG) >> 8) + (fG)) >> 8); }; fG = ((*src).G) + ((*dst).G) - fG; }; { { fR = ((((*src).R)) * (((*dst).R)) + 0x80); fR = ((((fR) >> 8) + (fR)) >> 8); }; fR = ((*src).R) + ((*dst).R) - fR; }; int x; { x = ((lhsA) * (rhsA) + 0x80); x = ((((x) >> 8) + (x)) >> 8); }; int z = rhsA - x; int masIndex = totalA * 3; uint taM = masTable[masIndex]; uint taA = masTable[masIndex + 1]; uint taS = masTable[masIndex + 2]; uint b = (uint)(((((long)((((*dst).B * y) + ((*src).B * z) + (fB * x)))) * taM) + taA) >> (int)taS); uint g = (uint)(((((long)((((*dst).G * y) + ((*src).G * z) + (fG * x)))) * taM) + taA) >> (int)taS); uint r = (uint)(((((long)((((*dst).R * y) + ((*src).R * z) + (fR * x)))) * taM) + taA) >> (int)taS); int a; { { a = ((lhsA) * (255 - (rhsA)) + 0x80); a = ((((a) >> 8) + (a)) >> 8); }; a += (rhsA); }; ret = b + (g << 8) + (r << 16) + ((uint)a << 24); }; dst->Bgra = ret; ++dst; ++src; --length; } }
			public unsafe override void Apply (ColorBgra* lhs, ColorBgra* rhs, ColorBgra* dst, int length) { while (length > 0) { int lhsA; { lhsA = ((*lhs).A); }; int rhsA; { rhsA = ApplyOpacity ((*rhs).A); }; int y; { y = ((lhsA) * (255 - rhsA) + 0x80); y = ((((y) >> 8) + (y)) >> 8); }; int totalA = y + rhsA; uint ret; if (totalA == 0) { ret = 0; } else { int fB; int fG; int fR; { { fB = ((((*rhs).B)) * (((*lhs).B)) + 0x80); fB = ((((fB) >> 8) + (fB)) >> 8); }; fB = ((*rhs).B) + ((*lhs).B) - fB; }; { { fG = ((((*rhs).G)) * (((*lhs).G)) + 0x80); fG = ((((fG) >> 8) + (fG)) >> 8); }; fG = ((*rhs).G) + ((*lhs).G) - fG; }; { { fR = ((((*rhs).R)) * (((*lhs).R)) + 0x80); fR = ((((fR) >> 8) + (fR)) >> 8); }; fR = ((*rhs).R) + ((*lhs).R) - fR; }; int x; { x = ((lhsA) * (rhsA) + 0x80); x = ((((x) >> 8) + (x)) >> 8); }; int z = rhsA - x; int masIndex = totalA * 3; uint taM = masTable[masIndex]; uint taA = masTable[masIndex + 1]; uint taS = masTable[masIndex + 2]; uint b = (uint)(((((long)((((*lhs).B * y) + ((*rhs).B * z) + (fB * x)))) * taM) + taA) >> (int)taS); uint g = (uint)(((((long)((((*lhs).G * y) + ((*rhs).G * z) + (fG * x)))) * taM) + taA) >> (int)taS); uint r = (uint)(((((long)((((*lhs).R * y) + ((*rhs).R * z) + (fR * x)))) * taM) + taA) >> (int)taS); int a; { { a = ((lhsA) * (255 - (rhsA)) + 0x80); a = ((((a) >> 8) + (a)) >> 8); }; a += (rhsA); }; ret = b + (g << 8) + (r << 16) + ((uint)a << 24); }; dst->Bgra = ret; ++dst; ++lhs; ++rhs; --length; } }
			public ScreenBlendOpWithOpacity (int opacity) { if (this.opacity < 0 || this.opacity > 255) { throw new ArgumentOutOfRangeException (); } this.opacity = opacity; }
		}
	}
}
