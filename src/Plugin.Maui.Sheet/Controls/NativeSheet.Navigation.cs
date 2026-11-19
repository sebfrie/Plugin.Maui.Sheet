using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Plugin.Maui.Sheet;

// In-sheet navigation: a stack of MAUI views shown one at a time in the same native sheet, each with its own header.
public partial class NativeSheet
{
	/// <summary>Attached property for the header items of a view in the <see cref="NavigationStack"/>.</summary>
	public static readonly BindableProperty ToolbarItemsProperty =
		BindableProperty.CreateAttached("ToolbarItems", typeof(IList<ToolbarItem>), typeof(NativeSheet), null,
			defaultValueCreator: view => new SheetToolbarItems(view as Element));

	readonly List<View> pushedViews = [];
	readonly SemaphoreSlim navigationGate = new(1, 1);
	bool headerUpdatePending;

	/// <summary>Raised when the user goes back a level (back button, iOS edge swipe, Android system back); not for <see cref="PopAsync"/>.</summary>
	public event EventHandler<SheetPoppedEventArgs>? Popped;

	/// <summary>Gets the views in the sheet, root (<see cref="Content"/>) first. The last one is shown.</summary>
	public IReadOnlyList<View> NavigationStack => PresentedContent is { } root ? (View[])[root, .. pushedViews] : [.. pushedViews];

	/// <summary>Gets the header title of a view in the <see cref="NavigationStack"/>.</summary>
	public static string? GetTitle(BindableObject view) => (string?)view.GetValue(TitleProperty);

	/// <summary>Sets the header title shown while <paramref name="view"/> is the top of the <see cref="NavigationStack"/>.</summary>
	public static void SetTitle(BindableObject view, string? value) => view.SetValue(TitleProperty, value);

	/// <summary>Gets the header subtitle of a view in the <see cref="NavigationStack"/>.</summary>
	public static string? GetSubtitle(BindableObject view) => (string?)view.GetValue(SubtitleProperty);

	/// <summary>Sets the header subtitle shown while <paramref name="view"/> is the top of the <see cref="NavigationStack"/>.</summary>
	public static void SetSubtitle(BindableObject view, string? value) => view.SetValue(SubtitleProperty, value);

	/// <summary>
	/// Gets the header items of a view: <see cref="ToolbarItemOrder.Primary"/>/<see cref="ToolbarItemOrder.Default"/>
	/// items become trailing actions, <see cref="ToolbarItemOrder.Secondary"/> items go into the overflow menu.
	/// Add to this list (XAML does); the items inherit the view's binding context.
	/// </summary>
	public static IList<ToolbarItem> GetToolbarItems(BindableObject view) => (IList<ToolbarItem>)view.GetValue(ToolbarItemsProperty);

	/// <summary>Replaces the header items of a view. Prefer adding to <see cref="GetToolbarItems"/>, which parents and tracks the items.</summary>
	public static void SetToolbarItems(BindableObject view, IList<ToolbarItem> value) => view.SetValue(ToolbarItemsProperty, value);

	/// <summary>
	/// Shows <paramref name="view"/> in this sheet on top of the current one, with the platform's push transition and a back button.
	/// The view becomes a logical child of the sheet (resources and styles resolve); a binding context set on it is kept.
	/// </summary>
	/// <exception cref="InvalidOperationException">The sheet has no root view yet, or <paramref name="view"/> is already in the stack.</exception>
	public async Task PushAsync(View view, bool animated = true)
	{
		ArgumentNullException.ThrowIfNull(view);
		await navigationGate.WaitAsync();
		try
		{
			if (PresentedContent is null)
				throw new InvalidOperationException("The sheet needs its root view (Content) before views can be pushed.");
			if (NavigationStack.Contains(view))
				throw new InvalidOperationException("The view is already in the sheet's navigation stack.");
			pushedViews.Add(view);
			AttachLevel(view);
			await Navigate(view, [], animated);
		}
		finally
		{
			navigationGate.Release();
		}
	}

