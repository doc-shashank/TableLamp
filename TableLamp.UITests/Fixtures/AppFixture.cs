using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Xunit;

namespace TableLamp.UITests.Fixtures
{
    /// <summary>
    /// AppFixture manages the lifecycle of the Table Lamp desktop process,
    /// UI Automation engine (UIA3), window resolution, clipping verification, and cleanup.
    /// </summary>
    public class AppFixture : IDisposable
    {
        public UIA3Automation Automation { get; private set; }
        public FlaUI.Core.Application? App { get; private set; }
        public string ExePath { get; private set; }

        public AppFixture()
        {
            Automation = new UIA3Automation();
            ExePath = ResolveExecutablePath();
            KillExistingProcesses();
        }

        public FlaUI.Core.Application Launch()
        {
            KillExistingProcesses();
            if (!File.Exists(ExePath))
            {
                throw new FileNotFoundException($"TableLamp executable not found at: {ExePath}");
            }

            var psi = new ProcessStartInfo
            {
                FileName = ExePath,
                WorkingDirectory = Path.GetDirectoryName(ExePath)!,
                UseShellExecute = false
            };

            App = FlaUI.Core.Application.Launch(psi);
            App.WaitWhileMainHandleIsMissing(TimeSpan.FromSeconds(10));
            return App;
        }

        public Window GetLauncherWindow(TimeSpan? timeout = null)
        {
            var waitTimeout = timeout ?? TimeSpan.FromSeconds(10);
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed < waitTimeout)
            {
                if (App != null && !App.HasExited)
                {
                    var windows = App.GetAllTopLevelWindows(Automation);
                    foreach (var w in windows)
                    {
                        var title = w.Title;
                        if (title.Contains("Table Lamp", StringComparison.OrdinalIgnoreCase))
                        {
                            return w;
                        }
                    }
                }
                Thread.Sleep(250);
            }

            throw new TimeoutException($"Timed out waiting for Table Lamp Launcher window after {waitTimeout.TotalSeconds} seconds.");
        }

        public Window GetMainScreenWindow(TimeSpan? timeout = null)
        {
            var waitTimeout = timeout ?? TimeSpan.FromSeconds(15);
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed < waitTimeout)
            {
                if (App != null && !App.HasExited)
                {
                    var windows = App.GetAllTopLevelWindows(Automation);
                    foreach (var w in windows)
                    {
                        // Look for a top-level window that contains the MainScreen navigation buttons
                        var nav = w.FindFirstDescendant(cf => cf.ByAutomationId("Nav_DashboardButton"));
                        if (nav != null)
                        {
                            return w;
                        }
                    }
                }
                Thread.Sleep(300);
            }

