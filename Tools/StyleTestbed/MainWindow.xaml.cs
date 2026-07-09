using System.Collections.Generic;
using System.Windows;
using Raisin.WPF.Base.Controls;

namespace StyleTestbed;

public partial class MainWindow : Window
{
    public List<SelectableItem> SampleFilterItems { get; } = new()
    {
        new("Alpha", null),
        new("Bravo", null),
        new("Charlie", null),
        new("Delta", null),
    };

    public MainWindow()
    {
        DataContext = this;
        InitializeComponent();
    }
}
