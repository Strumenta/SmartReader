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
			Reader sr = new Reader(url);

			sr.Debug = true;
			sr.Logger = new StringWriter();

			Article article = sr.Parse();		

			return Json(new
			{
				article = article,
				log = sr.Logger.ToString()
			});
		}
	}
}