            throw new TimeoutException($"Timed out waiting for MainScreen window after {waitTimeout.TotalSeconds} seconds.");
        }

        public Window? GetWindowByTitle(string titleSubstring, TimeSpan? timeout = null)
        {
            var waitTimeout = timeout ?? TimeSpan.FromSeconds(10);
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed < waitTimeout)
            {
                if (App != null && !App.HasExited)
                {
                    var windows = App.GetAllTopLevelWindows(Automation);
                    foreach (var w in windows)
                    {
                        if (w.Title.Contains(titleSubstring, StringComparison.OrdinalIgnoreCase))
                        {
                            return w;
                        }
                    }
                }
                Thread.Sleep(250);
            }

            return null;
        }

        /// <summary>
        /// Verifies that a UI element has valid dimensions and is not clipped out of its parent window or container.
        /// Addresses user requirement: 'Make sure that the UI elements are not clipping out of layout'.
        /// </summary>
        public static void AssertNotClipped(AutomationElement element, AutomationElement containerOrWindow, string elementName = "Element")
        {
            Assert.NotNull(element);
            Assert.NotNull(containerOrWindow);

            var elemBounds = element.BoundingRectangle;
            var containerBounds = containerOrWindow.BoundingRectangle;

            // 1. Element must have non-zero geometry
            Assert.True(elemBounds.Width > 0, $"{elementName} has invalid width: {elemBounds.Width}");
            Assert.True(elemBounds.Height > 0, $"{elementName} has invalid height: {elemBounds.Height}");

            // 2. Tolerance for window drop-shadows and DPI sub-pixel rounding
            const int tolerance = 10;

            // 3. Must be contained within the container / window bounds
            bool isHorizontallyWithin = elemBounds.Left >= (containerBounds.Left - tolerance) &&
                                        elemBounds.Right <= (containerBounds.Right + tolerance);
            bool isVerticallyWithin = elemBounds.Top >= (containerBounds.Top - tolerance) &&
                                      elemBounds.Bottom <= (containerBounds.Bottom + tolerance);

            Assert.True(isHorizontallyWithin, 
                $"{elementName} is clipped horizontally! Element: [{elemBounds.Left}, {elemBounds.Right}], Container: [{containerBounds.Left}, {containerBounds.Right}]");
            Assert.True(isVerticallyWithin, 
                $"{elementName} is clipped vertically! Element: [{elemBounds.Top}, {elemBounds.Bottom}], Container: [{containerBounds.Top}, {containerBounds.Bottom}]");
        }

        public static AutomationElement WaitForElement(AutomationElement parent, string automationId, TimeSpan? timeout = null)
        {
            var waitTimeout = timeout ?? TimeSpan.FromSeconds(5);
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed < waitTimeout)
            {
                var el = parent.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
                if (el != null)
                {
                    return el;
                }
                Thread.Sleep(200);
            }

            throw new TimeoutException($"Element with AutomationId '{automationId}' was not found within {waitTimeout.TotalSeconds} seconds.");
        }

        public static void ClickOrInvoke(AutomationElement element)
        {
            if (element.Patterns.Invoke.IsSupported)
            {
                try
                {
                    element.Patterns.Invoke.Pattern.Invoke();
                    return;
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    Thread.Sleep(200);
                    try
                    {
                        element.Patterns.Invoke.Pattern.Invoke();
                        return;
                    }
                    catch { }
                }
            }

            if (element.Patterns.Toggle.IsSupported)
            {
                element.Patterns.Toggle.Pattern.Toggle();
                return;
            }

            if (element.Patterns.SelectionItem.IsSupported)
            {
                element.Patterns.SelectionItem.Pattern.Select();
                return;
            }

            try
            {
                element.Focus();
                element.Click();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // In non-elevated or background console sessions, hardware mouse simulation
                // via SendInput encounters UIPI 'Access is denied'. Safely fall back to Focus.
                element.Focus();
            }
        }

        public static void ScrollIntoView(AutomationElement element)
        {
            if (element.Patterns.ScrollItem.IsSupported)
            {
                element.Patterns.ScrollItem.Pattern.ScrollIntoView();
                Thread.Sleep(300);
            }
        }

        private static string ResolveExecutablePath()
        {
            // First check environment override if set
            var envPath = Environment.GetEnvironmentVariable("TABLELAMP_EXE_PATH");
            if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath))
            {
                return envPath;
            }

            var baseDir = AppContext.BaseDirectory;
            var current = new DirectoryInfo(baseDir);

            // Search upwards to find solution root containing TableLamp\bin\Debug
            while (current != null)
            {
                // Skip if current directory is inside a test folder
                if (!current.Name.Contains("Tests", StringComparison.OrdinalIgnoreCase))
                {
                    var candidate = Path.Combine(
                        current.FullName, 
                        "bin", 
                        "Debug", 
                        "net8.0-windows10.0.26100.0", 
                        "win-x64", 
                        "TableLamp.exe"
                    );

                    // Ensure TableLamp.exe, TableLamp.dll, and TableLamp.pri all exist in the directory
                    if (File.Exists(candidate) && 
                        File.Exists(Path.Combine(Path.GetDirectoryName(candidate)!, "TableLamp.dll")) &&
                        File.Exists(Path.Combine(Path.GetDirectoryName(candidate)!, "TableLamp.pri")))
                    {
                        return candidate;
                    }
                }

                current = current.Parent;
            }

            // Fallback default relative to standard project layout
            return Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "bin", "Debug", "net8.0-windows10.0.26100.0", "win-x64", "TableLamp.exe"));
        }

        private static void KillExistingProcesses()
        {
            try
            {
                foreach (var proc in Process.GetProcessesByName("TableLamp"))
                {
                    try
                    {
                        proc.Kill();
                        proc.WaitForExit(2000);
                    }
                    catch
                    {
                        // Ignore if already dead or access denied
                    }
                }
            }
            catch
            {
                // Non-fatal
            }
        }

        public void Dispose()
        {
            try
            {
                if (App != null && !App.HasExited)
                {
                    try
                    {
                        App.Kill();
                    }
                    catch { }
                }
            }
            catch { }

            try
            {
                Automation?.Dispose();
            }
            catch { }

            KillExistingProcesses();
        }
    }
}
