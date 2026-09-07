using System;
using TableLamp.Services;
using TableLamp.Views;

namespace TableLamp.Controllers
{
    /// <summary>
    /// BasicUI controller responsible for controlling what and when content is shown
    /// on DashboardPage when invoked in Basic mode.
    /// In v0.0.2: Controls the Dashboard with centralized CardButtons ('Create a New Session',
    /// 'Review Session') and the Recent Sessions section.
    /// </summary>
    public class BasicUI
    {
        private readonly DashboardPage _dashboardPage;

        public BasicUI(DashboardPage dashboardPage)
        {
            _dashboardPage = dashboardPage ?? throw new ArgumentNullException(nameof(dashboardPage));
        }

        /// <summary>
        /// Loads recent sessions from SessionService and displays them in the Recent Sessions section of DashboardPage.
        /// </summary>
        public void LoadRecentSessions()
        {
            var recent = SessionService.Instance.GetRecentSessions(5);
            _dashboardPage.PopulateRecentSessions(recent);
        }
    }
}