	/// <summary>
	/// Removes the top <paramref name="count"/> views (never the root) with the platform's back transition and
	/// disconnects their handlers. Does not raise <see cref="Popped"/>.
	/// </summary>
	public async Task PopAsync(int count = 1, bool animated = true)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
		await navigationGate.WaitAsync();
		try
		{
			count = Math.Min(count, pushedViews.Count);
			if (count == 0)
				return;
			var popped = pushedViews.GetRange(pushedViews.Count - count, count);
			pushedViews.RemoveRange(pushedViews.Count - count, count);
			await Navigate(null, popped, animated);
			popped.ForEach(Release);
		}
		finally
		{
			navigationGate.Release();
		}
	}

	// The user went back on a platform where the handler animates the pop itself (Android, Windows).
	internal async void GoBack()
	{
		await navigationGate.WaitAsync();
		try
		{
			if (pushedViews.Count == 0)
				return;
			var view = pushedViews[^1];
			pushedViews.RemoveAt(pushedViews.Count - 1);
			await Navigate(null, (View[])[view], true);
			Release(view);
			Popped?.Invoke(this, new SheetPoppedEventArgs(view));
		}
		finally
		{
			navigationGate.Release();
		}
	}

	// The platform already removed the top level (iOS back button or edge swipe).
	internal void SendUserPopped()
	{
		if (pushedViews.Count == 0)
			return;
		var view = pushedViews[^1];
		pushedViews.RemoveAt(pushedViews.Count - 1);
		Release(view);
		Popped?.Invoke(this, new SheetPoppedEventArgs(view));
	}

	/// <summary>What the native header shows for the view at <paramref name="level"/> of the <see cref="NavigationStack"/>.</summary>
	internal SheetHeader HeaderAt(int level)
	{
		var stack = NavigationStack;
		var view = level < stack.Count ? stack[level] : null;
		var root = level == 0;
		IList<ToolbarItem> items = view is null ? [] : GetToolbarItems(view);
		return new SheetHeader(
			(view is null ? null : GetTitle(view)) ?? (root ? Title : null),
			(view is null ? null : GetSubtitle(view)) ?? (root ? Subtitle : null),
			ShowBack: !root,
			ShowClose: root && ShowNativeCloseButton,
			SecondaryActionText: root ? SecondaryActionText : null,
			PrimaryActionText: root ? PrimaryActionText : null,
			IsPrimaryActionEnabled,
			items.Where(i => i.Order != ToolbarItemOrder.Secondary).ToArray(),
			items.Where(i => i.Order == ToolbarItemOrder.Secondary).ToArray());
	}

	Task Navigate(View? pushed, IReadOnlyList<View> popped, bool animated)
	{
		if (Handler is null)
			return Task.CompletedTask;
		var request = new SheetNavigationRequest(pushed, popped, animated);
		Handler.Invoke(NativeSheetHandlerCommands.Navigate, request);
		return request.Done.Task;
	}

	// Called when the sheet closed: pushed views go away, every view's handlers are disconnected.
	void ReleaseNavigationStack()
	{
		var pushed = pushedViews.ToArray();
		pushedViews.Clear();
		foreach (var view in pushed)
			Release(view);
		PresentedContent?.DisconnectHandlers();
	}

	void Release(View view)
	{
		DetachLevel(view);
		view.DisconnectHandlers();
	}

	// A view in the stack is a logical child of the sheet (resources, styles, inherited binding context) and its
	// header (attached Title/Subtitle, ToolbarItems and their properties) is tracked.
	void AttachLevel(View view)
	{
		AddLogicalChild(view);
		view.PropertyChanged += OnLevelPropertyChanged;
		if (GetToolbarItems(view) is INotifyCollectionChanged items)
			items.CollectionChanged += OnToolbarItemsChanged;
		foreach (var item in GetToolbarItems(view))
			item.PropertyChanged += OnToolbarItemPropertyChanged;
	}

	void DetachLevel(View view)
	{
		view.PropertyChanged -= OnLevelPropertyChanged;
		if (GetToolbarItems(view) is INotifyCollectionChanged items)
			items.CollectionChanged -= OnToolbarItemsChanged;
		foreach (var item in GetToolbarItems(view))
			item.PropertyChanged -= OnToolbarItemPropertyChanged;
		RemoveLogicalChild(view);
	}

	void OnLevelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == TitleProperty.PropertyName || e.PropertyName == SubtitleProperty.PropertyName)
			UpdateHeader();
	}

	void OnToolbarItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		foreach (var item in e.OldItems?.OfType<ToolbarItem>() ?? [])
			item.PropertyChanged -= OnToolbarItemPropertyChanged;
		foreach (var item in e.NewItems?.OfType<ToolbarItem>() ?? [])
			item.PropertyChanged += OnToolbarItemPropertyChanged;
		UpdateHeader();
	}

	// IsEnabled also follows Command.CanExecute(CommandParameter): MenuItem refreshes it and raises PropertyChanged.
	void OnToolbarItemPropertyChanged(object? sender, PropertyChangedEventArgs e) => UpdateHeader();

	// Coalesces bursts of header changes (e.g. bindings settling) into one native update.
	void UpdateHeader()
	{
		if (headerUpdatePending || Handler is null)
			return;
		headerUpdatePending = true;
		Dispatcher.Dispatch(() =>
		{
			headerUpdatePending = false;
			Handler?.Invoke(NativeSheetHandlerCommands.UpdateHeader);
		});
	}
}

