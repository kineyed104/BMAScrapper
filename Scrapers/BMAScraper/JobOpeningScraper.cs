using Diagonostics;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace BMAScraper
{
    /// <summary>
    /// 주택관리사협회 구인 내용을 Scraping 
    /// </summary>
    public class JobOpeningScraper
    {
        const string baseurl = @"http://www.khma.org/portal/00011/00109/00114.web";
        const string pageurl = @"http://www.khma.org/portal/00011/00109/00114.web?gradeType=B&workGrade=&sigungu=&cpage={0}";
        const string detailUrl = @"http://www.khma.org/portal/00011/00109/00114.web?amode=view&idx={0}&gradeType=B&workGrade=&sigungu=";
        public void Save(int count = 0)
        {

            var number = count == 0 ? int.MaxValue : count;

            HtmlWeb basePage = new HtmlWeb();
            HtmlDocument basePageDoc = basePage.Load(baseurl);
            var lastPage = basePageDoc.DocumentNode.SelectSingleNode("//span[@class='m last']");
            var atag = lastPage.SelectSingleNode("a");
            var link = atag.GetAttributeValue("href", "");
            var queryValue = HttpUtility.ParseQueryString(link);

            if (int.TryParse(queryValue["amp;cpage"], out int lastPageNumber))
            {
                if (number > lastPageNumber)
                    number = lastPageNumber;


                for (int i = number; i > 0; i--)
                {
                    List<int> indexes = new List<int>();
                    try
                    {
                        HtmlWeb web = new HtmlWeb();
                        var pagedoc = web.Load(string.Format(pageurl, i));
                        var tableDiv = pagedoc.DocumentNode.SelectSingleNode("//div[@class='scroll1cont']");
                        var rows = tableDiv.SelectNodes("table/tbody/tr");
                        foreach (var row in rows)
                        {
                            var linkTd = row.SelectSingleNode("td[@class='tal']");
                            var detailAtag = linkTd.SelectSingleNode("a");
                            var detailLink = HttpUtility.HtmlDecode(detailAtag.GetAttributeValue("href", ""));
                            var queryValues = HttpUtility.ParseQueryString(detailLink);
                            if (int.TryParse(queryValues["idx"], out int index))
                                indexes.Add(index);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Write("error", ex.ToString());
                    }

                    Console.WriteLine(string.Join(",", indexes));

                    var list = new List<JobOpeningInfo>();
                    int processCount = 0;
                    foreach (var index in indexes)
                    {
                        try
                        {
                            if (TryGetInfo(string.Format(detailUrl, index), out JobOpeningInfo jobOpeningInfo))
                                list.Add(jobOpeningInfo);
                        }
                        catch (Exception ex)
                        {
                            Log.Write("TryGetInfo.Error", $"{index} {ex}");
                        }

                        Console.WriteLine($"{processCount++}/{indexes.Count}");
                    }

                    SaveDetail(list);
                }
            }
            else
            {
                //until load fail;
                return;
            }
        }

        private void SaveIdx(List<int> list)
        {
            using (FileStream st = File.Open("index.txt", FileMode.Append))
            {
                using (StreamWriter sw = new StreamWriter(st))
                {
                    sw.WriteLine(string.Join(",", list));
                }
            }
        }

        private void SaveDetail(List<JobOpeningInfo> list)
        {
            using (FileStream st = File.Open(DateTime.Now.ToString("yyyyMMdd") + ".txt", FileMode.Append))
            {
                using (StreamWriter sw = new StreamWriter(st))
                {
                    foreach (var info in list)
                    {
                        try
                        {
                            string aptname = "", locale = "";

                            if (info.datas.ContainsKey("아파트명"))
                                aptname = info.datas["아파트명"].Replace(",", " ");

                            if (info.datas.ContainsKey("우편주소"))
                                locale = info.datas["우편주소"].Replace(",", " ");
                            else if (info.datas.ContainsKey("방문주소"))
                                locale = info.datas["방문주소"].Replace(",", " ");
                            else if (info.datas.ContainsKey("근무지역"))
                                locale = info.datas["근무지역"].Replace(",", " ");

                            sw.WriteLine($"{info.registrationdate},{locale},{aptname},{info.email},{info.link}");
                        }
                        catch (Exception ex)
                        {

                            Log.Write("SaveFail", info.ToString());
                        }
                    }
                }
            }
        }

        private bool TryGetInfo(string link, out JobOpeningInfo jobOpeningInfo)
        {
            jobOpeningInfo = null;
            HtmlWeb web = new HtmlWeb();
            var doc = web.Load(link);
            try
            {
                var headdl = doc.DocumentNode.SelectSingleNode("//dl[@class='view1form1']");

                var headdd = headdl.SelectNodes("dd");
                var headdatas = headdd.Select(d => d.InnerText).ToArray();


                var tableDiv = doc.DocumentNode.SelectSingleNode("//div[@class='scroll1cont']");


                var rows = tableDiv.SelectNodes("table/tbody/tr");

                var datas = new Dictionary<string, string>();
                if (rows != null)
                {
                    foreach (var row in rows)
                    {
                        var header = row.SelectSingleNode("th").InnerText;
                        var node = row.SelectSingleNode("td");
                        var ul = node.SelectSingleNode("ul");
                        if (ul != null)
                        {
                            var data = ul.InnerText;
                            var encodedstring = HttpUtility.HtmlDecode(data).Trim();
                            var split = encodedstring.Split("\n", StringSplitOptions.TrimEntries);
                            foreach (var item in split)
                            {
                                if (string.IsNullOrWhiteSpace(item))
                                {
                                    var token = item.Split(":", StringSplitOptions.TrimEntries);
                                    if (token.Count() >= 2)
                                        datas[token[0]] = token[2];
                                }
                            }
                        }
                        else
                        {
                            var data = node.InnerText;
                            var encodedstring = HttpUtility.HtmlDecode(data).Trim();
                            encodedstring = string.Join(" ", encodedstring.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
                            datas[header] = encodedstring;
                        }
                    }
                }

                var email = tableDiv.SelectSingleNode("//span[@class='ls0 fsM']")?.InnerText;
                jobOpeningInfo = new JobOpeningInfo(headdatas, datas, email, link);
            }
            catch (Exception ex)
            {
                Log.Write("invalidFormat", link);
                Log.Write("Error", ex.ToString());
                return false;
            }

            return true;
        }
    }
}
