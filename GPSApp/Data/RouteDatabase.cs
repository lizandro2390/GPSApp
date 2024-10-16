using SQLite;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GPSApp.Data
{
    public class RouteDatabase
    {
        private static SQLiteAsyncConnection _database;

        public static async Task<SQLiteAsyncConnection> GetDatabaseAsync()
        {
            if (_database == null)
            {
                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "GPSRoutes.db");
                _database = new SQLiteAsyncConnection(dbPath);
                await _database.CreateTableAsync<Route>();
            }
            return _database;
        }

        public static async Task SaveRouteAsync(string routeData)
        {
            var db = await GetDatabaseAsync();
            var route = new Route
            {
                RouteData = routeData,
                DateSaved = DateTime.Now
            };
            await db.InsertAsync(route);
        }

        public static async Task<Route> GetLastSavedRouteAsync()
        {
            var db = await GetDatabaseAsync();
            return await db.Table<Route>().OrderByDescending(r => r.DateSaved).FirstOrDefaultAsync();
        }
    }
}


