using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Text;
using System.Web;
using System.Web.Http;

namespace Rochas.CacheServer.Controllers
{
    public class QueryController : ApiController
    {
        #region Public Methods

        public IEnumerable Get(string filter, string modelName = "", string selection = "")
        {
            GlobalConfiguration.Configuration.Formatters.Clear();
            GlobalConfiguration.Configuration.Formatters.Add(new JsonMediaTypeFormatter());

            HttpContext.Current.Response.Headers.Add("Access-Control-Allow-Origin", "*");

            filter = filter.ToLower();

            if (filter.Equals("all"))
                filter = string.Empty;

            return CacheServer.GetList(filter, modelName, selection);
        }

        #endregion
    }
}
