using System;
using System.Threading;
using FlaUI.Core.AutomationElements;
using TableLamp.UITests.Fixtures;
using Xunit;

namespace TableLamp.UITests.Tests
{
    public class DashboardUITests : IClassFixture<AppFixture>
    {
        private readonly AppFixture _fixture;

        public DashboardUITests(AppFixture fixture)
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
        public void Dashboard_ActionCards_RenderProperly_WithoutClipping()
        {
            var mainScreen = LaunchToMainScreen();

            var createHeader = AppFixture.WaitForElement(mainScreen, "Dashboard_CreateSessionHeaderButton");
            var reviewHeader = AppFixture.WaitForElement(mainScreen, "Dashboard_ReviewSessionCard");

            Assert.NotNull(createHeader);
            Assert.NotNull(reviewHeader);

            // Assert cards are not clipped
            AppFixture.AssertNotClipped(createHeader, mainScreen, "Dashboard_CreateSessionHeaderButton");
            AppFixture.AssertNotClipped(reviewHeader, mainScreen, "Dashboard_ReviewSessionCard");

            // Assert cards are positioned side-by-side without overlapping
            Assert.True(createHeader.BoundingRectangle.Right <= reviewHeader.BoundingRectangle.Left + 5,
                "CreateSessionHeaderButton overlaps ReviewSessionCard!");
        }

        [Fact]
        public void Dashboard_CreateSessionCard_ExpandsOnClick_ShowingCuratedAndCustomOptions()
        {
            var mainScreen = LaunchToMainScreen();

            var createHeader = AppFixture.WaitForElement(mainScreen, "Dashboard_CreateSessionHeaderButton");
            Assert.NotNull(createHeader);

            // Click the Create Session card header to expand options
            AppFixture.ClickOrInvoke(createHeader);
            Thread.Sleep(600);

            // Wait for Curated and Custom buttons to appear
            var curatedBtn = AppFixture.WaitForElement(mainScreen, "Dashboard_CuratedOptionButton", TimeSpan.FromSeconds(5));
            var customBtn = AppFixture.WaitForElement(mainScreen, "Dashboard_CustomOptionButton", TimeSpan.FromSeconds(5));

            Assert.NotNull(curatedBtn);
            Assert.NotNull(customBtn);

            // Assert option buttons are displayed inside the window without clipping
            AppFixture.AssertNotClipped(curatedBtn, mainScreen, "Dashboard_CuratedOptionButton");
            AppFixture.AssertNotClipped(customBtn, mainScreen, "Dashboard_CustomOptionButton");

            // Curated is on the left, Custom is on the right
            Assert.True(curatedBtn.BoundingRectangle.Right <= customBtn.BoundingRectangle.Left + 5,
                "CuratedOptionButton overlaps CustomOptionButton!");
        }

        [Fact]
        public void Dashboard_PendingReviewsSection_RendersWithoutClipping()
        {
            var mainScreen = LaunchToMainScreen();

            // Either the empty state border or the badge is rendered
            var emptyBorder = mainScreen.FindFirstDescendant(cf => cf.ByAutomationId("Dashboard_NoPendingReviewsBorder"));
            if (emptyBorder != null && !emptyBorder.BoundingRectangle.IsEmpty)
            {
                AppFixture.AssertNotClipped(emptyBorder, mainScreen, "Dashboard_NoPendingReviewsBorder");
            }

            var viewAllBtn = mainScreen.FindFirstDescendant(cf => cf.ByAutomationId("Dashboard_ViewAllSessionsButton"));
            if (viewAllBtn != null && !viewAllBtn.BoundingRectangle.IsEmpty)
            {
                AppFixture.AssertNotClipped(viewAllBtn, mainScreen, "Dashboard_ViewAllSessionsButton");
            }
        }

        [Fact]
        public void Dashboard_CreateSessionCard_CollapseToggleCycle()
        {
            var mainScreen = LaunchToMainScreen();

            var createHeader = AppFixture.WaitForElement(mainScreen, "Dashboard_CreateSessionHeaderButton");
            Assert.NotNull(createHeader);

            // Expand options
            AppFixture.ClickOrInvoke(createHeader);
            Thread.Sleep(600);

            var curatedBtn = AppFixture.WaitForElement(mainScreen, "Dashboard_CuratedOptionButton", TimeSpan.FromSeconds(5));
            Assert.NotNull(curatedBtn);
            AppFixture.AssertNotClipped(curatedBtn, mainScreen, "Dashboard_CuratedOptionButton");

            // Collapse options by clicking header again
            var createHeaderAgain = AppFixture.WaitForElement(mainScreen, "Dashboard_CreateSessionHeaderButton");
            AppFixture.ClickOrInvoke(createHeaderAgain);
            Thread.Sleep(600);

            // Re-verify Get Started indicator or create header is stable and not clipped
            var getStarted = AppFixture.WaitForElement(mainScreen, "Dashboard_GetStartedIndicator", TimeSpan.FromSeconds(5));
            Assert.NotNull(getStarted);
            AppFixture.AssertNotClipped(getStarted, mainScreen, "Dashboard_GetStartedIndicator");
        }

