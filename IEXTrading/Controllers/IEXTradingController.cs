using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using IEXTrading.Infrastructure.IEXTradingHandler;
using IEXTrading.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace IEXTrading.Controllers
{
    public class IEXTradingController : Controller
    {
        HttpClient httpClient;
        public IEXTradingController()
        {
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        public IActionResult Index()
        {
            List<Equity> Equities = new List<Equity>();
            string symbol = "AAL";
            // American Airlines group
            //string symbol = "AAPL"; // Apple Computer
            string BASE_URL = "https://api.iextrading.com/1.0/";
            string IEXTrading_API_PATH = BASE_URL + "stock/" + symbol + "/batch?types=chart&range=1y";
            HttpResponseMessage response = httpClient.GetAsync(IEXTrading_API_PATH).GetAwaiter().GetResult();
            string charts = "";
            if (response.IsSuccessStatusCode)
            {
                charts = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            }
            if (!charts.Equals(""))
            {
                ChartRoot root = JsonConvert.DeserializeObject<ChartRoot>(charts, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                Equities = root.chart.ToList();
            }
            foreach (Equity Equity in Equities)
            {
                Equity.symbol = symbol;
            }
            return View();
        }

        public IActionResult Symbols()
        {
            IEXHandler webHandler = new IEXHandler();
            List<Company> companies = webHandler.GetSymbols();  //Save companies in TempData for later
            companies = companies.GetRange(0, 9);
            TempData["Companies"] = JsonConvert.SerializeObject(companies);
            return View(companies);
        }
    }
}