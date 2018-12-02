using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using IEXTrading.Infrastructure.IEXTradingHandler;
using IEXTrading.Models;
using IEXTrading.Models.ViewModel;
using IEXTrading.DataAccess;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Http;

namespace MVCTemplate.Controllers
{
    public class HomeController : Controller
    {
        public ApplicationDbContext dbContext;

        public HomeController(ApplicationDbContext context)
        {
            dbContext = context;
        }

        public IActionResult Index(string symbol)
        {
            //Set ViewBag variable first and Initializing all the variables       
            ViewBag.dbSucessComp = 0;
            List<CompanyQuote> companyQuotes = new List<CompanyQuote>();
            Quote quote = new Quote();
            List<Company> companies = new List<Company>();

            if (symbol != null)
            {
                //Checking for any symbol with save_ as prefix inorder to save data
                if (symbol.Contains("save_"))
                {
                    //Checking for null contact
                    symbol = symbol.Split("save_")?.LastOrDefault();
                    if (SaveOrDeleteStock(symbol, true))
                        ViewBag.dbSuccessStock = "SuccessStock";
                }
                else
                {
                    Dictionary<string, Dictionary<string, Quote>> quotes = new Dictionary<string, Dictionary<string, Quote>>();
                    if (symbol != null)
                    {
                        //Fetching the quote from database
                        quote = dbContext.Quotes.Where(q => q.symbol == symbol).FirstOrDefault();
                    }
                }
                companies = GetCompanies("Companies");
            }
            else
            {
                List<Tuple<Company, Quote>> companyQuote = new List<Tuple<Company, Quote>>();
                //Getting the date of company
                string lastSavedStockTime = dbContext.Companies.FirstOrDefault()?.date;
                //We are refreshing the companies and quotes in database for every month
                if (lastSavedStockTime == null || (DateTime.Now.Date - Convert.ToDateTime(lastSavedStockTime).Date).Days > 30)
                {
                    if (!LoadData())
                        return View(companyQuote);
                }
                else
                {
                    companies = dbContext.Companies.ToList();
                }

                //Storing the companies into the session
                String companiesData = JsonConvert.SerializeObject(companies);
                HttpContext.Session.SetString("Companies", companiesData);
            }

            foreach (var company in companies)
            {
                if (company.symbol == quote.symbol)
                    companyQuotes.Add(new CompanyQuote
                    {
                        company = company,
                        avgTotalVolume = quote.avgTotalVolume,
                        close = quote.close,
                        latestPrice = quote.latestPrice,
                        marketCap = quote.marketCap,
                        primaryExchange = quote.primaryExchange,
                        week52High = quote.week52High,
                        week52Low = quote.week52Low
                    });
                else
                    companyQuotes.Add(new CompanyQuote { company = company });
            }

            return View(companyQuotes);

        }

        /// <summary>
        /// This Method returns the saved stocks if no symbols is passed or saves as user prefered stock if any symbol passed
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public IActionResult SavedStocks(string symbol)
        {
            List<Quote> quoteList = new List<Quote>();
            string quoteSymbol = "";
            if (symbol != null)
            {
                //Checking for any symbol with del_ as prefix inorder to delete the stock from user's saved stocks
                if (symbol.Contains("del_"))
                {
                    quoteSymbol = symbol.Split("del_")?.LastOrDefault();
                    if (SaveOrDeleteStock(quoteSymbol, false))
                        ViewBag.dbSuccessQuote = "success";
                }
            }
            //Returning only those stocks which are preferred by user
            List<string> companiesSymbols = dbContext.Companies.Where(c => c.IsPreferedByUser)?.Select(c => c.symbol)?.ToList();
            quoteList = dbContext.Quotes.Where(q => companiesSymbols.Contains(q.symbol)).ToList();
            return View(quoteList);
        }

