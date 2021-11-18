using System;

namespace BMAScraper
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            JobOpeningScraper scraper = new JobOpeningScraper();
            scraper.Save();
            
        }
    }
}
