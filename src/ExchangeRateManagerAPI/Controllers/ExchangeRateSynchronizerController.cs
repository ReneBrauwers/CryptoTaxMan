using ExchangeRateManagerAPI.Services;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace ExchangeRateManagerAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ExchangeRateSynchronizerController : ControllerBase
    {
        private readonly ExchangeRateCrawlerService _syncService;

        private readonly ILogger<ExchangeRateSynchronizerController> _logger;

        public ExchangeRateSynchronizerController(ILogger<ExchangeRateSynchronizerController> logger, ExchangeRateCrawlerService exchangeRateCrawlerService)
        {
            _logger = logger;
            _syncService = exchangeRateCrawlerService;
        }

        [HttpPost("StartSynchronisation")]
        public IActionResult StartSynchronisation([FromBody] string fromDateString = "20230701")
        {

            //check that the fromDateString is in the currect format yyyyMMdd
            if (!DateTime.TryParseExact(fromDateString, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                return BadRequest("Invalid date format. Please use yyyyMMdd");
            }

            var taskId = _syncService.StartExchangeRateSynchronisationTask(async () =>
            {
                // Adjust to call the new method for executing sequential API calls
                return await _syncService.ExecuteSequentialApiCalls();
            });

            var checkUrl = Url.Action(nameof(CheckSynchronisationTask), new { taskId });
            return Accepted(checkUrl);
        }

        [HttpGet("SynchronisationStatus/{taskId}")]
        public IActionResult CheckSynchronisationTask(string taskId)
        {
            var (isCompleted, result, error) = _syncService.CheckExchangeRateSynchronisationTaskStatus(taskId);

            if (!isCompleted)
            {
                return Accepted(new
                {
                    StatusUrl = Url.Action(nameof(CheckSynchronisationTask), new { taskId }),
                    StatusMessage = _syncService.StatusMessage
                });
            }

            if (error != null)
            {
                return StatusCode(500, new { ErrorMessage = error.Message, Exception = error.ToString() });
            }

            return Ok(result);
        }

     
        [HttpPost("StartCurrencyConversion")]
        public IActionResult StartCurrencyConversion([FromBody] string targetCurrency = "aud")
        {
            var taskId = _syncService.StartCurrencyConversionTask(targetCurrency);

            var checkUrl = Url.Action(nameof(CheckCurrencyConversionTask), new { taskId });
            return Accepted(checkUrl);
        }

        [HttpGet("CurrencyconversionStatus/{taskId}")]
        public IActionResult CheckCurrencyConversionTask(string taskId)
        {
            var (isCompleted, result, error) = _syncService.CheckCurrencyConversionTaskStatus(taskId);

            if (!isCompleted)
            {
                return Accepted(new
                {
                    StatusUrl = Url.Action(nameof(CheckCurrencyConversionTask), new { taskId }),
                    StatusMessage = _syncService.StatusMessage
                });
            }

            if (error != null)
            {
                return StatusCode(500, new { ErrorMessage = error.Message, Exception = error.ToString() });
            }

            return Ok(result);
        }

    }
}
