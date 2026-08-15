using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ScottPlot.WPF;

namespace IPCE.Desktop.Views.Plots;

internal static class PlotViewSaveHelper
{
    public static void Save(WpfPlot plot, UserControl owner)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "PNG 图像 (*.png)|*.png",
            DefaultExt = ".png",
        };
        if (dialog.ShowDialog(Window.GetWindow(owner)) == true)
        {
            plot.Plot.SavePng(
                dialog.FileName,
                Math.Max(1, (int)plot.ActualWidth),
                Math.Max(1, (int)plot.ActualHeight));
        }
    }
}
