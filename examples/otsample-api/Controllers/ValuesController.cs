using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using OpenTracing;
using otsample.api.Models;

namespace otsample.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {
        private readonly Random random = new Random();
        private readonly ITracer tracer;

        public ValuesController(ITracer tracer)
        {
            this.tracer = tracer;
        }

        [HttpPost]
        public async Task<ActionResult<ResultModel>> Post([FromBody] ValuesModel model)
        {
            var delay = random.Next(200) + 100;
            tracer.ActiveSpan?.Log(new Dictionary<string, object>()
            {
                ["a"] = $"{model.A}",
                ["b"] = $"{model.B}",
                ["delay"] = $"{delay}",
            });
            var result = default(int);
            using(var scope = tracer.BuildSpan("heavy-work").StartActive(finishSpanOnDispose: true))
            {
                result = model.A + model.B;
                await Task.Delay(delay);
                tracer.ActiveSpan?.Log(new Dictionary<string, object>()
                {
                    ["a"] = $"{model.A}",
                    ["b"] = $"{model.B}",
                    ["delay"] = $"{delay}",
                    ["result"] = $"{result}",
                });
            }
            return Ok(new ResultModel() { Result = result });
        }
    }
}
