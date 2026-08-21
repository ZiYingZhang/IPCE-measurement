using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using IPCE.Core.Domain;
using IPCE.Core.Errors;
using IPCE.Desktop.Localization;
using IPCE.Desktop.Plotting;
using IPCE.Desktop.State;
using IPCE.Desktop.ViewModels;

namespace IPCE.Desktop.Views;

public partial class ResultTabs : UserControl
{
    private static readonly ILocalizationService PlotText =
        EnglishPlotLocalizationService.Instance;
    private MainViewModel? _subscribedViewModel;

    public ResultTabs()
    {
        InitializeComponent();
        Loaded += (_, _) => Connect();
        Unloaded += (_, _) => Disconnect();
        DataContextChanged += (_, _) => Connect();
    }

    private void Connect()
    {
        Disconnect();
        _subscribedViewModel = DataContext as MainViewModel;
        if (_subscribedViewModel is null)
        {
            RenderAll();
            return;
        }

        _subscribedViewModel.Session.PropertyChanged += SessionChanged;
        _subscribedViewModel.Silicon.PropertyChanged += SiliconChanged;
        _subscribedViewModel.Sample.PropertyChanged += SampleChanged;
        _subscribedViewModel.Spectrum.PropertyChanged += SpectrumChanged;
        PropertyChangedEventManager.AddHandler(
            _subscribedViewModel.Localization,
            LocalizationChanged,
            "Item[]");
        UpdateLocalizedHeaders();
        RenderAll();
    }

    private void Disconnect()
    {
        if (_subscribedViewModel is null)
        {
            return;
        }

        _subscribedViewModel.Session.PropertyChanged -= SessionChanged;
        _subscribedViewModel.Silicon.PropertyChanged -= SiliconChanged;
        _subscribedViewModel.Sample.PropertyChanged -= SampleChanged;
        _subscribedViewModel.Spectrum.PropertyChanged -= SpectrumChanged;
        PropertyChangedEventManager.RemoveHandler(
            _subscribedViewModel.Localization,
            LocalizationChanged,
            "Item[]");
        _subscribedViewModel = null;
    }

    private void SessionChanged(
        object? sender,
        PropertyChangedEventArgs eventArgs)
    {
        switch (eventArgs.PropertyName)
        {
            case nameof(SessionState.SiliconTrace):
            case nameof(SessionState.SiliconAnchors):
            case nameof(SessionState.SampleTrace):
            case nameof(SessionState.SampleAnchors):
                RenderTraces();
                RenderSchedule();
                break;
            case nameof(SessionState.PowerDensity):
            case nameof(SessionState.PowerDensityStatus):
                RenderTraces();
                RenderPowerDensity();
                break;
            case nameof(SessionState.CalculatedIpce):
            case nameof(SessionState.CalculatedIpceStatus):
            case nameof(SessionState.ExternalIpce):
            case nameof(SessionState.SelectedIpceSource):
                RenderTraces();
                RenderIpce();
                RenderSpectrumIntegration();
                break;
            case nameof(SessionState.Spectrum):
            case nameof(SessionState.IntegrationResult):
            case nameof(SessionState.IntegrationStatus):
                RenderSpectrumIntegration();
                break;
        }
    }

