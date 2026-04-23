using geo_api.Model;
using geo_api.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
using MongoDB.Driver.Linq;
using System.Drawing;

namespace geo_api.Controllers
{
    [ApiController]
    [Route("api/shipment")]
    public class shipmentController : ControllerBase
    {
        private readonly mongoService _shipmentService;
        private double calculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var d1 = lat1 * (Math.PI / 180.0);
            var num1 = lon1 * (Math.PI / 180.0);
            var d2 = lat2 * (Math.PI / 180.0);
            var num2 = lon2 * (Math.PI / 180.0) - num1;
            var d3 = Math.Pow(Math.Sin((d2 - d1) / 2.0), 2.0)
                     + Math.Cos(d1) * Math.Cos(d2) * Math.Pow(Math.Sin(num2 / 2.0), 2.0);
            return 6371 * (2.0 * Math.Atan2(Math.Sqrt(d3), Math.Sqrt(1.0 - d3)));
        }

        public shipmentController(mongoService shipmentService)
        {
            _shipmentService = shipmentService;
        }

        [HttpGet]
        public async Task<IActionResult> Getshipments()
        {
            var shipments = await _shipmentService.Shipments.Find(_ => true).ToListAsync();
            return Ok(shipments);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var shipment = await _shipmentService.Shipments.AsQueryable().Where(q => q.Id == id).FirstOrDefaultAsync();
            if (shipment == null)
            {
                return NotFound("Shipment Not found");
            }
            return Ok(shipment);
        }

        [HttpGet("{tracknumber}")]
        public async Task<IActionResult> GetBytrackNumber(string trackNumber)
        {
            var shipment = await _shipmentService.Shipments.AsQueryable().Where(q => q.trackingNumber == trackNumber).FirstOrDefaultAsync();
            if (shipment == null)
            {
                return NotFound("Shipment Not found");
            }
            return Ok(shipment);
        }

        [HttpGet("{status}")]
        public async Task<IActionResult> GetBystatus(string status)
        {
            var shipment = await _shipmentService.Shipments.AsQueryable().Where(q => q.metaData.status == status).ToListAsync();
            if (shipment == null)
            {
                return NotFound("Shipment Not found");
            }
            return Ok(shipment);
        }

        [HttpGet("{customer}")]
        public async Task<IActionResult> GetBycustname(string custmorname)
        {
            var shipment = await _shipmentService.Shipments.AsQueryable().Where(q => q.metaData.customerName == custmorname).ToListAsync();
            if (shipment == null)
            {
                return NotFound("Shipment Not found");
            }
            return Ok(shipment);
        }

        [HttpGet("by-distance")]
        public async Task<IActionResult> GetShipmentsByDistance([FromQuery] double lon, [FromQuery] double lat)
        {
            if (lon < -180 || lon > 180 || lat < -90 || lat > 90)
            {
                return BadRequest("Invalid coordinates. Longitude must be -180–180, latitude -90–90.");
            }

            var warehousePoint = GeoJson.Point(GeoJson.Geographic(lon, lat));

            var results = await _shipmentService.Shipments.Aggregate()
                .AppendStage<Shipment>(new BsonDocument("$geoNear", new BsonDocument
                {
                    { "near", warehousePoint.ToBsonDocument() },
                    { "distanceField", "DistanceFromWarehouse" },
                    { "spherical", true },
                    { "distanceMultiplier", 0.001 }
                })).ToListAsync();

            return Ok(results);
        }

        [HttpGet("speeding-alerts")]
        public async Task<IActionResult> GetspeedAlerts([FromQuery] double speedLimit)
        {
            if (speedLimit <= 0)
            {
                return BadRequest($"Speed Limit {speedLimit} must be grater than zero.");
            }


            var shipments = await _shipmentService.Shipments.AsQueryable().Where(s => s.routeHistory.Count >= 2).ToListAsync();
            var alerts = shipments.Where(q =>
            {
                var last = q.routeHistory.OrderByDescending(g => g.timestamp).First();
                var previous = q.routeHistory.OrderByDescending(g => g.timestamp).Skip(1).First();
                
                double hour = (last.timestamp - previous.timestamp).TotalHours;
                if (hour < 0.010) return false;

                double distence = calculateDistance(last.Lat, last.Long, previous.Lat, previous.Long);

                return (distence/hour) >= speedLimit;
            }).ToList();
            return Ok(alerts);
        }