        /// <summary>
        /// if symbol exists, then it adds the data,else it populates all the top stocks
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public IActionResult Quotes(string symbol)
        {
            List<Quote> quoteList = new List<Quote>();
            string quoteSymbol = "";
            if (symbol != null)
            {
                //Checking for companies which contains symbol as save_ inorder to save the stock
                if (symbol.Contains("save_"))
                {
                    quoteSymbol = symbol.Split("save_").LastOrDefault();
                    if (SaveOrDeleteStock(quoteSymbol, true))
                        ViewBag.dbSuccessQuote = "success";
                }
            }

            //Fetching all the companies which has top stocks
            List<Company> companies = dbContext.Companies.Where(c => c.isTopStock).ToList();
            List<string> companiesSymbols = companies?.Select(c => c.symbol)?.ToList();
            quoteList = dbContext.Quotes.Where(q => companiesSymbols.Contains(q.symbol)).ToList();
            companies = companies.Where(c => companiesSymbols.Contains(c.symbol)).ToList();

            var companyQuote = new List<Tuple<Company, Quote>>();
            foreach (var company in companies)
            {
                Quote quote = quoteList.Where(q => q.symbol == company.symbol).FirstOrDefault();
                if (quote != null)
                {
                    companyQuote.Add(new Tuple<Company, Quote>(company, quote));
                }
            }

            return View(companyQuote);
        }

        public IActionResult AboutUs()
        {
            return View();
        }

        /****
         * The Chart action calls the GetChart method that returns 1 year's equities for the passed symbol.
         * A ViewModel CompaniesEquities containing the list of companies, prices, volumes, avg price and volume.
         * This ViewModel is passed to the Chart view.
        ****/
        public IActionResult Chart(string symbol)
        {
            //Set ViewBag variable first
            ViewBag.dbSuccessChart = 0;
            List<Equity> equities = new List<Equity>();
            if (symbol != null)
            {
                IEXHandler webHandler = new IEXHandler();
                equities = webHandler.GetChart(symbol);
                equities = equities.OrderBy(c => c.date).ToList(); //Make sure the data is in ascending order of date.
            }

            CompaniesEquities companiesEquities = getCompaniesEquitiesModel(equities);

            return View(companiesEquities);
        }

        /****
         * The Refresh action calls the ClearTables method to delete records from a or all tables.
         * Count of current records for each table is passed to the Refresh View.
        ****/
        public IActionResult Refresh(string tableToDel)
        {
            ClearTables(tableToDel);
            Dictionary<string, int> tableCount = new Dictionary<string, int>();
            tableCount.Add("Companies", dbContext.Companies.Where(c => c.IsPreferedByUser).Count());
            tableCount.Add("Charts", dbContext.Equities.Count());
            return View(tableCount);
        }

        /// <summary>
        /// Generic function to save as preferd stock by user or not
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="save"></param>
        /// <returns>bool</returns>
        public bool SaveOrDeleteStock(string symbol, bool save)
        {
            bool isSuccess = false;
            try
            {
                Company company = dbContext.Companies.Where(c => c.symbol == symbol).FirstOrDefault();
                company.IsPreferedByUser = save;
                dbContext.SaveChanges();
                return !isSuccess;
            }
            catch (Exception)
            {
                return isSuccess;
            }
        }

        /// <summary>
        /// Fetches all the Quote details of the symbol
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public IActionResult Details(string symbol)
        {
            Tuple<Company, Quote> companyQuote = null;
            if (symbol != null)
            {
                Company company = dbContext.Companies.Where(c => c.symbol == symbol).FirstOrDefault();
                Quote quote = dbContext.Quotes.Where(q => q.symbol == symbol).FirstOrDefault();
                companyQuote = new Tuple<Company, Quote>(company, quote);
            }
            return View(companyQuote);
        }


