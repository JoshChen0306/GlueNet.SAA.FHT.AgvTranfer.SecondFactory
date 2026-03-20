using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SCP.Models;

namespace SCP.Controllers
{
    [Authorize(Roles = "1")]
    public class TaskTypeRouteController : Controller
    {
        private readonly ILogger<TaskTypeRouteController> _logger;
        private readonly agvDB_1400004Context _DBContext;

        public TaskTypeRouteController(ILogger<TaskTypeRouteController> logger, agvDB_1400004Context DBContext)
        {
            _DBContext = DBContext;
            _logger = logger;
        }

        public IActionResult Index()
        {
            var query = _DBContext.oTaskTypeRoute.OrderBy(r => r.MoveType).ThenBy(r => r.FromFloor).ThenBy(r => r.ToFloor);
            return View(query);
        }

        [HttpPost]
        public IActionResult DataChange([FromBody] DataChange data)
        {
            var insertdata = data.insertdata;
            var updatedata = data.updatedata;
            var deletedata = data.deletedata;

            // 收集即將刪除的 Id，用於重複判斷時排除
            var deletingIds = new HashSet<int>();
            if (deletedata != null)
            {
                foreach (var d in deletedata)
                {
                    if (int.TryParse(d, out int id)) deletingIds.Add(id);
                }
            }

            if (insertdata != null && insertdata.Count > 0)
            {
                foreach (var item in insertdata)
                {
                    oTaskTypeRoute? route = JsonConvert.DeserializeObject<oTaskTypeRoute>(item.Value.ToString());
                    if (route != null && IsDuplicateRoute(route.MoveType, route.FromFloor, route.ToFloor, 0, deletingIds))
                    {
                        return BadRequest(new { message = $"路由重複：{route.MoveType} {route.FromFloor}->{route.ToFloor} 已存在" });
                    }
                    InsertRoute(route);
                }
            }

            if (updatedata != null && updatedata.Count > 0)
            {
                foreach (var item in updatedata)
                {
                    oTaskTypeRoute? route = JsonConvert.DeserializeObject<oTaskTypeRoute>(item.Value.ToString());
                    if (route != null && IsDuplicateRoute(route.MoveType, route.FromFloor, route.ToFloor, route.Id, deletingIds))
                    {
                        return BadRequest(new { message = $"路由重複：{route.MoveType} {route.FromFloor}->{route.ToFloor} 已存在" });
                    }
                    UpdateRoute(route);
                }
            }

            if (deletedata != null)
            {
                DeleteRoute(deletedata);
            }

            return Ok();
        }

        private bool IsDuplicateRoute(string moveType, string fromFloor, string toFloor, int excludeId, HashSet<int> deletingIds)
        {
            return _DBContext.oTaskTypeRoute.Any(r =>
                r.MoveType == moveType &&
                r.FromFloor == fromFloor &&
                r.ToFloor == toFloor &&
                r.Id != excludeId &&
                !deletingIds.Contains(r.Id));
        }

        private void InsertRoute(oTaskTypeRoute route)
        {
            try
            {
                string sql = "INSERT INTO oTaskTypeRoute (MoveType, FromFloor, ToFloor, TaskType, UseFlag, Remark) VALUES ({0}, {1}, {2}, {3}, {4}, {5})";
                _DBContext.Database.ExecuteSqlRaw(sql, route.MoveType, route.FromFloor, route.ToFloor, route.TaskType, route.UseFlag ?? "Y", route.Remark ?? "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "InsertRoute failed for {MoveType} {FromFloor}->{ToFloor}", route.MoveType, route.FromFloor, route.ToFloor);
            }
        }

        private void UpdateRoute(oTaskTypeRoute route)
        {
            try
            {
                _DBContext.oTaskTypeRoute
                    .Where(r => r.Id == route.Id)
                    .ExecuteUpdate(setters => setters
                        .SetProperty(r => r.MoveType, route.MoveType)
                        .SetProperty(r => r.FromFloor, route.FromFloor)
                        .SetProperty(r => r.ToFloor, route.ToFloor)
                        .SetProperty(r => r.TaskType, route.TaskType)
                        .SetProperty(r => r.UseFlag, route.UseFlag)
                        .SetProperty(r => r.Remark, route.Remark));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateRoute failed for Id {Id}", route.Id);
            }
        }

        private void DeleteRoute(List<string> deletedata)
        {
            try
            {
                var ids = deletedata.Select(d => int.Parse(d)).ToList();
                _DBContext.oTaskTypeRoute.Where(r => ids.Contains(r.Id)).ExecuteDelete();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteRoute failed");
            }
        }
    }
}
