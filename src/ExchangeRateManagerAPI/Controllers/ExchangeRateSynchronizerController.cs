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
            //var r = _syncService.ExecuteSequentialApiCalls();
            //return Ok(r);
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
            var (isCompleted, result) = _syncService.CheckTaskStatus(taskId);

            if (!isCompleted)
            {
                return Accepted(Url.Action(nameof(CheckSynchronisationTask), new { taskId, _syncService.StatusMessage }));
            }

            return Ok(result);
        }
    }
}
