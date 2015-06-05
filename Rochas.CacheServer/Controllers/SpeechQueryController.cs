using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;

namespace Rochas.CacheServer.Controllers
{
    public class SpeechQueryController : ApiController
    {
        #region Public Methods

        public string Post()
        {
            GlobalConfiguration.Configuration.Formatters.Clear();
            GlobalConfiguration.Configuration.Formatters.Add(new JsonMediaTypeFormatter());

            HttpContext.Current.Response.Headers.Add("Access-Control-Allow-Origin", "*");

            HttpPostedFile audioBuffer = HttpContext.Current.Request.Files[0];           

            string[] searchTerms = CacheServer.GetSpeech(audioBuffer.InputStream);

            string resultUri = string.Empty;
            string concatTerms = string.Empty;
            if (searchTerms.Length > 0)
                resultUri = string.Concat("api/query/", String.Join("|", searchTerms));

            return resultUri;
        }

        #endregion
    }
}