    private void SiliconChanged(
        object? sender,
        PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is
            nameof(SiliconWorkflowViewModel.Preview) or
            nameof(SiliconWorkflowViewModel.AveragingDurationSeconds) or
            nameof(SiliconWorkflowViewModel.SubtractDark) or
            nameof(SiliconWorkflowViewModel.DarkStartSeconds) or
            nameof(SiliconWorkflowViewModel.DarkEndSeconds))
        {
            RenderTraces();
            RenderSchedule();
        }
    }

    private void SampleChanged(
        object? sender,
        PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is
            nameof(SampleWorkflowViewModel.Preview) or
            nameof(SampleWorkflowViewModel.AveragingDurationSeconds) or
            nameof(SampleWorkflowViewModel.SubtractDark) or
            nameof(SampleWorkflowViewModel.DarkStartSeconds) or
            nameof(SampleWorkflowViewModel.DarkEndSeconds))
        {
            RenderTraces();
            RenderSchedule();
        }
    }

    private void SpectrumChanged(
        object? sender,
        PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is
            nameof(SpectrumWorkflowViewModel.IntegrationMinimumNanometres) or
            nameof(SpectrumWorkflowViewModel.IntegrationMaximumNanometres) or
            nameof(SpectrumWorkflowViewModel.Coverage))
        {
            RenderSpectrumIntegration();
        }
    }

    private void LocalizationChanged(
        object? sender,
        PropertyChangedEventArgs eventArgs)
    {
        UpdateLocalizedHeaders();
        RenderAll();
    }

    private void UpdateLocalizedHeaders()
    {
        SiliconWavelengthColumn.Header =
            Text["Results.WavelengthColumn"];
        SampleWavelengthColumn.Header =
            Text["Results.WavelengthColumn"];
        SiliconConfirmedTimeColumn.Header =
            Text["Results.ConfirmedTimeColumn"];
        SampleConfirmedTimeColumn.Header =
            Text["Results.ConfirmedTimeColumn"];
    }

    private void RenderAll()
    {
        RenderTraces();
        RenderSchedule();
        RenderPowerDensity();
        RenderIpce();
        RenderSpectrumIntegration();
    }

    private void RenderTraces()
    {
        MainViewModel? viewModel = _subscribedViewModel;
        IReadOnlyList<TraceMeanResult> siliconMeans =
            viewModel?.Silicon.PowerDensity?
                .Select(point => new TraceMeanResult(
                    point.WavelengthNm,
                    point.SiliconMeanCurrentAmperes,
                    point.SampleCount))
                .ToArray() ?? [];
        IReadOnlyList<TraceMeanResult> sampleMeans =
            viewModel?.Sample.CalculatedIpce?
                .Select(point => new TraceMeanResult(
                    point.WavelengthNm,
                    point.SampleMeanCurrentAmperes,
                    point.SampleCount))
                .ToArray() ?? [];
        SiliconTraceView.Render(ResultPlotModelBuilder.BuildTrace(
            PlotText["Plot.SiliconTraceTitle"],
            viewModel?.Silicon.Trace,
            viewModel?.Silicon.Anchors,
            viewModel?.Silicon.SubtractDark ?? false,
            viewModel?.Silicon.DarkStartSeconds ?? 0,
            viewModel?.Silicon.DarkEndSeconds ?? 0,
            viewModel?.Silicon.Preview,
            viewModel?.Silicon.AveragingDurationSeconds ?? 0,
            siliconMeans,
            viewModel?.Session.PowerDensityStatus ??
                new ResultStatus(ResultFreshness.Missing, ""),
            PlotText));
        SampleTraceView.Render(ResultPlotModelBuilder.BuildTrace(
            PlotText["Plot.SampleTraceTitle"],
            viewModel?.Sample.Trace,
            viewModel?.Sample.Anchors,
            viewModel?.Sample.SubtractDark ?? false,
            viewModel?.Sample.DarkStartSeconds ?? 0,
            viewModel?.Sample.DarkEndSeconds ?? 0,
            viewModel?.Sample.Preview,
            viewModel?.Sample.AveragingDurationSeconds ?? 0,
            sampleMeans,
            viewModel?.Session.CalculatedIpceStatus ??
                new ResultStatus(ResultFreshness.Missing, ""),
            PlotText));
    }

    private void RenderSchedule()
    {
        List<PlotSeries> series = [];
        AddScheduleSeries(
            series,
            PlotText["Plot.SiliconOwner"],
            "#1976D2",
            _subscribedViewModel?.Silicon.Preview);
        AddScheduleSeries(
            series,
            PlotText["Plot.SampleOwner"],
            "#00897B",
            _subscribedViewModel?.Sample.Preview);
        SchedulePlotView.Render(new PlotModel(
            PlotText["Plot.ScheduleTitle"],
            PlotText["Plot.WavelengthAxis"],
            PlotText["Plot.ConfirmedTimeAxis"],
            series,
            [],
            PlotText["Plot.EmptySchedule"]));
    }

    private void RenderPowerDensity()
    {
        IReadOnlyList<PowerDensityPoint>? points =
            _subscribedViewModel?.Silicon.PowerDensity;
        PlotSeries[] series = points is { Count: > 0 }
            ?
            [
                new PlotSeries(
                    PlotText["Plot.PowerDensitySeries"],
                    points.Select(point => point.WavelengthNm).ToArray(),
                    points.Select(point =>
                        point.IncidentPowerDensityWattsPerSquareCentimetre *
                        1e6).ToArray(),
                    PlotSeriesKind.Line,
                    "#00897B",
                    points.Select(point =>
                        point.IncidentPowerDensityStandardError * 1e6)
                        .ToArray()),
            ]
            : [];
        PowerDensityPlotView.Render(new PlotModel(
            PlotText["Plot.PowerDensityTitle"],
            PlotText["Plot.WavelengthAxis"],
            PlotText["Plot.PowerDensityAxis"],
            series,
            [],
            PlotText["Plot.EmptyPowerDensity"]));
    }

    private void RenderIpce()
    {
        List<PlotSeries> series = [];
        IReadOnlyList<IpcePoint>? calculated =
            _subscribedViewModel?.Sample.CalculatedIpce;
        if (calculated is { Count: > 0 })
        {
            series.Add(new PlotSeries(
                PlotText["Plot.CalculatedIpceSeries"],
                calculated.Select(point => point.WavelengthNm).ToArray(),
                calculated.Select(point => point.IpcePercent).ToArray(),
                PlotSeriesKind.Line,
                "#1976D2",
                calculated.Select(point =>
                    point.IpceEstimatedStandardErrorPercent).ToArray()));
        }

        ExternalIpceData? external =
            _subscribedViewModel?.Spectrum.ExternalIpce;
        if (external?.Points is { Count: > 0 } externalPoints)
        {
            series.Add(new PlotSeries(
                PlotText["Plot.ExternalIpceSeries"],
                externalPoints.Select(point => point.WavelengthNm).ToArray(),
                externalPoints.Select(point => point.IpcePercent).ToArray(),
                PlotSeriesKind.Line,
                "#EF6C00"));
        }

        IpcePlotView.Render(new PlotModel(
            PlotText["Plot.IpceComparisonTitle"],
            PlotText["Plot.WavelengthAxis"],
            "IPCE (%)",
            series,
            [],
            PlotText["Plot.EmptyIpce"]));
        IpcePlotView.SetSelectedSource(
            _subscribedViewModel?.Session.SelectedIpceSource ==
                IpceSource.External
                ? Text["Plot.ExternalIpceSeries"]
                : Text["Plot.CalculatedIpceSeries"],
            Text);
    }

    private void RenderSpectrumIntegration()
    {
        MainViewModel? viewModel = _subscribedViewModel;
        double minimum =
            viewModel?.Spectrum.IntegrationMinimumNanometres ?? 300;
        double maximum =
            viewModel?.Spectrum.IntegrationMaximumNanometres ?? 1100;
        IReadOnlyList<SpectrumPoint>? spectrum =
            viewModel?.Session.Spectrum;
        IReadOnlyList<IpceValue> selected = SelectedIpce(viewModel);
        IntegrationResult? integration =
            viewModel?.Spectrum.IntegrationResult;
        SpectrumPlotModels models =
            ResultPlotModelBuilder.BuildSpectrumIntegration(
                spectrum,
                selected,
                integration,
                minimum,
                maximum,
                PlotText);
        SpectrumIntegrationPlotView.Render(
            models.Irradiance,
            models.SelectedIpce,
            models.Cumulative,
            integration?.Summary,
            Text);
    }

    private void AddScheduleSeries(
        List<PlotSeries> target,
        string owner,
        string colorHex,
        SchedulePreview? preview)
    {
        if (preview is null)
        {
            return;
        }

        SchedulePoint[] valid = preview.Points
            .Where(point =>
                point.ReferenceTimeSeconds >= preview.Coverage.DataMinimum &&
                point.ReferenceTimeSeconds <= preview.Coverage.DataMaximum)
            .ToArray();
        SchedulePoint[] invalid = preview.Points.Except(valid).ToArray();
        if (valid.Length > 0)
        {
            target.Add(new PlotSeries(
                PlotText.Format("Plot.OwnerSchedule", owner),
                valid.Select(point => point.WavelengthNm).ToArray(),
                valid.Select(point => point.ReferenceTimeSeconds).ToArray(),
                PlotSeriesKind.Line,
                colorHex));
        }
        if (invalid.Length > 0)
        {
            target.Add(new PlotSeries(
                PlotText.Format("Plot.OwnerOutOfRange", owner),
                invalid.Select(point => point.WavelengthNm).ToArray(),
                invalid.Select(point => point.ReferenceTimeSeconds).ToArray(),
                PlotSeriesKind.Scatter,
                "#C62828"));
        }
        if (preview.Anchors.Count > 0)
        {
            target.Add(new PlotSeries(
                PlotText.Format("Plot.OwnerAnchors", owner),
                preview.Anchors.Select(point => point.WavelengthNm).ToArray(),
                preview.Anchors.Select(point =>
                    point.ConfirmedTimeSeconds).ToArray(),
                PlotSeriesKind.Scatter,
                "#EF6C00",
                contributesToAutoRange: false));
        }
    }

    private static IReadOnlyList<IpceValue> SelectedIpce(
        MainViewModel? viewModel)
    {
        if (viewModel is null)
        {
            return [];
        }

        if (viewModel.Session.SelectedIpceSource == IpceSource.External)
        {
            return viewModel.Session.ExternalIpce?.Points ?? [];
        }

        return viewModel.Session.CalculatedIpce?
            .Select(point => new IpceValue(
                point.WavelengthNm,
                point.IpcePercent))
            .ToArray() ?? [];
    }

    private void AddSiliconAnchor_Click(
        object sender,
        RoutedEventArgs eventArgs)
    {
        _subscribedViewModel?.Silicon.EditableAnchors.Add(
            new AnchorRowViewModel());
    }

    private void ApplySiliconAnchors_Click(
        object sender,
        RoutedEventArgs eventArgs)
    {
        if (_subscribedViewModel is null) return;
        CommitAnchorGrid(SiliconAnchorGrid);
        TryApplyAnchors(() =>
            _subscribedViewModel.Silicon.ReplaceAnchors(
                _subscribedViewModel.Silicon.EditableAnchors));
    }

    private void DeleteSiliconAnchor_Click(
        object sender,
        RoutedEventArgs eventArgs)
    {
        if (_subscribedViewModel is not null &&
            SiliconAnchorGrid.SelectedItem is AnchorRowViewModel row)
        {
            TryApplyAnchors(() =>
                _subscribedViewModel.Silicon.DeleteAnchor(row));
        }
    }

    private void AddSampleAnchor_Click(
        object sender,
        RoutedEventArgs eventArgs)
    {
        _subscribedViewModel?.Sample.EditableAnchors.Add(
            new AnchorRowViewModel());
    }

    private void ApplySampleAnchors_Click(
        object sender,
        RoutedEventArgs eventArgs)
    {
        if (_subscribedViewModel is null) return;
        CommitAnchorGrid(SampleAnchorGrid);
        TryApplyAnchors(() =>
            _subscribedViewModel.Sample.ReplaceAnchors(
                _subscribedViewModel.Sample.EditableAnchors));
    }

    private void DeleteSampleAnchor_Click(
        object sender,
        RoutedEventArgs eventArgs)
    {
        if (_subscribedViewModel is not null &&
            SampleAnchorGrid.SelectedItem is AnchorRowViewModel row)
        {
            TryApplyAnchors(() =>
                _subscribedViewModel.Sample.DeleteAnchor(row));
        }
    }

    private static void CommitAnchorGrid(DataGrid grid)
    {
        grid.CommitEdit(DataGridEditingUnit.Cell, true);
        grid.CommitEdit(DataGridEditingUnit.Row, true);
    }

    private void TryApplyAnchors(Action operation)
    {
        try
        {
            operation();
        }
        catch (IpceException exception)
        {
            MessageBox.Show(
                Window.GetWindow(this),
                new UserMessageLocalizer(Text).Localize(exception),
                Text["Dialog.InvalidAnchorEdit"],
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private ILocalizationService Text =>
        _subscribedViewModel?.Localization ??
        LocalizationService.Current;
}
