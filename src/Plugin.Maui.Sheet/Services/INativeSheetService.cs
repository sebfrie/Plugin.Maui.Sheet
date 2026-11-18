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

	/// <summary>Closes the most recently opened sheet of this service and completes once it is dismissed.</summary>
	Task CloseSheetAsync();
}
