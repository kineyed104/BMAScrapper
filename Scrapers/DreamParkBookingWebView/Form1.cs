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
        ManualResetEvent mre = new ManualResetEvent(false);

        List<PersonInfo> PersonInfos;
        Queue<PersonInfo> BookingQueue = new Queue<PersonInfo>();

        string PersonInfoFileName = "personInfo.txt";
        string IDFormat = "document.getElementById(\"ms_id\").value = \"{0}\"";
        string PasswordFormat = "document.getElementById(\"ms_password\").value = \"{0}\"";
        string SelectDateFormat = "xmlhttpPost(\"/include/reservation/reservation_time.asp?submitDate={0}&bk_div=I&str2=\",\"/include/reservation/reservation_check.asp?submitDate={0}&bk_div=I\");";
        string SelectTimeFormat = "javascript:xmlhttpPost2(\"/include/reservation/reservation_check.asp?submitDate={0}&bk_div=I&bk_time={1}&pin_no=\", \"I\");";
        string CheckPhoneFormat = "chkHandPhone(formWait,{0},{1});";
        string CerNumFormat = "document.formWait.serNum.value = '{0}'";
        string GetCertNoFormat = "document.formWait.cert_no.value";
        string ReservationFormat = "sms_send(\"{0}\",\"{1}\",\"I\")";

        string[] PhoneFormats = new string[] { "document.getElementById(\"a01_12\").value = \"{0}\"", "document.getElementById(\"a01_12_2\").value = \"{0}\"", "document.getElementById(\"a01_12_3\").value = \"{0}\"" };
        private bool startBooking;

        public Form1()
        {
            InitializeComponent();
            webView21.NavigationCompleted += WebView21_NavigationCompleted;
            GetPersonInfo();
        }

        private async void WebView21_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (startBooking)
                return;
            else
                startBooking = true;

            if (BookingQueue.TryDequeue(out PersonInfo pinfo))
            {
                var result = await Book(pinfo);
                if (result)
                {
                    pinfo.LastReservationDate = DateTime.Now;
                    SetPersonInfos();
                }

                webView21.CoreWebView2.Navigate(LogoutUrl);
                startBooking = false;

                if (BookingQueue.Count > 0)
                    webView21.CoreWebView2.Navigate(DreamParkurl);
            }
            else
                this.Close();
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

        private async Task Wait(int minSeccond, int maxSeccond)
        {
#if !DEBUG
            int waittime = (int)(minSeccond * 1000 + rand.NextDouble() * (maxSeccond - minSeccond) * 1000);
            await Task.Delay(waittime);
#else
            await Task.Run(() =>
            {
                mre.Reset();
                mre.WaitOne();
            });
#endif
        }

        private List<DateTime> GetTargetDates()
        {
            List<DateTime> result = new List<DateTime>();
            var now = DateTime.Now;
            string date;
            if (now.DayOfWeek == DayOfWeek.Monday)
            {
                for (int i = 0; i < 5; i++)
                {
                    result.Add(now.AddDays(14 + i));
                }
            }
            else if (now.DayOfWeek == DayOfWeek.Tuesday)
            {
                for (int i = 0; i < 2; i++)
                {
                    result.Add(now.AddDays(18 + i));
                }
            }

            return result;
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            await webView21.EnsureCoreWebView2Async(null);
            if (webView21 != null && webView21.CoreWebView2 != null)
            {
                webView21.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
                webView21.CoreWebView2.ScriptDialogOpening += CoreWebView2_ScriptDialogOpening;
                webView21.CoreWebView2.Navigate(DreamParkurl);
            }
        }

        private void CoreWebView2_ScriptDialogOpening(object sender, CoreWebView2ScriptDialogOpeningEventArgs e)
        {
            if (e.Kind == CoreWebView2ScriptDialogKind.Confirm)
                e.Accept();
        }

        private async Task<bool> Book(PersonInfo item)
        {
            if (item.LastReservationDate.Date >= DateTime.Now.Date)
                return false;

            await Wait(4, 8);
            var dd = await ExecuteAndLogAsync(string.Format(IDFormat, item.ID));

            var ddd = await ExecuteAndLogAsync(string.Format(PasswordFormat, item.Password));
            var result = await ExecuteAndLogAsync("fncLogin()");

            await Wait(1, 5);
            var source = webView21.Source;
            result = await ExecuteAndLogAsync("xmlhttpPost_real('/include/reservation/reservation01.asp','200');");  //숫자는 now 대기인 명수 
            await Wait(1, 5);


            //가능 한 시간 가져오기
            var datesCountstring = await ExecuteAndLogAsync("document.getElementsByClassName(\"bg_red\").length");
            List<int> availableDates = new List<int>();
            if (int.TryParse(datesCountstring, out int dateCount))
            {
                for (int i = 0; i < dateCount; i++)
                {
                    var available = await ExecuteAndLogAsync($"document.getElementsByClassName(\"bg_red\")[{i}].innerHTML");

                    if (!string.IsNullOrWhiteSpace(available))
                        available = available.Substring(1, available.Length - 2);

                    if (int.TryParse(available, out int day))
                        availableDates.Add(day);
                }
            }
            else
            {
                Log.WriteError("DreamLog", "Failed to Get Date");
                return false;
            }

            var dayIndex = rand.Next(0, availableDates.Count - 1);
            var targetDay = availableDates[dayIndex];
            var now = DateTime.Now;
            var year = now.Day > targetDay && now.Month == 12 ? now.Year + 1 : now.Year;
            var month = now.Day > targetDay ? (now.Month == 12 ? 1 : now.Month + 1) : now.Month;
            var targetDate = new DateTime(year, month, targetDay);
            Log.Write("DreamLog", $"target Date {targetDate.ToString("yyyyMMdd")}");

            var targetDateStr = targetDate.ToString("yyyyMMdd");
            var selectDateString = string.Format(SelectDateFormat, targetDateStr);
            result = await ExecuteAndLogAsync(selectDateString);

            await Wait(1, 5);
            //시간대 선택  8 ~ 11 ㅅㅣ간대도 있는거만 골라서 선택하도록 수정해야한다.

            string targetTime = "";
            if (rand.Next(0, 1) == 1)
                targetTime = "11";
            else
                targetTime = "08";

            var selectTimeString = string.Format(SelectTimeFormat, targetDateStr, targetTime);
            result = await ExecuteAndLogAsync(selectTimeString);

            await Wait(4, 7);
            result = await ExecuteAndLogAsync(string.Format(PhoneFormats[0], item.Phone[0]));
            result = await ExecuteAndLogAsync(string.Format(PhoneFormats[1], item.Phone[1]));
            result = await ExecuteAndLogAsync(string.Format(PhoneFormats[2], item.Phone[2]));
            result = await ExecuteAndLogAsync(string.Format(CheckPhoneFormat, targetTime, string.Join("", item.Phone)));
            await Wait(10, 30);

            result = await ExecuteAndLogAsync(GetCertNoFormat);
            if (string.IsNullOrWhiteSpace(result))
            {
                Log.WriteError("DreamLog", $"Failed to get certnum {result}");
                return false;
            }
            else
                result = result.Substring(1, result.Length - 2);

            var certnumString = string.Format(CerNumFormat, result);
            await ExecuteAndLogAsync(certnumString);


            await ExecuteAndLogAsync(string.Format(ReservationFormat, targetDateStr, targetTime));
            await Wait(2, 4);

            return true;
        }

        private async Task<string> ExecuteAndLogAsync(string command)
        {
            await Wait(0, 1);
            var result = await webView21.ExecuteScriptAsync(command);
            Log.Write("DreamLog", result);

            return result;
        }

        private async void Form1_SizeChanged(object sender, EventArgs e)
        {
        }

        private void button1_Click(object sender, EventArgs e)
        {
            mre.Set();
        }

        private async void button2_Click(object sender, EventArgs e)
        {
            if (BookingQueue.TryDequeue(out PersonInfo pinfo))
            {
                await Book(pinfo);
                pinfo.LastReservationDate = DateTime.Now;
                webView21.NavigationCompleted -= WebView21_NavigationCompleted;
                webView21.CoreWebView2.Navigate(LogoutUrl);
                SetPersonInfos();

                webView21.NavigationCompleted += WebView21_NavigationCompleted;
                if (BookingQueue.Count > 0)
                    webView21.CoreWebView2.Navigate(DreamParkurl);
            }
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
