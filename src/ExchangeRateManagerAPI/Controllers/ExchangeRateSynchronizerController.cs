using ExchangeRateManagerAPI.Services;
using Microsoft.AspNetCore.Mvc;

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

        [HttpPost]
        public IActionResult StartSynchronisation()
        {
            var taskId = _syncService.StartNewTask(async () =>
            {
                // Adjust to call the new method for executing sequential API calls
                return await _syncService.ExecuteSequentialApiCalls();
            });

            var checkUrl = Url.Action(nameof(CheckSynchronisationTask), new { taskId });
            return Accepted(checkUrl);
        }

        [HttpGet("{taskId}")]
        public IActionResult CheckSynchronisationTask(string taskId)
        {
            var (isCompleted, result, error) = _syncService.CheckTaskStatus(taskId);

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

    }
}
