using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SmartReader.WebDemo.Models;

namespace SmartReader.WebDemo.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public JsonResult Analyze(string url)
        {            
            Reader sr = new Reader(url);
         
            Article article = sr.GetArticle();
            var images = article.GetImagesAsync();
            images.Wait();

            return Json(new
            {
                article = article,
                images = $"{images.Result.Count()} images found",
                log = sr.LoggerDelegate.ToString()
            });
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
