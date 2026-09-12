using System;
using System.Threading;
using FlaUI.Core.AutomationElements;
using TableLamp.UITests.Fixtures;
using Xunit;

namespace TableLamp.UITests.Tests
{
    public class MainScreenNavigationUITests : IClassFixture<AppFixture>
    {
        private readonly AppFixture _fixture;

        public MainScreenNavigationUITests(AppFixture fixture)
        {
            _fixture = fixture;
        }

        private Window LaunchToMainScreen()
        {
            _fixture.Launch();
            var launcher = _fixture.GetLauncherWindow();
            var basicCard = AppFixture.WaitForElement(launcher, "Launcher_BasicCard");
            AppFixture.ClickOrInvoke(basicCard);

            return _fixture.GetMainScreenWindow(TimeSpan.FromSeconds(15));
        }

        [Fact]
        public void MainScreen_TopBar_RendersCorrectly_WithoutClipping()
        {
            var mainScreen = LaunchToMainScreen();
            Assert.NotNull(mainScreen);

            var versionText = AppFixture.WaitForElement(mainScreen, "MainScreen_VersionText");
            var dashboardBtn = AppFixture.WaitForElement(mainScreen, "Nav_DashboardButton");
            var calendarBtn = AppFixture.WaitForElement(mainScreen, "Nav_CalendarButton");
            var settingsBtn = AppFixture.WaitForElement(mainScreen, "Nav_SettingsButton");
            var themeBtn = AppFixture.WaitForElement(mainScreen, "Nav_ThemeToggleButton");
            var minBtn = AppFixture.WaitForElement(mainScreen, "MainScreen_MinimizeButton");
            var exitBtn = AppFixture.WaitForElement(mainScreen, "MainScreen_ExitButton");

            // Version check
            Assert.Contains("0.0.8.2", versionText.Name);

            // Assert no top-bar element is clipped out of the main window layout
            AppFixture.AssertNotClipped(versionText, mainScreen, "MainScreen_VersionText");
            AppFixture.AssertNotClipped(dashboardBtn, mainScreen, "Nav_DashboardButton");
            AppFixture.AssertNotClipped(calendarBtn, mainScreen, "Nav_CalendarButton");
            AppFixture.AssertNotClipped(settingsBtn, mainScreen, "Nav_SettingsButton");
            AppFixture.AssertNotClipped(themeBtn, mainScreen, "Nav_ThemeToggleButton");
            AppFixture.AssertNotClipped(minBtn, mainScreen, "MainScreen_MinimizeButton");
            AppFixture.AssertNotClipped(exitBtn, mainScreen, "MainScreen_ExitButton");
        }

        [Fact]
        public void MainScreen_CanNavigate_BetweenDashboardCalendarAndSettings()
        {
            var mainScreen = LaunchToMainScreen();
            Assert.NotNull(mainScreen);

            var calendarBtn = AppFixture.WaitForElement(mainScreen, "Nav_CalendarButton");
            var dashboardBtn = AppFixture.WaitForElement(mainScreen, "Nav_DashboardButton");
            var settingsBtn = AppFixture.WaitForElement(mainScreen, "Nav_SettingsButton");

            // 1. Navigate to Calendar
            AppFixture.ClickOrInvoke(calendarBtn);
            Thread.Sleep(500);

            var calendarView = AppFixture.WaitForElement(mainScreen, "Calendar_SessionsCalendarView", TimeSpan.FromSeconds(8));
            Assert.NotNull(calendarView);
            AppFixture.AssertNotClipped(calendarView, mainScreen, "Calendar_SessionsCalendarView");

            // 2. Navigate to Settings
            AppFixture.ClickOrInvoke(settingsBtn);
            Thread.Sleep(500);

            var curatedVersionText = AppFixture.WaitForElement(mainScreen, "Settings_CuratedContentVersionText", TimeSpan.FromSeconds(8));
            Assert.NotNull(curatedVersionText);
            AppFixture.AssertNotClipped(curatedVersionText, mainScreen, "Settings_CuratedContentVersionText");

            // 3. Return to Dashboard
            AppFixture.ClickOrInvoke(dashboardBtn);
            Thread.Sleep(500);

            var createCard = AppFixture.WaitForElement(mainScreen, "Dashboard_CreateSessionHeaderButton", TimeSpan.FromSeconds(8));
            Assert.NotNull(createCard);
            AppFixture.AssertNotClipped(createCard, mainScreen, "Dashboard_CreateSessionHeaderButton");
        }

        [Fact]
        public void MainScreen_ThemeToggle_IsClickable_WithoutError()
        {
            var mainScreen = LaunchToMainScreen();
            var themeBtn = AppFixture.WaitForElement(mainScreen, "Nav_ThemeToggleButton");

            // Toggle theme and verify app remains responsive
            AppFixture.ClickOrInvoke(themeBtn);
            Thread.Sleep(300);

            var dashboardBtn = AppFixture.WaitForElement(mainScreen, "Nav_DashboardButton");
            Assert.NotNull(dashboardBtn);
            AppFixture.AssertNotClipped(dashboardBtn, mainScreen, "Nav_DashboardButton");
        }
    }
}
