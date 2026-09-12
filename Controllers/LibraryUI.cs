using System;
using TableLamp.Views;

namespace TableLamp.Controllers
{
    /// <summary>
    /// LibraryUI controller responsible for controlling UI elements and workflow
    /// on DashboardPage when invoked in Library mode.
    /// </summary>
    public class LibraryUI
    {
        private readonly DashboardPage _dashboardPage;

        public LibraryUI(DashboardPage dashboardPage)
        {
            _dashboardPage = dashboardPage ?? throw new ArgumentNullException(nameof(dashboardPage));
        }

        public void ApplyLibraryMode()
        {
            _dashboardPage.ShowUnderConstruction("Library", "The Library workflow is currently under construction.");
        }
    }
}
