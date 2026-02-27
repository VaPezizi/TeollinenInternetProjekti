using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LukuOhjelma
{
    /// <summary>
    /// Interaction logic for StatisticsPage.xaml
    /// </summary>
    public partial class StatisticsPage : Page
    {
        private readonly ObservableCollection<MqttMeasurement> _measurements;
        private const double AxisMin = 0;
        private const double AxisMax = 4095;
        private const double AxisCenter = (AxisMin + AxisMax) / 2;
        private const double AxisRadius = AxisMax / 2;

        public StatisticsPage(ObservableCollection<MqttMeasurement> Measurements)
        {
            InitializeComponent();
            _measurements = Measurements;
            MeasurementsPlot.UserInputProcessor.Disable();
            RenderStickChart();
            RenderURMChart();
            RenderPotChart();
            CalculateStats();
        }

        private void MainMenuButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }

        private void DateRangeChanged(object? sender, SelectionChangedEventArgs e)
        {
            RenderStickChart();
            RenderURMChart();
            RenderPotChart();
            CalculateStats();
        }

        private IEnumerable<MqttMeasurement> GetFilteredMeasurements()
        {
            var query = _measurements.AsEnumerable();

            if (FromDatePicker.SelectedDate is DateTime fromDate)
                query = query.Where(m => m.Timestamp >= fromDate.Date);

            if (ToDatePicker.SelectedDate is DateTime toDate)
            {
                var endExclusive = toDate.Date.AddDays(1);
                query = query.Where(m => m.Timestamp < endExclusive);
            }

            return query;
        }

        private void CalculateStats()
        {
            var filtered = GetFilteredMeasurements().ToList();
            if (filtered.Count == 0)
            {
                AVGX.Text = "Average X: N/A";
                AVGY.Text = "Average Y: N/A";
                SWUP.Text = "SW %: N/A";
                return;
            }

            var avgx = filtered.Average(m => m.X);
            var avgy = filtered.Average(m => m.Y);
            var swu = filtered.Count(m => m.Sw) / (double)filtered.Count * 100;

            AVGX.Text = $"Average X: {avgx:F1}";
            AVGY.Text = $"Average Y: {avgy:F1}";
            SWUP.Text = $"SW %: {swu:F1} %";

            AVGPOT.Text = $"Average Pot: {filtered.Average(m => m.Pot):F1}";
            AVGURM.Text = $"Average URM: {filtered.Average(m => m.Urm):F1}";
        }

        private void RenderStickChart()
        {
            var filtered = GetFilteredMeasurements().ToList();

            MeasurementsPlot.Plot.Clear();

            var circle = MeasurementsPlot.Plot.Add.Circle(AxisCenter, AxisCenter, AxisRadius);
            circle.LineWidth = 2;
            circle.FillColor = ScottPlot.Colors.Transparent;
            circle.LineColor = ScottPlot.Colors.Red;

            MeasurementsPlot.Plot.Axes.SetLimits(AxisMin, AxisMax, AxisMin, AxisMax);
            MeasurementsPlot.Plot.Axes.Margins(0.15, 0.1);
            MeasurementsPlot.Plot.Axes.SquareUnits();

            if (filtered.Count > 0)
            {
                var xs = filtered.Select(m => m.X).ToArray();
                var ys = filtered.Select(m => m.Y).ToArray();
                MeasurementsPlot.Plot.Add.Scatter(xs, ys);
            }

            MeasurementsPlot.Plot.Title("Tatin sijainnit");
            MeasurementsPlot.Refresh();
        }

        private void RenderURMChart()
        {
            var filtered = GetFilteredMeasurements().ToList();
            URMPlot.Plot.Clear();

            if (filtered.Count > 0)
            {
                var xs = filtered.Select(m => m.Timestamp.ToOADate()).ToArray();
                var ys = filtered.Select(m => m.Urm).ToArray();
                URMPlot.Plot.Add.Scatter(xs, ys);
            }

            URMPlot.Plot.Title("URM over time");
            URMPlot.Plot.YLabel("URM");
            URMPlot.Plot.XLabel("Time");
            URMPlot.Refresh();
        }

        private void ResetButtonClick(object sender, RoutedEventArgs e)
        {
            FromDatePicker.SelectedDate = null;
            ToDatePicker.SelectedDate = null;
            RenderStickChart();
            RenderURMChart();
            RenderPotChart();
            CalculateStats();
        }

        private void ApplyButtonClick(object sender, RoutedEventArgs e)
        {
            RenderStickChart();
            RenderURMChart();
            RenderPotChart();
            CalculateStats();
        }

        private void RenderPotChart()
        {
            var filtered = GetFilteredMeasurements().ToList();
            POTplot.Plot.Clear();

            if (filtered.Count > 0)
            {
                var xs = filtered.Select(m => m.Timestamp.ToOADate()).ToArray();
                var ys = filtered.Select(m => m.Pot).ToArray();
                POTplot.Plot.Add.Scatter(xs, ys);
            }

            POTplot.Plot.Title("Pot over time");
            POTplot.Plot.YLabel("Pot");
            POTplot.Plot.XLabel("Time");
            POTplot.Refresh();
        }
    }
}

