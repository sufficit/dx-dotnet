using MercadoPago;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Collections.Specialized;

namespace MercadoPago
{
    public class MPRESTClient
    {
        private IWebProxy _proxy;
        private string _proxyHostName;
        private int _proxyPort = -1;

        private static TrafficLight _trafficLight;
        public string ProxyHostName
        {
            get { return _proxyHostName; }
            set { _proxyHostName = value; }
        }

        public int ProxyPort
        {
            get { return _proxyPort; }
            set { _proxyPort = value; }
        }

        #region Core Methods
        /// <summary>
        /// Class constructor.
        /// </summary>
        public MPRESTClient() {}

        /// <summary>
        /// Set class variables.
        /// </summary>
        /// <param name="proxyHostName">Proxy host to use.</param>
        /// <param name="proxyPort">Proxy port to use in the proxy host.</param>
        public MPRESTClient(string proxyHostName, int proxyPort)
        {
            _proxy = new WebProxy(proxyHostName, proxyPort);
            _proxyHostName = proxyHostName;
            _proxyPort = proxyPort;
        }

        public JToken ExecuteGenericRequest(
            HttpMethod httpMethod,
            string path,
            PayloadType payloadType,
            JObject payload)
        {
            if (SDK.GetAccessToken() != null)
            {
                path = SDK.BaseUrl + path + "?access_token=" + SDK.GetAccessToken();
            }

            MPRequest mpRequest = CreateRequest(httpMethod, path, payloadType, payload, null, 0, 0);

            if (new HttpMethod[] { HttpMethod.POST, HttpMethod.PUT }.Contains(httpMethod))
            {
                Stream requestStream = mpRequest.Request.GetRequestStream();
                requestStream.Write(mpRequest.RequestPayload, 0, mpRequest.RequestPayload.Length);
                requestStream.Close();
            }

            try
            {
                using (HttpWebResponse response = (HttpWebResponse)mpRequest.Request.GetResponse())
                {
                    Stream dataStream = response.GetResponseStream();
                    StreamReader reader = new StreamReader(dataStream, Encoding.UTF8);
                    String StringResponse = reader.ReadToEnd();
                    return JToken.Parse(StringResponse);
                }

            }
            catch (WebException ex)
            {
                HttpWebResponse errorResponse = ex.Response as HttpWebResponse;
                Stream dataStream = errorResponse.GetResponseStream();
                StreamReader reader = new StreamReader(dataStream, Encoding.UTF8);
                String StringResponse = reader.ReadToEnd();
                return JToken.Parse(StringResponse);
            }

        }

        /// <summary>
        /// Execute a request to an endpoint.
        /// </summary>
        /// <param name="httpMethod">Method to use in the request.</param>
        /// <param name="path">Endpoint we are pointing.</param>
        /// <param name="payloadType">Type of payload we are sending along with the request.</param>
        /// <param name="payload">The data we are sending.</param>
        /// <param name="colHeaders">Extra headers to send with the request.</param>
        /// <returns>Api response with the result of the call.</returns>
        public MPAPIResponse ExecuteRequest(
            HttpMethod httpMethod,
            string path,
            PayloadType payloadType,
            JObject payload,
            WebHeaderCollection colHeaders,
            int requestTimeout,
            int retries)
        {
            var requestOptions = CreateRequestOptions(colHeaders, requestTimeout, retries);
            return ExecuteRequest(httpMethod, path, payloadType, payload, requestOptions);
        }

        /// <summary>
        /// Core module implementation. Execute a request to an endpoint.
        /// This method is deprecated and will be removed in a future version, please use the
        /// <see cref="ExecuteRequest(HttpMethod, string, PayloadType, JObject, WebHeaderCollection, int, int)" /> method instead.
        /// </summary>
        /// <returns>Api response with the result of the call.</returns>
        // TODO; remove this method in a future major version
        [Obsolete("There is no use for this method.")]
        public MPAPIResponse ExecuteRequestCore(
            HttpMethod httpMethod,
            string path,
            PayloadType payloadType,
            JObject payload,
            WebHeaderCollection colHeaders,
            int connectionTimeout,
            int retries)
        {
            var requestOptions = CreateRequestOptions(colHeaders, connectionTimeout, retries);
            return ExecuteRequest(httpMethod, path, payloadType, payload, requestOptions);
        }

        /// <summary>
        /// Execute a request to an api endpoint.
        /// </summary>
        /// <returns>Api response with the result of the call.</returns>
        public MPAPIResponse ExecuteRequest(
            HttpMethod httpMethod,
            string path,
            PayloadType payloadType,
            JObject payload)
        {
            return ExecuteRequest(httpMethod, path, payloadType, payload, null);
        }