        [Fact]
        public void Dashboard_CuratedOption_NavigatesToSessionDetails()
        {
            var mainScreen = LaunchToMainScreen();

            var createHeader = AppFixture.WaitForElement(mainScreen, "Dashboard_CreateSessionHeaderButton");
            AppFixture.ClickOrInvoke(createHeader);
            Thread.Sleep(600);

            var curatedBtn = AppFixture.WaitForElement(mainScreen, "Dashboard_CuratedOptionButton", TimeSpan.FromSeconds(5));
            AppFixture.ClickOrInvoke(curatedBtn);
            Thread.Sleep(600);

            // Assert SessionDetailsPage is loaded with Curated mode
            var headerTitle = AppFixture.WaitForElement(mainScreen, "SessionDetails_HeaderTitle", TimeSpan.FromSeconds(5));
            var modeBadge = AppFixture.WaitForElement(mainScreen, "SessionDetails_ModeBadgeText", TimeSpan.FromSeconds(5));
            var backBtn = AppFixture.WaitForElement(mainScreen, "SessionDetails_BackButton", TimeSpan.FromSeconds(5));

            Assert.NotNull(headerTitle);
            Assert.NotNull(modeBadge);
            Assert.NotNull(backBtn);
            Assert.Equal("Curated", modeBadge.Name);

            AppFixture.AssertNotClipped(headerTitle, mainScreen, "SessionDetails_HeaderTitle");
            AppFixture.AssertNotClipped(modeBadge, mainScreen, "SessionDetails_ModeBadgeText");
            AppFixture.AssertNotClipped(backBtn, mainScreen, "SessionDetails_BackButton");

            // Return to Dashboard via back button
            AppFixture.ClickOrInvoke(backBtn);
            Thread.Sleep(600);

            var dashboardHeader = AppFixture.WaitForElement(mainScreen, "Dashboard_CreateSessionHeaderButton", TimeSpan.FromSeconds(5));
            Assert.NotNull(dashboardHeader);
            AppFixture.AssertNotClipped(dashboardHeader, mainScreen, "Dashboard_CreateSessionHeaderButton");
        }

        [Fact]
        public void Dashboard_CustomOption_NavigatesToSessionDetails()
        {
            var mainScreen = LaunchToMainScreen();

            var createHeader = AppFixture.WaitForElement(mainScreen, "Dashboard_CreateSessionHeaderButton");
            AppFixture.ClickOrInvoke(createHeader);
            Thread.Sleep(600);

            var customBtn = AppFixture.WaitForElement(mainScreen, "Dashboard_CustomOptionButton", TimeSpan.FromSeconds(5));
            AppFixture.ClickOrInvoke(customBtn);
            Thread.Sleep(600);

            // Assert SessionDetailsPage is loaded with Custom mode
            var headerTitle = AppFixture.WaitForElement(mainScreen, "SessionDetails_HeaderTitle", TimeSpan.FromSeconds(5));
            var modeBadge = AppFixture.WaitForElement(mainScreen, "SessionDetails_ModeBadgeText", TimeSpan.FromSeconds(5));
            var backBtn = AppFixture.WaitForElement(mainScreen, "SessionDetails_BackButton", TimeSpan.FromSeconds(5));

            Assert.NotNull(headerTitle);
            Assert.NotNull(modeBadge);
            Assert.NotNull(backBtn);
            Assert.Equal("Custom", modeBadge.Name);

            AppFixture.AssertNotClipped(headerTitle, mainScreen, "SessionDetails_HeaderTitle");
            AppFixture.AssertNotClipped(modeBadge, mainScreen, "SessionDetails_ModeBadgeText");
            AppFixture.AssertNotClipped(backBtn, mainScreen, "SessionDetails_BackButton");

            // Return to Dashboard via back button
            AppFixture.ClickOrInvoke(backBtn);
            Thread.Sleep(600);

            var dashboardHeader = AppFixture.WaitForElement(mainScreen, "Dashboard_CreateSessionHeaderButton", TimeSpan.FromSeconds(5));
            Assert.NotNull(dashboardHeader);
            AppFixture.AssertNotClipped(dashboardHeader, mainScreen, "Dashboard_CreateSessionHeaderButton");
        }

