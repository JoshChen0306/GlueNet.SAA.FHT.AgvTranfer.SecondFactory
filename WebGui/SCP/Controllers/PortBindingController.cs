using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SCP.Models;

namespace SCP.Controllers
{
    [Authorize(Roles = "1")]
    public class PortBindingController : Controller
    {
        private readonly ILogger<PortBindingController> _logger;
        private readonly agvDB_1400004Context _DBContext;

        public PortBindingController(ILogger<PortBindingController> logger, agvDB_1400004Context DBContext)
        {
            _DBContext = DBContext;
            _logger = logger;
        }

        public IActionResult Index()
        {
            var query = _DBContext.oPortBinding;
            ViewBag.StationList = _DBContext.oPort
                .Select(p => p.StationNo)
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            return View(query);
        }

        [HttpPost]
        public IActionResult DataChange([FromBody] DataChange data)
        {
            var insertdata = data.insertdata;
            var updatedata = data.updatedata;
            var deletedata = data.deletedata;

            if (insertdata != null && insertdata.Count > 0)
            {
                foreach (var item in insertdata)
                {
                    oPortBinding? binding = JsonConvert.DeserializeObject<oPortBinding>(item.Value.ToString());
                    InsertBinding(binding);
                }
            }

            if (updatedata != null && updatedata.Count > 0)
            {
                foreach (var item in updatedata)
                {
                    oPortBinding? binding = JsonConvert.DeserializeObject<oPortBinding>(item.Value.ToString());
                    UpdateBinding(binding);
                }
            }

            if (deletedata != null)
            {
                DeleteBinding(deletedata);
            }

            return Ok();
        }

        private void InsertBinding(oPortBinding binding)
        {
            try
            {
                string sql = "INSERT INTO oPortBinding (LoadingPort, UnloadingPort, FallbackAreas, UseFlag) VALUES ({0}, {1}, {2}, {3})";
                _DBContext.Database.ExecuteSqlRaw(sql, binding.LoadingPort, binding.UnloadingPort ?? "", binding.FallbackAreas ?? "", binding.UseFlag ?? "Y");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "InsertBinding failed for {LoadingPort}", binding.LoadingPort);
            }
        }

        private void UpdateBinding(oPortBinding binding)
        {
            try
            {
                _DBContext.oPortBinding
                    .Where(b => b.LoadingPort == binding.LoadingPort)
                    .ExecuteUpdate(setters => setters
                        .SetProperty(b => b.UnloadingPort, binding.UnloadingPort)
                        .SetProperty(b => b.FallbackAreas, binding.FallbackAreas)
                        .SetProperty(b => b.UseFlag, binding.UseFlag));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateBinding failed for {LoadingPort}", binding.LoadingPort);
            }
        }

        private void DeleteBinding(List<string> deletedata)
        {
            try
            {
                var query = _DBContext.oPortBinding.Where(b => deletedata.Contains(b.LoadingPort));
                query.ExecuteDelete();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteBinding failed");
            }
        }
    }
}
