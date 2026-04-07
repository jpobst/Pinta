using System;

namespace Pinta.Foundation;

/// <summary>
/// Abstract base class that all "user" blend ops derive from.
/// </summary>
[Serializable]
public abstract class UserBlendOp : BinaryPixelOp
{
	public override string ToString ()
	{
		return FoundationUtility.GetStaticName (GetType ());
	}
}
