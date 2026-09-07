using System;
using Microsoft.UI.Xaml;
using TableLamp.Views;

namespace TableLamp.Controllers
{
    /// <summary>
    /// CommonUI controller responsible for standard navigation, top-bar chrome,
    /// theme management, and window lifecycle across screens.
    /// </summary>
    public class CommonUI
    {
        private readonly MainScreen _mainScreen;

        public CommonUI(MainScreen mainScreen)
        {
            _mainScreen = mainScreen ?? throw new ArgumentNullException(nameof(mainScreen));
        }

        public void NavigateToDashboard()
        {
            _mainScreen.NavigateToDashboard();
        }

        public void NavigateToCalendar()
        {
            _mainScreen.NavigateToCalendar();
        }
    }
}