/// <summary>Event data for <see cref="NativeSheet.Popped"/>.</summary>
/// <param name="view">The view the user went back from; its handlers are already disconnected.</param>
public sealed class SheetPoppedEventArgs(View view) : EventArgs
{
	/// <summary>Gets the view that was removed from the sheet.</summary>
	public View View { get; } = view;
}

/// <summary>What the native header shows for one level of the navigation stack.</summary>
internal sealed record SheetHeader(
	string? Title,
	string? Subtitle,
	bool ShowBack,
	bool ShowClose,
	string? SecondaryActionText,
	string? PrimaryActionText,
	bool IsPrimaryActionEnabled,
	IReadOnlyList<ToolbarItem> Items,
	IReadOnlyList<ToolbarItem> OverflowItems)
{
	public bool IsEmpty =>
		string.IsNullOrEmpty(Title) && string.IsNullOrEmpty(Subtitle) && !ShowBack && !ShowClose &&
		string.IsNullOrEmpty(SecondaryActionText) && string.IsNullOrEmpty(PrimaryActionText) &&
		Items.Count == 0 && OverflowItems.Count == 0;
}

/// <summary>A push or pop for the handler; it completes <see cref="Done"/> when the transition finished.</summary>
internal sealed class SheetNavigationRequest(View? pushed, IReadOnlyList<View> popped, bool animated)
{
	public View? Pushed { get; } = pushed;

	public IReadOnlyList<View> Popped { get; } = popped;

	public bool Animated { get; } = animated;

	public TaskCompletionSource Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

/// <summary>Command names the sheet sends to its handler.</summary>
internal static class NativeSheetHandlerCommands
{
	public const string Navigate = "Navigate";
	public const string UpdateHeader = "UpdateHeader";
}

// The attached ToolbarItems list: items become logical children of their view, so bindings and resources resolve.
sealed class SheetToolbarItems(Element? owner) : ObservableCollection<ToolbarItem>
{
	protected override void InsertItem(int index, ToolbarItem item)
	{
		base.InsertItem(index, item);
		owner?.AddLogicalChild(item);
	}

	protected override void SetItem(int index, ToolbarItem item)
	{
		var old = this[index];
		base.SetItem(index, item);
		owner?.RemoveLogicalChild(old);
		owner?.AddLogicalChild(item);
	}

	protected override void RemoveItem(int index)
	{
		var item = this[index];
		base.RemoveItem(index);
		owner?.RemoveLogicalChild(item);
	}

	// Remove one by one: a Reset notification would not say which items to stop tracking.
	protected override void ClearItems()
	{
		while (Count > 0)
			RemoveItem(Count - 1);
	}
}
