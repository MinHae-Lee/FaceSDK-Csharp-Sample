using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static Alchera.FaceSDK.FaceSDK;

namespace Alchera.FaceSDK.FaceServer
{
    public class FaceSvrException : Exception
    {
        public FaceSvrException(string message)
            : base(message)
        {
        }
    }

    /*
        [Server Only]
        Server: Align, Liveness, Compare
        App:NONE

        [ClientServer]
        Server: Liveness, Compare
        App: Align + BestShot

        [ClientOnly]
        Server:  Compare
        App: Align + BestShot, Liveness

        [Serverless]
        Server: NONE
        App: Align + BestShot, Liveness, Compare
    */

    //
    // API
    //

    public abstract class Api
    {
        public enum Error
        {
            NoError = 0,

            NotReadyRequestPayload = 1000, // call MakeReq(..)
            ErrOnMakeReqPayload = 1001,

            UnSupportedContentType = 1500,
            UnSupportedHttpMethod = 1501,

            //
            // Request
            //

            FailHttpReqBase = 2000,

            // serialize body
            FailHttpReqSerialize = 2100,

            // request operation
            FailHttpRequest = 2200,


            //
            // Response
            //
            FailHttpResBase = 3000,

            // Bad Status
            FailHttpResStUnSupported = 3100,
            //FailHttpResStBadRequest = 3101,
            //FailHttpResStSvrError = 3102,

            // chech header
            UnsupportedHttpResContentType = 3200,
            InvalidHttpResStatus = 3210, // not a http status '200'
            InvalidHttpResContentType = 3220,

            // parse body
            FailHttpResParseBody = 3300,


            //
            // Server Err
            //
            InvalidConnState = 4000,

        }

        public enum Method
        {
            NONE,
            HEAD,
            GET,
            POST,
            PUT,
            DELETE,
        }

        public const string RST_CODE_SUCCESS = "SUCC-0000";
        public const string CT_MultipartFormData = "multipart/form-data";

        public const int API_JPG_ENC_QUALITY_LIVENESS = 100;  // 80: 80% , 100:100%

        public const string API_ATTACH_ZIP_FILENAME = "multiframe.zip";
        public const string API_ATTACH_ZIP_ENTRY_FILENAME_FMT = "{0}.jpg";
        // filename in zip must start with index 1, not zero Or "ERR-4023"
        public const int API_ATTACH_ZIP_ENTRY_FILENAME_START_IDX = 1;

        public const int API_TIMEOUT_DEFAULT_MS = 30000; // 30 second

        public static bool CheckContentHeaderJson(HttpContentHeaders hdr)
        {
            return hdr.ContentType.ToString() == "application/json" ? true : false;
        }

        //
        // perf
        //

        long svr_request_elapsed_ = 0;

        public long GetSvrRequestElapsedMS()
        {
            return svr_request_elapsed_;
        }

        public void SetSvrRequestElapsedMS(long elasped)
        {
            svr_request_elapsed_ = elasped;
        }

        //
        // entry
        //

        Error last_err_ = Error.NoError;
        string last_err_desc_ = "";
        object last_err_payload_ = null;

        public bool IsErr() { return last_err_ == Error.NoError ? false : true; }
        public Error GetLastErr() { return last_err_; }
        public string GetLastErrDesc() { return last_err_desc_; }

        public void SetLastErr(Error err = Error.NoError, string err_desc = "", object err_payload = null)
        {
            last_err_ = err;
            SetLastErrDesc(err_desc, err_payload);
        }

        public void SetLastErrDesc(string err_desc, object err_payload = null)
        {
            last_err_desc_ = err_desc;
            last_err_payload_ = err_payload;
        }


        readonly Method method_;
        readonly string path_;
        readonly string query_;

        readonly string content_type_;
        int timeout_ms_;

        public Method method { get { return method_; } }
        public string path { get { return path_; } }
        public string query { get { return query_; } }
        public string content_type { get { return content_type_; } }

        Api() { }


        // timeout=30 sec
        protected Api(string content_type, int timeout_ms, Method method, string path, string query = "")
        {
            method_ = method;
            path_ = path;
            query_ = query;
            content_type_ = content_type;
            timeout_ms_ = timeout_ms;
        }

