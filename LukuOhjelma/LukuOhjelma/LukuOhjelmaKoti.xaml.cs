using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Net.Http;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using System.Diagnostics;

namespace LukuOhjelma
{
    /// <summary>
    /// Interaction logic for LukuOhjelmaKoti.xaml
    /// </summary>
    public partial class LukuOhjelmaKoti : Page
    {
        private readonly IMqttClient _mqttClient;
        private readonly MqttDataViewModel _viewModel = new MqttDataViewModel();

        // Reuse a single HTTP client instance for loading initial data from the backend.
        private static readonly HttpClient _httpClient = new HttpClient();

        private const string BrokerHost = "d2df4fc873c54748bee9fd798c800064.s1.eu.hivemq.cloud"; 
        private const int BrokerPort = 8883;
        private const string Topic = "teollinen/data"; 

        public LukuOhjelmaKoti()
        {
            InitializeComponent();
            DataContext = _viewModel;

            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();

            Loaded += LukuOhjelmaKoti_Loaded;
            Unloaded += LukuOhjelmaKoti_Unloaded;
        }

        private async void LukuOhjelmaKoti_Loaded(object sender, RoutedEventArgs e)
        {
            // First load existing measurements from the backend API, then start MQTT subscription.
            await LoadInitialMeasurementsAsync();
            await ConnectAndSubscribeAsync();
        }