        [HttpGet("near")]
        public async Task<IActionResult> GetShipmentsNearMe(double Lat, double Long, double radius)
        {
            var myPoint = GeoJson.Point(GeoJson.Geographic(Long, Lat));
            // var myPoint = GeoJson.Point(GeoJson.Position(lat, lon));

            // var resquery = await _shipmentService.Shipments.AsQueryable().Where(q => q.currentLoc.Distance(myPoint) < radius).ToListAsync();
            var filter = Builders<Shipment>.Filter.NearSphere(s => s.currentLoc,myPoint,radius);

            var res = await _shipmentService.Shipments.Find(filter).ToListAsync();
            return Ok(res);
        }

        [HttpPost]
        public async Task<IActionResult> Createshipment([FromBody] ShipmentInput input)
        {
            var shipment = new Shipment
            {
                Id = null,
                trackingNumber = input.trackingNumber,
                metaData = input.metaData,
                routeHistory = new List<movementLog>(),
                currentLoc = GeoJson.Point(new GeoJson2DGeographicCoordinates(input.currentLoc.Longitude,input.currentLoc.Latitude))
            };

            await _shipmentService.Shipments.InsertOneAsync(shipment);
            return Ok(shipment);
        }

        [HttpPost("location/by-id")]
        public async Task<IActionResult> AddLocation(string id, [FromBody] LocationInput newLoc)
        {
            var shipment = await _shipmentService.Shipments.AsQueryable().Where(s => s.Id == id).FirstOrDefaultAsync();

            if (shipment is null)
                return NotFound("Shipment not found.");

            var log = new movementLog
            {
                timestamp = DateTime.UtcNow,
                Long = newLoc.Longitude,
                Lat = newLoc.Latitude
            };

            var update = Builders<Shipment>.Update.Push(s => s.routeHistory, log).Set(s => s.currentLoc,GeoJson.Point(new GeoJson2DGeographicCoordinates(newLoc.Longitude, newLoc.Latitude)));

            await _shipmentService.Shipments.UpdateOneAsync(s => s.Id == id, update);
            var updated = await _shipmentService.Shipments.AsQueryable().Where(s => s.Id == id).Select(s => s.routeHistory).FirstOrDefaultAsync();
            return Ok(updated);
        }

        [HttpPost("location/by-track")]
        public async Task<IActionResult> AddLocationbytrack(string trackNum, [FromBody] LocationInput newLoc)
        {
            var shipment = await _shipmentService.Shipments.AsQueryable().Where(s => s.trackingNumber == trackNum).FirstOrDefaultAsync();

            if (shipment is null)
                return NotFound("Shipment not found.");

            var log = new movementLog
            {
                timestamp = DateTime.UtcNow,
                Long = newLoc.Longitude,
                Lat = newLoc.Latitude
            };

            var update = Builders<Shipment>.Update.Push(s => s.routeHistory, log).Set(s => s.currentLoc, GeoJson.Point(new GeoJson2DGeographicCoordinates(newLoc.Longitude, newLoc.Latitude)));

            await _shipmentService.Shipments.UpdateOneAsync(s => s.trackingNumber == trackNum, update);
            var updated = await _shipmentService.Shipments.AsQueryable().Where(s => s.trackingNumber == trackNum).Select(s => s.routeHistory).FirstOrDefaultAsync();
            return Ok(updated);
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> Deleteshipment(string id)
        {
            var result = await _shipmentService.Shipments.DeleteOneAsync(s => s.Id == id);

            if (result.DeletedCount == 0)
            {
                NotFound("Shipment not found.");
            }
            return NoContent(); 
        }

        [HttpDelete("clean")]
        public async Task<IActionResult> cleanEverything()
        {
            await _shipmentService.Shipments.DeleteManyAsync(_ => true);
            return Ok("All shipment deleted");
        }

        //[HttpPost]
        //public async Task<IActionResult> createShipment(Shipment ship, double Long, double Lat)
        //{
        //    var exist = await _shipmentService.Shipments.Find(s => s.Id == ship.Id).FirstOrDefaultAsync();
        //    if (exist == null)
        //    {
        //        return BadRequest("Shipment already present");
        //    }

        //    var shipment = new Shipment
        //    {
        //        trackingNumber = ship.trackingNumber,
        //        currentLoc = GeoJson.Point(GeoJson.Geographic(Long, Lat)),
        //        routeHistory = new List<movementLog>
        //        {
        //            new movementLog
        //            {
        //                timestamp = DateTime.UtcNow,
        //                Lat = Lat,
        //                Long = Long
        //            }
        //        }
        //    };

        //    await _shipmentService.Shipments.InsertOneAsync(shipment);
        //    return Ok(shipment);
        //}

    }
}