        public int timeout_ms
        {
            get { return timeout_ms_; }
            set { timeout_ms_ = value; }
        }

        public virtual bool Serialize(MultipartFormDataContent content) { return false; } // multipart-formdata
        public virtual bool Serialize(StringContent content) { return false; } // url-encode, application/json ..

        // because httpContent is based from async, so all reading function is consisted only async func
        public virtual bool DispatchResponse(HttpResponseHeaders hdr, HttpContentHeaders content_hdr,
            HttpStatusCode status, String payload)
        {
            if (IsErr())
                return false;

            if (payload == null)
                payload = "";

            switch (status)
            {
                case HttpStatusCode.OK:
                    break;

                case HttpStatusCode.BadRequest:
                    SetResponse(payload, ResPayloadType.Str, status);
                    return true;

                case HttpStatusCode.InternalServerError:
                    SetResponse(payload, ResPayloadType.Str, status);
                    return true;

                default:
                    SetResponse(payload, ResPayloadType.Str, status);
                    return true;
            }

            if (!CheckContentHeaderJson(content_hdr))
            {
                SetLastErr(Api.Error.UnsupportedHttpResContentType, content_hdr.ContentType.ToString() + payload);
                return false;
            }

            return OnResponse(content_hdr, status, payload);
        }

        protected abstract bool OnResponse(HttpContentHeaders hdr, HttpStatusCode status, String payload);


        //
        // util
        //

        public static BlobImg[] EncodeBGRImgsToJpg(BlobImg[] bgr_imgs, int jpg_quality)
        {
            return ImgUtil.ConvBGRImgsToJpg(bgr_imgs, jpg_quality);
        }

        public static BlobImg EncodeBGRImgToJpg(BlobImg bgr_img, int jpg_quality)
        {
            return ImgUtil.ConvBGRImgToJpg(bgr_img, jpg_quality);
        }

        //
        // response 
        //

        HttpStatusCode res_http_st_code_ = (HttpStatusCode)0;
        public HttpStatusCode res_http_st_code { get { return res_http_st_code_; } }

        public enum ResPayloadType
        {
            None,
            Str,
            Json,
            Binary
        }
        ResPayloadType res_payload_type_ = ResPayloadType.None;
        public ResPayloadType res_payload_type { get { return res_payload_type_; } }

        object res_payload_ = ""; // response body

        public object GetResponse()
        {
            return res_payload_;
        }

        public bool IsHttpResponseOK()
        {
            return res_http_st_code_ == HttpStatusCode.OK;
        }

        public HttpStatusCode GetHttpResponseCode()
        {
            return res_http_st_code_;
        }

        public bool SetResponse(object payload, ResPayloadType payload_type = ResPayloadType.Json,
            HttpStatusCode http_st_code = HttpStatusCode.OK)
        {
            if (payload_type == ResPayloadType.Str)
            {
                if (!(payload is string))
                    return false;
            }

            res_payload_type_ = payload_type;
            res_payload_ = payload;

            if (payload == null)
            {
                res_payload_ = "";
                res_payload_type_ = ResPayloadType.None;
            }

            res_http_st_code_ = http_st_code;

            return true;
        }

        //
        // common response type
        //

        public class ResThresholdInfo
        {
            [Newtonsoft.Json.JsonProperty("fin_code")]
            public string fin_code { get; set; }

            [Newtonsoft.Json.JsonProperty("fin_name")]
            public string fin_name { get; set; }

            [Newtonsoft.Json.JsonProperty("auto_approve")]
            public ResRangeInfo auto_approve { get; set; }

            [Newtonsoft.Json.JsonProperty("auto_reject")]
            public ResRangeInfo auto_reject { get; set; }
        }

        public class ResRangeInfo
        {
            [Newtonsoft.Json.JsonProperty("min")]
            public double min { get; set; }
            [Newtonsoft.Json.JsonProperty("max")]
            public double max { get; set; }
        }

        public class ResReturnMsg
        {
            [Newtonsoft.Json.JsonProperty("return_code")]
            public string return_code { get; set; }

