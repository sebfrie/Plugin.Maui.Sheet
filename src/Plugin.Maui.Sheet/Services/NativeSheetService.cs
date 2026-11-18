using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Platform;

namespace Plugin.Maui.Sheet;

sealed class NativeSheetService(IServiceProvider services) : INativeSheetService
{
	readonly List<(NativeSheet Sheet, Task Dismissed)> openSheets = [];

	public Task ShowSheetAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TView>(
		Action<NativeSheet>? configure = null, CancellationToken cancellationToken = default)
		where TView : View =>
		MainThread.InvokeOnMainThreadAsync(async () =>
		{
			var window = Application.Current?.Windows.FirstOrDefault(w => w.IsActivated) ?? Application.Current?.Windows.FirstOrDefault();
			if (window?.Page is not { } page || window.Handler?.MauiContext is not { } mauiContext)
				throw new InvalidOperationException("A sheet needs a window with a page; none is open.");

			var sheet = new NativeSheet { Content = ActivatorUtilities.GetServiceOrCreateInstance<TView>(services) };
			configure?.Invoke(sheet);

			var dismissed = new TaskCompletionSource();
			sheet.Dismissed += (_, _) => dismissed.TrySetResult();
			var entry = (sheet, dismissed.Task);

			// The page gives the sheet its window and binding context; the sheet is not added to the page's layout.
			sheet.Parent = page;
			sheet.ToHandler(mauiContext);
			openSheets.Add(entry);
			try
			{
				using (cancellationToken.Register(() => sheet.IsOpen = false))
				{
					sheet.IsOpen = true;
					await dismissed.Task;
				}
			}
			finally
			{
				openSheets.Remove(entry);
				sheet.DisconnectHandlers();
				sheet.Parent = null;
			}
		});

	public Task CloseSheetAsync() =>
		MainThread.InvokeOnMainThreadAsync(() =>
		{
			if (openSheets.Count == 0)
				return Task.CompletedTask;
			var (sheet, dismissed) = openSheets[^1];
			sheet.IsOpen = false;
			return dismissed;
		});
}
