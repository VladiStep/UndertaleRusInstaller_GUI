using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static UndertaleRusInstallerGUI.Core;

namespace UndertaleRusInstallerGUI.Views
{
    public partial class ConfirmInstallView : UserControl
    {
        private readonly MainWindow mainWindow;
        public static bool AskNXTaleQuestion { get; set; }

        public ConfirmInstallView() // For the designer preview
        {
            InitializeComponent();
        }
        public ConfirmInstallView(MainWindow mainWindow)
        {
            InitializeComponent();

            this.mainWindow = mainWindow;
        }

        private void UserControl_AttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
        {
            if (mainWindow is null)
                return;

            mainWindow.ChangeNextButtonState(true);

            InstallInfoText.Inlines.Clear();

            if (AskNXTaleQuestion && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                bool replace = mainWindow.ScriptQuestion("�������� - �� ������� NXTale ������ �� �� Windows, ������� ��� ������� ���� ����������� �������� � ����������� ����.\n" +
                                                         "�������� ���?\n(���� � ��� ��� ����� ������, �� ����� �������� ������)");
                ReplaceNXTaleExe = replace;

                if (replace)
                    BackupText.Text = "����� ���������� ����� ������� ��������� ����� ���� ������������� ������ ����.";
                else
                    BackupText.Text = "����� ���������� ����� ������� ��������� ����� ����� ������ ����.";
            }

            FillInstallInfo();
        }

        private void FillInstallInfo()
        {
            Run[] runs = new[]
            {
                new Run($"��������� ����:\n\t{SelectedGame}\n"),
                new Run($"\n���� ����������� ����� ����:\n\t{DataPath}\n"),
            };
            InstallInfoText.Inlines.AddRange(runs);

            if (ReplaceNXTaleExe && File.Exists(NXTaleExePath))
            {
                Run run = new($"\n���� ����������� ������������ ����� ����:\n\t{NXTaleExePath}\n");
                InstallInfoText.Inlines.Add(run);
            }
        }

        private void ScrollViewer_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            // Ported from "SelectableTextBlock.cs"

            if (InstallInfoText.Text?.Length == 0)
                return;

            PointerPoint pointer = e.GetCurrentPoint(InstallInfoText);
            if (!pointer.Properties.IsLeftButtonPressed)
                return;

            Thickness padding = InstallInfoText.Padding;
            Point point = e.GetPosition(InstallInfoText) - new Point(padding.Left, padding.Top);
            int textPos = InstallInfoText.TextLayout.HitTestPoint(in point).TextPosition;
            InstallInfoText.SelectionStart = textPos;
            InstallInfoText.SelectionEnd = textPos;

            e.Pointer.Capture(InstallInfoText);
        }
        private void ScrollViewer_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
           if (e.Pointer.Captured == InstallInfoText)
                e.Pointer.Capture(null);
        }
    }
}
