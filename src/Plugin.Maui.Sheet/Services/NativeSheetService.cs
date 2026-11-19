using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Platform;

namespace Plugin.Maui.Sheet;

sealed class NativeSheetService(IServiceProvider services) : INativeSheetService
{
	readonly List<OpenSheet> openSheets = [];

	public Task ShowSheetAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TView>(
		Action<NativeSheet>? configure = null, CancellationToken cancellationToken = default)
		where TView : View =>
		ShowAsync<TView>(configure, cancellationToken);

	public async Task<TResult?> ShowSheetAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TView, TResult>(
		Action<NativeSheet>? configure = null, CancellationToken cancellationToken = default)
		where TView : View
	{
		var result = await ShowAsync<TView>(configure, cancellationToken);
		// A result of the wrong type is a bug in the caller: let the cast throw instead of hiding it.
		return result is null ? default : (TResult)result;
	}

	public Task CloseSheetAsync() => CloseSheetAsync(null);

	public Task CloseSheetAsync(object? result) =>
		MainThread.InvokeOnMainThreadAsync(() =>
		{
			if (openSheets.Count == 0)
				return Task.CompletedTask;
			var open = openSheets[^1];
			open.Result = result;
			open.Sheet.IsOpen = false;
			return open.Dismissed.Task;
		});

	public Task<NativeSheet> OpenAsync(View root, Action<NativeSheet>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(root);
		return MainThread.InvokeOnMainThreadAsync(async () =>
		{
			var open = Open(root, configure);
			await open.Opened.Task;
			return open.Sheet;
		});
	}

	Task<object?> ShowAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TView>(
		Action<NativeSheet>? configure, CancellationToken cancellationToken)
		where TView : View =>
		MainThread.InvokeOnMainThreadAsync(async () =>
		{
			var open = Open(ActivatorUtilities.GetServiceOrCreateInstance<TView>(services), configure);
			using (cancellationToken.Register(() => open.Sheet.IsOpen = false))
				await open.Dismissed.Task;
			return open.Result;
		});

	// Shows root in a new sheet on the active window. Once dismissed, the sheet disconnects and forgets itself.
	OpenSheet Open(View root, Action<NativeSheet>? configure)
	{
		var window = Application.Current?.Windows.FirstOrDefault(w => w.IsActivated) ?? Application.Current?.Windows.FirstOrDefault();
		if (window?.Page is not { } page || window.Handler?.MauiContext is not { } mauiContext)
			throw new InvalidOperationException("A sheet needs a window with a page; none is open.");

		var sheet = new NativeSheet { Content = root };
		configure?.Invoke(sheet);

		var open = new OpenSheet(sheet);
		sheet.Opened += OnOpened;
		sheet.Dismissed += OnDismissed;
		// The page gives the sheet its window and binding context; the sheet is not added to the page's layout.
		sheet.Parent = page;
		sheet.ToHandler(mauiContext);
		openSheets.Add(open);
		try
		{
			sheet.IsOpen = true;
		}
		catch
		{
			OnDismissed(sheet, EventArgs.Empty);
			throw;
		}
		return open;

		void OnOpened(object? sender, EventArgs e) => open.Opened.TrySetResult();

		void OnDismissed(object? sender, EventArgs e)
		{
			sheet.Opened -= OnOpened;
			sheet.Dismissed -= OnDismissed;
			openSheets.Remove(open);
			sheet.DisconnectHandlers();
			sheet.Parent = null;
			open.Opened.TrySetResult();
			open.Dismissed.TrySetResult();
		}
	}

	sealed class OpenSheet(NativeSheet sheet)
	{
		public NativeSheet Sheet { get; } = sheet;

		public TaskCompletionSource Opened { get; } = new();

		public TaskCompletionSource Dismissed { get; } = new();

		public object? Result { get; set; }
	}
}
