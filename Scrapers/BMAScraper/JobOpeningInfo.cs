using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BMAScraper
{
    public class JobOpeningInfo
    {
        public Dictionary<string, string> datas;
        public string email;
        public string link;
        public string registrationdate;
        public JobOpeningInfo(string[] headdatas, Dictionary<string, string> datas, string email, string link)
        {
            if (headdatas != null && headdatas.Length > 0)
                registrationdate = headdatas[0];

            this.datas = datas;
            this.email = email;
            this.link = link;
        }

        public override string ToString()
        {
            string result = "";
            if (datas != null)
            {
                foreach (var item in datas)
                {
                    result += item.Key + "=" + item.Value + ",";
                }
            }

            result += nameof(email) + "=" + email + ",";
            result += nameof(link) + "=" + link;
            return result;
        }
    }
}
