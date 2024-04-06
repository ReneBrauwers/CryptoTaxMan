using ExchangeRateManagerAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Models;
using System.Text;

namespace ExchangeRateManagerAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ExchangeRateInformationController : Controller
    {

        private readonly DatabaseService _databaseService;
        private readonly ILogger<ExchangeRateInformationController> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _config;

        public ExchangeRateInformationController(IConfiguration config,  ILogger<ExchangeRateInformationController> logger, IWebHostEnvironment environment, DatabaseService databaseService)
        {
            _logger = logger;
            _environment = environment;
            _databaseService = databaseService;
            _config = config;
        }



        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] DateTime transactionDate, [FromQuery] string currencyIn, [FromQuery] string exchangeCurrency = "aud", [FromQuery] bool nearestMatch = true)
        {
            ExchangeRate? result = null;
            int maxTimeTravelAllowed = _config.GetValue<int>("maxNearestMatchRange", 1);
            do
            {
                result = await _databaseService.FetchExchangeRateInformation(transactionDate, currencyIn.ToLower(), exchangeCurrency.ToLower());
                if (result is not null && nearestMatch)
                {
                    break;
                }

                if (result is null && nearestMatch)
                {
                    if(maxTimeTravelAllowed == 0)
                    {
                        break;
                    }
                    transactionDate = transactionDate.AddDays(-1);
                    maxTimeTravelAllowed--;
                }
            } while (nearestMatch);


            if (result is null)
            {
                return NotFound();
            }

            return Ok(result);
        }
    }
}