            [Newtonsoft.Json.JsonProperty("return_msg")]
            public string return_msg { get; set; }
        }

        //
        // response utlity
        //

        public enum LivenessConfidenceResult
        {
            Pass,
            Fail = -1,            
            Fail_With_Display_Detected = -2, // legacy bgr14 support
            Fail_Unknown = -9999,
        }

        public static LivenessConfidenceResult EvaluateLivenessConfidence(bool isLive, double confidence)
        {
            LivenessConfidenceResult rst;

            if (confidence < 0)
            {
                if (confidence == -2)
                {
                    // legacy bgr14 support
                    // -2: Fail_BGR14_DISPLAY_IS_DETECTED, BGR14 only
                    rst = LivenessConfidenceResult.Fail_With_Display_Detected;
                }
                else
                    rst = LivenessConfidenceResult.Fail_Unknown;

            }
            else
            {
                rst = isLive ? LivenessConfidenceResult.Pass : LivenessConfidenceResult.Fail;
            }

            return rst;
        }        

    }


    //
    // liveness bestshot
    //

    public class LivenessMulitFrameApi : Api
    {
        Req req_;

        public LivenessMulitFrameApi(int timeout_ms = API_TIMEOUT_DEFAULT_MS)
            : base(Api.CT_MultipartFormData, timeout_ms, Method.POST, "/liveness/multiframe/v2")
        {
        }

        //
        // request
        //

        public class Req
        {
            // [Mandatory]
            public int frame_counts;                       // 4, number of image files for measuring liveness
            public string filename_extension = "jpg";      // "jpg"
            public byte[] images;                          // zip file stream, 1.jpg, 2.jpg, 3.jpg, 4.jpg
        }

        public bool MakeReq(BlobImg[] liv_img_frames, string filename_extension = "jpg")
        {
            SetLastErr();

            Req req = new Req();
            string err_msg = _OnMakeReq(req, liv_img_frames, filename_extension);

            if (err_msg != "")
            {
                SetLastErr(Api.Error.ErrOnMakeReqPayload, err_msg);
                return false;
            }

            req_ = req;

            return true;
        }

        static string _OnMakeReq(Req req, BlobImg[] liv_img_frames, string filename_extension)
        {
            if (liv_img_frames == null || liv_img_frames.Length < 4)
                return "liv_img_frames.Length must be 4";

            if (filename_extension != "jpg")
                return "filename_extension must be jpg";

            req.frame_counts = liv_img_frames.Length;
            req.filename_extension = filename_extension;

            BlobImg[] liv_img_frames_jpg
                = Api.EncodeBGRImgsToJpg(liv_img_frames, Api.API_JPG_ENC_QUALITY_LIVENESS);

            if (liv_img_frames_jpg == null)
                return "failed to encode images to jpg";

            byte[] liv_img_frames_zip
                = ImgUtil.CreateZipStreamFromBGR(
                    liv_img_frames_jpg,
                    API_ATTACH_ZIP_ENTRY_FILENAME_FMT,
                    API_ATTACH_ZIP_ENTRY_FILENAME_START_IDX);

            if (liv_img_frames_zip == null)
                return "failed to create image zip";

            req.images = liv_img_frames_zip;

            return "";
        }

        public override bool Serialize(MultipartFormDataContent content)
        {
            if (req_ == null)
            {
                SetLastErr(Api.Error.NotReadyRequestPayload);
                return false;
            }

            content.Add(new StringContent(req_.frame_counts.ToString(), Encoding.UTF8), "frame_counts");
            content.Add(new StringContent(req_.filename_extension, Encoding.UTF8), "filename_extension");

            var img_zip_bytes = new ByteArrayContent(req_.images);
            img_zip_bytes.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
            content.Add(img_zip_bytes, "images", Api.API_ATTACH_ZIP_FILENAME);

            return true;
        }


        //
        // response
        //               

        public class Res
        {
            // -2: LivenessConfidence_Fail_BGR14_DISPLAY_IS_DETECTED , BGR14 only
            [Newtonsoft.Json.JsonProperty("confidence")]
            public double confidence { get; set; }

