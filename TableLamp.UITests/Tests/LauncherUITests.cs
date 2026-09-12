using System;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using TableLamp.UITests.Fixtures;
using Xunit;

namespace TableLamp.UITests.Tests
{
    public class LauncherUITests : IClassFixture<AppFixture>
    {
        private readonly AppFixture _fixture;

        public LauncherUITests(AppFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void Launcher_WindowLoads_AndDisplaysCorrectTitle()
        {
            _fixture.Launch();
            var launcher = _fixture.GetLauncherWindow();

            Assert.NotNull(launcher);
            Assert.Contains("Table Lamp", launcher.Title);
            Assert.False(launcher.BoundingRectangle.IsEmpty);

        }

        [Fact]
        public void Launcher_VersionText_IsVisible_AndNotClipped()
        {
            _fixture.Launch();
            var launcher = _fixture.GetLauncherWindow();

            var versionText = AppFixture.WaitForElement(launcher, "Launcher_VersionText");
            Assert.NotNull(versionText);
            
            // Validate version label content
            var text = versionText.Name;
            Assert.Contains("0.0.8.2", text);

            // Validate that version text is strictly inside the launcher window and not clipped
            AppFixture.AssertNotClipped(versionText, launcher, "Launcher_VersionText");
        }

        [Fact]
        public void Launcher_ModeCards_RenderProperly_AndAreNotClipped()
        {
            _fixture.Launch();
            var launcher = _fixture.GetLauncherWindow();

            // Locate the four cards and dev tools button
            var basicCard = AppFixture.WaitForElement(launcher, "Launcher_BasicCard");
            var advancedCard = AppFixture.WaitForElement(launcher, "Launcher_AdvancedCard");
            var generatorCard = AppFixture.WaitForElement(launcher, "Launcher_GeneratorCard");
            var libraryCard = AppFixture.WaitForElement(launcher, "Launcher_LibraryCard");
            var devToolsBtn = AppFixture.WaitForElement(launcher, "Launcher_DevToolsButton");

            Assert.NotNull(basicCard);
            Assert.NotNull(advancedCard);
            Assert.NotNull(generatorCard);
            Assert.NotNull(libraryCard);
            Assert.NotNull(devToolsBtn);

            // Validate none of the cards or buttons clip out of the launcher window
            AppFixture.AssertNotClipped(basicCard, launcher, "Launcher_BasicCard");
            AppFixture.AssertNotClipped(advancedCard, launcher, "Launcher_AdvancedCard");
            AppFixture.AssertNotClipped(generatorCard, launcher, "Launcher_GeneratorCard");
            AppFixture.AssertNotClipped(libraryCard, launcher, "Launcher_LibraryCard");
            AppFixture.AssertNotClipped(devToolsBtn, launcher, "Launcher_DevToolsButton");

            // Validate cards are arranged left-to-right without overlapping
            Assert.True(basicCard.BoundingRectangle.Right < advancedCard.BoundingRectangle.Left,
                "Basic card overlaps or is not to the left of Advanced card!");
            Assert.True(advancedCard.BoundingRectangle.Right < generatorCard.BoundingRectangle.Left,
                "Advanced card overlaps or is not to the left of Generator card!");
            Assert.True(generatorCard.BoundingRectangle.Right < libraryCard.BoundingRectangle.Left,
                "Generator card overlaps or is not to the left of Library card!");
        }

        [Fact]
        public void Launcher_CaptionControls_ArePositionedAtTopRight_AndNotClipped()
        {
            _fixture.Launch();
            var launcher = _fixture.GetLauncherWindow();

            var settingsBtn = AppFixture.WaitForElement(launcher, "Launcher_SettingsButton");
            var minBtn = AppFixture.WaitForElement(launcher, "Launcher_MinimizeButton");
            var exitBtn = AppFixture.WaitForElement(launcher, "Launcher_ExitButton");

            Assert.NotNull(settingsBtn);
            Assert.NotNull(minBtn);
            Assert.NotNull(exitBtn);

            AppFixture.AssertNotClipped(settingsBtn, launcher, "Launcher_SettingsButton");
            AppFixture.AssertNotClipped(minBtn, launcher, "Launcher_MinimizeButton");
            AppFixture.AssertNotClipped(exitBtn, launcher, "Launcher_ExitButton");

            // Buttons should be right-aligned near the top
            Assert.True(exitBtn.BoundingRectangle.Right <= launcher.BoundingRectangle.Right + 10);
            Assert.True(exitBtn.BoundingRectangle.Top >= launcher.BoundingRectangle.Top - 10);
        }

        [Fact]
        public void Launcher_ClickingBasicCard_LaunchesMainScreen()
        {
            _fixture.Launch();
            var launcher = _fixture.GetLauncherWindow();

            var basicCard = AppFixture.WaitForElement(launcher, "Launcher_BasicCard");
            Assert.NotNull(basicCard);

            // Click the Basic mode card
            AppFixture.ClickOrInvoke(basicCard);

            // Wait for and verify MainScreen appears
            var mainScreen = _fixture.GetMainScreenWindow(TimeSpan.FromSeconds(15));
            Assert.NotNull(mainScreen);

            var navDashboard = AppFixture.WaitForElement(mainScreen, "Nav_DashboardButton");
            Assert.NotNull(navDashboard);
            AppFixture.AssertNotClipped(navDashboard, mainScreen, "Nav_DashboardButton");
        }

        [Fact]
        public void Launcher_SettingsButton_TogglesSettingsView_AndReturnsOnBack()
        {
            _fixture.Launch();
            var launcher = _fixture.GetLauncherWindow();

            var settingsBtn = AppFixture.WaitForElement(launcher, "Launcher_SettingsButton");
            Assert.NotNull(settingsBtn);

            // Click settings button to open in-window settings view
            AppFixture.ClickOrInvoke(settingsBtn);
            System.Threading.Thread.Sleep(500);

            // Verify settings view header and back button are visible and not clipped
            var headerTitle = AppFixture.WaitForElement(launcher, "LauncherSettings_HeaderTitle", TimeSpan.FromSeconds(5));
            var backBtn = AppFixture.WaitForElement(launcher, "LauncherSettings_BackButton", TimeSpan.FromSeconds(5));

            Assert.NotNull(headerTitle);
            Assert.NotNull(backBtn);
            AppFixture.AssertNotClipped(headerTitle, launcher, "LauncherSettings_HeaderTitle");
            AppFixture.AssertNotClipped(backBtn, launcher, "LauncherSettings_BackButton");

            // Click back button to return to mode cards view
            AppFixture.ClickOrInvoke(backBtn);
            System.Threading.Thread.Sleep(500);

            // Verify mode cards reappear without clipping
            var basicCard = AppFixture.WaitForElement(launcher, "Launcher_BasicCard", TimeSpan.FromSeconds(5));
            Assert.NotNull(basicCard);
            AppFixture.AssertNotClipped(basicCard, launcher, "Launcher_BasicCard");
        }

        [Fact]
        public void Launcher_DevToolsButton_OpensDevToolsWindow()
        {
            _fixture.Launch();
            var launcher = _fixture.GetLauncherWindow();

            var devToolsBtn = AppFixture.WaitForElement(launcher, "Launcher_DevToolsButton");
            Assert.NotNull(devToolsBtn);

            // Click Dev Tools button
            AppFixture.ClickOrInvoke(devToolsBtn);

            // Find and verify Dev Tools window
            var devWindow = _fixture.GetWindowByTitle("Custom Presets Editor", TimeSpan.FromSeconds(10));
            Assert.NotNull(devWindow);
            Assert.False(devWindow.BoundingRectangle.IsEmpty);

            // Clean up by closing Dev Tools window
            devWindow.Close();
            System.Threading.Thread.Sleep(500);
        }

        [Fact]
        public void Launcher_AdvancedCard_IsDisabledAndDoesNotLaunchMainScreen()
        {
            _fixture.Launch();
            var launcher = _fixture.GetLauncherWindow();

            var advancedCard = AppFixture.WaitForElement(launcher, "Launcher_AdvancedCard");
            Assert.NotNull(advancedCard);

            // Interacting with disabled Advanced card should not trigger MainScreen launch
            AppFixture.ClickOrInvoke(advancedCard);
            System.Threading.Thread.Sleep(400);

            Assert.NotNull(launcher);
            Assert.False(launcher.BoundingRectangle.IsEmpty);

            // Launcher basic card should still be responsive
            var basicCard = AppFixture.WaitForElement(launcher, "Launcher_BasicCard");
            Assert.NotNull(basicCard);
            AppFixture.AssertNotClipped(basicCard, launcher, "Launcher_BasicCard");
        }

        [Fact]
        public void Launcher_GeneratorCard_LaunchesMainScreenInGeneratorMode()
        {
            _fixture.Launch();
            var launcher = _fixture.GetLauncherWindow();

            var generatorCard = AppFixture.WaitForElement(launcher, "Launcher_GeneratorCard");
            Assert.NotNull(generatorCard);

            // Clicking Generator launches MainScreen
            AppFixture.ClickOrInvoke(generatorCard);
            var mainScreen = _fixture.GetMainScreenWindow(TimeSpan.FromSeconds(15));
            Assert.NotNull(mainScreen);

            // Under Construction message should be visible for Generator
            var ucTitle = AppFixture.WaitForElement(mainScreen, "Dashboard_UnderConstructionTitle", TimeSpan.FromSeconds(5));
            Assert.NotNull(ucTitle);
            Assert.Equal("Generator", ucTitle.Name);

            // In Generator mode, ModeBadge should display Generator
            var modeBadge = AppFixture.WaitForElement(mainScreen, "MainScreen_ModeBadge", TimeSpan.FromSeconds(5));
            Assert.NotNull(modeBadge);
            Assert.Equal("Generator", modeBadge.Name);
        }

        [Fact]
        public void Launcher_LibraryCard_LaunchesMainScreenInLibraryMode()
        {
            _fixture.Launch();
            var launcher = _fixture.GetLauncherWindow();

            var libraryCard = AppFixture.WaitForElement(launcher, "Launcher_LibraryCard");
            Assert.NotNull(libraryCard);

            // Clicking Library launches MainScreen
            AppFixture.ClickOrInvoke(libraryCard);
            var mainScreen = _fixture.GetMainScreenWindow(TimeSpan.FromSeconds(15));
            Assert.NotNull(mainScreen);

            // Under Construction message should be visible for Library
            var ucTitle = AppFixture.WaitForElement(mainScreen, "Dashboard_UnderConstructionTitle", TimeSpan.FromSeconds(5));
            Assert.NotNull(ucTitle);
            Assert.Equal("Library", ucTitle.Name);

            // In Library mode, ModeBadge should display Library
            var modeBadge = AppFixture.WaitForElement(mainScreen, "MainScreen_ModeBadge", TimeSpan.FromSeconds(5));
            Assert.NotNull(modeBadge);
            Assert.Equal("Library", modeBadge.Name);
        }
    }
}