        private async void LukuOhjelmaKoti_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_mqttClient.IsConnected)
                {
                    await _mqttClient.DisconnectAsync();
                }
            }
            catch
            {
            }
        }

        private async Task LoadInitialMeasurementsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("http://193.166.25.207:3001/api/measurements");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                MeasurementsResponse? wrapper = null;
                List<ApiMeasurement>? items = null;

                try
                {
                    // Prefer wrapped form: { measurements: [...] }
                    wrapper = JsonSerializer.Deserialize<MeasurementsResponse>(json, options);
                }
                catch (JsonException)
                {
                    // If the API returns a bare array instead of an object, fall back below.
                }

                if (wrapper?.measurements != null)
                {
                    items = wrapper.measurements;
                }
                else
                {
                    // Fallback: API returned a plain array of measurements.
                    items = JsonSerializer.Deserialize<List<ApiMeasurement>>(json, options);
                }

                if (items == null)
                {
                    return;
                }

                // Update collection on UI thread.
                Dispatcher.Invoke(() =>
                {
                    _viewModel.Measurements.Clear();

                    foreach (var item in items)
                    {
						bool swBool = false;
						if (item.sw.ValueKind == JsonValueKind.Number)
						{
							if (item.sw.TryGetInt32(out var n))
							{
								swBool = n != 0;
							}
						}
						else if (item.sw.ValueKind == JsonValueKind.True)
						{
							swBool = true;
						}

                        var measurement = new MqttMeasurement
                        {
							Id = item._id,
                            Timestamp = item.timestamp ?? DateTime.Now,
                            Y = item.y,
                            X = item.x,
							Sw = swBool,
                            Pot = item.pot,
                            Urm = item.urm
                        };

                        _viewModel.Measurements.Add(measurement);
                    }
                });
            }
            catch (Exception ex)
            {
                // If the backend is unavailable or deserialization fails, show an error but still allow live MQTT.
                MessageBox.Show($"Failed to load existing measurements: {ex.Message}", "Backend error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task ConnectAndSubscribeAsync()
        {
            if (_mqttClient.IsConnected)
            {
                return;
            }

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(BrokerHost, BrokerPort)
                .WithClientId($"LukuOhjelmaClient_{Environment.MachineName}")
                .WithCredentials("teollineninternet", "Teollineninternet1")
                .WithTls(o =>
                {
                    
                    o.UseTls = true;
                    o.AllowUntrustedCertificates = true;
                    o.IgnoreCertificateChainErrors = true;
                    o.IgnoreCertificateRevocationErrors = true;
                    o.SslProtocol = System.Security.Authentication.SslProtocols.Tls12;
                    
                })
             
                .WithProtocolVersion(MqttProtocolVersion.V500)
                .Build();

            _mqttClient.ApplicationMessageReceivedAsync += e =>
            {
                HandleIncomingMessage(e.ApplicationMessage);
                return Task.CompletedTask;
            };

            try
            {
                await _mqttClient.ConnectAsync(options, CancellationToken.None);
                await _mqttClient.SubscribeAsync(Topic);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to connect to MQTT broker: {ex.Message}", "MQTT error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HandleIncomingMessage(MqttApplicationMessage message)
        {
            try
            {
                var payloadSegment = message.PayloadSegment;
                var payload = payloadSegment.Count > 0 && payloadSegment.Array != null
                    ? Encoding.UTF8.GetString(payloadSegment.Array, payloadSegment.Offset, payloadSegment.Count)
                    : string.Empty;

                if (string.IsNullOrWhiteSpace(payload))
                {
                    return;
                }

                var data = JsonSerializer.Deserialize<MqttPayload>(payload, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (data == null)
                {
                    return;
                }

                // Marshal updates to the UI thread.
                Dispatcher.Invoke(() =>
                {
                    var measurement = new MqttMeasurement
                    {
                        Id = data._id ?? data.id,
                        Timestamp = DateTime.Now,
                        Y = data.y,
                        X = data.x,
						Sw = data.sw != 0,
                        Pot = data.pot,
                        Urm = data.urm
                    };

                    // Add latest measurement to the list shown in the UI.
                    _viewModel.Measurements.Add(measurement);

                    // Optionally keep the list from growing without bound.
                    if (_viewModel.Measurements.Count > 1000)
                    {
                        _viewModel.Measurements.RemoveAt(0);
                    }
                });
            }
            catch
            {
                // Swallow malformed messages for now; consider logging if needed.
            }
        }

        private void MeasurementsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_viewModel.SelectedMeasurement != null)
            {
                _viewModel.ListViewVisibility = Visibility.Collapsed;
                _viewModel.DetailViewVisibility = Visibility.Visible;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.DetailViewVisibility = Visibility.Collapsed;
            _viewModel.ListViewVisibility = Visibility.Visible;
            _viewModel.SelectedMeasurement = null;
            MeasurementsListBox.SelectedItem = null;
        }

        private void StatisticsButton_Click(object sender, RoutedEventArgs e)
        {
            var statsPage = new StatisticsPage(_viewModel.Measurements);
            NavigationService?.Navigate(statsPage);
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            await DeleteSelectedMeasurementsAsync();
        }

        private async void MeasurementsListBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                await DeleteSelectedMeasurementsAsync();
                e.Handled = true;
            }
        }

        private async Task DeleteSelectedMeasurementsAsync()
        {
            // Collect all measurements that are marked for deletion via the checkbox.
            var selected = _viewModel.Measurements
                .Where(m => m.IsMarkedForDeletion)
                .ToList();

            if (selected.Count == 0)
            {
                Debug.WriteLine("No measurements marked for deletion.");
                return;
            }

            foreach (var measurement in selected)
            {
                if (!string.IsNullOrWhiteSpace(measurement.Id))
                {
                    try
                    {
                        var response = await _httpClient.DeleteAsync($"http://193.166.25.207:3001/api/data/{measurement.Id}");
                        if (!response.IsSuccessStatusCode)
                        {
                            MessageBox.Show($"Failed to delete measurement {measurement.Id}: {response.StatusCode}", "Backend error", MessageBoxButton.OK, MessageBoxImage.Warning);
                            continue;
                        }

                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to delete measurement {measurement.Id}: {ex.Message}", "Backend error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        continue;
                    }
                }

                _viewModel.Measurements.Remove(measurement);
            }
        }
    }

    public sealed class MqttDataViewModel : INotifyPropertyChanged
    {
        private double _y;
        private double _x;
        private bool _sw;
        private double _pot;
        private double _urm;

        private Visibility _listViewVisibility = Visibility.Visible;
        private Visibility _detailViewVisibility = Visibility.Collapsed;

        private MqttMeasurement? _selectedMeasurement;

        public ObservableCollection<MqttMeasurement> Measurements { get; } = new ObservableCollection<MqttMeasurement>();

        public MqttMeasurement? SelectedMeasurement
        {
            get => _selectedMeasurement;
            set => SetField(ref _selectedMeasurement, value);
        }

        public double Y
        {
            get => _y;
            set => SetField(ref _y, value);
        }

        public double X
        {
            get => _x;
            set => SetField(ref _x, value);
        }

        public bool Sw
        {
            get => _sw;
            set => SetField(ref _sw, value);
        }

        public double Pot
        {
            get => _pot;
            set => SetField(ref _pot, value);
        }

        public double Urm
        {
            get => _urm;
            set => SetField(ref _urm, value);
        }

        public Visibility ListViewVisibility
        {
            get => _listViewVisibility;
            set => SetField(ref _listViewVisibility, value);
        }

        public Visibility DetailViewVisibility
        {
            get => _detailViewVisibility;
            set => SetField(ref _detailViewVisibility, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public sealed class MqttMeasurement
    {
        public string? Id { get; set; }
        public DateTime Timestamp { get; set; }
        public double Y { get; set; }
        public double X { get; set; }
        public bool Sw { get; set; }
        public double Pot { get; set; }
        public double Urm { get; set; }

        // When true, this row will be deleted when the user presses the Delete button.
        public bool IsMarkedForDeletion { get; set; }

        public override string ToString()
        {
            // Used by default ListBox item rendering when no template is applied.
            return $"{Timestamp:HH:mm:ss} | X={X:F2}, Y={Y:F2}, Pot={Pot:F2}, Urm={Urm:F2}, Sw={Sw}";
        }
    }

    public sealed class MqttPayload
    {
        public string? _id { get; set; }
        public string? id { get; set; }
        public double y { get; set; }
        public double x { get; set; }
        public int sw { get; set; }
        public double pot { get; set; }
        public double urm { get; set; }
    }

    // DTO used for deserializing measurements from the backend REST API.
    public sealed class ApiMeasurement
    {
        public string? _id { get; set; }
        public double y { get; set; }
        public double x { get; set; }
		public JsonElement sw { get; set; }
        public double pot { get; set; }
        public double urm { get; set; }
        public DateTime? timestamp { get; set; }
    }

	public sealed class MeasurementsResponse
	{
		public List<ApiMeasurement>? measurements { get; set; }
	}
}
