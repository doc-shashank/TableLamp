using System;
using TableLamp.Views;

namespace TableLamp.Controllers
{
    /// <summary>
    /// GeneratorUI controller responsible for controlling UI elements and workflow
    /// on DashboardPage when invoked in Generator mode.
    /// </summary>
    public class GeneratorUI
    {
        private readonly DashboardPage _dashboardPage;

        public GeneratorUI(DashboardPage dashboardPage)
        {
            _dashboardPage = dashboardPage ?? throw new ArgumentNullException(nameof(dashboardPage));
        }

        public void ApplyGeneratorMode()
        {
            _dashboardPage.ShowUnderConstruction("Generator", "The Generator workflow is currently under construction.");
        }
    }
}
