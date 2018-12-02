using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IEXTrading.Models.ViewModel
{
    public class CompanyQuote
    {
        public Company company { get; set; }

        public string primaryExchange { get; set; }

        public float? latestPrice { get; set; }

        public float? week52High { get; set; }
        public float? week52Low { get; set; }

        public float? close { get; set; }
        public float? marketCap { get; set; }
        public float? avgTotalVolume { get; set; }
    }
}
