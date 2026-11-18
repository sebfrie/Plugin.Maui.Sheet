using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Plugin.Maui.Sheet;

/// <summary>Registers Plugin.Maui.Sheet with a <see cref="MauiAppBuilder"/>.</summary>
public static class MauiAppBuilderExtensions
{
	/// <summary>
	/// Registers the <see cref="NativeSheet"/> handler and <see cref="INativeSheetService"/>.
	/// </summary>
	public static MauiAppBuilder UseNativeSheet(this MauiAppBuilder builder)
	{
#if ANDROID || IOS || MACCATALYST || WINDOWS
		builder.ConfigureMauiHandlers(handlers => handlers.AddHandler<NativeSheet, NativeSheetHandler>());
#endif
		builder.Services.TryAddSingleton<INativeSheetService, NativeSheetService>();
		return builder;
	}
}
