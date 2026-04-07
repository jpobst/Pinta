using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Pinta.Foundation;

/// <summary>Contains delegates for asynchronous event handlers.</summary>
/// <typeparam name="TSender">Type of event sender.</typeparam>
/// <typeparam name="TArgs">Type of event arguments.</typeparam>
public static class AsyncEventHandler<TSender, TArgs>
{
	/// <summary>
	/// Represents an asynchronous event handler that returns a result.
	/// </summary>
	public delegate Task<TResult> Returning<TResult> (TSender sender, TArgs args);

	/// <summary>
	/// Represents a simple asynchronous event handler that does not return a value.
	/// </summary>
	public delegate Task Simple (TSender sender, TArgs args);
}

/// <summary>
/// Provides extension methods for invoking asynchronous event handlers sequentially.
/// </summary>
public static class AsyncEventHandlerExtensions
{
	/// <summary>
	/// Decomposes the asynchronous multi-cast delegate
	/// and executes its constituent delegates one after another
	/// </summary>
	public static async Task<ImmutableArray<TResult>> InvokeSequential<TSender, TArgs, TResult> (
		this AsyncEventHandler<TSender, TArgs>.Returning<TResult> handlerBundle,
		TSender sender,
		TArgs args)
	{
		var invocationList = handlerBundle.GetInvocationList ();

		var builder = ImmutableArray.CreateBuilder<TResult> (invocationList.Length);

		foreach (
			var item
			in invocationList.Cast<AsyncEventHandler<TSender, TArgs>.Returning<TResult>> ()) {

			TResult itemResult = await item (sender, args);
			builder.Add (itemResult);
		}

		return builder.ToImmutable ();
	}

	/// <summary>
	/// Decomposes the asynchronous multi-cast delegate
	/// and executes its constituent delegates one after another
	/// </summary>
	public static async Task InvokeSequential<TSender, TArgs> (
		this AsyncEventHandler<TSender, TArgs>.Simple handlerBundle,
		TSender sender,
		TArgs args)
	{
		var invocationList =
			handlerBundle
			.GetInvocationList ()
			.Cast<AsyncEventHandler<TSender, TArgs>.Simple> ();

		foreach (var item in invocationList)
			await item (sender, args);
	}
}
