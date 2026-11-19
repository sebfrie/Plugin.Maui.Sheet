using System.Runtime.InteropServices;
using ObjCRuntime;
using UIKit;

namespace Plugin.Maui.Sheet;

// MAUI presents its first modal page (and so CommunityToolkit popups) from the window's page controller. While a sheet
// is up, that controller is already presenting the sheet, so UIKit refuses the modal and MAUI's push never completes.
// This replaces -[UIViewController presentViewController:animated:completion:] with a version that, only when the
// receiver is presenting a NativeSheet, presents from the top of that sheet instead. Every other call is unchanged.
static unsafe class ModalPresentationRedirect
{
	static IntPtr original;

	// Main thread only (called when a sheet is presented).
	public static void Install()
	{
		if (original != IntPtr.Zero)
			return;
		var method = class_getInstanceMethod(Class.GetHandle(typeof(UIViewController)), Selector.GetHandle("presentViewController:animated:completion:"));
		original = method_setImplementation(method, (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, byte, IntPtr, void>)&Present);
	}

	[UnmanagedCallersOnly]
	static void Present(IntPtr self, IntPtr selector, IntPtr controller, byte animated, IntPtr completion)
	{
		var presenter = self;
		try
		{
			if (Runtime.GetNSObject<UIViewController>(self)?.PresentedViewController is NativeSheetHandler.SheetNavigationController { IsBeingDismissed: false } sheet)
			{
				UIViewController top = sheet;
				while (top.PresentedViewController is { IsBeingDismissed: false } next)
					top = next;
				presenter = top.Handle;
			}
		}
		catch (Exception)
		{
			// Exceptions must not cross into Objective-C; present from the original receiver as UIKit would.
		}
		((delegate* unmanaged<IntPtr, IntPtr, IntPtr, byte, IntPtr, void>)original)(presenter, selector, controller, animated, completion);
	}

	[DllImport("/usr/lib/libobjc.dylib")]
	static extern IntPtr class_getInstanceMethod(IntPtr cls, IntPtr name);

	[DllImport("/usr/lib/libobjc.dylib")]
	static extern IntPtr method_setImplementation(IntPtr method, IntPtr implementation);
}
