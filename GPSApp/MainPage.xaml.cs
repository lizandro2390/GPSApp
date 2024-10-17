using Microsoft.Maui.Controls.Maps;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Maui.Maps;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GPSApp
{
    public partial class MainPage : ContentPage
    {
        private readonly HttpClient httpClient = new HttpClient();
        private readonly string herokuUrl = "https://esp32-heroku-server-55c8bf48821f.herokuapp.com/api/gpsdata";
        private const int updateInterval = 5000;
        private const string googleApiKey = "AIzaSyCezotEhTt2r0ZZCg8p03ujD3DkNjIOBU0";
        private Pin devicePin;
        private Pin esp32Pin;
        private Location lastDeviceLocation;
        private Location lastEsp32Location;
        private Location lastCenterLocation;
        private double lastZoomRadius = 1.0;

        public MainPage()
        {
            InitializeComponent();
            InitializeMap();
            StartLocationUpdates();
            CenterMapCommand = new Command(CenterMapOnDevice);
            ExitAppCommand = new Command(ExitApp);
            ChangeMapTypeCommand = new Command(ChangeMapType); // Nuevo comando para cambiar el tipo de mapa
            BindingContext = this;
        }

        public ICommand CenterMapCommand { get; }
        public ICommand ExitAppCommand { get; }
        public ICommand ChangeMapTypeCommand { get; }  // Comando para el cambio de tipo de mapa

        private void InitializeMap()
        {
            devicePin = new Pin { Label = "Mi Dispositivo", Type = PinType.Place, Location = new Location(0, 0) };
            esp32Pin = new Pin { Label = "ESP32", Type = PinType.Place, Location = new Location(0, 0) };

            map.Pins.Add(devicePin);
            map.Pins.Add(esp32Pin);
            map.MoveToRegion(MapSpan.FromCenterAndRadius(new Location(0, 0), Distance.FromKilometers(lastZoomRadius)));
        }

        private void ChangeMapType()
        {
            // Alterna entre los tipos de mapa
            switch (map.MapType)
            {
                case MapType.Street:
                    map.MapType = MapType.Satellite;
                    break;
                case MapType.Satellite:
                    map.MapType = MapType.Hybrid;
                    break;
                case MapType.Hybrid:
                    map.MapType = MapType.Street;
                    break;
            }
        }

        private async void StartLocationUpdates()
        {
            while (true)
            {
                await UpdateDeviceLocation();
                await UpdateEsp32Location();
                await DrawRoute();
                await Task.Delay(updateInterval);
            }
        }

        private async Task UpdateDeviceLocation()
        {
            try
            {
                var location = await Geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Best));
                if (location != null && location.Latitude != 0 && location.Longitude != 0)
                {
                    lastDeviceLocation = new Location(location.Latitude, location.Longitude);
                    deviceCoordinatesLabel.Text = $"Device Coordinates: {location.Latitude}, {location.Longitude}";
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Error al obtener la ubicación del dispositivo: {ex.Message}", "OK");
            }
        }

        private async Task UpdateEsp32Location()
        {
            try
            {
                var response = await httpClient.GetStringAsync(herokuUrl);
                var esp32Location = JsonConvert.DeserializeObject<LocationData>(response);

                if (esp32Location != null && esp32Location.Latitude != 0 && esp32Location.Longitude != 0)
                {
                    lastEsp32Location = new Location(esp32Location.Latitude, esp32Location.Longitude);
                    esp32CoordinatesLabel.Text = $"ESP32 Coordinates: {esp32Location.Latitude}, {esp32Location.Longitude}";
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Error al obtener la ubicación del ESP32: {ex.Message}", "OK");
            }
        }

        private async Task DrawRoute()
        {
            if (lastDeviceLocation != null && lastEsp32Location != null)
            {
                string url = $"https://maps.googleapis.com/maps/api/directions/json?origin={lastDeviceLocation.Latitude},{lastDeviceLocation.Longitude}&destination={lastEsp32Location.Latitude},{lastEsp32Location.Longitude}&key={googleApiKey}";

                try
                {
                    var response = await httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        var responseData = await response.Content.ReadAsStringAsync();
                        var routeData = JObject.Parse(responseData);

                        var status = routeData["status"]?.ToString();
                        if (status != "OK")
                        {
                            await DisplayAlert("Error", $"Error de la API de Directions: {status}", "OK");
                            return;
                        }

                        var points = DecodePolyline(routeData["routes"][0]["overview_polyline"]["points"].ToString());
                        var polyline = new Polyline
                        {
                            StrokeColor = Colors.Blue,
                            StrokeWidth = 6
                        };

                        foreach (var point in points)
                        {
                            polyline.Geopath.Add(point);
                        }

                        map.Pins.Clear();
                        map.MapElements.Clear();

                        devicePin = new Pin { Label = "Mi Dispositivo", Type = PinType.Place, Location = lastDeviceLocation };
                        esp32Pin = new Pin { Label = "ESP32", Type = PinType.Place, Location = lastEsp32Location };

                        map.Pins.Add(devicePin);
                        map.Pins.Add(esp32Pin);
                        map.MapElements.Add(polyline);

                        double totalDistance = 0;
                        for (int i = 1; i < points.Count; i++)
                        {
                            totalDistance += points[i - 1].CalculateDistance(points[i], DistanceUnits.Kilometers);
                        }

                        distanceLabel.Text = $"Distancia por la ruta: {totalDistance:F2} km";

                        var midLatitude = (lastDeviceLocation.Latitude + lastEsp32Location.Latitude) / 2;
                        var midLongitude = (lastDeviceLocation.Longitude + lastEsp32Location.Longitude) / 2;
                        var centerLocation = new Location(midLatitude, midLongitude);

                        if (lastCenterLocation == null || lastCenterLocation.CalculateDistance(centerLocation, DistanceUnits.Kilometers) > 0.01)
                        {
                            lastCenterLocation = centerLocation;
                            var distance = lastDeviceLocation.CalculateDistance(lastEsp32Location, DistanceUnits.Kilometers);
                            lastZoomRadius = distance + 0.5;

                            map.MoveToRegion(MapSpan.FromCenterAndRadius(centerLocation, Distance.FromKilometers(lastZoomRadius)));
                        }
                    }
                    else
                    {
                        string errorMessage = await response.Content.ReadAsStringAsync();
                        await DisplayAlert("Error", $"Error al obtener la ruta: {response.ReasonPhrase}\nDetalles: {errorMessage}", "OK");
                    }
                }
                catch (HttpRequestException httpEx)
                {
                    await DisplayAlert("Error", $"Error al obtener la ruta: {httpEx.Message}", "OK");
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Ocurrió un error: {ex.Message}", "OK");
                }
            }
        }

        private List<Location> DecodePolyline(string encodedPoints)
        {
            var poly = new List<Location>();
            int index = 0, len = encodedPoints.Length;
            int lat = 0, lng = 0;

            while (index < len)
            {
                int b, shift = 0, result = 0;
                do
                {
                    b = encodedPoints[index++] - 63;
                    result |= (b & 0x1f) << shift;
                    shift += 5;
                } while (b >= 0x20);
                int dlat = ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
                lat += dlat;

                shift = 0;
                result = 0;
                do
                {
                    b = encodedPoints[index++] - 63;
                    result |= (b & 0x1f) << shift;
                    shift += 5;
                } while (b >= 0x20);
                int dlng = ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
                lng += dlng;

                var p = new Location((lat / 1E5), (lng / 1E5));
                poly.Add(p);
            }

            return poly;
        }

        private void CenterMapOnDevice()
        {
            if (lastDeviceLocation != null)
            {
                map.MoveToRegion(MapSpan.FromCenterAndRadius(lastDeviceLocation, Distance.FromKilometers(lastZoomRadius)));
            }
        }

        private void ExitApp()
        {
#if ANDROID
            Android.OS.Process.KillProcess(Android.OS.Process.MyPid());
#elif IOS
            Thread.CurrentThread.Abort();
#endif
        }

        public class LocationData
        {
            public double Latitude { get; set; }
            public double Longitude { get; set; }
        }
    }
}



