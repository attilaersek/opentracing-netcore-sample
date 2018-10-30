using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OpenTracing;
using otsample.web.Models;
using otsample_web.Models;

namespace otsample.web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ITracer tracer;
        private readonly IHttpClientFactory clientFactory;

        public HomeController(IHttpClientFactory clientFactory, ITracer tracer)
        {
            this.clientFactory = clientFactory;
            this.tracer = tracer;
        }

        public async Task<IActionResult> Index()
        {
            tracer.ActiveSpan?.Log(new Dictionary<string, object> {
                { "event", "calling service" },
            });
            using (var scope = tracer.BuildSpan("get-result").StartActive(finishSpanOnDispose: true))
            {
                var response = await clientFactory.CreateClient().PostAsync(
                    "https://localhost:5001/api/values",
                    new StringContent(
                        JsonConvert.SerializeObject(
                            new
                            {
                                a = 1,
                                b = 1,
                            }
                        ),
                        Encoding.UTF8,
                        "application/json"
                    ));
                response.EnsureSuccessStatusCode();
                ViewData["result"] = JsonConvert.DeserializeObject<ResultModel>(await response.Content.ReadAsStringAsync()).Result;
            }
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
