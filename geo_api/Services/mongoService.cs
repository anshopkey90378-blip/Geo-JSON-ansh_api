using geo_api.Model;
using MongoDB.Driver;

namespace geo_api.Services
{
    public class mongoService
    {
        private readonly IMongoCollection<Shipment> _shipments;

        public mongoService(IConfiguration config)
        {
            var client = new MongoClient(config["MongoDB:ConnectionString"]);
            var db = client.GetDatabase(config["MongoDB:Database"]);
            _shipments = db.GetCollection<Shipment>("Ship");
        }

        public IMongoCollection<Shipment> Shipments => _shipments;
    }
}
