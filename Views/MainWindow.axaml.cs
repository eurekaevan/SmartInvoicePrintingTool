using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using SmartInvoicePrintingTool.ViewModels;
using SmartInvoicePrintingTool.Utils;

namespace SmartInvoicePrintingTool.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainWindowViewModel vm)
        {
            // 将原生的文件夹选择器注入给 ViewModel
            vm.SelectFolder = async (title) => await SafeFileDialogs.PickFolderAsync(this, title);
        }
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is MainWindowViewModel vm)
            await vm.InitializeAsync();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is IDisposable disposable)
            disposable.Dispose();

        base.OnClosed(e);
    }
}
