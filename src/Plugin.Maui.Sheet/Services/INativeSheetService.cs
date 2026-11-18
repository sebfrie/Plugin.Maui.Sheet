using System.Diagnostics.CodeAnalysis;

namespace Plugin.Maui.Sheet;

/// <summary>
/// Opens and closes native sheets from view models, without declaring a <see cref="NativeSheet"/> in XAML.
/// Registered by <see cref="MauiAppBuilderExtensions.UseNativeSheet"/>.
/// </summary>
public interface INativeSheetService
{
	/// <summary>
	/// Resolves <typeparamref name="TView"/> from the service provider (or creates it with constructor injection),
	/// shows it in a native sheet on the active window and completes when that sheet has been dismissed.
	/// </summary>
	/// <typeparam name="TView">The MAUI view shown as sheet content.</typeparam>
	/// <param name="configure">Sets sheet properties such as <see cref="NativeSheet.Title"/> or <see cref="NativeSheet.Detents"/>.</param>
	/// <param name="cancellationToken">Closes the sheet when cancelled.</param>
	Task ShowSheetAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TView>(
		Action<NativeSheet>? configure = null, CancellationToken cancellationToken = default)
		where TView : View;

	/// <summary>
	/// Like <see cref="ShowSheetAsync{TView}"/>, and completes with the result passed to <see cref="CloseSheetAsync(object?)"/>,
	/// or <see langword="default"/> when the sheet was dismissed any other way.
	/// </summary>
	/// <typeparam name="TView">The MAUI view shown as sheet content.</typeparam>
	/// <typeparam name="TResult">The type of the result the sheet returns.</typeparam>
	/// <param name="configure">Sets sheet properties such as <see cref="NativeSheet.Title"/> or <see cref="NativeSheet.Detents"/>.</param>
	/// <param name="cancellationToken">Closes the sheet when cancelled.</param>
	/// <exception cref="InvalidCastException">The sheet was closed with a result that is not a <typeparamref name="TResult"/>.</exception>
	Task<TResult?> ShowSheetAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TView, TResult>(
		Action<NativeSheet>? configure = null, CancellationToken cancellationToken = default)
		where TView : View;

	/// <summary>Closes the most recently opened sheet of this service and completes once it is dismissed.</summary>
	Task CloseSheetAsync();

	/// <summary>
	/// Closes the most recently opened sheet of this service with a result for
	/// <see cref="ShowSheetAsync{TView, TResult}"/>, and completes once it is dismissed.
	/// </summary>
	Task CloseSheetAsync(object? result);
}
