using Avalonia.Controls;
using SerwerGraNaGrafie.ViewModels;

namespace SerwerGraNaGrafie.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        MyReferences.MainWindow = this;
    }
    
}