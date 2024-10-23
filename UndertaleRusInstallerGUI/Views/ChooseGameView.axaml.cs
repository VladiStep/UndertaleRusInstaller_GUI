using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace UndertaleRusInstallerGUI.Views
{
    public partial class ChooseGameView : UserControl
    {
        private readonly MainWindow mainWindow;
        private bool gameIsSelected;

        public ChooseGameView() // For the designer preview
        {
            InitializeComponent();
        }
        public ChooseGameView(MainWindow mainWindow)
        {
            InitializeComponent();

            this.mainWindow = mainWindow;
        }

        private void UserControl_AttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
        {
            if (mainWindow is not null)
            {
                mainWindow.ChangeNextButtonState(gameIsSelected);
                mainWindow.ChangeCopyrightState(true);
            }
        }

        private void OnGameSelected()
        {
            if (!gameIsSelected)
            {
                gameIsSelected = true;
                mainWindow?.ChangeNextButtonState(true);
            }
        }
        private void UTRadioButton_Click(object sender, RoutedEventArgs e)
        {
            Core.SelectedGame = Core.GameType.Undertale;
            ConfirmInstallView.AskXBOXTALEQuestion = false;

            OnGameSelected();
        }
        private void NXTRadioButton_Click(object sender, RoutedEventArgs e)
        {
            Core.SelectedGame = Core.GameType.XBOXTALE;
            ConfirmInstallView.AskXBOXTALEQuestion = true;

            OnGameSelected();
        }
    }
}
