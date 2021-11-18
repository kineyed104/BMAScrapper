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

namespace KFEMAScraper
{
    /// <summary>
    /// 주택관리사협회 구인 내용을 Scraping 
    /// </summary>
    public class JobOpeningScraper
    {
        const string baseurl = @"http://www.fema.or.kr/board/recruit.php";
        const string pageurl = @"http://www.fema.or.kr";
        public void Save(int count = 0)
        {

            var number = count == 0 ? int.MaxValue : count;

            HtmlWeb basePage = new HtmlWeb();
            HtmlDocument basePageDoc = basePage.Load(baseurl);
            var content = basePageDoc.DocumentNode.SelectSingleNode("//div[@id='content']");
            var litable = content.SelectSingleNode("table");
            var litd = litable.SelectSingleNode("tr/td");
            var atags = litd.SelectNodes("a");

            List<string> urls = new List<string>() { baseurl };
            foreach (var atag in atags)
            {
                urls.Add(pageurl+HttpUtility.HtmlDecode(atag.GetAttributeValue("href", "")));
            }


            foreach (var item in urls)
            {
                try
                {
                    HtmlWeb web = new HtmlWeb();
                    var pagedoc = web.Load(item);
                    var contentdoc = pagedoc.DocumentNode.SelectSingleNode("//div[@id='content']");
                    var tablecondoc = contentdoc.SelectSingleNode("div[@id='tblist']");
                    var tabledoc = tablecondoc.SelectSingleNode("table");
                    var tbodydoc = tabledoc.SelectSingleNode("tbody");
                    var trs = tbodydoc.SelectNodes("tr");

                    var list = new List<JobOpeningInfo>();
                    int processCount = 0;

                    foreach (var tr in trs)
                    {
                        try
                        {
                            var tds = tr.SelectNodes("td");
                            var atag = tds[1].SelectSingleNode("a");
                            var detailLink = HttpUtility.HtmlDecode(atag.GetAttributeValue("href", ""));
                            var url = pageurl + detailLink;

                            if (TryGetInfo(url, out JobOpeningInfo jobOpeningInfo))
                                list.Add(jobOpeningInfo);
                        }
                        catch (Exception ex)
                        {
                            Log.Write("TryGetInfo.Error", $"{ex}");
                        }

                        Console.WriteLine($"{processCount++}/{trs.Count}");
                    }

                    SaveDetail(list);
                }
                catch (Exception ex)
                {
                    Log.Write("error", ex.ToString());
                }
            }

        }

        private bool TryGetInfo(string url, out JobOpeningInfo jobOpeningInfo)
        {
            jobOpeningInfo = null;

            try
            {
                HtmlWeb web = new HtmlWeb();
                var pagedoc = web.Load(url);
                var descdoc = pagedoc.DocumentNode.SelectSingleNode("//form[@name='maruForm']");
                var tables = descdoc.SelectNodes("table");

                if (tables.Count < 2)
                    return false;

                var dic = new Dictionary<string, string>();

                foreach (var table in tables)
                {
                    var trs = table.SelectNodes("tr");

                    foreach (var tr in trs)
                    {
                        var tds = tr.SelectNodes("td");
                        if (tds.Count != 2)
                            continue;

                        dic.Add(HttpUtility.HtmlDecode(tds[0].InnerText).Trim(), HttpUtility.HtmlDecode(tds[1].InnerText).Trim());
                    }
                }

                jobOpeningInfo = new JobOpeningInfo(dic);
            }
            catch (Exception ex)
            {
                Log.Write("invalidFormat", url);
                Log.Write("Error", ex.ToString());
                return false;
            }

            return true;
        }

        private void SaveDetail(List<JobOpeningInfo> list)
        {
            using (FileStream st = File.Open(DateTime.Now.ToString("yyyyMMdd") + ".txt", FileMode.Append))
            {
                using (StreamWriter sw = new StreamWriter(st))
                {
                    foreach (var info in list)
                    {
                        var line = "";
                        foreach (var item in info.data)
                        {
                            line+=string.Join(" ", item.Value.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries)).Replace(","," ")+",";
                        }

                        sw.WriteLine(line);
                    }
                }
            }
        }
    }
}
