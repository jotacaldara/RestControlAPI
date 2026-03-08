using Microsoft.AspNetCore.Mvc;

namespace RestControlAPI.Controllers
{
    public class MenuController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
