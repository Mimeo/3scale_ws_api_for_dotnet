using System;
using System.Collections.Generic;
using System.Text;
using CS_threescale;
using System.Net;
using System.IO;
using System.Collections;

namespace CS_threescale
{
    public class Api:IApi
    {
        string provider_key;
        string hostURI;

        const int APP_ID = 1;
        const int USER_KEY = 2;  

        const string contentType = "application/x-www-form-urlencoded";

        public Api()
        {
            hostURI = "http://su1.3scale.net";
        }

        public Api(string provider_key):this()
        {
            if ((provider_key == null) || (provider_key.Length <= 0)) throw new ApiException("argument error: undefined provider_key");
            this.provider_key = provider_key;
           
        }

        public Api(string hostURI, string provider_key):this(provider_key)
        {
            if ((hostURI == null) || (hostURI.Length <= 0)) throw new ApiException("argument error: undefined server");
            this.hostURI = hostURI;
        }

        #region IApiCommand Members

        public string HostURI
        {
            get { return hostURI; }
            set { hostURI = value; }
        }

        public AuthorizeResponse authorize(string app_id) {
            return aux_authorize(APP_ID,app_id, null);
        }

        public AuthorizeResponse authorize(string app_id, string app_key) {
          return aux_authorize(APP_ID, app_id, app_key); 
        }

        public AuthorizeResponse authorize_user_key(string user_key) {
          return aux_authorize(USER_KEY, user_key, null);  
        }

