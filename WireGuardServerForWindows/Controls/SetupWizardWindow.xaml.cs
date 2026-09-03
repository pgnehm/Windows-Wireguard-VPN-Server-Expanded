using System;
using System.Windows;
using System.Windows.Input;
using WireGuardServerForWindows.Models;

namespace WireGuardServerForWindows.Controls
{
    /// <summary>
    /// Interaction logic for SetupWizardWindow.xaml
    /// </summary>
    public partial class SetupWizardWindow : Window
    {
        public SetupWizardWindow()
        {
            InitializeComponent();
        }

        protected override void OnActivated(EventArgs e)
        {
            Mouse.OverrideCursor = null;
        }

        protected override void OnClosed(EventArgs e)
        {
            if (DataContext is SetupWizardViewModel viewModel)
            {
                viewModel.Refresh();
            }

            base.OnClosed(e);
        }
    }
}
