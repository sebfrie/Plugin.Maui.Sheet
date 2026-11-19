using System.ComponentModel;
using Xunit;

namespace Plugin.Maui.Sheet.Tests;

public class SheetDetentTests
{
	[Theory]
	[InlineData("Medium", SheetDetentKind.Medium, 0)]
	[InlineData(" large ", SheetDetentKind.Large, 0)]
	[InlineData("FitContent", SheetDetentKind.FitContent, 0)]
	[InlineData("Fraction:0.3", SheetDetentKind.Fraction, 0.3)]
	[InlineData("height : 250", SheetDetentKind.Height, 250)]
	public void Parse_RoundTrips(string text, SheetDetentKind kind, double value)
	{
		var detent = SheetDetent.Parse(text);

		Assert.Equal(kind, detent.Kind);
		Assert.Equal(value, detent.Value);
		Assert.Equal(detent, SheetDetent.Parse(detent.ToString()));
	}

	[Theory]
	[InlineData("")]
	[InlineData("1")]
	[InlineData("Fraction")]
	[InlineData("Fraction:abc")]
	[InlineData("Huge")]
	public void Parse_RejectsInvalidText(string text) =>
		Assert.Throws<FormatException>(() => SheetDetent.Parse(text));

	[Theory]
	[InlineData(0.05)]
	[InlineData(1.5)]
	[InlineData(double.NaN)]
	public void Fraction_RejectsOutOfRange(double fraction) =>
		Assert.Throws<ArgumentOutOfRangeException>(() => SheetDetent.Fraction(fraction));

	[Fact]
	public void Height_RejectsNonPositive() =>
		Assert.Throws<ArgumentOutOfRangeException>(() => SheetDetent.Height(0));

	[Fact]
	public void Resolve_CapsAtMaximum()
	{
		Assert.Equal(400, SheetDetent.Medium.Resolve(800, 0));
		Assert.Equal(800, SheetDetent.Large.Resolve(800, 0));
		Assert.Equal(200, SheetDetent.Fraction(0.25).Resolve(800, 0));
		Assert.Equal(800, SheetDetent.Height(1000).Resolve(800, 0));
		Assert.Equal(300, SheetDetent.FitContent.Resolve(800, 300));
		Assert.Equal(800, SheetDetent.FitContent.Resolve(800, double.PositiveInfinity));
	}

	[Fact]
	public void ListConverter_ParsesCommaSeparatedDetents()
	{
		var detents = (IList<SheetDetent>)new SheetDetentsTypeConverter().ConvertFrom("FitContent, Fraction:0.5 ,Large")!;

		Assert.Equal([SheetDetent.FitContent, SheetDetent.Fraction(0.5), SheetDetent.Large], detents);
		Assert.Equal("FitContent, Fraction:0.5, Large", new SheetDetentsTypeConverter().ConvertTo(detents, typeof(string)));
	}
}

public class NativeSheetTests
{
	[Fact]
	public void Defaults_MatchTheDocumentedValues()
	{
		var sheet = new NativeSheet();

		Assert.False(sheet.IsOpen);
		Assert.True(sheet.IsDismissible);
		Assert.True(sheet.ShowGrabber);
		Assert.True(sheet.IsPrimaryActionEnabled);
		Assert.True(sheet.IsDraggable);
		Assert.Equal(-1, sheet.CornerRadius);
		Assert.Equal([SheetDetent.Medium, SheetDetent.Large], sheet.Detents);
		Assert.True(sheet.HeaderAt(0).IsEmpty);
	}

	[Fact]
	public void TakesNoSpaceInThePageLayout() =>
		Assert.Equal(Size.Zero, ((IView)new NativeSheet()).Measure(500, 500));

	[Fact]
	public void Detents_IgnoresAnEmptyList()
	{
		var sheet = new NativeSheet { Detents = [SheetDetent.FitContent] };

		sheet.Detents = [];

		Assert.Equal([SheetDetent.FitContent], sheet.Detents);
	}

	[Fact]
	public void ActionWithoutCommand_ClosesTheSheet()
	{
		var sheet = new NativeSheet { IsOpen = true };

		sheet.SendSecondaryAction();

		Assert.False(sheet.IsOpen);
	}

