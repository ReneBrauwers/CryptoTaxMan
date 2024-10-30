using ExchangeRateManagerAPI.Model;
using ExchangeRateManagerAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.Enums;
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
        public IActionResult StartSynchronisation([FromBody] StartSynchronisationRequest req)
        {

            //check that the fromDateString is in the currect format yyyyMMdd
            if (!DateTime.TryParseExact(req.fromDateString, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {

                return BadRequest("Invalid date format. Please use yyyyMMdd");
            }


            if(Enum.TryParse<SupportedExchanges>(req.exchangeSupported, false, out SupportedExchanges supportedExchange))
            {
                var taskId = _syncService.StartExchangeRateSynchronisationTask(async () =>
                {

                    // Adjust to call the new method for executing sequential API calls
                    return await _syncService.ExecuteSequentialApiCalls(supportedExchanges: supportedExchange);
                });

                var checkUrl = Url.Action(nameof(CheckSynchronisationTask), new { taskId });
                return Accepted(checkUrl);
            }
            else
            {
                return BadRequest("Invalid exchange supported. Please use one of the following: All, CoinGecko, CoinMarketCap, LiveCoinWatch");
            }




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
        public IActionResult StartCurrencyConversion([FromBody] StartCurrencyConversionRequest req)
        {
            var taskId = _syncService.StartCurrencyConversionTask(req.targetCurrency);

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

        [HttpGet("CalculateExchangeRateFluctuations")]
        public IActionResult CalculateExchangeRateFluctuations()
        {
            var taskId = _syncService.StartCalculateExchangeRateFluctuations();

            var checkUrl = Url.Action(nameof(CalculateExchangeRateFluctuations), new { taskId });
            return Accepted(checkUrl);
        }

        [HttpGet("CalculateExchangeRateFluctuations/{taskId}")]
        public IActionResult CheckCalculateExchangeRateFluctuationsTask(string taskId)
        {
            var (isCompleted, result, error) = _syncService.CheckCalculateExchangeRateFluctuationsTaskStatus(taskId);

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

        //CheckCalculateExchangeRateFluctuationsTaskStatus

    }
}