            [Newtonsoft.Json.JsonProperty("is_live")]
            public bool is_live { get; set; }

            [Newtonsoft.Json.JsonProperty("threshold_info")]
            public ResThresholdInfo threshold_info { get; set; }

            [Newtonsoft.Json.JsonProperty("return_msg")]
            public ResReturnMsg return_msg { get; set; }
        }               

        protected override bool OnResponse(HttpContentHeaders hdr, HttpStatusCode status, String payload)
        {
            SetResponse(null); // clear

            Res res = null;

            try
            {
                res = JsonConvert.DeserializeObject<Res>(payload);
            }
            catch (Exception e)
            {
                SetLastErr(Api.Error.FailHttpResParseBody, e.Message, payload);
                return false;
            }                        

            SetResponse(res);

            return true;
        }

        //
        // util
        //

        public bool IsLivenessPass()
        {
            if (IsErr())
                return false;

            if (IsHttpResponseOK())
            {
                Res res = GetResponse() as Res;

                LivenessConfidenceResult liv_rst
                    = EvaluateLivenessConfidence(res.is_live, res.confidence);

                return (liv_rst == LivenessConfidenceResult.Pass) ? true : false;
            }

            return false;
        }

    }


    //
    // face compare
    //

    public class FaceCompareApi : Api
    {
        Req req_;

        public FaceCompareApi(int timeout_ms = API_TIMEOUT_DEFAULT_MS)
            : base(Api.CT_MultipartFormData, timeout_ms, Method.POST, "/compare")
        {
        }
        
        //
        // request
        //

        public class Req
        {
            /*
               [image_a] from gallay
                 > detect face area and add face margin => crop

               [image_b-b]
                 > use last liveness bestshot(4th shot) image in 4 bestshot
                 > send last liveness bestshot to server
            */

            // [Mandatory]
            public string image_a_validate = "N";
            public string image_b_validate = "Y";
            public byte[] image_a;                         // jpeg, q=100
            public byte[] image_b;                         // jpeg, q=100
        }

        public bool MakeReq(BlobImg img1, BlobImg img2,
            bool validate_img1=false, bool validate_img2 = true)
        {
            SetLastErr();

            Req req = new Req();
            string err_msg = _OnMakeReq(req, img1, img2, validate_img1, validate_img2);

            if (err_msg != "")
            {
                SetLastErr(Api.Error.ErrOnMakeReqPayload, err_msg);
                return false;
            }

            req_ = req;
            return true;
        }

        static string _OnMakeReq(Req req, BlobImg img1, BlobImg img2,
            bool validate_img1, bool validate_img2)
        {
            if (img1 == null || img2 == null)
                return "invalid calling param img1 or img2";

            BlobImg img1_jpg
               = Api.EncodeBGRImgToJpg(img1, Api.API_JPG_ENC_QUALITY_LIVENESS);

            if (img1_jpg == null)
                return "failed to encode img1 to jpg";

            BlobImg img2_jpg
               = Api.EncodeBGRImgToJpg(img2, Api.API_JPG_ENC_QUALITY_LIVENESS);

            if (img2_jpg == null)
                return "failed to encode img2 to jpg";

            req.image_a_validate = validate_img1 ? "Y" : "N";
            req.image_b_validate = validate_img2 ? "Y" : "N";
            req.image_a = img1_jpg.data_;
            req.image_b = img2_jpg.data_;

            return "";
        }

        public override bool Serialize(MultipartFormDataContent content)
        {
            if (req_ == null)
            {
                SetLastErr(Api.Error.NotReadyRequestPayload);
                return false;
            }

            content.Add(new StringContent(req_.image_a_validate, Encoding.UTF8), "image_a_validate");
            content.Add(new StringContent(req_.image_b_validate, Encoding.UTF8), "image_b_validate");

            var image_a = new ByteArrayContent(req_.image_a);
            image_a.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            content.Add(image_a, "image_a", "image_a.jpg");

            var image_b = new ByteArrayContent(req_.image_b);
            image_b.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            content.Add(image_b, "image_b", "image_b.jpg");

            return true;
        }