        /****
         * Saves the equities in database.
        ****/
        public IActionResult SaveCharts(string symbol)
        {
            IEXHandler webHandler = new IEXHandler();
            List<Equity> equities = webHandler.GetChart(symbol);
            //List<Equity> equities = JsonConvert.DeserializeObject<List<Equity>>(TempData["Equities"].ToString());
            foreach (Equity equity in equities)
            {
                if (dbContext.Equities.Where(c => c.date.Equals(equity.date)).Count() == 0)
                {
                    dbContext.Equities.Add(equity);
                }
            }

            dbContext.SaveChanges();
            ViewBag.dbSuccessChart = 1;

            CompaniesEquities companiesEquities = getCompaniesEquitiesModel(equities);

            return View("Chart", companiesEquities);
        }

        /// <summary>
        /// Fetches the Companies from the session
        /// </summary>
        /// <param name="sessionName"></param>
        /// <returns></returns>
        public List<Company> GetCompanies(string sessionName)
        {
            List<Company> companies = new List<Company>();
            string companiesData = HttpContext.Session.GetString(sessionName);
            if (companiesData != "")
            {
                companies = JsonConvert.DeserializeObject<List<Company>>(companiesData);
            }
            return companies;
        }

        /****
         * Deletes the records from tables.
        ****/
        public void ClearTables(string tableToDel)
        {
            if ("all".Equals(tableToDel))
            {
                //First remove equities and then the companies
                dbContext.Equities.RemoveRange(dbContext.Equities);
                dbContext.Companies.RemoveRange(dbContext.Companies.Where(c => c.IsPreferedByUser));
            }
            else if ("Companies".Equals(tableToDel))
            {
                //Remove only those that don't have Equity stored in the Equitites table
                dbContext.Companies.RemoveRange(dbContext.Companies
                                                         .Where(c => (c.Equities.Count == 0) && (c.IsPreferedByUser))
                                                                      );
            }
            else if ("Charts".Equals(tableToDel))
            {
                dbContext.Equities.RemoveRange(dbContext.Equities);
            }
            dbContext.SaveChanges();
        }

        //Loads all the data of companies and stocks from API and stores in database
        public bool LoadData()
        {
            bool isFailure = true;
            try
            {
                IEXHandler webHandler = new IEXHandler();
                List<Company> companies = null;
                List<Quote> quotes = null;
                List<Quote> topQuotes = null;
                //Removing those companies whose types are N/A and isenabled as false assuming them as companies which are not trust worthy
                companies = webHandler.GetSymbols()?.Where(a => a.isEnabled && a.type != "N/A").ToList();
                dbContext.Companies.RemoveRange(dbContext.Companies);
                dbContext.SaveChanges();
                quotes = webHandler.GetAllQuotesFromAPI(companies);
                topQuotes = webHandler.GetTopQuotes(companies, quotes);
                companies?.Where(c => topQuotes.Select(t => t.symbol).Contains(c.symbol))?.ToList()?.ForEach(x => x.isTopStock = true);
                dbContext.Companies.AddRange(companies);
                dbContext.SaveChanges();
                dbContext.Quotes.RemoveRange(dbContext.Quotes);
                dbContext.SaveChanges();
                dbContext.Quotes.AddRange(quotes);
                dbContext.SaveChanges();
                return !isFailure;
            }
            catch (Exception)
            {
                return isFailure;
            }
        }

        /****
         * Returns the ViewModel CompaniesEquities based on the data provided.
         ****/
        public CompaniesEquities getCompaniesEquitiesModel(List<Equity> equities)
        {
            List<Company> companies = dbContext.Companies.Where(c => c.IsPreferedByUser).ToList();

            if (equities.Count == 0)
            {
                return new CompaniesEquities(companies, null, "", "", "", 0, 0);
            }

            Equity current = equities.Last();
            string dates = string.Join(",", equities.Select(e => e.date));
            string prices = string.Join(",", equities.Select(e => e.high));
            string volumes = string.Join(",", equities.Select(e => e.volume / 1000000)); //Divide vol by million
            float avgprice = equities.Average(e => e.high);
            double avgvol = equities.Average(e => e.volume) / 1000000; //Divide volume by million
            return new CompaniesEquities(companies, equities.Last(), dates, prices, volumes, avgprice, avgvol);
        }

    }
}