        [Fact]
        public void Dashboard_ReviewSession_NavigatesToSessionLists()
        {
            var mainScreen = LaunchToMainScreen();

            var reviewHeader = AppFixture.WaitForElement(mainScreen, "Dashboard_ReviewSessionCard");
            Assert.NotNull(reviewHeader);

            AppFixture.ClickOrInvoke(reviewHeader);
            Thread.Sleep(600);

            // Assert SessionListsPage is loaded
            var headerTitle = AppFixture.WaitForElement(mainScreen, "SessionLists_HeaderTitle", TimeSpan.FromSeconds(5));
            var backBtn = AppFixture.WaitForElement(mainScreen, "SessionLists_BackButton", TimeSpan.FromSeconds(5));

            Assert.NotNull(headerTitle);
            Assert.NotNull(backBtn);
            Assert.Equal("Review Sessions", headerTitle.Name);

            AppFixture.AssertNotClipped(headerTitle, mainScreen, "SessionLists_HeaderTitle");
            AppFixture.AssertNotClipped(backBtn, mainScreen, "SessionLists_BackButton");

            // Return to Dashboard
            AppFixture.ClickOrInvoke(backBtn);
            Thread.Sleep(600);

            var dashboardHeader = AppFixture.WaitForElement(mainScreen, "Dashboard_CreateSessionHeaderButton", TimeSpan.FromSeconds(5));
            Assert.NotNull(dashboardHeader);
            AppFixture.AssertNotClipped(dashboardHeader, mainScreen, "Dashboard_CreateSessionHeaderButton");
        }

        [Fact]
        public void Dashboard_ViewAllSessions_NavigatesToRecentSessionPage()
        {
            var mainScreen = LaunchToMainScreen();

            var viewAllBtn = AppFixture.WaitForElement(mainScreen, "Dashboard_ViewAllSessionsButton");
            Assert.NotNull(viewAllBtn);

            AppFixture.ClickOrInvoke(viewAllBtn);
            Thread.Sleep(600);

            // Assert RecentSessionPage is loaded
            var headerTitle = AppFixture.WaitForElement(mainScreen, "RecentSession_HeaderTitle", TimeSpan.FromSeconds(5));
            var backBtn = AppFixture.WaitForElement(mainScreen, "RecentSession_BackButton", TimeSpan.FromSeconds(5));
            var datePicker = AppFixture.WaitForElement(mainScreen, "RecentSession_DatePicker", TimeSpan.FromSeconds(5));

            Assert.NotNull(headerTitle);
            Assert.NotNull(backBtn);
            Assert.NotNull(datePicker);
            Assert.Equal("Recent Sessions", headerTitle.Name);

            AppFixture.AssertNotClipped(headerTitle, mainScreen, "RecentSession_HeaderTitle");
            AppFixture.AssertNotClipped(backBtn, mainScreen, "RecentSession_BackButton");
            AppFixture.AssertNotClipped(datePicker, mainScreen, "RecentSession_DatePicker");

            // Return to Dashboard
            AppFixture.ClickOrInvoke(backBtn);
            Thread.Sleep(600);

            var dashboardHeader = AppFixture.WaitForElement(mainScreen, "Dashboard_CreateSessionHeaderButton", TimeSpan.FromSeconds(5));
            Assert.NotNull(dashboardHeader);
            AppFixture.AssertNotClipped(dashboardHeader, mainScreen, "Dashboard_CreateSessionHeaderButton");
        }

        [Fact]
        public void Dashboard_RecentSessions_EmptyStateElementRendersWithoutClipping()
        {
            var mainScreen = LaunchToMainScreen();

            var emptyBorder = mainScreen.FindFirstDescendant(cf => cf.ByAutomationId("Dashboard_NoRecentSessionsBorder"));
            if (emptyBorder != null && !emptyBorder.BoundingRectangle.IsEmpty)
            {
                var emptyText = AppFixture.WaitForElement(mainScreen, "Dashboard_NoRecentSessionsText");
                Assert.NotNull(emptyText);
                Assert.Contains("No recently recorded session", emptyText.Name);
                AppFixture.AssertNotClipped(emptyBorder, mainScreen, "Dashboard_NoRecentSessionsBorder");
            }
        }
    }
}
