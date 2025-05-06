using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Game2048;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void UIElement_OnGotFocus(object sender, RoutedEventArgs e)
    {
        GamePage.Focus();
    }

    private void MainWindow_OnKeyDown(object sender, KeyEventArgs e)
    {
        GamePage.InvokeKeyDown(e);
    }
}