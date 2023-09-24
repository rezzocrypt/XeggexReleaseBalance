using Flurl.Http;
using System;
using System.Linq;
using System.Text;

namespace XeggexReleaseBalance
{

    class CoinInfo
    {
        public string asset { get; set; }
        public string name { get; set; }
        public double available { get; set; }
        public double pending { get; set; }
        public double held { get; set; }
        public string assetid { get; set; }
    }

    class marketPair
    {
        public string trading_pairs
        {
            get { return $"{first}_{second}"; }
            set { var r = value.Split("_"); first = r[0]; second = r[1]; }
        }
        public double last_price { get; set; }

        public string first { get; set; }
        public string second { get; set; }
    }

    class convertToken
    {
        public string asset { get; set; }
        public double minimalSum { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var apiUrl = "https://api.xeggex.com/api/v2";

            var nonConvert = new[] { "PDN", "SKY", "USDT" };
            var convertToToken = new[] {
                new convertToken { asset = "USDT", minimalSum = 0.0001  },
                new convertToken { asset = "BNB", minimalSum = 0.000005  },
                new convertToken { asset = "DOGE", minimalSum = 0.002  },
                new convertToken { asset = "USDC", minimalSum = 0.0001  },
                new convertToken { asset = "BTC", minimalSum = 0.000000004  }
            };
            var nonConvertable = nonConvert.Union(convertToToken.Select(i => i.asset)).Distinct().ToArray();

            var secretHash = Encoding.ASCII.GetBytes("c8bb739005197c4c1cd20790636e83e2:0b065267fe45c06bc0d64033f840c79a91421e7e5d72d80b");

            //получаем возможные направления обмена
            var marketPairs = (apiUrl + "/summary")
                .GetJsonAsync<marketPair[]>()
                .Result;

            var asyncResult = (apiUrl + "/balances")
                .WithHeader("Authorization", "Basic " + Convert.ToBase64String(secretHash))
                .GetJsonAsync<CoinInfo[]>();

            var result = asyncResult.Result
                .Where(item => item.available > 0)
                .Where(item => !nonConvertable.Contains(item.asset))
                //.Where (item => new[] { "HOW"}.Contains(item.asset))
                .ToArray();


            foreach (var token in result)
            {
                var convertAvail = 0;
                var availableforTrade = marketPairs
                    .Where(item => item.first == token.asset)
                    .ToArray();

                foreach (var convertTo in convertToToken)
                {
                    var trade = availableforTrade
                        .Where(item => item.second == convertTo.asset)
                        .Select(item => new { token = item.second, sum = item.last_price * token.available, minimalSum = convertTo.minimalSum })
                        .FirstOrDefault();

                    if (trade == null)
                        continue;

                    convertAvail += 1;
                    if (trade.minimalSum > trade.sum)
                    {
                        Console.WriteLine($"Pair {token.asset}/{convertTo.asset} balance very small");
                        continue;
                    }

                    var asyncOrder = (apiUrl + "/createorder")
                        .WithHeader("Authorization", "Basic " + Convert.ToBase64String(secretHash))
                        .PostJsonAsync(new { symbol = $"{token.asset}/{convertTo.asset}", side = "sell", quantity = token.available, type = "market", strictValidate = false });
                    try
                    {
                        var orderResult = asyncOrder.Result;
                        Console.WriteLine($"Pair {token.asset}/{convertTo.asset} converted");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Pair {token.asset}/{convertTo.asset} convert error");
                    }

                }

                if (convertAvail == 0)
                {
                    Console.WriteLine($"{token.asset} exchange pair not found. Available: {string.Join(",", availableforTrade.Select(i => i.second).ToArray())}");
                    continue;
                }
            }
            Console.WriteLine("Complete.");
            Console.ReadLine();
        }
    }
}