        private AuthorizeResponse aux_authorize(int key_type, string id, string key) {

            if ((key_type==APP_ID) && ((id==null) || (id.Length <= 0))) throw new ApiException("argument error: undefined app_id");
            if ((key_type==USER_KEY) && ((id==null) || (id.Length <= 0))) throw new ApiException("argument error: undefined user_key");

            string URL = hostURI + "/transactions/authorize.xml";

            string content = "?provider_key=" + provider_key;

            if (key_type==USER_KEY) {content = content + "&user_key=" + id;}
            else{content = content + "&app_id=" + id; }
              
            if ((key_type==APP_ID) && (key!=null) ){
                if (key.Length <= 0) throw new ApiException("argument error: undefined app_key");
                else content = content + "&app_key=" + key;
            }

            URL = URL + content;
                
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(URL);

            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.OK:
                            string responseBody;

                            using(Stream responseStream = response.GetResponseStream())
                            using(StreamReader responseStreamReader = new StreamReader(responseStream, Encoding.UTF8))
                            {
                                responseBody = responseStreamReader.ReadToEnd();
                            }

                            AuthorizeResponse auth_response = SerializeHelper<AuthorizeResponse>.Ressurect(responseBody);

                            return auth_response;
                    }
                }
            }
            catch (WebException webException)
            {

                if (webException.Response == null) throw webException;

                HttpWebResponse response = (HttpWebResponse)webException.Response;

                string responseBody;

                using(Stream responseStream = response.GetResponseStream())
                using(StreamReader responseStreamReader = new StreamReader(responseStream, Encoding.UTF8))
                {
                    responseBody = responseStreamReader.ReadToEnd();
                }

                ApiError err;

                try
                {
                    err = SerializeHelper<ApiError>.Ressurect(responseBody);
                }
                catch (Exception) {
                    err = null;
                }

                if (err != null) throw new ApiException(err.code + " : " + err.message);

                switch (response.StatusCode)
                {
                    case HttpStatusCode.Forbidden:
                        throw new ApiException("Forbidden");

                    case HttpStatusCode.BadRequest:
                        throw new ApiException("Bad request");
                    
                    case HttpStatusCode.InternalServerError:
                        throw new ApiException("Internal server error");
                    
                    case HttpStatusCode.NotFound:
                        throw new ApiException("Request route not found");

                    case HttpStatusCode.Conflict:
                        AuthorizeResponse auth_response = SerializeHelper<AuthorizeResponse>.Ressurect(responseBody);
                        return auth_response;
                       
                    default:
                        throw new ApiException("Unknown Exception: " + responseBody);
                }

            }

            return null;
        }

        
        public void report(Hashtable transactions)
        {
            if ((transactions == null) || (transactions.Count <= 0)) throw new ApiException("argument error: undefined transactions, must be at least one");

            string URL = hostURI + "/transactions.xml";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(URL);
          
            request.ContentType = contentType;
            request.Method = "POST";

            StringBuilder contentBuilder = new StringBuilder("provider_key=");
            contentBuilder.Append(provider_key);

            AddTransactions(contentBuilder, transactions);

            Console.WriteLine("content: " + contentBuilder);
            
            byte[] contentBytes = Encoding.UTF8.GetBytes(contentBuilder.ToString());

            request.ContentLength = contentBytes.Length;

            try
            {
                using(Stream requestStream = request.GetRequestStream())
                {
                    requestStream.Write(contentBytes, 0, contentBytes.Length);
                }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    //Console.WriteLine(".--------------- " + response.StatusCode + " :::: " + HttpStatusCode.OK);
                }
            }
            catch (WebException webException)
            {
                if (webException.Response == null) throw webException;

                using(HttpWebResponse response = (HttpWebResponse)webException.Response)
                {
                    string responseBody;

                    using(Stream responseStream = response.GetResponseStream())
                    using(StreamReader responseStreamReader = new StreamReader(responseStream, Encoding.UTF8))
                    {
                        responseBody = responseStreamReader.ReadToEnd();
                    }

                    ApiError err;

                    try
                    {
                        err = SerializeHelper<ApiError>.Ressurect(responseBody);
                    }
                    catch(Exception)
                    {
                        err = null;
                    }

                    if(err != null) throw new ApiException(err.code + " : " + err.message);

                    switch(response.StatusCode)
                    {
                        case HttpStatusCode.Forbidden:
                            throw new ApiException("Forbidden");

                        case HttpStatusCode.BadRequest:
                            throw new ApiException("Bad request");

                        case HttpStatusCode.InternalServerError:
                            throw new ApiException("Internal server error");

                        case HttpStatusCode.NotFound:
                            throw new ApiException("Request route not found");

                        default:
                            throw new ApiException("Unknown Exception: " + responseBody);
                    }
                }
            }
            return;
        }
        
        private void AddTransactions(StringBuilder contentBuilder, Hashtable transactions)
        {
            string app_id;
            //string client_ip;
            string timestamp;
            Hashtable obj;
            Hashtable usage;

            foreach (DictionaryEntry entri in transactions) 
            {
                app_id = null;
                //client_ip = null;
                timestamp = null;
                obj = null;
                usage = null;

                obj = (Hashtable)entri.Value;

                app_id = (string)obj["app_id"];
                string user_key = (string)obj["user_key"];
                //client_ip = (string)obj["client_ip"];
                timestamp = (string)obj["timestamp"];
                usage = (Hashtable)obj["usage"];

                //if ((app_id == null) || (app_id.Length <= 0)) throw new ApiException("argument error: undefined transaction, app_id is missing in one record");
                if ((usage == null) || (usage.Count <= 0)) throw new ApiException("argument error: undefined transaction, usage is missing in one record");

                if ((timestamp!=null) && (timestamp.Length <=0)) timestamp=null;
                
                if ((app_id!=null) && (app_id.Length>0)) {
                    contentBuilder.AppendFormat("&transactions[{0}][{1}]={2}",entri.Key,"app_id",app_id);
                }
                
                if ((user_key!=null) && (user_key.Length>0)) {
                    contentBuilder.AppendFormat("&transactions[{0}][{1}]={2}", entri.Key, "user_key", user_key);
                }

                if(timestamp != null)
                {
                    contentBuilder.AppendFormat("&transactions[{0}][{1}]={2}", entri.Key, "timestamp", timestamp);
                }

                foreach (DictionaryEntry entri_usage in usage) 
                {
                    contentBuilder.AppendFormat("&transactions[{0}][{1}][{2}]={3}",entri.Key,"usage",entri_usage.Key,entri_usage.Value);
                }
            }

        }
        
        
        #endregion
    }
}
