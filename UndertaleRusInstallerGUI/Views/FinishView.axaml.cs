using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Core.Plugins;
using Avalonia.LogicalTree;
using System.Linq;
using static UndertaleRusInstallerGUI.Core;

namespace UndertaleRusInstallerGUI.Views
{
    public partial class FinishView : UserControl
    {
        private readonly MainWindow mainWindow;

        public FinishView() // For the designer preview
        {
            InitializeComponent();
        }
        public FinishView(MainWindow mainWindow)
        {
            InitializeComponent();

            this.mainWindow = mainWindow;
        }

        private void UserControl_AttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
        {
            HeaderSuccessText.Text = $"Установка русификатора {SelectedGame} выполнена.";
            SuccessRun.Text = $"Установка русификатора {SelectedGame} успешно завершена.";

            if (mainWindow is not null)
            {
                mainWindow.ChangeNextButtonState(true);
                mainWindow.ChangeCopyrightState(false);
            }
        }
    }
}
