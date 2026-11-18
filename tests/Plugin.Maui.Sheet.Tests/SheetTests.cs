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
		Assert.Equal(-1, sheet.CornerRadius);
		Assert.Equal([SheetDetent.Medium, SheetDetent.Large], sheet.Detents);
		Assert.False(sheet.HasHeader);
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
