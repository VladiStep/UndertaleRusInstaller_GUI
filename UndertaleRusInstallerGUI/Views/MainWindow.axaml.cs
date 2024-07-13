using Avalonia.Controls;
using Avalonia.Interactivity;
using System.IO;
using System;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;

namespace UndertaleRusInstallerGUI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        Parts = new UserControl[]
        {
            new StartView(this),
            new ChooseGameView(this),
            new ChooseDataFileView(this),
            new ChooseArchivePathView(this),
            new LoadGameView(this),
            new ConfirmInstallView(this),
            new InstallModView(this),
            new FinishView(this)
        };
        lastPartIndex = (short)(Parts.Length - 1);

        InitializeComponent();

        Core.MainWindow = this;
    }
    public UserControl[] Parts { get; set; }
    private short currPartIndex = 0;
    private readonly short lastPartIndex = -1;
    private readonly Thickness copyrightMargin = new(0, 0, 5, 0);
    private ushort? tempGoBackAmount = null;
    private ushort? tempGoNextAmount = null;

    public void ChangeBackButtonState(bool state, ushort amount = 1)
    {
        BackButton.IsEnabled = state;
        if (amount != 1)
            tempGoBackAmount = amount;
    }
    public void ChangeNextButtonState(bool state, ushort amount = 1)
    {
        NextButton.IsEnabled = state;
        if (amount != 1)
            tempGoNextAmount = amount;
    }
    public void ChangeCopyrightState(bool state)
    {
        CopyrightText.IsVisible = state;
        SeparatorRect.Margin = state ? copyrightMargin : default;
    }
    public void GoBack(ushort amount = 1)
    {
        if (currPartIndex == 0)
            return;

        if (tempGoBackAmount is not null)
        {
            amount = (ushort)tempGoBackAmount;
            tempGoBackAmount = null;
        }

        currPartIndex -= (short)amount;
        if (Parts[currPartIndex] is LoadGameView)
            currPartIndex--; // Skip "LoadGameView"

        RefreshCurrentPart();
    }
    public void GoForward(ushort amount = 1)
    {
        if (NextButton.Classes.Contains("finish"))
        {
            OnFinish();
            return;
        }

        if (currPartIndex == lastPartIndex)
            return;

        if (tempGoNextAmount is not null)
        {
            amount = (ushort)tempGoNextAmount;
            tempGoNextAmount = null;
        }

        currPartIndex += (short)amount;

        RefreshCurrentPart();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        GoBack();
    }
    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        GoForward();
    }
    private void RefreshCurrentPart()
    {
        CurrentPartControl.Content = Parts[currPartIndex];
        if (Parts[currPartIndex] is ConfirmInstallView)
            NextButton.Classes.Add("install");
        else
            NextButton.Classes.Remove("install");

        if (currPartIndex == lastPartIndex)
        {
            BackButton.IsVisible = false;
            NextButton.Classes.Add("finish");
            return;
        }

        BackButton.IsVisible = currPartIndex != 0;
    }

    private bool OnCancel()
    {
        if (ScriptQuestion("Вы действительно хотите отменить установку русификатора?"))
        {
            Closing -= Window_Closing;
            Close();
            return true;
        }

        return false;
    }
    private void Window_Closing(object sender, WindowClosingEventArgs e)
    {
#if DEBUG
        if (Design.IsDesignMode)
            return;
#endif
        e.Cancel = true;

        if (OnCancel())
            e.Cancel = false;
    }
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        OnCancel();
    }
    
    private void OnFinish()
    {
        Closing -= Window_Closing;
        Close();
    }
}
