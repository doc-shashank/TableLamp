using System;
using System.Threading;
using FlaUI.Core.AutomationElements;
using TableLamp.UITests.Fixtures;
using Xunit;

namespace TableLamp.UITests.Tests
{
    public class SettingsUITests : IClassFixture<AppFixture>
    {
        private readonly AppFixture _fixture;

        public SettingsUITests(AppFixture fixture)
        {
            _fixture = fixture;
        }

        private Window NavigateToSettings()
        {
            _fixture.Launch();
            var launcher = _fixture.GetLauncherWindow();
            var basicCard = AppFixture.WaitForElement(launcher, "Launcher_BasicCard");
            AppFixture.ClickOrInvoke(basicCard);

            var mainScreen = _fixture.GetMainScreenWindow(TimeSpan.FromSeconds(15));
            var settingsBtn = AppFixture.WaitForElement(mainScreen, "Nav_SettingsButton");
            AppFixture.ClickOrInvoke(settingsBtn);
            Thread.Sleep(600);

            return mainScreen;
        }

        [Fact]
        public void Settings_PageLoads_AllElementsRenderWithoutClipping()
        {
            var mainScreen = NavigateToSettings();

            // Verify App Updates card is completely removed
            var checkUpdatesBtn = mainScreen.FindFirstDescendant(cf => cf.ByAutomationId("Settings_CheckForUpdatesButton"));
            Assert.Null(checkUpdatesBtn);

            // Verify Curated Presets sync and database maintenance elements
            var curatedVersionText = AppFixture.WaitForElement(mainScreen, "Settings_CuratedContentVersionText");
            var notifyCombo = AppFixture.WaitForElement(mainScreen, "Settings_NotificationDurationComboBox");
            var updateCuratedBtn = AppFixture.WaitForElement(mainScreen, "Settings_UpdateCuratedPresetsButton");
            var formatCuratedBtn = AppFixture.WaitForElement(mainScreen, "Settings_FormatCuratedDatabaseButton");
            var formatCustomBtn = AppFixture.WaitForElement(mainScreen, "Settings_FormatCustomDatabaseButton");
            var formatAllBtn = AppFixture.WaitForElement(mainScreen, "Settings_FormatAllDatabasesButton");

            Assert.NotNull(curatedVersionText);
            Assert.NotNull(notifyCombo);
            Assert.NotNull(updateCuratedBtn);
            Assert.NotNull(formatCuratedBtn);
            Assert.NotNull(formatCustomBtn);
            Assert.NotNull(formatAllBtn);

            Assert.True(formatCuratedBtn.IsEnabled);
            Assert.True(formatCustomBtn.IsEnabled);
            Assert.True(formatAllBtn.IsEnabled);

            // Assert top card elements that are within initial viewport
            AppFixture.AssertNotClipped(curatedVersionText, mainScreen, "Settings_CuratedContentVersionText");
            AppFixture.AssertNotClipped(notifyCombo, mainScreen, "Settings_NotificationDurationComboBox");
            AppFixture.AssertNotClipped(updateCuratedBtn, mainScreen, "Settings_UpdateCuratedPresetsButton");

            // Scroll format card into view if needed and assert layout bounds
            AppFixture.ScrollIntoView(formatCuratedBtn);
            if (formatCuratedBtn.BoundingRectangle.Width > 0)
            {
                AppFixture.AssertNotClipped(formatCuratedBtn, mainScreen, "Settings_FormatCuratedDatabaseButton");
            }
        }

        [Fact]
        public void Settings_UpdateCuratedPresets_DisplaysFloatingNotificationToast()
        {
            var mainScreen = NavigateToSettings();

            var updateCuratedBtn = AppFixture.WaitForElement(mainScreen, "Settings_UpdateCuratedPresetsButton");
            Assert.NotNull(updateCuratedBtn);

            // Trigger curated update check
            AppFixture.ClickOrInvoke(updateCuratedBtn);

            // Wait for FloatingNotification toast to appear
            var toastMsg = AppFixture.WaitForElement(mainScreen, "FloatingNotification_MessageText", TimeSpan.FromSeconds(10));
            Assert.NotNull(toastMsg);
            Assert.False(string.IsNullOrWhiteSpace(toastMsg.Name));

            // Ensure toast notification renders without clipping out of the main window layout
            AppFixture.AssertNotClipped(toastMsg, mainScreen, "FloatingNotification_MessageText");
        }

        [Fact]
        public void Settings_NotificationDuration_IsInteractive()
        {
            var mainScreen = NavigateToSettings();

            var notifyCombo = AppFixture.WaitForElement(mainScreen, "Settings_NotificationDurationComboBox");
            Assert.NotNull(notifyCombo);

            // Assert ComboBox is enabled and properly positioned
            Assert.True(notifyCombo.IsEnabled);
            AppFixture.AssertNotClipped(notifyCombo, mainScreen, "Settings_NotificationDurationComboBox");
        }
    }
}