	[Fact]
	public void ActionWithCommand_RunsItAndKeepsTheSheetOpen()
	{
		object? received = null;
		var sheet = new NativeSheet
		{
			IsOpen = true,
			PrimaryActionCommand = new Command(p => received = p),
			PrimaryActionCommandParameter = 42,
		};

		sheet.SendPrimaryAction();

		Assert.Equal(42, received);
		Assert.True(sheet.IsOpen);
	}

	[Theory]
	[InlineData(true, false)]
	[InlineData(false, true)]
	public void DismissAttempted_DefaultsToTheDismissibleSetting(bool isDismissible, bool expectedCancel)
	{
		var sheet = new NativeSheet { IsDismissible = isDismissible };
		bool? cancel = null;
		sheet.DismissAttempted += (_, e) => cancel = e.Cancel;

		Assert.Equal(expectedCancel, sheet.SendDismissAttempted());
		Assert.Equal(expectedCancel, cancel);
	}

	[Fact]
	public void DismissAttempted_HandlerCanVeto()
	{
		var sheet = new NativeSheet();
		sheet.DismissAttempted += (_, e) => e.Cancel = true;

		Assert.True(sheet.SendDismissAttempted());
	}

	[Fact]
	public void DismissAttempted_CannotOverrideANonDismissibleSheet()
	{
		var sheet = new NativeSheet { IsDismissible = false };
		sheet.DismissAttempted += (_, e) => e.Cancel = false;

		Assert.True(sheet.SendDismissAttempted());
	}

	[Fact]
	public void DetentChanged_FiresOnlyForRealChanges()
	{
		var sheet = new NativeSheet();
		var raised = new List<SheetDetent>();
		sheet.DetentChanged += (_, e) => raised.Add(e.Detent);

		sheet.SendDetentChanged(SheetDetent.Large);
		sheet.SendDetentChanged(SheetDetent.Large);

		Assert.Equal([SheetDetent.Large], raised);
		Assert.Equal(SheetDetent.Large, sheet.SelectedDetent);
	}

	[Fact]
	public void Dismissed_ResetsIsOpenFirst()
	{
		var sheet = new NativeSheet { IsOpen = true };
		bool? isOpenWhenRaised = null;
		sheet.Dismissed += (_, _) => isOpenWhenRaised = sheet.IsOpen;

		sheet.SendDismissed();

		Assert.False(isOpenWhenRaised);
	}

	[Fact]
	public void ContentTemplate_IsBuiltOnlyWhileOpenAndThenReleased()
	{
		var sheet = new NativeSheet { BindingContext = "vm", ContentTemplate = new DataTemplate(() => new Label()) };
		Assert.Null(sheet.PresentedContent);

		sheet.CreateTemplatedContent();
		var content = Assert.IsType<Label>(sheet.PresentedContent);
		Assert.Same(sheet, content.Parent);
		Assert.Equal("vm", content.BindingContext);

		sheet.CreateTemplatedContent();
		Assert.Same(content, sheet.PresentedContent);

		sheet.ReleaseTemplatedContent();
		Assert.Null(sheet.PresentedContent);
		Assert.Null(content.Parent);
	}

	[Fact]
	public void Content_WinsOverContentTemplate()
	{
		var content = new Label();
		var sheet = new NativeSheet { Content = content, ContentTemplate = new DataTemplate(() => new Button()) };

		sheet.CreateTemplatedContent();

		Assert.Same(content, sheet.PresentedContent);
	}

	[Fact]
	public void Content_IsALogicalChildThatInheritsTheBindingContext()
	{
		var content = new Label();
		var sheet = new NativeSheet { BindingContext = "vm", Content = content };

		Assert.Same(sheet, content.Parent);
		Assert.Equal("vm", content.BindingContext);

		sheet.Content = null;
		Assert.Null(content.Parent);
	}
}

