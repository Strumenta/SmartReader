using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using SmartReader;
using System.Text;

namespace SmartReaderDemo.Controllers
{
	public class HomeController : Controller
	{
		public IActionResult Index()
		{
			return View();
		}

		public JsonResult Analyze(string url)
		{
            StringWriter log = new StringWriter();

            Reader sr = new Reader(url);  

			sr.Debug = true;
			sr.LoggerDelegate = log.WriteLine;

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
	}
}
