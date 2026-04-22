using geo_api.Model;
using geo_api.Services;
using Microsoft.AspNetCore.Mvc;
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

        [HttpPost("location")]
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


        [HttpPost("seed")]
        public async Task<IActionResult> SeedData()
        {
            var samples = new List<Shipment>
            {
                new Shipment { trackingNumber = "TRK1001", currentLoc = GeoJson.Point(GeoJson.Geographic(-74.0404, 40.6780)) },
                new Shipment { trackingNumber = "TRK1002", currentLoc = GeoJson.Point(GeoJson.Geographic(-73.9968, 40.6708)) },
                new Shipment { trackingNumber = "TRK1003", currentLoc = GeoJson.Point(GeoJson.Geographic(-74.0501, 40.7099)) },
                new Shipment { trackingNumber = "TRK1004", currentLoc = GeoJson.Point(GeoJson.Geographic(-74.0328, 40.7279)) },
                new Shipment { trackingNumber = "TRK1005", currentLoc = GeoJson.Point(GeoJson.Geographic(-73.9729, 40.7253)) },
                new Shipment { trackingNumber = "TRK1006", currentLoc = GeoJson.Point(GeoJson.Geographic(-74.0168, 40.7145)) },
                new Shipment { trackingNumber = "TRK1007", currentLoc = GeoJson.Point(GeoJson.Geographic(-73.9966, 40.6847)) },
                new Shipment { trackingNumber = "TRK1008", currentLoc = GeoJson.Point(GeoJson.Geographic(-74.0004, 40.7017)) },
                new Shipment { trackingNumber = "TRK1009", currentLoc = GeoJson.Point(GeoJson.Geographic(-74.0345, 40.7289)) },
                new Shipment { trackingNumber = "TRK1010", currentLoc = GeoJson.Point(GeoJson.Geographic(-74.0203, 40.7069)) }
            };
            samples[0].routeHistory.Add(new movementLog { timestamp = DateTime.UtcNow, Long = -74.0404, Lat = 40.6780 });

            await _shipmentService.Shipments.InsertManyAsync(samples);
            return Ok("10 samples seeded successfully.");
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