        /// <summary>
        /// Execute a request to an api endpoint.
        /// </summary>
        /// <returns>Api response with the result of the call.</returns>
        public MPAPIResponse ExecuteRequest(
            HttpMethod httpMethod,
            string path,
            PayloadType payloadType,
            JObject payload,
            MPRequestOptions requestOptions)
        {
            if (requestOptions == null) {
                requestOptions = new MPRequestOptions();
            }

            MPRequest mpRequest = CreateRequest(httpMethod, path, payloadType, payload, requestOptions);
            string result = string.Empty;
            int retriesLeft = requestOptions.Retries;

            if (new HttpMethod[] { HttpMethod.POST, HttpMethod.PUT }.Contains(httpMethod))
            {
                using (Stream requestStream = mpRequest.Request.GetRequestStream()) {
                    requestStream.Write(mpRequest.RequestPayload, 0, mpRequest.RequestPayload.Length);
                }
            }

            try
            {
                return ExecuteRequest(mpRequest.Request, 
                    response => new MPAPIResponse(httpMethod, mpRequest.Request, payload, response),
                    requestOptions.Retries);
            }
            catch (Exception ex)
            {
                throw new MPRESTException(ex.Message);
            }
        }

        /// <summary>
        /// Create a request to use in the call to a certain endpoint.
        /// </summary>
        /// <returns>Api response with the result of the call.</returns>
        public MPRequest CreateRequest(HttpMethod httpMethod,
            string path,
            PayloadType payloadType,
            JObject payload,
            WebHeaderCollection colHeaders,
            int connectionTimeout,
            int retries)
        {
            var requestOptions = CreateRequestOptions(colHeaders, connectionTimeout, retries);
            return CreateRequest(httpMethod, path, payloadType, payload, requestOptions);
        }

        /// <summary>
        /// Create a request to use in the call to a certain endpoint.
        /// </summary>
        /// <returns>Api response with the result of the call.</returns>
        public MPRequest CreateRequest(HttpMethod httpMethod,
            string path,
            PayloadType payloadType,
            JObject payload,
            MPRequestOptions requestOptions)
        {

            if (string.IsNullOrEmpty(path))
                throw new MPRESTException("Uri can not be an empty string.");

            if (httpMethod.Equals(HttpMethod.GET))
            {
                if (payload != null)
                {
                    throw new MPRESTException("Payload not supported for this method.");
                }
            }
            else if (httpMethod.Equals(HttpMethod.POST))
            {
                //if (payload == null)
                //{
                //    throw new MPRESTException("Must include payload for this method.");
                //}
            }
            else if (httpMethod.Equals(HttpMethod.PUT))
            {
                if (payload == null)
                {
                    throw new MPRESTException("Must include payload for this method.");
                }
            }
            else if (httpMethod.Equals(HttpMethod.DELETE))
            {
                if (payload != null)
                {
                    throw new MPRESTException("Payload not supported for this method.");
                }
            }

            MPRequest mpRequest = new MPRequest();
            mpRequest.Request = (HttpWebRequest)HttpWebRequest.Create(path);
            mpRequest.Request.Method = httpMethod.ToString();

            if (requestOptions == null)
            {
                requestOptions = new MPRequestOptions();
            }

            if (requestOptions.Timeout > 0)
            {
                mpRequest.Request.Timeout = requestOptions.Timeout;
            }

            mpRequest.Request.Headers.Add("x-product-id", "BC32BHVTRPP001U8NHL0");
            if (requestOptions.CustomHeaders != null)
            {
                foreach (var header in requestOptions.CustomHeaders)
                {
                    if (mpRequest.Request.Headers[header.Key] == null)
                    {
                        mpRequest.Request.Headers.Add(header.Key, header.Value);
                    }
                }
            }

            if (payload != null) // POST & PUT
            {
                byte[] data = null;
                if (payloadType != PayloadType.JSON)
                {
                    var parametersDict = payload.ToObject<Dictionary<string, string>>();
                    var parametersString = new StringBuilder();
                    parametersString.Append(string.Format("{0}={1}", parametersDict.First().Key, parametersDict.First().Value));
                    parametersDict.Remove(parametersDict.First().Key);
                    foreach (var value in parametersDict)
                    {
                        parametersString.Append(string.Format("&{0}={1}", value.Key, value.Value.ToString()));
                    }

                    data = Encoding.ASCII.GetBytes(parametersString.ToString());
                }
                else
                {
                    data = Encoding.ASCII.GetBytes(payload.ToString());
                }

                mpRequest.Request.UserAgent = "MercadoPago DotNet SDK/" + SDK.Version;
                mpRequest.Request.ContentLength = data.Length;
                mpRequest.Request.ContentType = payloadType == PayloadType.JSON ? "application/json" : "application/x-www-form-urlencoded";
                mpRequest.RequestPayload = data;
            }

            IWebProxy proxy = requestOptions.Proxy != null ? requestOptions.Proxy : (_proxy != null ? _proxy : SDK.Proxy);
            if (proxy != null)
            {
                mpRequest.Request.Proxy = proxy;
            }

            return mpRequest;
        }

