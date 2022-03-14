using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Diagonostics;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;

namespace DreamParkBookingWebView
{
    public partial class Form1 : Form
    {
        private readonly string DreamParkurl = "https://www.dreamparkcc.or.kr/index.asp";
        //Timer Timer = new Timer();
        Random rand = new Random(DateTime.Now.Millisecond);

        List<PersonInfo> PersonInfos;

        string PersonInfoFileName = "personInfo.txt";
        string IDFormat = "document.getElementById(\"ms_id\").value = \"{0}\"";
        string PasswordFormat = "document.getElementById(\"ms_password\").value = \"{0}\"";
        string SelectDateFormat = "xmlhttpPost(\"/include/reservation/reservation_time.asp?submitDate={0}&bk_div=I&str2=\",\"/include/reservation/reservation_check.asp?submitDate={0}&bk_div=I\");";
        string[] PhoneFormats = new string[] { "document.getElementById(\"a01_12\").value = \"{0}\"", "document.getElementById(\"a01_12_2\").value = \"{0}\"", "document.getElementById(\"a01_12_3\").value = \"{0}\"" };
        public Form1()
        {
            InitializeComponent();
            webView21.NavigationCompleted += WebView21_NavigationCompleted;
            GetPersonInfo();
        }

        private void GetPersonInfo()
        {
            var json = File.ReadAllText(PersonInfoFileName);
            PersonInfos = JsonConvert.DeserializeObject<List<PersonInfo>>(json);
        }

        
        private void SetPersonInfos()
        {
            var json = JsonConvert.SerializeObject(PersonInfos);
            if(File.Exists(PersonInfoFileName))
            File.Delete(PersonInfoFileName);
            File.WriteAllText(PersonInfoFileName,json);
        }

        private void WebView21_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            ResetEvent.Set();
        }

        private void Wait(int minSeccond, int maxSeccond)
        {
            int waittime = (int)(minSeccond * 1000 + rand.NextDouble() * (maxSeccond - minSeccond) * 1000);
            Thread.Sleep(waittime);
        }

        private string GetTargetDate()
        {
            var now = DateTime.Now;
            string date;
            if (now.DayOfWeek == DayOfWeek.Monday)
                date = now.AddDays(14 + rand.Next(0, 4)).ToString("yyyyMMdd");
            else if (now.DayOfWeek == DayOfWeek.Tuesday)
                date = now.AddDays(18 + rand.Next(0, 1)).ToString("yyyyMMdd");
            else
                date = null;

            return date;
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            await webView21.EnsureCoreWebView2Async(null);
            if (webView21 != null && webView21.CoreWebView2 != null)
            {
                webView21.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
                if (PersonInfos != null && PersonInfos.Count > 0)
                    Book();
            }
        }

        AutoResetEvent ResetEvent = new AutoResetEvent(false);
        private async void Book()
        {
            foreach (var item in PersonInfos)
            {
                webView21.CoreWebView2.Navigate(DreamParkurl);
                ResetEvent.WaitOne();

                await Book(item);
                item.LastReservationDate = DateTime.Now;
            }

            SetPersonInfos();
        }

        private async Task Book(PersonInfo item)
        {
            if (item.LastReservationDate.Date >= DateTime.Now.Date)
                return;

            var targetDate = GetTargetDate();

            if (string.IsNullOrWhiteSpace(targetDate))
            {
                Log.Write("Failed", "예약 가능 일자가 아님");
                return;
            }

            var dd = await webView21.ExecuteScriptAsync(string.Format(IDFormat, item.ID));
            var ddd = await webView21.ExecuteScriptAsync(string.Format(PasswordFormat, item.Password));
            var result = await webView21.ExecuteScriptAsync("fncLogin()");

            Wait(2, 4);

            result = await webView21.ExecuteScriptAsync("xmlhttpPost_real('/include/reservation/reservation01.asp','681');");

            Wait(2, 4);

            var selectDateString = string.Format(SelectDateFormat, GetTargetDate());
            result = await webView21.ExecuteScriptAsync(selectDateString);

            Wait(2, 4);
            //시간대 선택  8 ~ 11

            Wait(4, 6);
            result = await webView21.ExecuteScriptAsync(string.Format(PhoneFormats[0], item.Phone[0]));
            result = await webView21.ExecuteScriptAsync(string.Format(PhoneFormats[1], item.Phone[1]));
            result = await webView21.ExecuteScriptAsync(string.Format(PhoneFormats[2], item.Phone[2]));
            // sms 요청 

            Wait(10, 20);
            //sms받은거 확인하는 곳에 입력 
            //예약 버튼 클릭 


            Wait(2, 4);
            //예약 됐으면 로그아웃 
        }
    }

    public class PersonInfo
    {
        public string ID { get; set; }
        public string Password { get; set; }

        public string[] Phone { get; set; }
        public DateTime LastReservationDate { get; set; }
    }
}
