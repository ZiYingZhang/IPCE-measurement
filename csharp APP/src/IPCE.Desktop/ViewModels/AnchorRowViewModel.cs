namespace IPCE.Desktop.ViewModels;

public sealed class AnchorRowViewModel : ViewModelBase
{
    private double _wavelengthNm;
    private double _confirmedTimeSeconds;

    public AnchorRowViewModel(
        double wavelengthNm = 0,
        double confirmedTimeSeconds = 0)
    {
        _wavelengthNm = wavelengthNm;
        _confirmedTimeSeconds = confirmedTimeSeconds;
    }

    public double WavelengthNm
    {
        get => _wavelengthNm;
        set => SetProperty(ref _wavelengthNm, value);
    }

    public double ConfirmedTimeSeconds
    {
        get => _confirmedTimeSeconds;
        set => SetProperty(ref _confirmedTimeSeconds, value);
    }
}