        //
        // response
        //

        public class Res
        {
            [JsonProperty("similarity_confidence")]
            public double similarity_confidence { get; set; }

            [JsonProperty("match_result")]
            public int match_result { get; set; }

            [JsonProperty("threshold_info")]
            public ResThresholdInfo threshold_info { get; set; }

            [JsonProperty("return_msg")]
            public ResReturnMsg return_msg { get; set; }
        }

        protected override bool OnResponse(HttpContentHeaders hdr, HttpStatusCode status, String payload)
        {
            SetResponse(null); // clear

            Res res = null;

            try
            {
                res = JsonConvert.DeserializeObject<Res>(payload);
            }
            catch (Exception e)
            {
                SetLastErr(Api.Error.FailHttpResParseBody, e.Message, payload);
                return false;
            }

            SetResponse(res);

            return true;
        }

    }


    //
    // Server Connection
    //
    
    public class Conn
    {
        public enum Error
        {
            NoError = 0,

            InvalidURI = 1000,

            InvalidReqBody = 1000,
            InvalidResJson = 2000,
            ServerConn = 3000
        }


        //
        // entry
        //

        Error last_err_ = Error.NoError;
        string last_err_desc_ = "";
        object last_err_payload_ = null;

        public bool IsErr() { return last_err_ == Error.NoError ? false : true;  }
        public Error GetLastErr() { return last_err_; }
        public string GetLastErrDesc() { return last_err_desc_; }
        public object GetLastErrPayload() { return last_err_payload_; }

        public void SetLastErr(Error err = Error.NoError, string err_desc = "", object err_payload = null)
        {
            last_err_ = err;
            SetLastErrDesc(err_desc, err_payload);
        }

        public void SetLastErrDesc(string err_desc="", object err_payload=null) {
            last_err_desc_ = err_desc;
            last_err_payload_ = err_payload;
        }
        
        public readonly string host_ = ""; // "face-b144-vit-l.devalc.internal";
        public readonly string proto_ = ""; // http, https

        // [port_=0] resolve known port using 'type_'
        // 80: type_=http,  443: type_=https
        public readonly int port_ = 0;

        protected Uri proxy_uri_ = null;

        public virtual Uri GetProxy()
        {
            return proxy_uri_;
        }

        protected virtual bool SetProxy(Uri proxy_uri=null)
        {
            proxy_uri_ = proxy_uri;
            return true;
        }


        public Conn(string host, string proto="http", int port=80)
        {
            proto_ = proto;
            host_ = host;
            port_ = port;

            if (port_ == 0)
            {
                if(proto == "https")
                {
                    port_ = 443;
                }
                else if(proto == "http")
                {
                    port_ = 80;
                }
                else
                {
                    SetLastErr(Error.InvalidURI, "failed resolve port, proto = " + proto);
                }
            }
        }

        public Conn(string conn_uri)
        {
            if (!conn_uri.Contains("://"))
                conn_uri = "https://" + conn_uri;

            try
            {
                Uri uri = new Uri(conn_uri);

                proto_ = uri.Scheme;
                host_ = uri.Host;
                port_ = uri.Port;
            }
            catch(System.UriFormatException e)
            {
                SetLastErr(Error.InvalidURI, "invalid uri=" + conn_uri + ", " + e.Message);
            }
        }
    }

    public class RestConn : Conn
    {        
        HttpClient client_;
        
        
        public string GetURL(Api api = null, bool use_cdn_proxy_cache_buster = false)
        {
            string url = proto_ + "://" + host_;

            if (proto_ == "https" && port_ != 443)
            {
                url += ":" + port_;
            }
            else if (proto_ == "http" && port_ != 80)
            {
                url += ":" + port_;
            }

            if (api == null)
                return url;

            url += api.path;

            if (api.query != "")
            {
                url += "?" + api.query;
            }

            if (use_cdn_proxy_cache_buster)
            {
                if (api.query == "")
                    url += "?";
                else
                    url += "&";

                url += "_={Guid.NewGuid()}";
            }

            return url;
        }

