using Diagonostics;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DreamParkBooking
{

    class AutoBooking
    {
        public string ID { get; }
        public string Password { get; }

        const string baseurl = "https://www.dreamparkcc.or.kr/";
        const string loginUrl = "07membership/login_ok.asp";
        const string bookingUrl2 = "include/reservation/phonecheck_t.asp";

        HttpClientHelper helper = new HttpClientHelper(baseurl);
        public AutoBooking(string id, string password)
        {
            ID = id;
            Password = password;
        }

        internal void Start()
        {
            if (Login() && Book())
                Log.Write("Success", $"{ID}");
        }

        public bool Login()
        {
            try
            {
                var loginValue = new Dictionary<string, string>();
                loginValue.Add("ms_id", ID);
                loginValue.Add("ms_password", Password);
                var ts = helper.PostAndReadAsync<Stream>(loginUrl, loginValue);
                ts.Wait();
                string page;
                using (StreamReader sr = new StreamReader(ts.Result))
                {
                    page = sr.ReadToEnd();
                }
                Uri uri = new Uri(baseurl);
                var cookies = helper.Cookies.GetCookies(uri).Cast<Cookie>();
                var dd = cookies.Where(d => d.Name.Contains("ASPSESSION")).FirstOrDefault();

                if (dd != null)
                    helper.SetCookie(dd.Name + "=" + dd.Value);

                return true;
            }
            catch (Exception ex)
            {
                Log.Write("Fail", $"{ID} failed. \n" + ex.ToString());
                return false;
            }

        }

        public bool Book()
        {
            try
            {
                var now = DateTime.Now;
                string date;
                if (now.DayOfWeek == DayOfWeek.Monday)
                    date = now.AddDays(18).ToString("yyyyMMdd");
                else if (now.DayOfWeek == DayOfWeek.Tuesday)
                    date = now.AddDays(18).ToString("yyyyMMdd");
                else
                {
                    Console.WriteLine("몇일 뒤?");
                    if (int.TryParse(Console.ReadLine(), out int day))
                    {
                        date = now.AddDays(day).ToString("yyyyMMdd");
                    }
                    else
                    {
                        Log.Write("Fail", $"{ID} failed. 예약일자가 아닙니다.");
                        return false;
                    }
                }

                var bookContent = new Dictionary<string, string>();
                bookContent.Add("book_date", date);
                bookContent.Add("book_time", "11");
                bookContent.Add("book_cos", "");
                bookContent.Add("bk_div", "I");
                var ts = helper.PostAndReadAsync<Stream>(bookingUrl2, bookContent);
                ts.Wait();
                string page;
                using (StreamReader sr = new StreamReader(ts.Result))
                {
                    page = sr.ReadToEnd();
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Write("Fail", $"{ID} failed. \n" + ex.ToString());
                return false;
            }

        }
    }
}

