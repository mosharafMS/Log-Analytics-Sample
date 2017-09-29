using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;


namespace SendLogstoLogAnalytics2
{
    class Program
    {

        static void Main(string[] args)
        {
            //stitch the appsettings file
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appSettings.json", optional: false, reloadOnChange: true);

            IConfigurationRoot configuration = configBuilder.Build();

           


            //variables
            // An example JSON object, with key/value pairs
            string json = @"[{""CitizenName"":""Mohamed Sharaf"",""CitizenID"":""XYZ123""},{""NumericValue"":""123543854"",""TextData"":""This is a text data lol""}]";

            // Update customerId to your Operations Management Suite workspace ID
            string customerId = configuration["OMS-WorkspaceID"];
                
            while(customerId == "")
            {
                Console.WriteLine("OMS workspace id not found in the configuration file");
                Console.WriteLine("Please enter workspace id then press enter");
                customerId=Console.ReadLine();
            }
            configuration["OMS-WorkspaceID"] = customerId;
            
            // For sharedKey, use either the primary or the secondary Connected Sources client authentication key   
            string sharedKey = configuration["OMS-WorkspaceKey"];
            while (sharedKey == "")
            {
                Console.WriteLine("OMS workspace key not found in the configuration file");
                Console.WriteLine("Please enter workspace key then press enter");
                sharedKey = Console.ReadLine();
            }
            configuration["OMS-WorkspaceKey"] = sharedKey;

            // LogName is name of the event type that is being submitted to Log Analytics
            string logName = configuration["LogName"];
            while(logName=="")
            {
                Console.WriteLine("Log name not found in the configuration file");
                Console.WriteLine("Please enter Log name  then press enter");
                logName = Console.ReadLine();
            }
            configuration["LogName"] = logName;

            // You can use an optional field to specify the timestamp from the data. If the time field is not specified, Log Analytics assumes the time is the message ingestion time
            string TimeStampField = DateTime.UtcNow.ToString();



            // Create a hash for the API signature
            var datestring = DateTime.UtcNow.ToString("r");
            string stringToHash = "POST\n" + json.Length + "\napplication/json\n" + "x-ms-date:" + datestring + "\n/api/logs";
            string hashedString = BuildSignature(stringToHash, sharedKey);
            string signature = "SharedKey " + customerId + ":" + hashedString;

            //looping
            List<Task> tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                //PostData(signature, datestring, json)
                Console.WriteLine("Posting {0} asynchronasly",datestring);
                Task lastTask = Task.Run(() => PostData(signature, datestring, json, customerId, logName, TimeStampField));
                tasks.Add(lastTask);
            }
            Task.WaitAll(tasks.ToArray());
            Console.WriteLine("Work complated...press any key to terminate");
            Console.ReadKey(true);
        }

        // Build the API signature
        public static string BuildSignature(string message, string secret)
        {
            var encoding = new System.Text.ASCIIEncoding();
            byte[] keyByte = Convert.FromBase64String(secret);
            byte[] messageBytes = encoding.GetBytes(message);
            using (var hmacsha256 = new HMACSHA256(keyByte))
            {
                byte[] hash = hmacsha256.ComputeHash(messageBytes);
                return Convert.ToBase64String(hash);
            }
        }


        // Send a request to the POST API endpoint
        public static async Task<string> PostData(string signature, string date, string json,string customerId,string LogName,string TimeStampField)
        {
            try
            {
                string url = "https://" + customerId + ".ods.opinsights.azure.com/api/logs?api-version=2016-04-01";

                System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("Log-Type", LogName);
                client.DefaultRequestHeaders.Add("Authorization", signature);
                client.DefaultRequestHeaders.Add("x-ms-date", date);
                client.DefaultRequestHeaders.Add("time-generated-field", TimeStampField);

                System.Net.Http.HttpContent httpContent = new StringContent(json, Encoding.UTF8);
                httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                var response = await client.PostAsync(new Uri(url), httpContent);

                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Status: " + response.StatusCode);
                return await response.Content.ReadAsStringAsync();        
 
            }
            catch (Exception excep)
            {
                Console.WriteLine("API Post Exception: " + excep.Message);
                return "ERROR";
            }
        }

       

    }
}
