using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            //_measurements = Measurements;
            InitializeComponent();
            _measurements = Measurements;
            RenderChart();
        }


        private void MainMenuButton_Click(object sender, RoutedEventArgs e)
        {
             NavigationService?.GoBack();
        }

        private void RenderChart()
        {             // Clear existing data points
            
            //.Series[0].Points.Clear();
            // Add new data points from the measurements

            if (_measurements.Count == 0)
            {
                MeasurementsPlot.Refresh();
                return;
            }

            /*var xs = _measurements.Select(m => m.Timestamp.ToOADate()).ToArray();
            var ys = _measurements.Select(m => m.Y).ToArray();*/
            var xs = _measurements.Select(m => m.X).ToArray();
            var ys = _measurements.Select(m => m.Y).ToArray();

            MeasurementsPlot.Plot.Clear();

            var circle = MeasurementsPlot.Plot.Add.Circle(AxisCenter, AxisCenter, AxisRadius);
            circle.LineWidth = 2;
            circle.FillColor = ScottPlot.Colors.Transparent;
            circle.LineColor = ScottPlot.Colors.Red;

            MeasurementsPlot.Plot.Axes.SetLimits(AxisMin, AxisMax, AxisMin, AxisMax);
            MeasurementsPlot.Plot.Axes.SquareUnits();

            MeasurementsPlot.Plot.Add.Scatter(xs, ys);

            //MeasurementsPlot.Plot.Add.Radar(xs, ys);
            //MeasurementsPlot.Plot.XAxis.DateTimeFormat(true);
            //MeasurementsPlot.Plot.Axes.X
            MeasurementsPlot.Plot.Title("Pisteet");
            //MeasurementsPlot.Plot.YLabel("Y");
            //MeasurementsPlot.Plot.XLabel("X");
            MeasurementsPlot.Refresh();
        }
    }

}

