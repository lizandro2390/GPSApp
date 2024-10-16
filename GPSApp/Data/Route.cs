using SQLite;

namespace GPSApp.Data
{
    public class Route
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string RouteData { get; set; } // Almacena el JSON de la ruta
        public DateTime DateSaved { get; set; }
    }
}

