using ExchangeRateManagerAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Models;
using System.Text;

namespace ExchangeRateManagerAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DatabaseAdminController : Controller
    {
      
        private readonly DatabaseAdminService _adminService;
        private readonly ILogger<DatabaseAdminController> _logger;
        private readonly IWebHostEnvironment _environment;

        public DatabaseAdminController(ILogger<DatabaseAdminController>  logger, IWebHostEnvironment environment, DatabaseAdminService adminService)
        {
            _logger = logger;      
            _environment = environment;
            _adminService = adminService;
        }

        [HttpPost("StartExportExchangeRateData")]
        public IActionResult StartExportExchangeRateData()
        {
           // var result = await  _adminService.ExportExchangeRateData(string.Empty);
          //  return Ok(result);
            var taskId = _adminService.StartNewTask(async () =>
            {
                // Adjust to call the new method for executing sequential API calls
                return await _adminService.ExportExchangeRateData(string.Empty);
            });

            var checkUrl = Url.Action(nameof(CheckSynchronisationTask), new { taskId });
            return Accepted(checkUrl);

            
        }

        [HttpPost("StartRecreateAndRestoreDatabase")]
        public  IActionResult StartRecreateAndRestoreDatabase()
        {
            //var result = await _adminService.ExecuteRecreateAndRestoreFromFiles(string.Empty);
           // return Ok(result);
            var taskId = _adminService.StartNewTask(async () =>
            {
                // Adjust to call the new method for executing sequential API calls
                return await _adminService.ExecuteRecreateAndRestoreFromFiles(string.Empty);
            });

            var checkUrl = Url.Action(nameof(CheckSynchronisationTask), new { taskId });
            return Accepted(checkUrl);
        }

        [HttpGet("{taskId}")]
        public IActionResult CheckSynchronisationTask(string taskId)
        {
            var (isCompleted, result) = _adminService.CheckTaskStatus(taskId);

            if (!isCompleted)
            {
                return Accepted(Url.Action(nameof(CheckSynchronisationTask), new { taskId }));
            }

             
            return Ok(new { result });
        }
    }
}
