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
        private readonly string LogoutUrl = "https://www.dreamparkcc.or.kr/07membership/logout.asp";
        //Timer Timer = new Timer();
        Random rand = new Random(DateTime.Now.Millisecond);

        List<PersonInfo> PersonInfos;
        Queue<PersonInfo> BookingQueue = new Queue<PersonInfo>();

        string PersonInfoFileName = "personInfo.txt";
        string IDFormat = "document.getElementById(\"ms_id\").value = \"{0}\"";
        string PasswordFormat = "document.getElementById(\"ms_password\").value = \"{0}\"";
        string SelectDateFormat = "xmlhttpPost(\"/include/reservation/reservation_time.asp?submitDate={0}&bk_div=I&str2=\",\"/include/reservation/reservation_check.asp?submitDate={0}&bk_div=I\");";
        string SelectTimeFormat = "xmlhttpPost2(\"/include/reservation/reservation_check.asp?submitDate={0}&bk_div=I&bk_time={1}&pin_no=\", \"I\");";
        string CheckPhoneFormat = "chkHandPhone(formWait,{0},{1});";
        string CerNumFormat = "document.formWait.serNum.value = '{0}'";
        string GetCertNoFormat = "document.formWait.cert_no.value";
        string ReservationFormat = "sms_send(\"{0}\",\"{1}\",\"I\")";

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
            foreach (var item in PersonInfos)
            {
                BookingQueue.Enqueue(item);
            }
        }


        private void SetPersonInfos()
        {
            var json = JsonConvert.SerializeObject(PersonInfos);
            if (File.Exists(PersonInfoFileName))
                File.Delete(PersonInfoFileName);
            File.WriteAllText(PersonInfoFileName, json);
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

                webView21.CoreWebView2.Navigate(DreamParkurl);
            }
        }

        private async void WebView21_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if(BookingQueue.TryDequeue(out PersonInfo pinfo))
            {
                await Book(pinfo);
                pinfo.LastReservationDate = DateTime.Now;
                webView21.CoreWebView2.Navigate(LogoutUrl);
                SetPersonInfos();

                if (BookingQueue.Count > 0)
                    webView21.CoreWebView2.Navigate(DreamParkurl);
            }
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

            Wait(4, 8);
            var dd = await ExecuteAndLogAsync(string.Format(IDFormat, item.ID));

            var ddd = await ExecuteAndLogAsync(string.Format(PasswordFormat, item.Password));
            var result = await ExecuteAndLogAsync("fncLogin()");

            Wait(1, 5);
            var source = webView21.Source;
            result = await ExecuteAndLogAsync("xmlhttpPost_real('/include/reservation/reservation01.asp','200');");  //숫자는 now 대기인 명수 
            Wait(1, 5);

            var selectDateString = string.Format(SelectDateFormat, targetDate);
            result = await ExecuteAndLogAsync(selectDateString);

            Wait(1, 5);
            //시간대 선택  8 ~ 11

            string targetTime = "";
            if (rand.Next(0, 1) == 1)
                targetTime = "11";
            else
                targetTime = "08";

            var selectTimeString = string.Format(SelectTimeFormat, targetDate, targetTime);
            result = await ExecuteAndLogAsync(selectTimeString);

            Wait(4, 7);
            result = await ExecuteAndLogAsync(string.Format(PhoneFormats[0], item.Phone[0]));
            result = await ExecuteAndLogAsync(string.Format(PhoneFormats[1], item.Phone[1]));
            result = await ExecuteAndLogAsync(string.Format(PhoneFormats[2], item.Phone[2]));

            result = await ExecuteAndLogAsync(string.Format(CheckPhoneFormat, targetTime, string.Join("", item.Phone)));
            Wait(10, 30);

            result = await ExecuteAndLogAsync(GetCertNoFormat);
            var certnumString = string.Format(CerNumFormat, result);
            await ExecuteAndLogAsync(certnumString);


            await ExecuteAndLogAsync(string.Format(ReservationFormat, targetDate, targetTime));
            Wait(2, 4);
        }

        private async Task<string> ExecuteAndLogAsync(string command)
        {
            var result = await webView21.ExecuteScriptAsync(command);
            Log.Write("DreamLog", result);

            return result;
        }

        private async void Form1_SizeChanged(object sender, EventArgs e)
        {
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
