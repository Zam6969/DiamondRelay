using System;
using System.ComponentModel;
using VrcTwitchOscBridge.Infrastructure;
using VrcTwitchOscBridge.Models;

namespace VrcTwitchOscBridge.ViewModels;

public sealed class CashPaymentManagerViewModel : ObservableObject, IDisposable
{
    private readonly MainWindowViewModel? mainWindowViewModel;
    private bool disposed;

    public CashPaymentConnectionSettings CashPayments { get; }

    public CashPaymentManagerViewModel(AppSettings settings, MainWindowViewModel? mainWindowViewModel)
    {
        ArgumentNullException.ThrowIfNull(settings);
        CashPayments = settings.CashPayments;
        this.mainWindowViewModel = mainWindowViewModel;
        CashPayments.PropertyChanged += OnCashPaymentsPropertyChanged;
    }



    private void OnCashPaymentsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        CashPayments.PropertyChanged -= OnCashPaymentsPropertyChanged;
    }
}