        private MPRequestOptions CreateRequestOptions(WebHeaderCollection colHeaders, int connectionTimeout, int retries)
        {
            IDictionary<String, String> headers = new Dictionary<String, String>();
            if (colHeaders != null)
            {
                foreach (var header in colHeaders)
                {
                    headers.Add(header.ToString(), colHeaders[header.ToString()]);
                }
            }

            return new MPRequestOptions
            {
                Timeout = connectionTimeout,
                Retries = retries,
                CustomHeaders = headers
            };
        }

        private MPAPIResponse ExecuteRequest(HttpWebRequest request, Func<HttpWebResponse, MPAPIResponse> convertToApiResponse, int retries)
        {
            do
            {
                try
                {
                    using (HttpWebResponse resultApi = (HttpWebResponse)request.GetResponse())
                    {
                        var response =  convertToApiResponse(resultApi);
                        var th = new Thread(() => SendInsightInformationAsync (response , retries));
                        th.Start();
                        return response;
                    }
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError)
                    {
                        using (HttpWebResponse errorResponse = ex.Response as HttpWebResponse)
                        {
                             var response =  convertToApiResponse(errorResponse);
                             var th = new Thread(() => SendInsightInformationAsync (response , retries));
                             th.Start();
                             return response;
                        }
                    }
                    
                    if (--retries == 0)
                        throw;
                }

            } while (true);
        }

        
        private async Task<T> ExecuteRequestAsync(MPRequest request)
        {
                try
                {
                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponseAsync())
                    {
                        var newStream = response.GetResponseStream();
                        var sr = new StreamReader(newStream);    
                        var result = sr.ReadToEnd();    
                        return JsonConvert.DeserializeObject<T>(result);
                        
                    }
                }
                catch (WebException ex)
                {
                    //TODO log error?
                    return null;
                }

        }
        private async void SendInsightInformationAsync(MPAPIResponse apiResult, int retries)
        {
            if (this._trafficLight == null || this._trafficLight.ExpirationTime > DateTime.Now)
            {
                    this._trafficLight = await GetTrafficLightAsync();
                    this._trafficLight.ExpiredTime = DateTime.Now.AddSeconds(trafficLight.TTL);
            }

            if (this._trafficLight.SendData)
            {
                var client_information = new {
                    name ="MercadoPago-SDK-DotNet",
                    version  = SDK.Version
                };

                var connection_info = new {
                    protocol_info = new{ 
                            name = "http",
                            protocol_http =  new {
                                request_method = apiResult.HttpMethod,
                                request_url = apiResult.Url,
                                request_headers = new {
                                content_type = apiResult.Request.Headers["Content-Type"] ?? string.Empty,
                                content_length = apiResult.Request.Headers["Content-Length"] ?? string.Empty,
                                },
                                response_status_code = apiResult.StatusCode,
                                response_headers = new {
                                content_type = apiResult.Response.Headers["Content-Type"] ?? string.Empty,
                                content_length = apiResult.Response.Headers["Content-Length"] ?? string.Empty,
                                },
                            }
                    }
                };

                var body = new JObject();
                body.Add("client_info" ,  client_information);
                body.Add("connection_info" ,  connection_info);
                var request = CreateRequest(HttpMethod.POST, "https://events.mercadopago.com/v2/metric", PayloadType.JSON ,body,null,1000,0);
                await ExecuteRequestAsync(request);
            }
             
        }

        private async TrafficLight GetTrafficLightAsync()
        {
            
            var client_information = new {
                name ="MercadoPago-SDK-DotNet",
                version  = SDK.Version
            };
            var body = new JObject();
            body.Add("client-info" , client_information);

            var request = CreateRequest(httpMethod.Posdt,"https://events.mercadopago.com/v2/traffic-light" , PayloadType.JSON , body , null , 1000 , 0);

           return await ExecuteRequestAsync<TrafficLight>(request);
        }

        #endregion
    }
}

//https://github.com/masroore/CurlSharp/blob/master/CurlSharp/CurlEasy.cs