public class SheetNavigationTests
{
	[Fact]
	public async Task PushAndPop_MaintainTheStackWithoutRaisingPopped()
	{
		var root = new Label();
		var sheet = new NativeSheet { BindingContext = "vm", Content = root };
		var first = new Label();
		var second = new Label { BindingContext = "own" };
		var popped = new List<View>();
		sheet.Popped += (_, e) => popped.Add(e.View);

		await sheet.PushAsync(first);
		await sheet.PushAsync(second);

		Assert.Equal([root, first, second], sheet.NavigationStack);
		Assert.Same(sheet, first.Parent);
		Assert.Equal("vm", first.BindingContext);
		Assert.Equal("own", second.BindingContext);

		await sheet.PopAsync(5);

		Assert.Equal([root], sheet.NavigationStack);
		Assert.Null(first.Parent);
		Assert.Null(second.Parent);
		Assert.Empty(popped);
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sheet.PopAsync(0));
	}

	[Fact]
	public async Task Push_NeedsARootAndANewView()
	{
		var sheet = new NativeSheet();
		await Assert.ThrowsAsync<InvalidOperationException>(() => sheet.PushAsync(new Label()));

		sheet.Content = new Label();
		await Assert.ThrowsAsync<InvalidOperationException>(() => sheet.PushAsync(sheet.Content));
	}

	[Fact]
	public async Task UserBack_PopsOneLevelAndRaisesPopped()
	{
		var sheet = new NativeSheet { Content = new Label() };
		var first = new Label();
		var second = new Label();
		await sheet.PushAsync(first);
		await sheet.PushAsync(second);
		var popped = new List<View>();
		sheet.Popped += (_, e) => popped.Add(e.View);

		sheet.GoBack(); // Android, Windows: the handler animates the pop
		sheet.SendUserPopped(); // iOS: UIKit already popped
		sheet.SendUserPopped(); // nothing left to pop

		Assert.Equal([second, first], popped);
		Assert.Single(sheet.NavigationStack);
	}

	[Fact]
	public async Task Dismissed_ReleasesPushedViews()
	{
		var sheet = new NativeSheet { Content = new Label(), IsOpen = true };
		var pushed = new Label();
		await sheet.PushAsync(pushed);

		sheet.SendDismissed();

		Assert.Single(sheet.NavigationStack);
		Assert.Null(pushed.Parent);
	}

	[Fact]
	public async Task Header_ComesFromEachViewAndFallsBackToTheSheetAtTheRoot()
	{
		var root = new Label();
		var sheet = new NativeSheet
		{
			Title = "Sheet",
			Subtitle = "Details",
			Content = root,
			ShowNativeCloseButton = true,
			PrimaryActionText = "Done",
			SecondaryActionText = "Cancel",
		};
		var pushed = new Label();
		NativeSheet.SetTitle(pushed, "Pushed");
		await sheet.PushAsync(pushed);

		var rootHeader = sheet.HeaderAt(0);
		Assert.Equal(("Sheet", "Details", false, true, "Done", "Cancel"),
			(rootHeader.Title, rootHeader.Subtitle, rootHeader.ShowBack, rootHeader.ShowClose, rootHeader.PrimaryActionText, rootHeader.SecondaryActionText));
		var pushedHeader = sheet.HeaderAt(1);
		Assert.Equal(("Pushed", (string?)null, true, false, (string?)null, (string?)null),
			(pushedHeader.Title, pushedHeader.Subtitle, pushedHeader.ShowBack, pushedHeader.ShowClose, pushedHeader.PrimaryActionText, pushedHeader.SecondaryActionText));

		NativeSheet.SetTitle(root, "Root");
		Assert.Equal("Root", sheet.HeaderAt(0).Title);
	}

	[Fact]
	public void ToolbarItems_AreLogicalChildrenSplitByOrder()
	{
		var root = new Label();
		var save = new ToolbarItem { Text = "Save" };
		var delete = new ToolbarItem { Text = "Delete", Order = ToolbarItemOrder.Secondary };
		NativeSheet.GetToolbarItems(root).Add(save);
		NativeSheet.GetToolbarItems(root).Add(delete);
		var sheet = new NativeSheet { Content = root };
		sheet.BindingContext = "vm";

		Assert.Same(root, save.Parent);
		Assert.Equal("vm", save.BindingContext);
		Assert.Equal([save], sheet.HeaderAt(0).Items);
		Assert.Equal([delete], sheet.HeaderAt(0).OverflowItems);

		NativeSheet.GetToolbarItems(root).Clear();
		Assert.Null(save.Parent);
		Assert.Null(delete.Parent);
	}
}
