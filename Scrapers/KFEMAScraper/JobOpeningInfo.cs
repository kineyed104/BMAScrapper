using System.Collections.Generic;

namespace KFEMAScraper
{
    internal class JobOpeningInfo
    {
        public Dictionary<string, string> data;

        public JobOpeningInfo(Dictionary<string, string> dic)
        {
            data = dic;
        }
    }
}