        public RestConn(string conn_uri, bool use_proxy=false, Uri proxy_uri=null)
            : base(conn_uri)
        {
            if (use_proxy)
            {
                if (proxy_uri == null)
                    proxy_uri = new Uri("http://127.0.0.1:8000");

                SetProxy(proxy_uri);

                WebProxy proxy = new WebProxy(GetProxy());
                HttpClientHandler proxy_handler = new HttpClientHandler() { Proxy = proxy, UseProxy = true, };

                proxy_handler.ServerCertificateCustomValidationCallback = (req, cert, chain, errors) =>
                {
                    return true; // do not use in production
                };

                client_ = new HttpClient(proxy_handler);
            }
            else
            {
                client_ = new HttpClient();
            }

            client_.DefaultRequestHeaders.ExpectContinue = false;

            client_.DefaultRequestHeaders.CacheControl
                = new System.Net.Http.Headers.CacheControlHeaderValue
                {
                    NoCache = true,
                    NoStore = true,
                    MustRevalidate = true
                };

            client_.DefaultRequestHeaders.Pragma.Add(new NameValueHeaderValue("no-cache"));
        }

        public async Task<Api> SendRequestAsync(Api api)
        {
            if (IsErr())
            {
                api.SetLastErr(Api.Error.InvalidConnState, GetLastErrDesc());
                return api; // error exit
            }

            HttpContent req_content = null;
            HttpResponseMessage response = null;
            Stopwatch svr_request_elapsed = null;

            try
            {
                switch (api.method)
                {
                    case Api.Method.POST:
                        if (api.content_type == Api.CT_MultipartFormData)
                        {
                            string boundary = "----FSVRBoundary_" + DateTime.Now.Ticks.ToString("x");
                            MultipartFormDataContent content = new MultipartFormDataContent(boundary);

                            if (api.Serialize(content))
                            {
                                client_.Timeout = TimeSpan.FromMilliseconds(api.timeout_ms);
                                req_content = content;
                            }
                            else
                            {
                                ;
                            }
                        }
                        else
                        {
                            api.SetLastErr(Api.Error.UnSupportedContentType, api.content_type);
                        }
                                                
                        svr_request_elapsed = new Stopwatch();
                        svr_request_elapsed.Start();

                        try
                        {
                            response = await client_.PostAsync(GetURL(api), req_content);
                        } catch(TaskCanceledException ex_cancel) {                            
                            //Console.WriteLine("[ERR] canceled api call, ex=" + ex_cancel.Message);
                        }

                        svr_request_elapsed.Stop();
                        api.SetSvrRequestElapsedMS(svr_request_elapsed.ElapsedMilliseconds);

                        break;

                    case Api.Method.GET:

                        svr_request_elapsed = new Stopwatch();
                        svr_request_elapsed.Start();

                        response = await client_.GetAsync(GetURL(api));

                        svr_request_elapsed.Stop();
                        api.SetSvrRequestElapsedMS(svr_request_elapsed.ElapsedMilliseconds);

                        break;

                    default:
                        {
                            api.SetLastErr(Api.Error.UnSupportedHttpMethod, api.method.ToString());
                            return api; // error exit
                        }                        
                }


                //
                // parse response
                //                

                //response.EnsureSuccessStatusCode();

                if(response.Content == null)
                    return api; // empty body

                string res_mime_type = response.Content.Headers.ContentType.MediaType;

                if (res_mime_type.StartsWith("text/") || res_mime_type == "application/json")
                {
                    api.DispatchResponse(response.Headers, response.Content.Headers, 
                        response.StatusCode, await response.Content.ReadAsStringAsync());
                }
                else
                {
                    //response.Content.ReadAsByteArrayAsync
                    //response.Content.ReadAsStreamAsync
                    api.SetLastErr(Api.Error.UnsupportedHttpResContentType, res_mime_type);
                    return api; // error exit
                }

            }
            catch (HttpRequestException ex_http_req)
            {
                api.SetLastErr(Api.Error.FailHttpRequest, $"HR: 0x{ex_http_req.HResult:X8}, Err=" + ex_http_req.Message);
            }
            finally {

            }

            return api;
        }

    }
}
