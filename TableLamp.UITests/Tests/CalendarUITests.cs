using System;
using System.Threading;
using FlaUI.Core.AutomationElements;
using TableLamp.UITests.Fixtures;
using Xunit;

namespace TableLamp.UITests.Tests
{
    public class CalendarUITests : IClassFixture<AppFixture>
    {
        private readonly AppFixture _fixture;

        public CalendarUITests(AppFixture fixture)
        {
            _fixture = fixture;
        }

        private Window NavigateToCalendar()
        {
            _fixture.Launch();
            var launcher = _fixture.GetLauncherWindow();
            var basicCard = AppFixture.WaitForElement(launcher, "Launcher_BasicCard");
            AppFixture.ClickOrInvoke(basicCard);

            var mainScreen = _fixture.GetMainScreenWindow(TimeSpan.FromSeconds(15));
            var calendarBtn = AppFixture.WaitForElement(mainScreen, "Nav_CalendarButton");
            AppFixture.ClickOrInvoke(calendarBtn);
            Thread.Sleep(600);

            return mainScreen;
        }

        [Fact]
        public void Calendar_PageLoads_CalendarViewAndDayDetailsRenderWithoutClipping()
        {
            var mainScreen = NavigateToCalendar();

            var calendarView = AppFixture.WaitForElement(mainScreen, "Calendar_SessionsCalendarView", TimeSpan.FromSeconds(8));
            var dateHeaderText = AppFixture.WaitForElement(mainScreen, "Calendar_SelectedDateHeaderText", TimeSpan.FromSeconds(8));

            Assert.NotNull(calendarView);
            Assert.NotNull(dateHeaderText);

            // Assert neither element is clipped out of the main window bounds
            AppFixture.AssertNotClipped(calendarView, mainScreen, "Calendar_SessionsCalendarView");
            AppFixture.AssertNotClipped(dateHeaderText, mainScreen, "Calendar_SelectedDateHeaderText");

            // Calendar is situated to the left of the day details header
            Assert.True(calendarView.BoundingRectangle.Right <= dateHeaderText.BoundingRectangle.Left + 50,
                "SessionsCalendarView overlaps or is not positioned to the left of SelectedDateHeaderText!");
        }

        [Fact]
        public void Calendar_SessionsCalendarView_IsInteractive()
        {
            var mainScreen = NavigateToCalendar();

            var calendarView = AppFixture.WaitForElement(mainScreen, "Calendar_SessionsCalendarView", TimeSpan.FromSeconds(8));
            Assert.NotNull(calendarView);
            Assert.True(calendarView.IsEnabled);

            // Click within calendar view to verify responsiveness
            AppFixture.ClickOrInvoke(calendarView);
            Thread.Sleep(300);

            // Verify element still renders without clipping
            AppFixture.AssertNotClipped(calendarView, mainScreen, "Calendar_SessionsCalendarView");
        }
    }
}
