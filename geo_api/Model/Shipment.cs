using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.GeoJsonObjectModel;

namespace geo_api.Model
{
    public class Shipment
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        public string trackingNumber { get; set; }
        public shipmentMetadata metaData { get; set; }
        public GeoJsonPoint<GeoJson2DGeographicCoordinates> currentLoc { get; set; }
        public List<movementLog> routeHistory { get; set; } = new();
    }

    public class shipmentMetadata
    {
        // customer details
        public string customerId { get; set; }
        public string customerName { get; set; }
        public string shippingAddress { get; set; }

        // Product details
        public string Productname { get; set; }
        public string Category { get; set; }
        public double Price { get; set; }
        public bool IsFragile { get; set; }
        public string status { get; set; }

    }

    public class movementLog
    {
        public DateTime timestamp { get; set; }
        public double Long {  get; set; }
        public double Lat {  get; set; }
    }

    public class LocationInput
    {
        public double Longitude { get; set; }
        public double Latitude { get; set; }
    }

    public class ShipmentInput
    {
        public string trackingNumber { get; set; }
        public shipmentMetadata metaData { get; set; }
        public LocationInput currentLoc { get; set; }
    }
}
