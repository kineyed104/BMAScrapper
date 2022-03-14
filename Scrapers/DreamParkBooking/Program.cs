using System;
using System.Threading;

namespace DreamParkBooking
{
    class Program
    {
        static void Main(string[] args)
        {
            foreach (var arg in args)
            {
                var tokens = arg.Split("=", StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length != 2)
                    continue;

                var id = tokens[0];
                var pw = tokens[1];
                AutoBooking autobook = new AutoBooking(id,pw);
                autobook.Start();

                Thread.Sleep(29000);
            }
        }
    }
}
