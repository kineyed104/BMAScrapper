using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DreamParkBooking
{
    public class HttpClientHelper
    {
        public HttpClient HttpClient { get; private set; }
        public Func<HttpResponseMessage, Task<bool>> UnauthorizedEventHandler;
        public CookieContainer Cookies = new CookieContainer();

        public HttpClientHelper(string baseUrl = null, (string cookieName, string customCookie)? cookieData = null)
        {
            var httpClientHandler = new HttpClientHandler();
            httpClientHandler.CookieContainer = Cookies;

            if (cookieData.HasValue)
                httpClientHandler.CookieContainer.Add(new Uri(baseUrl),
                    new Cookie(cookieData.Value.cookieName, cookieData.Value.customCookie));

            // 인증서의 유효성 여부를 체크함, 테스트시에만 사용
            httpClientHandler.ServerCertificateCustomValidationCallback =
                        (message, cert, chain, errors) => { return true; };

            HttpClient = new HttpClient(httpClientHandler);

            if (baseUrl != null)
                HttpClient.BaseAddress = new Uri(baseUrl);

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            HttpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            HttpClient.DefaultRequestHeaders.Add("Pragma","no-cache");
            HttpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/96.0.4664.93 Safari/537.36");
            HttpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9");
            
        }


        public void SetCookie(string cookie)
        {
            HttpClient.DefaultRequestHeaders.Remove("Cookie");
            HttpClient.DefaultRequestHeaders.Add("Cookie",cookie);
        }

        public async Task<HttpResponseMessage> PostAsync(string url, Dictionary<string, string> formcontent, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
        {
            var queryString = GetQueryString(url);

            var contentList = formcontent.Select(d => $"{Uri.EscapeDataString(d.Key)}={Uri.EscapeDataString(d.Value)}");
            var message = new HttpRequestMessage(HttpMethod.Post, new Uri(HttpClient.BaseAddress, queryString));
            message.Content = new StringContent(string.Join("&", contentList));
            message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            
            var response = await SendAsync(message, completionOption);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                if (await UnauthorizedEventHandler?.Invoke(response))
                    response = await SendAsync(message, completionOption);

            await EnsureSuccessStatusCode(response);
            return response;
        }

        public async Task<TResult> PostAndReadAsync<TResult>(string url, Dictionary<string, string> formcontent)
        {
            if (typeof(TResult) == typeof(Stream))
            {
                var response = await PostAsync(url, formcontent, HttpCompletionOption.ResponseHeadersRead);
                return (TResult)(object)await response.Content.ReadAsStreamAsync();
            }
            else if (typeof(TResult) == typeof(HttpResponseMessage))
            {
                return (TResult)(object)await PostAsync(url, formcontent, HttpCompletionOption.ResponseContentRead);
            }
            else
            {
                var response = await PostAsync(url, formcontent, HttpCompletionOption.ResponseContentRead);
                return await Deserialize<TResult>(response.Content);
            }
        }





        public async Task<TResult> GetAsync<TResult>(string url, object parameters = null)
        {
            var queryString = GetQueryString(url, parameters);

            var response = await HttpClient.GetAsync(queryString);
            if (typeof(TResult) == typeof(HttpResponseMessage))
                return (TResult)(object)response;

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                if (await UnauthorizedEventHandler?.Invoke(response))
                    response = await GetAsync(queryString);

            await EnsureSuccessStatusCode(response);

            return await Deserialize<TResult>(response.Content);
        }

        public async Task<HttpResponseMessage> PostAsync(string url, object content, object parameters = null, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
        {
            var queryString = GetQueryString(url, parameters);

            using (var httpContent = Serialize(content))
            {
                //TODO: BaseAddress null일 경우 동작 확인 필요함.
                var message = new HttpRequestMessage(HttpMethod.Post, new Uri(HttpClient.BaseAddress, queryString));
                message.Content = httpContent;

                var response = await SendAsync(message, completionOption);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    if (await UnauthorizedEventHandler?.Invoke(response))
                        response = await SendAsync(message, completionOption);

                await EnsureSuccessStatusCode(response);
                return response;
            }
        }

        public async Task<HttpResponseMessage> PutAsync(string url, object content, object parameters = null, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
        {
            var queryString = GetQueryString(url, parameters);

            using (var httpContent = Serialize(content))
            {
                var message = new HttpRequestMessage(HttpMethod.Put, new Uri(HttpClient.BaseAddress, queryString));
                message.Content = httpContent;

                var response = await SendAsync(message, completionOption);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    if (await UnauthorizedEventHandler?.Invoke(response))
                        response = await SendAsync(message, completionOption);

                await EnsureSuccessStatusCode(response);

                return response;
            }
        }

        public async Task<TResult> PostAndReadAsync<TResult>(string url, object content, object parameters = null)
        {
            if (typeof(TResult) == typeof(Stream))
            {
                var response = await PostAsync(url, content, parameters, HttpCompletionOption.ResponseHeadersRead);
                return (TResult)(object)await response.Content.ReadAsStreamAsync();
            }
            else if (typeof(TResult) == typeof(HttpResponseMessage))
            {
                return (TResult)(object)await PostAsync(url, content, parameters, HttpCompletionOption.ResponseContentRead);
            }
            else
            {
                var response = await PostAsync(url, content, parameters, HttpCompletionOption.ResponseContentRead);
                return await Deserialize<TResult>(response.Content);
            }
        }

        public async Task<TResult> PutAndReadAsync<TResult>(string url, object content, object parameters = null)
        {
            if (typeof(TResult) == typeof(HttpResponseMessage))
            {
                return (TResult)(object)await PutAsync(url, content, parameters, HttpCompletionOption.ResponseContentRead);
            }
            else
            {
                var response = await PutAsync(url, content, parameters, HttpCompletionOption.ResponseContentRead);
                return await Deserialize<TResult>(response.Content);
            }
        }

        private string GetQueryString(string url, object parameters = null)
        {
            return parameters == null ? url : $"{url}?{GetPropertyQueryString(parameters)}";
        }

        private string GetPropertyQueryString(object obj)
        {
            var list = new NameValueCollection();
            foreach (var p in obj.GetType().GetProperties())
            {
                var value = p.GetValue(obj);
                if (value is DateTime dateTimeValue)
                    list[p.Name] = ToISOString(dateTimeValue, true);
                else
                    list[p.Name] = value?.ToString();
            }

            return ToQueryString(list);
        }

        private string ToISOString(DateTime time, bool includeMilliseconds = false)
        {
            if (includeMilliseconds)
                return time.ToString("yyyy-MM-dd HH:mm:ss.fffffff");
            else
                return time.ToString("yyyy_MM-dd HH:mm:ss");
        }

        private string ToQueryString(NameValueCollection list)
        {
            var encodedList = list.AllKeys.Select(key => $"{WebUtility.UrlEncode(key)}={WebUtility.UrlEncode(list[key])}");
            return String.Join("&", encodedList);
        }

        private Task<HttpResponseMessage> GetAsync(string url)
        {
            try
            {
                return HttpClient.GetAsync(url);
            }
            catch (HttpRequestException ex)
            {
                if (ex.InnerException == null)
                    throw;
                else
                {
                    if ((ex.InnerException is WebException webEx && webEx.Status == WebExceptionStatus.NameResolutionFailure)
                        || (ex.InnerException is SocketException socketEx && socketEx.SocketErrorCode == SocketError.HostNotFound))
                    {
                        Reset();

                        return HttpClient.GetAsync(url);
                    }
                    else
                        throw;
                }
            }
        }

        private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
        {
            try
            {
                return await HttpClient.SendAsync(request, completionOption);
            }
            catch (HttpRequestException ex)
            {
                if (ex.InnerException == null)
                    throw;
                else
                {
                    if ((ex.InnerException is WebException webEx && webEx.Status == WebExceptionStatus.NameResolutionFailure)
                        || (ex.InnerException is SocketException socketEx && socketEx.SocketErrorCode == SocketError.HostNotFound))
                    {
                        Reset();

                        return await HttpClient.SendAsync(request, completionOption);
                    }
                    else
                        throw;
                }
            }
        }

        private void Reset()
        {
            var httpClient = new HttpClient();
            httpClient.BaseAddress = HttpClient.BaseAddress;
            httpClient.MaxResponseContentBufferSize = HttpClient.MaxResponseContentBufferSize;
            httpClient.Timeout = HttpClient.Timeout;

            foreach (var header in HttpClient.DefaultRequestHeaders)
            {
                httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
            }

            HttpClient.Dispose();
            HttpClient = httpClient;
        }

        private static async Task<HttpResponseMessage> EnsureSuccessStatusCode(HttpResponseMessage message)
        {
            if (message.IsSuccessStatusCode)
                return message;
            else
                throw new HttpRequestException($"StatusCode={message.StatusCode}, ReasonPhrase={message.ReasonPhrase}, Content={(message.Content != null ? await message.Content.ReadAsStringAsync() : "")}");
        }

        private async Task<TResult> Deserialize<TResult>(HttpContent content)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<TResult>(await content.ReadAsStringAsync());
        }

        private HttpContent Serialize(object obj)
        {
            if (obj is HttpContent)
                return (HttpContent)obj;

            return new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(obj), Encoding.UTF8, "application/json");
        }

        #region IDisposable Members
        protected bool disposed = false;
        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
                return;

            if (disposing)
            {
                HttpClient.Dispose();
            }

            //Dispose unmanaged resources here.

            disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~HttpClientHelper()
        {
            Dispose(false);
        }
        #endregion
    }
}
