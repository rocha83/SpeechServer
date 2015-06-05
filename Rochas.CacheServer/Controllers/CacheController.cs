using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Web;
using System.Web.Http;

namespace Bayer.CacheServer.Controllers
{
    public class CacheController : ApiController
    {
        #region Public Methods

        public string Get(string filter, string destination = "")
        {
            GlobalConfiguration.Configuration.Formatters.Clear();
            GlobalConfiguration.Configuration.Formatters.Add(new JsonMediaTypeFormatter());

            filter = filter.ToLower();

            if (filter.Equals("all"))
                filter = string.Empty;

            return CacheServer.GetJson(filter);
        }

        [HttpPost]
        public void Post()
        {
            // Obtem o buffer da biblioteca de modelos do cliente para realizar as consultas
            // Obtem o nome do modelo a utilizar e a fonte de dados composta JSON/XML/CSV
            if (HttpContext.Current.Request.Files.Count > 0)
            {
                MemoryStream asmBuffer = new MemoryStream();
                HttpContext.Current.Request.Files[0].InputStream.CopyTo(asmBuffer);

                try
                {
                    string className = HttpUtility.HtmlDecode(HttpContext.Current.Request.Form["modelName"]);
                    string dataSource = HttpUtility.HtmlDecode(HttpContext.Current.Request.Form["dataSource"]);

                    if ((asmBuffer != null) && !string.IsNullOrEmpty(className) && !string.IsNullOrEmpty(dataSource))
                        CacheServer.Post(asmBuffer.ToArray(), className, dataSource);
                    else
                        throw new HttpResponseException(HttpStatusCode.BadRequest);
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
            else
                throw new HttpResponseException(HttpStatusCode.NoContent);
        }

        #endregion
    }
}
