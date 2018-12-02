using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using IEXTrading.Models;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace IEXTrading.Infrastructure.IEXTradingHandler
{
    public class IEXHandler
    {
        static string BASE_URL = "https://api.iextrading.com/1.0/"; //This is the base URL, method specific URL is appended to this.
        HttpClient httpClient;

        public IEXHandler()
        {
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        /****
         * Calls the IEX reference API to get the list of symbols. 
        ****/
        public List<Company> GetSymbols()
        {
            string IEXTrading_API_PATH = BASE_URL + "ref-data/symbols";
            string companyList = "";

            List<Company> companies = null;

            httpClient.BaseAddress = new Uri(IEXTrading_API_PATH);
            HttpResponseMessage response = httpClient.GetAsync(IEXTrading_API_PATH).GetAwaiter().GetResult();
            if (response.IsSuccessStatusCode)
            {
                companyList = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            }

            if (!companyList.Equals(""))
            {
                companies = JsonConvert.DeserializeObject<List<Company>>(companyList);
            }
            return companies;
        }

        /// <summary>
        /// Calls the IEX API to fetch all the quotes for all the provided companies
        /// </summary>
        /// <param name="companies"></param>
        /// <returns></returns>
        public List<Quote> GetAllQuotesFromAPI(List<Company> companies)
        {
            List<Quote> quotesList = new List<Quote>();
            int skipCount = 0;
            Dictionary<string, Dictionary<string, Quote>> quoteList = new Dictionary<string, Dictionary<string, Quote>>();
            //while loop to make api call for all the symbols with 100 in each call.
            while (true)
            {
                //Checking if skip count is less than companies count
                if (skipCount < companies.Count())
                {
                    string symbols = "";
                    //Skip count for every loop of 100
                    symbols = string.Join(",", companies.Select(a => a.symbol).Skip(skipCount).Take(100));

                    //Using the format method.
                    Dictionary<string, Dictionary<string, Quote>> quotes = null;
                    string quotesJson = GetQuote(symbols);

                    if (!string.IsNullOrEmpty(quotesJson))
                    {
                        //Deserializing json using dictionary of Quote
                        quotes = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Quote>>>(quotesJson);
                        skipCount += 100;
                        quoteList = quoteList.Concat(quotes).ToDictionary(a => a.Key, a => a.Value);
                    }
                }
                else
                {
                    break;
                }
            }
            if (quoteList != null)
            {
                foreach (var quote in quoteList)
                {
                    if (quote.Value != null)
                    {
                        if (quote.Value.FirstOrDefault().Value != null)
                        {
                            var quoteValue = quote.Value.FirstOrDefault().Value;
                            quote.Value.FirstOrDefault().Value.week52PriceRange = (quoteValue.close - quoteValue.week52Low) / (quoteValue.week52High - quoteValue.week52Low);
                            quotesList.Add(quote.Value.FirstOrDefault().Value);
                        }
                    }
                }
            }

            return quotesList;
        }

        /// <summary>
        /// Gets the top 5 stocks 
        /// </summary>
        /// <param name="companies"></param>
        /// <param name="quotes"></param>
        /// <returns></returns>
        public List<Quote> GetTopQuotes(List<Company> companies, List<Quote> quotes)
        {
            //Reference:https://www.aaii.com/journal/article/quantitative-strategies-for-selecting-stocks
            //*******52-Week Price Range Strategy*********
            //A simple way to measure price momentum is to consider the proximity of a stock to its 52-week high or low. 
            //The formula we used for this was current price minus 52-week low divided by 52-week high minus 52-week low. 
            //Stocks that score 82% or higher on this formula tend to outperform, while stocks that score 41% or lower tend to underperform.
            List<Quote> quotesList = quotes.Where(a => (!string.IsNullOrEmpty(a.companyName) &&
                         //Removing those quotes whose week52high - week52low is 0 to avoid 0 condition in denominator for top strategies algorithm
                         (a.week52High - a.week52Low) > 0) &&
                         //Selecting stocks whose volume is greater than 30 million for genuine stocks
                         (a.latestVolume > 30000000)).ToList();

            if (quotesList != null && quotesList.Count() > 0)
            {
                foreach (var quote in quotesList)
                {
                    quote.week52PriceRange = (quote.close - quote.week52Low) / (quote.week52High - quote.week52Low);
                }
            }

            //Returning only top 5 stocks whose week 52 price range is greater than or equal to 0.82
            return quotesList.OrderByDescending(a => a.week52PriceRange).Where(a => a.week52PriceRange >= 0.82).Take(5).ToList();
        }

        /// <summary>
        /// Get Quote of the symbol from the IEX API
        /// </summary>
        /// <param name="symbols"></param>
        /// <returns></returns>
        public string GetQuote(string symbols)
        {
            //Using the format method.
            string IEXTrading_API_PATH = BASE_URL + "stock/market/batch?symbols={0}&types=quote";
            IEXTrading_API_PATH = string.Format(IEXTrading_API_PATH, symbols);
            string quotesJson = "";
            HttpResponseMessage response = httpClient.GetAsync(IEXTrading_API_PATH).GetAwaiter().GetResult();
            if (response.IsSuccessStatusCode)
            {
                quotesJson = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            }
            else
            {
                quotesJson = null;
            }
            return quotesJson;
        }

        /****
         * Calls the IEX stock API to get 1 year's chart for the supplied symbol. 
        ****/
        public List<Equity> GetChart(string symbol)
        {
            //Using the format method.
            //string IEXTrading_API_PATH = BASE_URL + "stock/{0}/batch?types=chart&range=1y";
            //IEXTrading_API_PATH = string.Format(IEXTrading_API_PATH, symbol);

            string IEXTrading_API_PATH = BASE_URL + "stock/" + symbol + "/batch?types=chart&range=1y";

            string charts = "";
            List<Equity> Equities = new List<Equity>();
            httpClient.BaseAddress = new Uri(IEXTrading_API_PATH);
            HttpResponseMessage response = httpClient.GetAsync(IEXTrading_API_PATH).GetAwaiter().GetResult();
            if (response.IsSuccessStatusCode)
            {
                charts = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            }
            if (!charts.Equals(""))
            {
                ChartRoot root = JsonConvert.DeserializeObject<ChartRoot>(charts, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                Equities = root.chart.ToList();
            }
            //make sure to add the symbol the chart
            foreach (Equity Equity in Equities)
            {
                Equity.symbol = symbol;
            }

            return Equities;
        }
    }
}
